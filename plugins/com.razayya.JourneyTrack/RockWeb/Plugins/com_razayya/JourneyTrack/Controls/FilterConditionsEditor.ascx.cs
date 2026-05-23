using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.UI;
using System.Web.UI.WebControls;

using com.razayya.JourneyTrack.CalculationTypes;
using ComparisonType = com.razayya.JourneyTrack.Model.ComparisonType;

using Newtonsoft.Json;

using Rock;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_razayya.JourneyTrack.Controls
{
    public partial class FilterConditionsEditor : UserControl
    {
        #region Public API

        public string Value
        {
            get { return GetJson(); }
            set { SetJson( value ); }
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
        // (Listed by Person property name → Rock.SystemGuid.DefinedType.* string Guid.)
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

        // A ValueShape collapses Source+Key+FieldType into a small alphabet that
        // drives (a) which comparisons appear in the dropdown and (b) which Value
        // control is rendered. Keeps the matrix maintainable in one place.
        private enum ValueShape
        {
            None,        // no key chosen yet
            BlankOnly,   // File, Image, Matrix, Encrypted, Lava, SSN — values are 1:1 / opaque
            Boolean,
            Text,
            Numeric,
            Date,
            Picker       // DefinedValue, Campus, Person, Group, etc.
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

            // BlankOnly: types whose stored value is opaque/1:1 (File guid, matrix guid, encrypted blob, lava template).
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

        private List<FilterCondition> Conditions
        {
            get
            {
                var json = ViewState["Conditions"] as string;
                if ( string.IsNullOrEmpty( json ) )
                {
                    return new List<FilterCondition>();
                }
                try { return JsonConvert.DeserializeObject<List<FilterCondition>>( json ) ?? new List<FilterCondition>(); }
                catch { return new List<FilterCondition>(); }
            }
            set { ViewState["Conditions"] = JsonConvert.SerializeObject( value ?? new List<FilterCondition>() ); }
        }

        private bool MatchAll
        {
            get { return ( ViewState["MatchAll"] as bool? ) ?? true; }
            set { ViewState["MatchAll"] = value; }
        }

        #endregion

        #region Page Lifecycle

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );
            if ( !Page.IsPostBack )
            {
                BindRepeater();
            }
        }

        #endregion

        #region Public Get/Set JSON

        /// <summary>
        /// True once the editor has been seeded (or interacted with) at least once in
        /// the current page lifetime. The detail block checks this before re-seeding
        /// the editor on calc-type toggles, so switching away and back doesn't blow
        /// away the user's in-session row state.
        /// </summary>
        public bool HasInSessionState
        {
            get { return ( ViewState["FCE_HasState"] as bool? ) ?? false; }
            private set { ViewState["FCE_HasState"] = value; }
        }

        private void SetJson( string json )
        {
            try
            {
                var list = JsonConvert.DeserializeObject<List<FilterCondition>>( json ?? "[]" )
                    ?? new List<FilterCondition>();
                Conditions = list;
            }
            catch
            {
                Conditions = new List<FilterCondition>();
            }

            HasInSessionState = true;
            BindRepeater();
        }

        private string GetJson()
        {
            CaptureRowsToState();
            return JsonConvert.SerializeObject( Conditions );
        }

        public void SetMatchAll( bool matchAll )
        {
            MatchAll = matchAll;
            cbMatchAll.Checked = matchAll;
        }

        public bool GetMatchAll()
        {
            return cbMatchAll.Checked;
        }

        /// <summary>
        /// Returns user-facing validation errors. Empty list means the editor's
        /// current configuration is OK to save. An empty FilterConditions list is
        /// considered valid (placeholder semantics — calc just matches nobody).
        /// </summary>
        public List<string> GetValidationErrors()
        {
            CaptureRowsToState();
            var errors = new List<string>();
            var list = Conditions;
            for ( int i = 0; i < list.Count; i++ )
            {
                var c = list[i];
                var prefix = "Filter row " + ( i + 1 );

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
            return errors;
        }

        #endregion

        #region Binding

        private void BindRepeater()
        {
            cbMatchAll.Checked = MatchAll;
            var list = Conditions;
            phNoRows.Visible = list.Count == 0;
            rRows.DataSource = list;
            rRows.DataBind();

            if ( pnlRaw.Visible )
            {
                ceRawJson.Text = JsonConvert.SerializeObject( list, Formatting.Indented );
            }
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
            // Only surface comparisons that make sense for this row's Source+Key shape.
            // E.g. File / Matrix / Encrypted attrs collapse to IsBlank/IsNotBlank only.
            var shape = GetShape( condition );
            var allowed = GetAllowedComparisons( shape );
            ddlComp.Items.Clear();
            foreach ( var ct in allowed )
            {
                ddlComp.Items.Add( new ListItem( SplitCamelCase( ct.ToString() ), ct.ToString() ) );
            }
            // If the stored Comparison isn't valid for the current shape, snap to the
            // first allowed value (which is the sensible default for that shape).
            var resolvedComparison = allowed.Contains( condition.Comparison ) ? condition.Comparison : allowed[0];
            ddlComp.SetValue( resolvedComparison.ToString() );

            // ===== Value (hidden for IsBlank / IsNotBlank; smart-rendered otherwise) =====
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

            // Default: free-text
            tbVal.Visible = true;
            tbVal.Text = condition.Value ?? string.Empty;
        }

        private void CaptureRowsToState()
        {
            HasInSessionState = true;
            var list = new List<FilterCondition>();
            foreach ( RepeaterItem item in rRows.Items )
            {
                if ( item.ItemType != ListItemType.Item && item.ItemType != ListItemType.AlternatingItem )
                {
                    continue;
                }

                var ddlSource  = ( RockDropDownList ) item.FindControl( "ddlSource" );
                var ddlKeyProp = ( RockDropDownList ) item.FindControl( "ddlKeyProperty" );
                var ddlKeyAttr = ( RockDropDownList ) item.FindControl( "ddlKeyAttribute" );
                var ddlComp    = ( RockDropDownList ) item.FindControl( "ddlComparison" );

                var source = ddlSource.SelectedValue.ConvertToEnumOrNull<FilterSource>() ?? FilterSource.Property;
                var key    = source == FilterSource.Property ? ddlKeyProp.SelectedValue : ddlKeyAttr.SelectedValue;
                var comp   = ddlComp.SelectedValue.ConvertToEnumOrNull<ComparisonType>() ?? ComparisonType.EqualTo;
                var value  = ExtractValueFromRow( item, source, key, comp );

                list.Add( new FilterCondition
                {
                    Source = source,
                    Key = key,
                    Comparison = comp,
                    Value = value
                } );
            }
            Conditions = list;
            MatchAll = cbMatchAll.Checked;
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

        protected void ddlSource_SelectedIndexChanged( object sender, EventArgs e )
        {
            CaptureRowsToState();
            BindRepeater();
        }

        protected void ddlKey_SelectedIndexChanged( object sender, EventArgs e )
        {
            CaptureRowsToState();
            BindRepeater();
        }

        protected void ddlComparison_SelectedIndexChanged( object sender, EventArgs e )
        {
            CaptureRowsToState();
            BindRepeater();
        }

        protected void lbAddRow_Click( object sender, EventArgs e )
        {
            CaptureRowsToState();
            var list = Conditions;
            list.Add( new FilterCondition
            {
                Source = FilterSource.Property,
                Key = string.Empty,
                Comparison = ComparisonType.EqualTo,
                Value = string.Empty
            } );
            Conditions = list;
            BindRepeater();
        }

        protected void rRows_ItemCommand( object source, RepeaterCommandEventArgs e )
        {
            if ( e.CommandName != "DeleteRow" )
            {
                return;
            }

            CaptureRowsToState();
            var index = e.CommandArgument.ToString().AsInteger();
            var list = Conditions;
            if ( index >= 0 && index < list.Count )
            {
                list.RemoveAt( index );
                Conditions = list;
            }
            BindRepeater();
        }

        protected void lbApplyRaw_Click( object sender, EventArgs e )
        {
            nbRawError.Visible = false;
            try
            {
                var list = JsonConvert.DeserializeObject<List<FilterCondition>>( ceRawJson.Text ?? "[]" )
                    ?? new List<FilterCondition>();
                Conditions = list;
                BindRepeater();
            }
            catch ( Exception ex )
            {
                nbRawError.Text = "Could not parse JSON: " + ex.Message;
                nbRawError.Visible = true;
            }
        }

        protected void lbToggleRaw_Click( object sender, EventArgs e )
        {
            CaptureRowsToState();
            pnlRaw.Visible = !pnlRaw.Visible;
            hfShowRaw.Value = pnlRaw.Visible ? "true" : "false";
            lToggleRawText.Text = pnlRaw.Visible ? "Hide raw JSON" : "Show raw JSON";
            BindRepeater();
        }

        #endregion

        #region Helpers

        private static string SplitCamelCase( string s )
        {
            if ( string.IsNullOrEmpty( s ) ) return s;
            return System.Text.RegularExpressions.Regex.Replace( s, "(?<=[a-z])([A-Z])", " $1" );
        }

        #endregion
    }
}
