using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.UI;
using System.Web.UI.WebControls;

using com.razayya.JourneyTrack.CalculationTypes;
using com.razayya.JourneyTrack.Logic;
using ComparisonType = com.razayya.JourneyTrack.Model.ComparisonType;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rock;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_razayya.JourneyTrack.Controls
{
    /// <summary>
    /// Visual editor for a PersonFilter calc's FilterConditions. Supports a
    /// two-level ANY/ALL structure: a top-level combine (All/Any) over one or more
    /// groups, each group combining its own condition rows with All/Any. Serializes
    /// to the shared nested logic-tree JSON (<see cref="LogicTree"/>). Configs deeper
    /// than two levels (groups within groups) fall back to a raw-JSON view so the
    /// visual editor never silently flattens or clobbers them.
    /// </summary>
    public partial class FilterConditionsEditor : UserControl
    {
        #region Public API

        public string Value
        {
            get { return GetJson(); }
            set { SetJson( value ); }
        }

        #endregion

        #region Internal group model

        private class EditGroup
        {
            public LogicGroupType Type { get; set; } = LogicGroupType.All;
            public List<FilterCondition> Conditions { get; set; } = new List<FilterCondition>();
        }

        #endregion

        #region Property Resolution

        private struct PersonPropertyInfo
        {
            public string Name;
            public Type Type;
            public Guid? DefinedTypeGuid;
        }

        // Hardcoded map of Person *ValueId properties to their DefinedType Guids.
        private static readonly Dictionary<string, Guid> _personDefinedTypeMap = new Dictionary<string, Guid>( StringComparer.OrdinalIgnoreCase )
        {
            { "ConnectionStatusValueId",        new Guid( "2E6540EA-63F0-40FE-BE50-F2A84735E600" ) }, // PERSON_CONNECTION_STATUS
            { "MaritalStatusValueId",           new Guid( "B4B92C3F-A935-40E1-A00B-BA484EAD613B" ) }, // PERSON_MARITAL_STATUS
            { "RecordStatusValueId",            new Guid( "8522BADD-2871-45A5-81DD-C76DA07E2E7E" ) }, // PERSON_RECORD_STATUS
            { "RecordStatusReasonValueId",      new Guid( "E17D5988-0372-4792-82CF-9E37C79F7319" ) }, // PERSON_RECORD_STATUS_REASON
            { "RecordTypeValueId",              new Guid( "26be73a6-a9c5-4e94-ae00-3afdcf8c9afd" ) }, // PERSON_RECORD_TYPE
            { "ReviewReasonValueId",            new Guid( "F2A772C8-D5BA-4D6D-9CFD-1EAEA2D46BE9" ) }, // PERSON_REVIEW_REASON
            { "SuffixValueId",                  new Guid( "0BDB1C63-FFEF-4BC2-9F49-3DF5C0FBC22D" ) }, // PERSON_SUFFIX
            { "TitleValueId",                   new Guid( "4784CD23-518B-43EE-9B97-225BF6E07846" ) }  // PERSON_TITLE
        };

        private static List<PersonPropertyInfo> _cachedPersonProperties;
        private static List<PersonPropertyInfo> GetPersonProperties()
        {
            if ( _cachedPersonProperties != null )
            {
                return _cachedPersonProperties;
            }

            var props = typeof( Person )
                .GetProperties( BindingFlags.Public | BindingFlags.Instance )
                .Where( p => p.CanRead
                    && p.GetIndexParameters().Length == 0
                    && ( p.PropertyType.IsValueType
                        || p.PropertyType == typeof( string )
                        || Nullable.GetUnderlyingType( p.PropertyType ) != null ) )
                .Select( p => new PersonPropertyInfo
                {
                    Name = p.Name,
                    Type = Nullable.GetUnderlyingType( p.PropertyType ) ?? p.PropertyType,
                    DefinedTypeGuid = _personDefinedTypeMap.TryGetValue( p.Name, out var g ) ? g : ( Guid? ) null
                } )
                .OrderBy( p => p.Name )
                .ToList();

            _cachedPersonProperties = props;
            return props;
        }

        private static List<AttributeCache> GetPersonAttributes()
        {
            var personEt = EntityTypeCache.Get( typeof( Person ) );
            if ( personEt == null )
            {
                return new List<AttributeCache>();
            }

            return AttributeCache.AllForEntityType( personEt.Id )
                .Where( a => a.IsActive
                    && string.IsNullOrEmpty( a.EntityTypeQualifierColumn ) )
                .OrderBy( a => a.Name )
                .ToList();
        }

        #endregion

        #region Value-shape → allowed comparisons

        private enum ValueShape
        {
            None,
            BlankOnly,
            Boolean,
            Text,
            Numeric,
            Date,
            Picker
        }

        private static readonly ComparisonType[] _cmpBlankOnly = new[] { ComparisonType.IsBlank, ComparisonType.IsNotBlank };
        private static readonly ComparisonType[] _cmpEqOnly    = new[] { ComparisonType.EqualTo, ComparisonType.NotEqualTo, ComparisonType.IsBlank, ComparisonType.IsNotBlank };
        private static readonly ComparisonType[] _cmpText      = new[] { ComparisonType.EqualTo, ComparisonType.NotEqualTo, ComparisonType.IsBlank, ComparisonType.IsNotBlank, ComparisonType.Contains };
        private static readonly ComparisonType[] _cmpNumeric   = new[] { ComparisonType.EqualTo, ComparisonType.NotEqualTo, ComparisonType.IsBlank, ComparisonType.IsNotBlank, ComparisonType.GreaterThan, ComparisonType.LessThan, ComparisonType.GreaterThanOrEqualTo, ComparisonType.LessThanOrEqualTo };

        private static ComparisonType[] GetAllowedComparisons( ValueShape shape )
        {
            switch ( shape )
            {
                case ValueShape.BlankOnly: return _cmpBlankOnly;
                case ValueShape.Boolean:   return _cmpEqOnly;
                case ValueShape.Picker:    return _cmpEqOnly;
                case ValueShape.Text:      return _cmpText;
                case ValueShape.Numeric:   return _cmpNumeric;
                case ValueShape.Date:      return _cmpNumeric;
                case ValueShape.None:      return _cmpEqOnly;
                default:                   return _cmpEqOnly;
            }
        }

        private static ValueShape GetShape( FilterCondition c )
        {
            if ( string.IsNullOrWhiteSpace( c.Key ) ) return ValueShape.None;

            if ( c.Source == FilterSource.Property )
            {
                var info = GetPersonProperties().FirstOrDefault( p => string.Equals( p.Name, c.Key, StringComparison.OrdinalIgnoreCase ) );
                if ( string.IsNullOrEmpty( info.Name ) ) return ValueShape.Text;
                if ( info.DefinedTypeGuid.HasValue ) return ValueShape.Picker;
                if ( string.Equals( info.Name, "PrimaryCampusId", StringComparison.OrdinalIgnoreCase ) ) return ValueShape.Picker;
                if ( info.Type == typeof( bool ) ) return ValueShape.Boolean;
                if ( info.Type == typeof( DateTime ) ) return ValueShape.Date;
                if ( info.Type == typeof( int ) || info.Type == typeof( long )
                    || info.Type == typeof( decimal ) || info.Type == typeof( double ) || info.Type == typeof( float ) )
                    return ValueShape.Numeric;
                return ValueShape.Text;
            }

            // FilterSource.Attribute
            var attr = GetPersonAttributes().FirstOrDefault( a => string.Equals( a.Key, c.Key, StringComparison.OrdinalIgnoreCase ) );
            if ( attr == null ) return ValueShape.Text;
            var ft = attr.FieldType?.Class ?? string.Empty;

            if ( ft.EndsWith( ".FileFieldType" )
                || ft.EndsWith( ".ImageFieldType" )
                || ft.EndsWith( ".BinaryFileFieldType" )
                || ft.EndsWith( ".BackgroundCheckFieldType" )
                || ft.EndsWith( ".MatrixFieldType" )
                || ft.EndsWith( ".AttributeMatrixFieldType" )
                || ft.EndsWith( ".KeyValueListFieldType" )
                || ft.EndsWith( ".EncryptedTextFieldType" )
                || ft.EndsWith( ".SSNFieldType" )
                || ft.EndsWith( ".LavaFieldType" )
                || ft.EndsWith( ".LavaCommandsFieldType" )
                || ft.EndsWith( ".CodeEditorFieldType" )
                || ft.EndsWith( ".HtmlFieldType" )
                || ft.EndsWith( ".MarkdownFieldType" ) )
                return ValueShape.BlankOnly;

            if ( ft.EndsWith( ".BooleanFieldType" ) ) return ValueShape.Boolean;
            if ( ft.EndsWith( ".DateFieldType" ) || ft.EndsWith( ".DateTimeFieldType" ) || ft.EndsWith( ".DateRangeFieldType" ) || ft.EndsWith( ".TimeFieldType" ) )
                return ValueShape.Date;
            if ( ft.EndsWith( ".IntegerFieldType" ) || ft.EndsWith( ".DecimalFieldType" )
                || ft.EndsWith( ".RangeSliderFieldType" ) || ft.EndsWith( ".DayOfWeekFieldType" )
                || ft.EndsWith( ".MonthDayFieldType" ) )
                return ValueShape.Numeric;
            if ( ft.EndsWith( ".DefinedValueFieldType" ) || ft.EndsWith( ".DefinedValueRangeFieldType" )
                || ft.EndsWith( ".CampusFieldType" ) || ft.EndsWith( ".CampusesFieldType" )
                || ft.EndsWith( ".PersonFieldType" ) || ft.EndsWith( ".GroupFieldType" )
                || ft.EndsWith( ".GroupTypeFieldType" ) || ft.EndsWith( ".GroupRoleFieldType" )
                || ft.EndsWith( ".CategoryFieldType" ) || ft.EndsWith( ".LocationFieldType" )
                || ft.EndsWith( ".ScheduleFieldType" ) || ft.EndsWith( ".StepProgramFieldType" )
                || ft.EndsWith( ".StepTypeFieldType" ) || ft.EndsWith( ".StepProgramStepTypeFieldType" )
                || ft.EndsWith( ".SingleSelectFieldType" ) || ft.EndsWith( ".MultiSelectFieldType" )
                || ft.EndsWith( ".SelectSingleFieldType" ) || ft.EndsWith( ".SelectMultipleFieldType" )
                || ft.EndsWith( ".ConnectionStatusFieldType" ) || ft.EndsWith( ".ConnectionStateFieldType" )
                || ft.EndsWith( ".WorkflowTypeFieldType" ) || ft.EndsWith( ".PageReferenceFieldType" )
                || ft.EndsWith( ".EntityTypeFieldType" ) || ft.EndsWith( ".MediaElementFieldType" ) )
                return ValueShape.Picker;
            return ValueShape.Text;
        }

        #endregion

        #region ViewState

        private List<EditGroup> Groups
        {
            get
            {
                var json = ViewState["Groups"] as string;
                if ( string.IsNullOrEmpty( json ) )
                {
                    return new List<EditGroup>();
                }
                try { return JsonConvert.DeserializeObject<List<EditGroup>>( json ) ?? new List<EditGroup>(); }
                catch { return new List<EditGroup>(); }
            }
            set { ViewState["Groups"] = JsonConvert.SerializeObject( value ?? new List<EditGroup>() ); }
        }

        private LogicGroupType TopType
        {
            get { return ( ViewState["TopType"] as string ).ConvertToEnumOrNull<LogicGroupType>() ?? LogicGroupType.Any; }
            set { ViewState["TopType"] = value.ToString(); }
        }

        /// <summary>When true, the config is deeper than 2 levels — only the raw-JSON view is shown.</summary>
        private bool ForceRawOnly
        {
            get { return ( ViewState["ForceRawOnly"] as bool? ) ?? false; }
            set { ViewState["ForceRawOnly"] = value; }
        }

        /// <summary>Set when the loaded config was a legacy flat array (so SetMatchAll may still set the group/top type).</summary>
        private bool LoadedFromLegacyArray
        {
            get { return ( ViewState["LoadedLegacy"] as bool? ) ?? false; }
            set { ViewState["LoadedLegacy"] = value; }
        }

        #endregion

        #region Page Lifecycle

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );
            if ( !Page.IsPostBack )
            {
                BindGroups();
            }
        }

        #endregion

        #region Public Get/Set JSON

        public bool HasInSessionState
        {
            get { return ( ViewState["FCE_HasState"] as bool? ) ?? false; }
            private set { ViewState["FCE_HasState"] = value; }
        }

        private void SetJson( string json )
        {
            HasInSessionState = true;
            ForceRawOnly = false;
            LoadedFromLegacyArray = false;

            JToken root = null;
            if ( !string.IsNullOrWhiteSpace( json ) )
            {
                try { root = JToken.Parse( json ); } catch { root = null; }
            }

            if ( root == null )
            {
                // Blank/invalid → no groups. A subsequent SetMatchAll may seed a single group.
                Groups = new List<EditGroup>();
                TopType = LogicGroupType.Any;
                LoadedFromLegacyArray = true;
            }
            else if ( root.Type == JTokenType.Array )
            {
                // Legacy flat array → one group holding all conditions. Group/top type
                // is finalized by the SetMatchAll the host calls right after this.
                var leaves = SafeLeaves( root );
                Groups = new List<EditGroup> { new EditGroup { Type = LogicGroupType.All, Conditions = leaves } };
                TopType = LogicGroupType.All;
                LoadedFromLegacyArray = true;
            }
            else if ( TryCoerceToTwoLevel( root, out var top, out var groups ) )
            {
                TopType = top;
                Groups = groups;
            }
            else
            {
                // Deeper than two levels (groups within groups) — fall back to raw JSON.
                ForceRawOnly = true;
                ViewState["RawJson"] = json;
            }

            if ( ForceRawOnly )
            {
                pnlRaw.Visible = true;
                hfShowRaw.Value = "true";
                lToggleRawText.Text = "Hide raw JSON";
            }

            BindGroups();
        }

        private string GetJson()
        {
            if ( ForceRawOnly )
            {
                return ( ViewState["RawJson"] as string ) ?? ( ceRawJson.Text ?? "[]" );
            }

            CaptureToState();

            var groups = Groups;
            if ( groups.Count == 0 || groups.All( g => g.Conditions.Count == 0 ) )
            {
                // No real conditions → emit empty array (engine treats as "matches nobody").
                return "[]";
            }

            var root = new LogicNode<FilterCondition>
            {
                Type = TopType,
                Children = groups.Select( g => new LogicNode<FilterCondition>
                {
                    Type = g.Type,
                    Children = g.Conditions.Select( c => new LogicNode<FilterCondition> { Leaf = c } ).ToList()
                } ).ToList()
            };
            return LogicTree.ToJson( root );
        }

        private static List<FilterCondition> SafeLeaves( JToken arrayToken )
        {
            try { return arrayToken.ToObject<List<FilterCondition>>() ?? new List<FilterCondition>(); }
            catch { return new List<FilterCondition>(); }
        }

        /// <summary>
        /// Tries to map a parsed tree onto the two-level model the visual editor renders:
        /// a top group whose children are EITHER all leaves (→ one implicit group) or all
        /// groups-of-leaves. Returns false for anything deeper or mixed.
        /// </summary>
        private bool TryCoerceToTwoLevel( JToken root, out LogicGroupType topType, out List<EditGroup> groups )
        {
            topType = LogicGroupType.Any;
            groups = new List<EditGroup>();

            var node = LogicTree.Parse<FilterCondition>( root.ToString(), LogicGroupType.All );
            if ( node == null )
            {
                return false;
            }

            // A bare leaf root → single group with that one condition.
            if ( node.IsLeaf )
            {
                topType = LogicGroupType.All;
                groups.Add( new EditGroup { Type = LogicGroupType.All, Conditions = node.Leaf != null ? new List<FilterCondition> { node.Leaf } : new List<FilterCondition>() } );
                return true;
            }

            var children = node.Children ?? new List<LogicNode<FilterCondition>>();

            // All children are leaves → one group.
            if ( children.All( c => c.IsLeaf ) )
            {
                topType = node.Type.Value;
                groups.Add( new EditGroup
                {
                    Type = node.Type.Value,
                    Conditions = children.Where( c => c.Leaf != null ).Select( c => c.Leaf ).ToList()
                } );
                return true;
            }

            // All children are groups whose own children are all leaves → two-level.
            if ( children.All( c => !c.IsLeaf && ( c.Children ?? new List<LogicNode<FilterCondition>>() ).All( gc => gc.IsLeaf ) ) )
            {
                topType = node.Type.Value;
                foreach ( var groupNode in children )
                {
                    groups.Add( new EditGroup
                    {
                        Type = groupNode.Type.Value,
                        Conditions = ( groupNode.Children ?? new List<LogicNode<FilterCondition>>() )
                            .Where( c => c.Leaf != null ).Select( c => c.Leaf ).ToList()
                    } );
                }
                return true;
            }

            return false;
        }

        public void SetMatchAll( bool matchAll )
        {
            // Only meaningful for legacy/empty loads; nested configs carry their own
            // top/group types parsed from JSON and must not be overwritten.
            if ( !LoadedFromLegacyArray )
            {
                return;
            }

            var t = matchAll ? LogicGroupType.All : LogicGroupType.Any;
            TopType = t;
            var groups = Groups;
            if ( groups.Count == 1 )
            {
                groups[0].Type = t;
                Groups = groups;
            }
            BindGroups();
        }

        public bool GetMatchAll()
        {
            // Best-effort back-compat signal for the host's MatchAll attribute value.
            return TopType == LogicGroupType.All;
        }

        public List<string> GetValidationErrors()
        {
            if ( ForceRawOnly )
            {
                return new List<string>();
            }

            CaptureToState();
            var errors = new List<string>();
            var groups = Groups;
            for ( int g = 0; g < groups.Count; g++ )
            {
                var conditions = groups[g].Conditions;
                for ( int i = 0; i < conditions.Count; i++ )
                {
                    var c = conditions[i];
                    var prefix = ( groups.Count > 1 ? "Group " + ( g + 1 ) + ", filter row " : "Filter row " ) + ( i + 1 );

                    if ( string.IsNullOrWhiteSpace( c.Key ) )
                    {
                        errors.Add( prefix + ": " + ( c.Source == FilterSource.Property ? "Person Property" : "Person Attribute" ) + " is required." );
                    }

                    bool needsValue = c.Comparison != ComparisonType.IsBlank
                                   && c.Comparison != ComparisonType.IsNotBlank;
                    if ( needsValue && string.IsNullOrEmpty( c.Value ) )
                    {
                        errors.Add( prefix + ": Value is required for comparison '" + SplitCamelCase( c.Comparison.ToString() ) + "'." );
                    }
                }
            }
            return errors;
        }

        #endregion

        #region Binding

        private static void BindGroupTypeChoices( RockDropDownList ddl, LogicGroupType selected )
        {
            ddl.Items.Clear();
            ddl.Items.Add( new ListItem( "All are true", LogicGroupType.All.ToString() ) );
            ddl.Items.Add( new ListItem( "Any are true", LogicGroupType.Any.ToString() ) );
            ddl.Items.Add( new ListItem( "None are true", LogicGroupType.AllFalse.ToString() ) );
            ddl.Items.Add( new ListItem( "Not all are true", LogicGroupType.AnyFalse.ToString() ) );
            ddl.SetValue( selected.ToString() );
        }

        private void BindGroups()
        {
            BindGroupTypeChoices( ddlTopType, TopType );

            pnlGroups.Visible = !ForceRawOnly;
            nbForceRaw.Visible = ForceRawOnly;

            var groups = Groups;
            phNoGroups.Visible = !ForceRawOnly && groups.Count == 0;
            rGroups.DataSource = groups;
            rGroups.DataBind();

            if ( pnlRaw.Visible )
            {
                ceRawJson.Text = ForceRawOnly
                    ? ( ( ViewState["RawJson"] as string ) ?? "[]" )
                    : GetJsonForRawPreview();
            }
        }

        private string GetJsonForRawPreview()
        {
            // Pretty-print the current visual state for the raw panel without disturbing state.
            var groups = Groups;
            if ( groups.Count == 0 || groups.All( g => g.Conditions.Count == 0 ) )
            {
                return "[]";
            }
            var root = new LogicNode<FilterCondition>
            {
                Type = TopType,
                Children = groups.Select( g => new LogicNode<FilterCondition>
                {
                    Type = g.Type,
                    Children = g.Conditions.Select( c => new LogicNode<FilterCondition> { Leaf = c } ).ToList()
                } ).ToList()
            };
            return JToken.Parse( LogicTree.ToJson( root ) ).ToString( Formatting.Indented );
        }

        protected void rGroups_ItemDataBound( object sender, RepeaterItemEventArgs e )
        {
            if ( e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem )
            {
                return;
            }

            var group = ( EditGroup ) e.Item.DataItem;

            var ddlGroupType = ( RockDropDownList ) e.Item.FindControl( "ddlGroupType" );
            BindGroupTypeChoices( ddlGroupType, group.Type );

            var phNoRows = ( PlaceHolder ) e.Item.FindControl( "phNoRows" );
            phNoRows.Visible = group.Conditions.Count == 0;

            var rRows = ( Repeater ) e.Item.FindControl( "rRows" );
            rRows.DataSource = group.Conditions;
            rRows.DataBind();
        }

        protected void rRows_ItemDataBound( object sender, RepeaterItemEventArgs e )
        {
            if ( e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem )
            {
                return;
            }

            var condition = ( FilterCondition ) e.Item.DataItem;
            BindRow( e.Item, condition );
        }

        private void BindRow( RepeaterItem item, FilterCondition condition )
        {
            var ddlSource     = ( RockDropDownList ) item.FindControl( "ddlSource" );
            var ddlKeyProp    = ( RockDropDownList ) item.FindControl( "ddlKeyProperty" );
            var ddlKeyAttr    = ( RockDropDownList ) item.FindControl( "ddlKeyAttribute" );
            var ddlComp       = ( RockDropDownList ) item.FindControl( "ddlComparison" );
            var tbVal         = ( RockTextBox ) item.FindControl( "tbValueText" );
            var cbVal         = ( RockDropDownList ) item.FindControl( "ddlValueBool" );
            var dpVal         = ( DatePicker ) item.FindControl( "dpValueDate" );
            var cpVal         = ( CampusPicker ) item.FindControl( "cpValueCampus" );
            var dvpVal        = ( DefinedValuePicker ) item.FindControl( "dvpValue" );

            // ===== Source =====
            ddlSource.Items.Clear();
            ddlSource.Items.Add( new ListItem( "Property",  FilterSource.Property.ToString() ) );
            ddlSource.Items.Add( new ListItem( "Attribute", FilterSource.Attribute.ToString() ) );
            ddlSource.SetValue( condition.Source.ToString() );

            // ===== Key (Property dropdown OR Attribute dropdown depending on Source) =====
            if ( condition.Source == FilterSource.Property )
            {
                ddlKeyProp.Visible = true;
                ddlKeyAttr.Visible = false;
                ddlKeyProp.Items.Clear();
                ddlKeyProp.Items.Add( new ListItem( "(select)", string.Empty ) );
                foreach ( var p in GetPersonProperties() )
                {
                    ddlKeyProp.Items.Add( new ListItem( p.Name, p.Name ) );
                }
                ddlKeyProp.SetValue( condition.Key ?? string.Empty );
            }
            else
            {
                ddlKeyProp.Visible = false;
                ddlKeyAttr.Visible = true;
                ddlKeyAttr.Items.Clear();
                ddlKeyAttr.Items.Add( new ListItem( "(select)", string.Empty ) );
                foreach ( var a in GetPersonAttributes() )
                {
                    ddlKeyAttr.Items.Add( new ListItem( a.Name + " (" + a.Key + ")", a.Key ) );
                }
                ddlKeyAttr.SetValue( condition.Key ?? string.Empty );
            }

            // ===== Comparison =====
            var shape = GetShape( condition );
            var allowed = GetAllowedComparisons( shape );
            ddlComp.Items.Clear();
            foreach ( var ct in allowed )
            {
                ddlComp.Items.Add( new ListItem( SplitCamelCase( ct.ToString() ), ct.ToString() ) );
            }
            var resolvedComparison = allowed.Contains( condition.Comparison ) ? condition.Comparison : allowed[0];
            ddlComp.SetValue( resolvedComparison.ToString() );

            // ===== Value =====
            bool needsValue = resolvedComparison != ComparisonType.IsBlank
                           && resolvedComparison != ComparisonType.IsNotBlank;

            tbVal.Visible = cbVal.Visible = dpVal.Visible = cpVal.Visible = dvpVal.Visible = false;
            if ( needsValue )
            {
                RenderSmartValueControl( condition, tbVal, cbVal, dpVal, cpVal, dvpVal );
            }
        }

        private void RenderSmartValueControl(
            FilterCondition condition,
            RockTextBox tbVal,
            RockDropDownList cbVal,
            DatePicker dpVal,
            CampusPicker cpVal,
            DefinedValuePicker dvpVal )
        {
            if ( condition.Source == FilterSource.Property && !string.IsNullOrWhiteSpace( condition.Key ) )
            {
                var info = GetPersonProperties().FirstOrDefault( p => string.Equals( p.Name, condition.Key, StringComparison.OrdinalIgnoreCase ) );
                if ( !string.IsNullOrEmpty( info.Name ) )
                {
                    if ( info.DefinedTypeGuid.HasValue )
                    {
                        var dt = DefinedTypeCache.Get( info.DefinedTypeGuid.Value );
                        if ( dt != null )
                        {
                            dvpVal.Visible = true;
                            dvpVal.DefinedTypeId = dt.Id;
                            dvpVal.SetValue( condition.Value );
                            return;
                        }
                    }

                    if ( string.Equals( info.Name, "PrimaryCampusId", StringComparison.OrdinalIgnoreCase ) )
                    {
                        cpVal.Visible = true;
                        cpVal.SetValue( condition.Value.AsIntegerOrNull() );
                        return;
                    }

                    if ( info.Type == typeof( bool ) )
                    {
                        cbVal.Visible = true;
                        cbVal.SetValue( condition.Value.AsBoolean() ? "True" : "False" );
                        return;
                    }

                    if ( info.Type == typeof( DateTime ) )
                    {
                        dpVal.Visible = true;
                        dpVal.SelectedDate = condition.Value.AsDateTime();
                        return;
                    }
                }
            }
            else if ( condition.Source == FilterSource.Attribute && !string.IsNullOrWhiteSpace( condition.Key ) )
            {
                var attr = GetPersonAttributes().FirstOrDefault( a => string.Equals( a.Key, condition.Key, StringComparison.OrdinalIgnoreCase ) );
                if ( attr != null )
                {
                    var ftClass = attr.FieldType?.Class ?? string.Empty;
                    if ( ftClass.EndsWith( ".BooleanFieldType" ) )
                    {
                        cbVal.Visible = true;
                        cbVal.SetValue( condition.Value.AsBoolean() ? "True" : "False" );
                        return;
                    }
                    if ( ftClass.EndsWith( ".DateFieldType" ) || ftClass.EndsWith( ".DateTimeFieldType" ) )
                    {
                        dpVal.Visible = true;
                        dpVal.SelectedDate = condition.Value.AsDateTime();
                        return;
                    }
                    if ( ftClass.EndsWith( ".CampusFieldType" ) )
                    {
                        cpVal.Visible = true;
                        cpVal.SetValue( condition.Value.AsIntegerOrNull() );
                        return;
                    }
                    if ( ftClass.EndsWith( ".DefinedValueFieldType" ) )
                    {
                        var dtGuidStr = attr.QualifierValues != null
                            && attr.QualifierValues.TryGetValue( "definedtype", out var dtCfg )
                                ? dtCfg.Value
                                : null;
                        var dtId = dtGuidStr.AsIntegerOrNull();
                        if ( dtId.HasValue )
                        {
                            dvpVal.Visible = true;
                            dvpVal.DefinedTypeId = dtId.Value;
                            dvpVal.SetValue( condition.Value );
                            return;
                        }
                    }
                }
            }

            tbVal.Visible = true;
            tbVal.Text = condition.Value ?? string.Empty;
        }

        /// <summary>
        /// Reads every group's All/Any selector and its condition rows back into the
        /// Groups ViewState model. The single source of truth for postback handlers.
        /// </summary>
        private void CaptureToState()
        {
            if ( ForceRawOnly )
            {
                return;
            }

            HasInSessionState = true;
            var groups = new List<EditGroup>();

            foreach ( RepeaterItem gItem in rGroups.Items )
            {
                if ( gItem.ItemType != ListItemType.Item && gItem.ItemType != ListItemType.AlternatingItem )
                {
                    continue;
                }

                var ddlGroupType = ( RockDropDownList ) gItem.FindControl( "ddlGroupType" );
                var groupType = ddlGroupType.SelectedValue.ConvertToEnumOrNull<LogicGroupType>() ?? LogicGroupType.All;

                var conditions = new List<FilterCondition>();
                var innerRep = ( Repeater ) gItem.FindControl( "rRows" );
                foreach ( RepeaterItem rItem in innerRep.Items )
                {
                    if ( rItem.ItemType != ListItemType.Item && rItem.ItemType != ListItemType.AlternatingItem )
                    {
                        continue;
                    }

                    var ddlSource  = ( RockDropDownList ) rItem.FindControl( "ddlSource" );
                    var ddlKeyProp = ( RockDropDownList ) rItem.FindControl( "ddlKeyProperty" );
                    var ddlKeyAttr = ( RockDropDownList ) rItem.FindControl( "ddlKeyAttribute" );
                    var ddlComp    = ( RockDropDownList ) rItem.FindControl( "ddlComparison" );

                    var source = ddlSource.SelectedValue.ConvertToEnumOrNull<FilterSource>() ?? FilterSource.Property;
                    var key    = source == FilterSource.Property ? ddlKeyProp.SelectedValue : ddlKeyAttr.SelectedValue;
                    var comp   = ddlComp.SelectedValue.ConvertToEnumOrNull<ComparisonType>() ?? ComparisonType.EqualTo;
                    var value  = ExtractValueFromRow( rItem, source, key, comp );

                    conditions.Add( new FilterCondition
                    {
                        Source = source,
                        Key = key,
                        Comparison = comp,
                        Value = value
                    } );
                }

                groups.Add( new EditGroup { Type = groupType, Conditions = conditions } );
            }

            Groups = groups;
            TopType = ddlTopType.SelectedValue.ConvertToEnumOrNull<LogicGroupType>() ?? LogicGroupType.Any;
        }

        private static string ExtractValueFromRow( RepeaterItem item, FilterSource source, string key, ComparisonType comp )
        {
            if ( comp == ComparisonType.IsBlank || comp == ComparisonType.IsNotBlank )
            {
                return string.Empty;
            }

            var tbVal  = ( RockTextBox ) item.FindControl( "tbValueText" );
            var cbVal  = ( RockDropDownList ) item.FindControl( "ddlValueBool" );
            var dpVal  = ( DatePicker ) item.FindControl( "dpValueDate" );
            var cpVal  = ( CampusPicker ) item.FindControl( "cpValueCampus" );
            var dvpVal = ( DefinedValuePicker ) item.FindControl( "dvpValue" );

            if ( cbVal.Visible )  return cbVal.SelectedValue ?? "False";
            if ( dpVal.Visible )  return dpVal.SelectedDate.HasValue ? dpVal.SelectedDate.Value.ToString( "yyyy-MM-dd" ) : string.Empty;
            if ( cpVal.Visible )  return cpVal.SelectedCampusId?.ToString() ?? string.Empty;
            if ( dvpVal.Visible ) return dvpVal.SelectedValue ?? string.Empty;
            return tbVal.Text ?? string.Empty;
        }

        #endregion

        #region Postback Handlers

        protected void ddlTopType_SelectedIndexChanged( object sender, EventArgs e )
        {
            CaptureToState();
            BindGroups();
        }

        protected void ddlGroupType_SelectedIndexChanged( object sender, EventArgs e )
        {
            CaptureToState();
            BindGroups();
        }

        protected void ddlSource_SelectedIndexChanged( object sender, EventArgs e )
        {
            CaptureToState();
            SnapComparisonToShapeDefault( sender );
            BindGroups();
        }

        protected void ddlKey_SelectedIndexChanged( object sender, EventArgs e )
        {
            CaptureToState();
            SnapComparisonToShapeDefault( sender );
            BindGroups();
        }

        /// <summary>
        /// Walks from a row control up to its (group index, row index) and snaps that
        /// row's Comparison to the first allowed value for its new shape.
        /// </summary>
        private void SnapComparisonToShapeDefault( object sender )
        {
            var ctl = sender as Control;
            var rowItem = ctl?.NamingContainer as RepeaterItem;
            var groupItem = rowItem?.NamingContainer?.NamingContainer as RepeaterItem;
            if ( rowItem == null || groupItem == null || rowItem.ItemIndex < 0 || groupItem.ItemIndex < 0 )
            {
                return;
            }

            var groups = Groups;
            if ( groupItem.ItemIndex >= groups.Count ) return;
            var conditions = groups[groupItem.ItemIndex].Conditions;
            if ( rowItem.ItemIndex >= conditions.Count ) return;

            var allowed = GetAllowedComparisons( GetShape( conditions[rowItem.ItemIndex] ) );
            conditions[rowItem.ItemIndex].Comparison = allowed[0];
            conditions[rowItem.ItemIndex].Value = string.Empty;
            Groups = groups;
        }

        protected void ddlComparison_SelectedIndexChanged( object sender, EventArgs e )
        {
            CaptureToState();
            BindGroups();
        }

        protected void lbAddGroup_Click( object sender, EventArgs e )
        {
            CaptureToState();
            var groups = Groups;
            groups.Add( new EditGroup
            {
                Type = LogicGroupType.All,
                Conditions = new List<FilterCondition>
                {
                    new FilterCondition { Source = FilterSource.Property, Key = string.Empty, Comparison = ComparisonType.EqualTo, Value = string.Empty }
                }
            } );
            Groups = groups;
            BindGroups();
        }

        protected void rGroups_ItemCommand( object source, RepeaterCommandEventArgs e )
        {
            CaptureToState();
            var groups = Groups;
            var groupIndex = e.CommandArgument.ToString().AsInteger();

            if ( e.CommandName == "AddCondition" )
            {
                if ( groupIndex >= 0 && groupIndex < groups.Count )
                {
                    groups[groupIndex].Conditions.Add( new FilterCondition
                    {
                        Source = FilterSource.Property,
                        Key = string.Empty,
                        Comparison = ComparisonType.EqualTo,
                        Value = string.Empty
                    } );
                    Groups = groups;
                }
            }
            else if ( e.CommandName == "DeleteGroup" )
            {
                if ( groupIndex >= 0 && groupIndex < groups.Count )
                {
                    groups.RemoveAt( groupIndex );
                    Groups = groups;
                }
            }
            else
            {
                return; // not ours (e.g. a bubbled DeleteRow) — leave for the inner handler
            }

            BindGroups();
        }

        protected void rRows_ItemCommand( object source, RepeaterCommandEventArgs e )
        {
            if ( e.CommandName != "DeleteRow" )
            {
                return;
            }

            CaptureToState();

            var groupItem = e.Item.NamingContainer?.NamingContainer as RepeaterItem;
            if ( groupItem == null )
            {
                return;
            }
            var groupIndex = groupItem.ItemIndex;
            var rowIndex = e.CommandArgument.ToString().AsInteger();

            var groups = Groups;
            if ( groupIndex >= 0 && groupIndex < groups.Count )
            {
                var conditions = groups[groupIndex].Conditions;
                if ( rowIndex >= 0 && rowIndex < conditions.Count )
                {
                    conditions.RemoveAt( rowIndex );
                    Groups = groups;
                }
            }
            BindGroups();
        }

        protected void lbApplyRaw_Click( object sender, EventArgs e )
        {
            nbRawError.Visible = false;
            var text = ceRawJson.Text ?? "[]";
            try
            {
                // Re-run SetJson so the same coerce/guard logic applies; a now-valid
                // two-level config returns to the visual editor automatically.
                JToken.Parse( text ); // validate
                var keepRawOpen = true;
                SetJson( text );
                pnlRaw.Visible = keepRawOpen;
                hfShowRaw.Value = keepRawOpen ? "true" : "false";
                lToggleRawText.Text = pnlRaw.Visible ? "Hide raw JSON" : "Show raw JSON";
                BindGroups();
            }
            catch ( Exception ex )
            {
                nbRawError.Text = "Could not parse JSON: " + ex.Message;
                nbRawError.Visible = true;
            }
        }

        protected void lbToggleRaw_Click( object sender, EventArgs e )
        {
            CaptureToState();
            pnlRaw.Visible = !pnlRaw.Visible;
            hfShowRaw.Value = pnlRaw.Visible ? "true" : "false";
            lToggleRawText.Text = pnlRaw.Visible ? "Hide raw JSON" : "Show raw JSON";
            BindGroups();
        }

        #endregion

        #region Helpers

        private static string SplitCamelCase( string s )
        {
            if ( string.IsNullOrEmpty( s ) ) return s;
            return System.Text.RegularExpressions.Regex.Replace( s, "(?<=[a-z])([A-Z])", " $1" );
        }

        #endregion

        #region View-mode summary formatter

        /// <summary>
        /// Render the configured FilterConditions JSON as a friendly vertical summary.
        /// Handles both a legacy flat array (joined by matchAll) and the nested
        /// two-level group tree.
        /// </summary>
        public static string FormatSummaryHtml( string filterConditionsJson, bool matchAll )
        {
            JToken root = null;
            if ( !string.IsNullOrWhiteSpace( filterConditionsJson ) )
            {
                try { root = JToken.Parse( filterConditionsJson ); } catch { return "<em class='text-muted'>(invalid JSON)</em>"; }
            }

            if ( root == null )
            {
                return "<em class='text-muted'>(no conditions configured &mdash; matches nobody)</em>";
            }

            if ( root.Type == JTokenType.Array )
            {
                var conditions = SafeLeaves( root );
                if ( conditions.Count == 0 )
                {
                    return "<em class='text-muted'>(no conditions configured &mdash; matches nobody)</em>";
                }
                return RenderConditionList( conditions, matchAll ? "AND" : "OR" );
            }

            // Nested tree.
            var node = LogicTree.Parse<FilterCondition>( filterConditionsJson, LogicGroupType.All );
            if ( node == null || LogicTree.IsEmpty( node ) )
            {
                return "<em class='text-muted'>(no conditions configured &mdash; matches nobody)</em>";
            }

            return RenderNode( node, 0 );
        }

        private static string RenderNode( LogicNode<FilterCondition> node, int depth )
        {
            if ( node == null )
            {
                return string.Empty;
            }

            if ( node.IsLeaf )
            {
                return "<div>" + FormatConditionLine( node.Leaf ) + "</div>";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append( "<div class='text-muted small'>" ).Append( GroupLabel( node.Type.Value ) ).Append( ":</div>" );
            sb.Append( "<div style='margin-left:16px;border-left:2px solid #eee;padding-left:8px;'>" );
            var children = node.Children ?? new List<LogicNode<FilterCondition>>();
            for ( int i = 0; i < children.Count; i++ )
            {
                sb.Append( RenderNode( children[i], depth + 1 ) );
            }
            sb.Append( "</div>" );
            return sb.ToString();
        }

        private static string GroupLabel( LogicGroupType type )
        {
            switch ( type )
            {
                case LogicGroupType.All:      return "Match ALL of";
                case LogicGroupType.Any:      return "Match ANY of";
                case LogicGroupType.AllFalse: return "NONE of these are true";
                case LogicGroupType.AnyFalse: return "NOT ALL of these are true";
                default:                      return "Match";
            }
        }

        private static string RenderConditionList( List<FilterCondition> conditions, string joiner )
        {
            var sb = new System.Text.StringBuilder();
            for ( int i = 0; i < conditions.Count; i++ )
            {
                if ( i > 0 )
                {
                    sb.Append( "<div class='text-muted small'>" ).Append( joiner ).Append( "</div>" );
                }
                sb.Append( "<div>" ).Append( FormatConditionLine( conditions[i] ) ).Append( "</div>" );
            }
            return sb.ToString();
        }

        private static string FormatConditionLine( FilterCondition c )
        {
            if ( c == null || string.IsNullOrWhiteSpace( c.Key ) )
            {
                return "<span class='text-danger'>(incomplete row)</span>";
            }

            var keyLabel = System.Web.HttpUtility.HtmlEncode( GetKeyLabel( c ) );
            var cmpText = ComparisonText( c.Comparison );

            if ( c.Comparison == ComparisonType.IsBlank || c.Comparison == ComparisonType.IsNotBlank )
            {
                return "<strong>" + keyLabel + "</strong> <span class='text-muted'>" + cmpText + "</span>";
            }

            var resolved = ResolveValueForDisplay( c );
            return "<strong>" + keyLabel + "</strong> "
                + "<span class='text-muted'>" + cmpText + "</span> "
                + System.Web.HttpUtility.HtmlEncode( resolved ?? string.Empty );
        }

        private static string GetKeyLabel( FilterCondition c )
        {
            if ( c.Source == FilterSource.Property )
            {
                var split = SplitCamelCase( c.Key );
                if ( split.EndsWith( " Value Id", StringComparison.Ordinal ) )
                    return split.Substring( 0, split.Length - " Value Id".Length );
                if ( split.EndsWith( " Id", StringComparison.Ordinal ) )
                    return split.Substring( 0, split.Length - " Id".Length );
                return split;
            }
            var attr = GetPersonAttributes().FirstOrDefault( a => string.Equals( a.Key, c.Key, StringComparison.OrdinalIgnoreCase ) );
            return attr != null ? attr.Name : c.Key;
        }

        private static string ComparisonText( ComparisonType cmp )
        {
            switch ( cmp )
            {
                case ComparisonType.EqualTo:               return "equal to";
                case ComparisonType.NotEqualTo:            return "not equal to";
                case ComparisonType.IsBlank:               return "is blank";
                case ComparisonType.IsNotBlank:            return "is not blank";
                case ComparisonType.Contains:              return "contains";
                case ComparisonType.GreaterThan:           return "greater than";
                case ComparisonType.LessThan:              return "less than";
                case ComparisonType.GreaterThanOrEqualTo:  return "&ge;";
                case ComparisonType.LessThanOrEqualTo:     return "&le;";
                default:                                   return SplitCamelCase( cmp.ToString() );
            }
        }

        private static string ResolveValueForDisplay( FilterCondition c )
        {
            var raw = c.Value ?? string.Empty;
            var shape = GetShape( c );

            if ( shape == ValueShape.Picker )
            {
                if ( c.Source == FilterSource.Property )
                {
                    var info = GetPersonProperties().FirstOrDefault( p => string.Equals( p.Name, c.Key, StringComparison.OrdinalIgnoreCase ) );
                    if ( info.DefinedTypeGuid.HasValue )
                    {
                        var dv = Rock.Web.Cache.DefinedValueCache.Get( raw.AsInteger() );
                        if ( dv != null ) return dv.Value;
                    }
                    if ( string.Equals( info.Name, "PrimaryCampusId", StringComparison.OrdinalIgnoreCase ) )
                    {
                        var campus = Rock.Web.Cache.CampusCache.Get( raw.AsInteger() );
                        if ( campus != null ) return campus.Name;
                    }
                }
                else
                {
                    var attr = GetPersonAttributes().FirstOrDefault( a => string.Equals( a.Key, c.Key, StringComparison.OrdinalIgnoreCase ) );
                    if ( attr != null )
                    {
                        var ft = attr.FieldType?.Class ?? string.Empty;
                        if ( ft.EndsWith( ".DefinedValueFieldType" ) )
                        {
                            var dv = Rock.Web.Cache.DefinedValueCache.Get( raw.AsGuidOrNull() ?? Guid.Empty );
                            if ( dv == null ) dv = Rock.Web.Cache.DefinedValueCache.Get( raw.AsInteger() );
                            if ( dv != null ) return dv.Value;
                        }
                        else if ( ft.EndsWith( ".CampusFieldType" ) )
                        {
                            var campus = Rock.Web.Cache.CampusCache.Get( raw.AsGuidOrNull() ?? Guid.Empty );
                            if ( campus == null ) campus = Rock.Web.Cache.CampusCache.Get( raw.AsInteger() );
                            if ( campus != null ) return campus.Name;
                        }
                    }
                }
            }
            else if ( shape == ValueShape.Date )
            {
                var dt = raw.AsDateTime();
                if ( dt.HasValue ) return dt.Value.ToShortDateString();
            }

            return raw;
        }

        #endregion
    }
}
