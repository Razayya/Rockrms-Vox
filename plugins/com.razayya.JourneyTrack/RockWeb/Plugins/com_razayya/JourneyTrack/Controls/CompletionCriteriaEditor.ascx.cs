using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

using com.razayya.JourneyTrack.CalculationTypes;
using com.razayya.JourneyTrack.Data;
using com.razayya.JourneyTrack.Logic;
using com.razayya.JourneyTrack.Model;
using ComparisonType = com.razayya.JourneyTrack.Model.ComparisonType;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rock;
using Rock.Data;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_razayya.JourneyTrack.Controls
{
    /// <summary>
    /// Visual editor for a Completion calc's CompletionCriteria. Two-level ANY/ALL:
    /// a top-level combine over one or more groups, each group combining sibling-calc
    /// criteria with All/Any/None/Not-all. Serializes to the shared nested logic-tree
    /// JSON (<see cref="LogicTree"/>). A legacy flat array loads into a single group
    /// (only its required criteria, matching the engine's legacy semantics). Configs
    /// deeper than two levels fall back to a read-only raw-JSON view.
    /// </summary>
    public partial class CompletionCriteriaEditor : UserControl
    {
        #region Public API

        public string Value
        {
            get { return GetJson(); }
            set { SetJson( value ); }
        }

        /// <summary>The Stage whose sibling JourneyCalculations populate the dropdown. Set BEFORE Value.</summary>
        public int StageId
        {
            get { return ( ViewState["StageId"] as int? ) ?? 0; }
            set { ViewState["StageId"] = value; }
        }

        /// <summary>Id of the calc being edited (excluded from the sibling dropdown).</summary>
        public int ExcludeCalculationId
        {
            get { return ( ViewState["ExcludeCalculationId"] as int? ) ?? 0; }
            set { ViewState["ExcludeCalculationId"] = value; }
        }

        #endregion

        #region Internal group model

        private class EditGroup
        {
            public LogicGroupType Type { get; set; } = LogicGroupType.All;
            public List<CompletionCriterion> Criteria { get; set; } = new List<CompletionCriterion>();
        }

        #endregion

        #region ViewState

        private List<EditGroup> Groups
        {
            get
            {
                var json = ViewState["Groups"] as string;
                if ( string.IsNullOrEmpty( json ) ) return new List<EditGroup>();
                try { return JsonConvert.DeserializeObject<List<EditGroup>>( json ) ?? new List<EditGroup>(); }
                catch { return new List<EditGroup>(); }
            }
            set { ViewState["Groups"] = JsonConvert.SerializeObject( value ?? new List<EditGroup>() ); }
        }

        private LogicGroupType TopType
        {
            get { return ( ViewState["TopType"] as string ).ConvertToEnumOrNull<LogicGroupType>() ?? LogicGroupType.All; }
            set { ViewState["TopType"] = value.ToString(); }
        }

        private bool ForceRawOnly
        {
            get { return ( ViewState["ForceRawOnly"] as bool? ) ?? false; }
            set { ViewState["ForceRawOnly"] = value; }
        }

        // Per-postback sibling cache.
        private List<JourneyCalculation> _siblings;
        private List<JourneyCalculation> GetSiblings()
        {
            if ( _siblings != null ) return _siblings;
            if ( StageId <= 0 ) return _siblings = new List<JourneyCalculation>();

            using ( var rockContext = new RockContext() )
            {
                _siblings = new JourneyCalculationService( rockContext ).Queryable()
                    .Where( c => c.StageId == StageId && c.Id != ExcludeCalculationId )
                    .OrderBy( c => c.Order )
                    .ThenBy( c => c.Name )
                    .ToList();
            }
            return _siblings;
        }

        #endregion

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );
            if ( !Page.IsPostBack )
            {
                BindGroups();
            }
        }

        #region Public Get/Set JSON

        public bool HasInSessionState
        {
            get { return ( ViewState["CCE_HasState"] as bool? ) ?? false; }
            private set { ViewState["CCE_HasState"] = value; }
        }

        private void SetJson( string json )
        {
            HasInSessionState = true;
            ForceRawOnly = false;

            JToken root = null;
            if ( !string.IsNullOrWhiteSpace( json ) )
            {
                try { root = JToken.Parse( json ); } catch { root = null; }
            }

            if ( root == null )
            {
                Groups = new List<EditGroup>();
                TopType = LogicGroupType.All;
            }
            else if ( root.Type == JTokenType.Array )
            {
                // Legacy flat array → single All group of the REQUIRED criteria
                // (mirrors the engine's legacy "AND the required criteria" behavior).
                var leaves = SafeLeaves( root ).Where( c => c.IsRequired ).ToList();
                Groups = new List<EditGroup> { new EditGroup { Type = LogicGroupType.All, Criteria = leaves } };
                TopType = LogicGroupType.All;
            }
            else if ( TryCoerceToTwoLevel( root, out var top, out var groups ) )
            {
                TopType = top;
                Groups = groups;
            }
            else
            {
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
            if ( groups.Count == 0 || groups.All( g => g.Criteria.Count == 0 ) )
            {
                return "[]";
            }

            var root = new LogicNode<CompletionCriterion>
            {
                Type = TopType,
                Children = groups.Select( g => new LogicNode<CompletionCriterion>
                {
                    Type = g.Type,
                    Children = g.Criteria.Select( c => new LogicNode<CompletionCriterion> { Leaf = c } ).ToList()
                } ).ToList()
            };
            return LogicTree.ToJson( root );
        }

        private static List<CompletionCriterion> SafeLeaves( JToken arrayToken )
        {
            try { return arrayToken.ToObject<List<CompletionCriterion>>() ?? new List<CompletionCriterion>(); }
            catch { return new List<CompletionCriterion>(); }
        }

        private bool TryCoerceToTwoLevel( JToken root, out LogicGroupType topType, out List<EditGroup> groups )
        {
            topType = LogicGroupType.All;
            groups = new List<EditGroup>();

            var node = LogicTree.Parse<CompletionCriterion>( root.ToString(), LogicGroupType.All );
            if ( node == null )
            {
                return false;
            }

            if ( node.IsLeaf )
            {
                groups.Add( new EditGroup { Type = LogicGroupType.All, Criteria = node.Leaf != null ? new List<CompletionCriterion> { node.Leaf } : new List<CompletionCriterion>() } );
                return true;
            }

            var children = node.Children ?? new List<LogicNode<CompletionCriterion>>();

            if ( children.All( c => c.IsLeaf ) )
            {
                topType = node.Type.Value;
                groups.Add( new EditGroup
                {
                    Type = node.Type.Value,
                    Criteria = children.Where( c => c.Leaf != null ).Select( c => c.Leaf ).ToList()
                } );
                return true;
            }

            if ( children.All( c => !c.IsLeaf && ( c.Children ?? new List<LogicNode<CompletionCriterion>>() ).All( gc => gc.IsLeaf ) ) )
            {
                topType = node.Type.Value;
                foreach ( var groupNode in children )
                {
                    groups.Add( new EditGroup
                    {
                        Type = groupNode.Type.Value,
                        Criteria = ( groupNode.Children ?? new List<LogicNode<CompletionCriterion>>() )
                            .Where( c => c.Leaf != null ).Select( c => c.Leaf ).ToList()
                    } );
                }
                return true;
            }

            return false;
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
                var criteria = groups[g].Criteria;
                for ( int i = 0; i < criteria.Count; i++ )
                {
                    var c = criteria[i];
                    var prefix = ( groups.Count > 1 ? "Group " + ( g + 1 ) + ", criterion " : "Criterion " ) + ( i + 1 );

                    if ( c.JourneyCalculationId <= 0 )
                    {
                        errors.Add( prefix + ": Calculation must be selected." );
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
            _siblings = null; // force refetch
            var siblings = GetSiblings();
            nbNoSiblings.Visible = siblings.Count == 0;
            lbAddGroup.Enabled = siblings.Count > 0;

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
            var groups = Groups;
            if ( groups.Count == 0 || groups.All( g => g.Criteria.Count == 0 ) )
            {
                return "[]";
            }
            var root = new LogicNode<CompletionCriterion>
            {
                Type = TopType,
                Children = groups.Select( g => new LogicNode<CompletionCriterion>
                {
                    Type = g.Type,
                    Children = g.Criteria.Select( c => new LogicNode<CompletionCriterion> { Leaf = c } ).ToList()
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
            phNoRows.Visible = group.Criteria.Count == 0;

            var rRows = ( Repeater ) e.Item.FindControl( "rRows" );
            rRows.DataSource = group.Criteria;
            rRows.DataBind();
        }

        protected void rRows_ItemDataBound( object sender, RepeaterItemEventArgs e )
        {
            if ( e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem )
            {
                return;
            }

            var criterion = ( CompletionCriterion ) e.Item.DataItem;
            BindRow( e.Item, criterion );
        }

        private void BindRow( RepeaterItem item, CompletionCriterion criterion )
        {
            var ddlCalc = ( RockDropDownList ) item.FindControl( "ddlCalc" );
            var ddlComp = ( RockDropDownList ) item.FindControl( "ddlComparison" );
            var tbVal   = ( RockTextBox ) item.FindControl( "tbValue" );

            ddlCalc.Items.Clear();
            ddlCalc.Items.Add( new ListItem( "(select)", string.Empty ) );
            foreach ( var sib in GetSiblings() )
            {
                ddlCalc.Items.Add( new ListItem( sib.Name, sib.Id.ToString() ) );
            }
            ddlCalc.SetValue( criterion.JourneyCalculationId.ToString() );

            ddlComp.Items.Clear();
            foreach ( var ct in Enum.GetValues( typeof( ComparisonType ) ).Cast<ComparisonType>() )
            {
                ddlComp.Items.Add( new ListItem( SplitCamelCase( ct.ToString() ), ct.ToString() ) );
            }
            ddlComp.SetValue( criterion.Comparison.ToString() );

            bool needsValue = criterion.Comparison != ComparisonType.IsBlank
                           && criterion.Comparison != ComparisonType.IsNotBlank;
            tbVal.Visible = needsValue;
            tbVal.Text = needsValue ? ( criterion.Value ?? string.Empty ) : string.Empty;
        }

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

                var criteria = new List<CompletionCriterion>();
                var innerRep = ( Repeater ) gItem.FindControl( "rRows" );
                foreach ( RepeaterItem rItem in innerRep.Items )
                {
                    if ( rItem.ItemType != ListItemType.Item && rItem.ItemType != ListItemType.AlternatingItem )
                    {
                        continue;
                    }

                    var ddlCalc = ( RockDropDownList ) rItem.FindControl( "ddlCalc" );
                    var ddlComp = ( RockDropDownList ) rItem.FindControl( "ddlComparison" );
                    var tbVal   = ( RockTextBox ) rItem.FindControl( "tbValue" );

                    var comp = ddlComp.SelectedValue.ConvertToEnumOrNull<ComparisonType>() ?? ComparisonType.EqualTo;
                    criteria.Add( new CompletionCriterion
                    {
                        JourneyCalculationId = ddlCalc.SelectedValue.AsInteger(),
                        // The two-level tree expresses requiredness via the group type;
                        // every captured leaf participates, so IsRequired is always true.
                        IsRequired = true,
                        Comparison = comp,
                        Value = ( comp == ComparisonType.IsBlank || comp == ComparisonType.IsNotBlank )
                            ? string.Empty
                            : ( tbVal.Text ?? string.Empty )
                    } );
                }

                groups.Add( new EditGroup { Type = groupType, Criteria = criteria } );
            }

            Groups = groups;
            TopType = ddlTopType.SelectedValue.ConvertToEnumOrNull<LogicGroupType>() ?? LogicGroupType.All;
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
                Criteria = new List<CompletionCriterion>
                {
                    new CompletionCriterion { JourneyCalculationId = 0, IsRequired = true, Comparison = ComparisonType.IsNotBlank, Value = string.Empty }
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

            if ( e.CommandName == "AddCriterion" )
            {
                if ( groupIndex >= 0 && groupIndex < groups.Count )
                {
                    groups[groupIndex].Criteria.Add( new CompletionCriterion
                    {
                        JourneyCalculationId = 0,
                        IsRequired = true,
                        Comparison = ComparisonType.IsNotBlank,
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
                return;
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
                var criteria = groups[groupIndex].Criteria;
                if ( rowIndex >= 0 && rowIndex < criteria.Count )
                {
                    criteria.RemoveAt( rowIndex );
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
                JToken.Parse( text );
                SetJson( text );
                pnlRaw.Visible = true;
                hfShowRaw.Value = "true";
                lToggleRawText.Text = "Hide raw JSON";
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

        private static string SplitCamelCase( string s )
        {
            if ( string.IsNullOrEmpty( s ) ) return s;
            return System.Text.RegularExpressions.Regex.Replace( s, "(?<=[a-z])([A-Z])", " $1" );
        }

        #region View-mode summary formatter

        /// <summary>
        /// Render the configured CompletionCriteria JSON as a friendly summary.
        /// Handles a legacy flat array (AND-joined, with optional labels) and the
        /// nested two-level group tree. Stage Id resolves sibling calc names.
        /// </summary>
        public static string FormatSummaryHtml( string criteriaJson, int stageId, int excludeCalcId )
        {
            JToken root = null;
            if ( !string.IsNullOrWhiteSpace( criteriaJson ) )
            {
                try { root = JToken.Parse( criteriaJson ); } catch { return "<em class='text-muted'>(invalid JSON)</em>"; }
            }

            if ( root == null )
            {
                return "<em class='text-muted'>(no criteria configured &mdash; matches nobody)</em>";
            }

            Dictionary<int, string> calcNames;
            using ( var rockContext = new RockContext() )
            {
                calcNames = new JourneyCalculationService( rockContext ).Queryable().AsNoTracking()
                    .Where( c => c.StageId == stageId && c.Id != excludeCalcId )
                    .Select( c => new { c.Id, c.Name } )
                    .ToDictionary( c => c.Id, c => c.Name );
            }

            if ( root.Type == JTokenType.Array )
            {
                var list = SafeLeaves( root );
                if ( list.Count == 0 )
                {
                    return "<em class='text-muted'>(no criteria configured &mdash; matches nobody)</em>";
                }
                var sb = new System.Text.StringBuilder();
                for ( int i = 0; i < list.Count; i++ )
                {
                    if ( i > 0 ) sb.Append( "<div class='text-muted small'>AND</div>" );
                    sb.Append( "<div>" ).Append( FormatCriterionLine( list[i], calcNames, true ) ).Append( "</div>" );
                }
                return sb.ToString();
            }

            var node = LogicTree.Parse<CompletionCriterion>( criteriaJson, LogicGroupType.All );
            if ( node == null || LogicTree.IsEmpty( node ) )
            {
                return "<em class='text-muted'>(no criteria configured &mdash; matches nobody)</em>";
            }
            return RenderNode( node, calcNames );
        }

        private static string RenderNode( LogicNode<CompletionCriterion> node, Dictionary<int, string> calcNames )
        {
            if ( node == null )
            {
                return string.Empty;
            }

            if ( node.IsLeaf )
            {
                return "<div>" + FormatCriterionLine( node.Leaf, calcNames, false ) + "</div>";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append( "<div class='text-muted small'>" ).Append( GroupLabel( node.Type.Value ) ).Append( ":</div>" );
            sb.Append( "<div style='margin-left:16px;border-left:2px solid #eee;padding-left:8px;'>" );
            foreach ( var child in node.Children ?? new List<LogicNode<CompletionCriterion>>() )
            {
                sb.Append( RenderNode( child, calcNames ) );
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

        private static string FormatCriterionLine( CompletionCriterion c, Dictionary<int, string> calcNames, bool showOptional )
        {
            if ( c == null )
            {
                return "<span class='text-danger'>(incomplete row)</span>";
            }

            var name = calcNames.TryGetValue( c.JourneyCalculationId, out var n ) ? n : ( "calc #" + c.JourneyCalculationId );
            var keyLabel = System.Web.HttpUtility.HtmlEncode( name );
            var optionalSuffix = ( showOptional && !c.IsRequired ) ? " <span class='label label-default'>optional</span>" : string.Empty;

            string cmpText;
            switch ( c.Comparison )
            {
                case ComparisonType.EqualTo:               cmpText = "equal to"; break;
                case ComparisonType.NotEqualTo:            cmpText = "not equal to"; break;
                case ComparisonType.IsBlank:               cmpText = "is blank"; break;
                case ComparisonType.IsNotBlank:            cmpText = "is not blank"; break;
                case ComparisonType.Contains:              cmpText = "contains"; break;
                case ComparisonType.GreaterThan:           cmpText = "greater than"; break;
                case ComparisonType.LessThan:              cmpText = "less than"; break;
                case ComparisonType.GreaterThanOrEqualTo:  cmpText = "&ge;"; break;
                case ComparisonType.LessThanOrEqualTo:     cmpText = "&le;"; break;
                default:                                   cmpText = SplitCamelCase( c.Comparison.ToString() ); break;
            }

            if ( c.Comparison == ComparisonType.IsBlank || c.Comparison == ComparisonType.IsNotBlank )
            {
                return "<strong>" + keyLabel + "</strong> <span class='text-muted'>" + cmpText + "</span>" + optionalSuffix;
            }
            return "<strong>" + keyLabel + "</strong> "
                + "<span class='text-muted'>" + cmpText + "</span> "
                + System.Web.HttpUtility.HtmlEncode( c.Value ?? string.Empty )
                + optionalSuffix;
        }

        #endregion
    }
}
