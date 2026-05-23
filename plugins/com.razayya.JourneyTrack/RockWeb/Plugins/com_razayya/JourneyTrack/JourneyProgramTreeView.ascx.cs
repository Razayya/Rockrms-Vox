using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Web;

using com.razayya.JourneyTrack.Data;
using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Web.UI;

namespace RockWeb.Plugins.com_razayya.JourneyTrack
{
    [DisplayName( "Journey Program Tree View" )]
    [Category( "Razayya > JourneyTrack" )]
    [Description( "Displays a navigable tree of all Journey Programs with their Sub Groups and Calculations." )]

    [LinkedPage( "Group Detail Page",
        Description = "Page that shows the Journey Program detail.",
        IsRequired = true,
        Order = 0,
        Key = "GroupDetailPage" )]

    [LinkedPage( "Sub Group Detail Page",
        Description = "Page that shows the JourneyStage detail.",
        IsRequired = true,
        Order = 1,
        Key = "SubGroupDetailPage" )]

    [LinkedPage( "JourneyCalculation Detail Page",
        Description = "Page that shows the JourneyCalculation detail.",
        IsRequired = true,
        Order = 2,
        Key = "JourneyCalculationDetailPage" )]

    public partial class JourneyProgramTreeView : RockBlock
    {
        private const string NodeType_Group = "group";
        private const string NodeType_SubGroup = "subgroup";
        private const string NodeType_Calc = "calc";

        #region Properties

        private int SelectedGroupId
        {
            get { return ViewState["SelectedGroupId"] as int? ?? 0; }
            set { ViewState["SelectedGroupId"] = value; }
        }

        private int SelectedSubGroupId
        {
            get { return ViewState["SelectedSubGroupId"] as int? ?? 0; }
            set { ViewState["SelectedSubGroupId"] = value; }
        }

        #endregion

        #region Fields

        private string _selectedType = string.Empty;
        private int _selectedId;
        private string _groupBaseUrl;
        private string _subGroupBaseUrl;
        private string _calcBaseUrl;

        #endregion

        #region Base Control Methods

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                LoadTree();
            }
        }

        #endregion

        #region Events

        protected void lbAddGroup_Click( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "GroupDetailPage", "JourneyProgramId", 0 );
        }

        protected void lbAddSubGroup_Click( object sender, EventArgs e )
        {
            int groupId = SelectedGroupId;
            if ( groupId > 0 )
            {
                NavigateToLinkedPage( "SubGroupDetailPage", "StageId", 0, "JourneyProgramId", groupId );
            }
        }

        protected void lbAddCalculation_Click( object sender, EventArgs e )
        {
            int subGroupId = SelectedSubGroupId;
            if ( subGroupId > 0 )
            {
                NavigateToLinkedPage( "JourneyCalculationDetailPage", "JourneyCalculationId", 0, "StageId", subGroupId );
            }
        }

        #endregion

        #region Methods

        private void LoadTree()
        {
            ResolveSelectedNode();

            lbAddGroup.Enabled = true;
            lbAddSubGroup.Enabled = SelectedGroupId > 0;
            lbAddCalculation.Enabled = SelectedSubGroupId > 0;

            // Cache base URLs once instead of resolving per-node
            _groupBaseUrl = LinkedPageUrl( "GroupDetailPage", new Dictionary<string, string> { { "JourneyProgramId", "PLACEHOLDER" } } );
            _subGroupBaseUrl = LinkedPageUrl( "SubGroupDetailPage", new Dictionary<string, string> { { "StageId", "PLACEHOLDER" } } );
            _calcBaseUrl = LinkedPageUrl( "JourneyCalculationDetailPage", new Dictionary<string, string> { { "JourneyCalculationId", "PLACEHOLDER" } } );

            RenderTree();
        }

        private void ResolveSelectedNode()
        {
            int calcId = PageParameter( "JourneyCalculationId" ).AsInteger();
            int subGroupId = PageParameter( "StageId" ).AsInteger();
            int groupId = PageParameter( "JourneyProgramId" ).AsInteger();

            if ( calcId > 0 )
            {
                _selectedType = NodeType_Calc;
                _selectedId = calcId;

                using ( var rockContext = new RockContext() )
                {
                    var calc = new JourneyCalculationService( rockContext ).Queryable()
                        .Where( c => c.Id == calcId )
                        .Select( c => new { c.StageId, GroupId = c.Stage.JourneyProgramId } )
                        .FirstOrDefault();

                    if ( calc != null )
                    {
                        SelectedSubGroupId = calc.StageId;
                        SelectedGroupId = calc.GroupId;
                    }
                }
            }
            else if ( subGroupId > 0 )
            {
                _selectedType = NodeType_SubGroup;
                _selectedId = subGroupId;
                SelectedSubGroupId = subGroupId;

                using ( var rockContext = new RockContext() )
                {
                    SelectedGroupId = new StageService( rockContext )
                        .GetSelect( subGroupId, sg => sg.JourneyProgramId );
                }
            }
            else if ( groupId > 0 )
            {
                _selectedType = NodeType_Group;
                _selectedId = groupId;
                SelectedGroupId = groupId;
            }
        }

        private void RenderTree()
        {
            using ( var rockContext = new RockContext() )
            {
                var groups = new JourneyProgramService( rockContext ).Queryable()
                    .Include( g => g.Stages.Select( sg => sg.Calculations ) )
                    .OrderBy( g => g.Order )
                    .ThenBy( g => g.Name )
                    .ToList();

                if ( !groups.Any() )
                {
                    lTreeHtml.Text = "<div class='padding-all-md text-muted'>No JourneyCalculation groups have been configured.</div>";
                    return;
                }

                var sb = new StringBuilder();
                sb.Append( "<ul class='rocktree'>" );

                foreach ( var group in groups )
                {
                    RenderGroupNode( sb, group );
                }

                sb.Append( "</ul>" );
                lTreeHtml.Text = sb.ToString();
            }
        }

        private string BuildGroupUrl( int id )
        {
            return _groupBaseUrl.Replace( "PLACEHOLDER", id.ToString() );
        }

        private string BuildSubGroupUrl( int id )
        {
            return _subGroupBaseUrl.Replace( "PLACEHOLDER", id.ToString() );
        }

        private string BuildCalcUrl( int id )
        {
            return _calcBaseUrl.Replace( "PLACEHOLDER", id.ToString() );
        }

        private void RenderGroupNode( StringBuilder sb, JourneyProgram group )
        {
            bool isSelected = _selectedType == NodeType_Group && _selectedId == group.Id;
            bool isExpanded = SelectedGroupId == group.Id;

            var subGroups = group.Stages
                .OrderBy( sg => sg.Order )
                .ThenBy( sg => sg.Name )
                .ToList();

            bool hasChildren = subGroups.Any();
            string inactiveClass = group.IsActive ? "" : " is-inactive";

            sb.AppendFormat( "<li class='rocktree-item{0}' data-id='g-{1}'>", inactiveClass, group.Id );

            if ( hasChildren )
            {
                sb.AppendFormat(
                    "<span class='rocktree-icon js-synctree-toggle'><i class='fa {0}'></i></span>",
                    isExpanded ? "fa-chevron-down" : "fa-chevron-right" );
            }
            else
            {
                sb.Append( "<span class='rocktree-icon'></span>" );
            }

            string selectedClass = isSelected ? " selected" : "";
            sb.AppendFormat(
                "<span class='rocktree-name js-synctree-toggle{0}'><i class='fa fa-sync'></i> {1} <span class='label label-tree'>{2}</span>"
                    + " <a class='js-synctree-nav' href='{3}' title='Edit group'><i class='fa fa-pencil'></i></a></span>",
                selectedClass,
                HttpUtility.HtmlEncode( group.Name ),
                subGroups.Count,
                HttpUtility.HtmlAttributeEncode( BuildGroupUrl( group.Id ) ) );

            if ( hasChildren )
            {
                string displayStyle = isExpanded ? "" : " style='display:none'";
                sb.AppendFormat( "<ul class='rocktree-children'{0}>", displayStyle );

                foreach ( var subGroup in subGroups )
                {
                    RenderSubGroupNode( sb, subGroup );
                }

                sb.Append( "</ul>" );
            }

            sb.Append( "</li>" );
        }

        private void RenderSubGroupNode( StringBuilder sb, Stage subGroup )
        {
            bool isSelected = _selectedType == NodeType_SubGroup && _selectedId == subGroup.Id;
            bool isExpanded = SelectedSubGroupId == subGroup.Id;

            var calcs = subGroup.Calculations
                .OrderBy( c => c.Order )
                .ThenBy( c => c.Name )
                .ToList();

            bool hasChildren = calcs.Any();
            string inactiveClass = subGroup.IsActive ? "" : " is-inactive";

            sb.AppendFormat( "<li class='rocktree-item{0}' data-id='sg-{1}'>", inactiveClass, subGroup.Id );

            if ( hasChildren )
            {
                sb.AppendFormat(
                    "<span class='rocktree-icon js-synctree-toggle'><i class='fa {0}'></i></span>",
                    isExpanded ? "fa-chevron-down" : "fa-chevron-right" );
            }
            else
            {
                sb.Append( "<span class='rocktree-icon'></span>" );
            }

            string selectedClass = isSelected ? " selected" : "";
            sb.AppendFormat(
                "<span class='rocktree-name js-synctree-toggle{0}'><i class='fa fa-layer-group'></i> {1} <span class='label label-tree'>{2}</span>"
                    + " <a class='js-synctree-nav' href='{3}' title='Edit sub group'><i class='fa fa-pencil'></i></a></span>",
                selectedClass,
                HttpUtility.HtmlEncode( subGroup.Name ),
                calcs.Count,
                HttpUtility.HtmlAttributeEncode( BuildSubGroupUrl( subGroup.Id ) ) );

            if ( hasChildren )
            {
                string displayStyle = isExpanded ? "" : " style='display:none'";
                sb.AppendFormat( "<ul class='rocktree-children'{0}>", displayStyle );

                foreach ( var calc in calcs )
                {
                    RenderCalculationNode( sb, calc );
                }

                sb.Append( "</ul>" );
            }

            sb.Append( "</li>" );
        }

        private void RenderCalculationNode( StringBuilder sb, JourneyCalculation calc )
        {
            bool isSelected = _selectedType == NodeType_Calc && _selectedId == calc.Id;
            string inactiveClass = calc.IsActive ? "" : " is-inactive";
            string selectedClass = isSelected ? " selected" : "";

            sb.AppendFormat( "<li class='rocktree-item rocktree-leaf{0}' data-id='c-{1}'>", inactiveClass, calc.Id );
            sb.AppendFormat(
                "<a class='rocktree-name{0}' href='{1}'><i class='fa fa-calculator'></i> {2}</a>",
                selectedClass,
                HttpUtility.HtmlAttributeEncode( BuildCalcUrl( calc.Id ) ),
                HttpUtility.HtmlEncode( calc.Name ) );
            sb.Append( "</li>" );
        }

        #endregion
    }
}
