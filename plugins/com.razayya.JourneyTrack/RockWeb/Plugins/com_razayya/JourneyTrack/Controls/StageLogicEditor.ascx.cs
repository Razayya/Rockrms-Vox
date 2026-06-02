using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

using com.razayya.JourneyTrack.Data;
using com.razayya.JourneyTrack.Logic;
using com.razayya.JourneyTrack.Model;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rock;
using Rock.Data;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_razayya.JourneyTrack.Controls
{
    /// <summary>
    /// Visual editor for a Stage's optional LogicTreeJson — a two-level ANY/ALL tree
    /// over the Stage's calculations. Leaves reference a calc by Id (<see cref="StageLogicLeaf"/>);
    /// the engine folds the calcs' matched sets in set-space (All=Intersect, Any=Union,
    /// None/Not-all complement vs population). Empty = the Stage's default gate applies.
    /// Configs deeper than two levels fall back to a read-only raw-JSON view.
    /// </summary>
    public partial class StageLogicEditor : UserControl
    {
        #region Public API

        public string Value
        {
            get { return GetJson(); }
            set { SetJson( value ); }
        }

        /// <summary>The Stage whose calculations populate the leaf dropdowns. Set BEFORE Value.</summary>
        public int StageId
        {
            get { return ( ViewState["StageId"] as int? ) ?? 0; }
            set { ViewState["StageId"] = value; }
        }

        #endregion

        #region Internal group model

        private class EditGroup
        {
            public LogicGroupType Type { get; set; } = LogicGroupType.All;
            public List<StageLogicLeaf> Leaves { get; set; } = new List<StageLogicLeaf>();
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
            get { return ( ViewState["TopType"] as string ).ConvertToEnumOrNull<LogicGroupType>() ?? LogicGroupType.Any; }
            set { ViewState["TopType"] = value.ToString(); }
        }

        private bool ForceRawOnly
        {
            get { return ( ViewState["ForceRawOnly"] as bool? ) ?? false; }
            set { ViewState["ForceRawOnly"] = value; }
        }

        // Per-postback calc cache (calcs in this Stage).
        private List<JourneyCalculation> _calcs;
        private List<JourneyCalculation> GetCalcs()
        {
            if ( _calcs != null ) return _calcs;
            if ( StageId <= 0 ) return _calcs = new List<JourneyCalculation>();

            using ( var rockContext = new RockContext() )
            {
                _calcs = new JourneyCalculationService( rockContext ).Queryable()
                    .Where( c => c.StageId == StageId )
                    .OrderBy( c => c.Order )
                    .ThenBy( c => c.Name )
                    .ToList();
            }
            return _calcs;
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

        private void SetJson( string json )
        {
            ForceRawOnly = false;

            JToken root = null;
            if ( !string.IsNullOrWhiteSpace( json ) )
            {
                try { root = JToken.Parse( json ); } catch { root = null; }
            }

            if ( root == null )
            {
                Groups = new List<EditGroup>();
                TopType = LogicGroupType.Any;
            }
            else if ( root.Type == JTokenType.Array )
            {
                var leaves = SafeLeaves( root );
                Groups = new List<EditGroup> { new EditGroup { Type = LogicGroupType.All, Leaves = leaves } };
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
                return ( ViewState["RawJson"] as string ) ?? ( ceRawJson.Text ?? string.Empty );
            }

            CaptureToState();

            var groups = Groups;
            // No real leaves → emit empty so the engine falls back to the default gate.
            if ( groups.Count == 0 || groups.All( g => g.Leaves.Count == 0 ) )
            {
                return string.Empty;
            }

            var root = new LogicNode<StageLogicLeaf>
            {
                Type = TopType,
                Children = groups.Select( g => new LogicNode<StageLogicLeaf>
                {
                    Type = g.Type,
                    Children = g.Leaves.Select( l => new LogicNode<StageLogicLeaf> { Leaf = l } ).ToList()
                } ).ToList()
            };
            return LogicTree.ToJson( root );
        }

        private static List<StageLogicLeaf> SafeLeaves( JToken arrayToken )
        {
            try { return arrayToken.ToObject<List<StageLogicLeaf>>() ?? new List<StageLogicLeaf>(); }
            catch { return new List<StageLogicLeaf>(); }
        }

        private bool TryCoerceToTwoLevel( JToken root, out LogicGroupType topType, out List<EditGroup> groups )
        {
            topType = LogicGroupType.Any;
            groups = new List<EditGroup>();

            var node = LogicTree.Parse<StageLogicLeaf>( root.ToString(), LogicGroupType.All );
            if ( node == null )
            {
                return false;
            }

            if ( node.IsLeaf )
            {
                groups.Add( new EditGroup { Type = LogicGroupType.All, Leaves = node.Leaf != null ? new List<StageLogicLeaf> { node.Leaf } : new List<StageLogicLeaf>() } );
                return true;
            }

            var children = node.Children ?? new List<LogicNode<StageLogicLeaf>>();

            if ( children.All( c => c.IsLeaf ) )
            {
                topType = node.Type.Value;
                groups.Add( new EditGroup
                {
                    Type = node.Type.Value,
                    Leaves = children.Where( c => c.Leaf != null ).Select( c => c.Leaf ).ToList()
                } );
                return true;
            }

            if ( children.All( c => !c.IsLeaf && ( c.Children ?? new List<LogicNode<StageLogicLeaf>>() ).All( gc => gc.IsLeaf ) ) )
            {
                topType = node.Type.Value;
                foreach ( var groupNode in children )
                {
                    groups.Add( new EditGroup
                    {
                        Type = groupNode.Type.Value,
                        Leaves = ( groupNode.Children ?? new List<LogicNode<StageLogicLeaf>>() )
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
                var leaves = groups[g].Leaves;
                for ( int i = 0; i < leaves.Count; i++ )
                {
                    if ( leaves[i].CalcId <= 0 )
                    {
                        var prefix = ( groups.Count > 1 ? "Group " + ( g + 1 ) + ", row " : "Row " ) + ( i + 1 );
                        errors.Add( prefix + ": a Calculation must be selected." );
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
            _calcs = null;
            var calcs = GetCalcs();
            nbNoCalcs.Visible = calcs.Count == 0;
            lbAddGroup.Enabled = calcs.Count > 0;

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
                    ? ( ( ViewState["RawJson"] as string ) ?? string.Empty )
                    : GetJsonForRawPreview();
            }
        }

        private string GetJsonForRawPreview()
        {
            var groups = Groups;
            if ( groups.Count == 0 || groups.All( g => g.Leaves.Count == 0 ) )
            {
                return string.Empty;
            }
            var root = new LogicNode<StageLogicLeaf>
            {
                Type = TopType,
                Children = groups.Select( g => new LogicNode<StageLogicLeaf>
                {
                    Type = g.Type,
                    Children = g.Leaves.Select( l => new LogicNode<StageLogicLeaf> { Leaf = l } ).ToList()
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
            phNoRows.Visible = group.Leaves.Count == 0;

            var rRows = ( Repeater ) e.Item.FindControl( "rRows" );
            rRows.DataSource = group.Leaves;
            rRows.DataBind();
        }

        protected void rRows_ItemDataBound( object sender, RepeaterItemEventArgs e )
        {
            if ( e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem )
            {
                return;
            }

            var leaf = ( StageLogicLeaf ) e.Item.DataItem;
            var ddlCalc = ( RockDropDownList ) e.Item.FindControl( "ddlCalc" );
            ddlCalc.Items.Clear();
            ddlCalc.Items.Add( new ListItem( "(select)", string.Empty ) );
            foreach ( var calc in GetCalcs() )
            {
                ddlCalc.Items.Add( new ListItem( calc.Name, calc.Id.ToString() ) );
            }
            ddlCalc.SetValue( leaf.CalcId.ToString() );
        }

        private void CaptureToState()
        {
            if ( ForceRawOnly )
            {
                return;
            }

            var groups = new List<EditGroup>();
            foreach ( RepeaterItem gItem in rGroups.Items )
            {
                if ( gItem.ItemType != ListItemType.Item && gItem.ItemType != ListItemType.AlternatingItem )
                {
                    continue;
                }

                var ddlGroupType = ( RockDropDownList ) gItem.FindControl( "ddlGroupType" );
                var groupType = ddlGroupType.SelectedValue.ConvertToEnumOrNull<LogicGroupType>() ?? LogicGroupType.All;

                var leaves = new List<StageLogicLeaf>();
                var innerRep = ( Repeater ) gItem.FindControl( "rRows" );
                foreach ( RepeaterItem rItem in innerRep.Items )
                {
                    if ( rItem.ItemType != ListItemType.Item && rItem.ItemType != ListItemType.AlternatingItem )
                    {
                        continue;
                    }
                    var ddlCalc = ( RockDropDownList ) rItem.FindControl( "ddlCalc" );
                    leaves.Add( new StageLogicLeaf { CalcId = ddlCalc.SelectedValue.AsInteger() } );
                }

                groups.Add( new EditGroup { Type = groupType, Leaves = leaves } );
            }

            Groups = groups;
            TopType = ddlTopType.SelectedValue.ConvertToEnumOrNull<LogicGroupType>() ?? LogicGroupType.Any;
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

        protected void lbAddGroup_Click( object sender, EventArgs e )
        {
            CaptureToState();
            var groups = Groups;
            groups.Add( new EditGroup
            {
                Type = LogicGroupType.All,
                Leaves = new List<StageLogicLeaf> { new StageLogicLeaf { CalcId = 0 } }
            } );
            Groups = groups;
            BindGroups();
        }

        protected void rGroups_ItemCommand( object source, RepeaterCommandEventArgs e )
        {
            CaptureToState();
            var groups = Groups;
            var groupIndex = e.CommandArgument.ToString().AsInteger();

            if ( e.CommandName == "AddCalc" )
            {
                if ( groupIndex >= 0 && groupIndex < groups.Count )
                {
                    groups[groupIndex].Leaves.Add( new StageLogicLeaf { CalcId = 0 } );
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
                var leaves = groups[groupIndex].Leaves;
                if ( rowIndex >= 0 && rowIndex < leaves.Count )
                {
                    leaves.RemoveAt( rowIndex );
                    Groups = groups;
                }
            }
            BindGroups();
        }

        protected void lbApplyRaw_Click( object sender, EventArgs e )
        {
            nbRawError.Visible = false;
            var text = ceRawJson.Text ?? string.Empty;
            if ( string.IsNullOrWhiteSpace( text ) )
            {
                SetJson( string.Empty );
                return;
            }
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

        #region View-mode summary formatter

        /// <summary>
        /// Render a Stage's LogicTreeJson as a friendly nested summary, resolving calc
        /// Ids to names. Returns empty string when no tree is configured (default gate).
        /// </summary>
        public static string FormatSummaryHtml( string logicJson, int stageId )
        {
            if ( string.IsNullOrWhiteSpace( logicJson ) )
            {
                return string.Empty;
            }

            var node = LogicTree.Parse<StageLogicLeaf>( logicJson, LogicGroupType.All );
            if ( node == null || LogicTree.IsEmpty( node ) )
            {
                return string.Empty;
            }

            Dictionary<int, string> calcNames;
            using ( var rockContext = new RockContext() )
            {
                calcNames = new JourneyCalculationService( rockContext ).Queryable().AsNoTracking()
                    .Where( c => c.StageId == stageId )
                    .Select( c => new { c.Id, c.Name } )
                    .ToDictionary( c => c.Id, c => c.Name );
            }

            return RenderNode( node, calcNames );
        }

        private static string RenderNode( LogicNode<StageLogicLeaf> node, Dictionary<int, string> calcNames )
        {
            if ( node == null )
            {
                return string.Empty;
            }

            if ( node.IsLeaf )
            {
                var id = node.Leaf?.CalcId ?? 0;
                var name = calcNames.TryGetValue( id, out var n ) ? n : ( "calc #" + id );
                return "<div><strong>" + System.Web.HttpUtility.HtmlEncode( name ) + "</strong></div>";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append( "<div class='text-muted small'>" ).Append( GroupLabel( node.Type.Value ) ).Append( ":</div>" );
            sb.Append( "<div style='margin-left:16px;border-left:2px solid #eee;padding-left:8px;'>" );
            foreach ( var child in node.Children ?? new List<LogicNode<StageLogicLeaf>>() )
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

        #endregion
    }
}
