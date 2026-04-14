using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Web;

using com.razayya.CustomPersonAttributeSyncEngine.Data;
using com.razayya.CustomPersonAttributeSyncEngine.Model;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Web.UI;

namespace RockWeb.Plugins.com_razayya.CustomPersonAttributeSyncEngine
{
    [DisplayName( "Calculation Group Tree View" )]
    [Category( "Razayya > Attribute Sync Engine" )]
    [Description( "Displays a navigable tree of all Calculation Groups with their Sub Groups and Calculations." )]

    [LinkedPage( "Group Detail Page",
        Description = "Page that shows the Calculation Group detail.",
        IsRequired = true,
        Order = 0,
        Key = "GroupDetailPage" )]

    [LinkedPage( "Sub Group Detail Page",
        Description = "Page that shows the Calculation Sub Group detail.",
        IsRequired = true,
        Order = 1,
        Key = "SubGroupDetailPage" )]

    [LinkedPage( "Calculation Detail Page",
        Description = "Page that shows the Calculation detail.",
        IsRequired = true,
        Order = 2,
        Key = "CalculationDetailPage" )]

    public partial class CalculationGroupTreeView : RockBlock
    {
        #region Properties

        private string SelectedType
        {
            get { return ViewState["SelectedType"] as string ?? string.Empty; }
            set { ViewState["SelectedType"] = value; }
        }

        private int SelectedId
        {
            get { return ViewState["SelectedId"] as int? ?? 0; }
            set { ViewState["SelectedId"] = value; }
        }

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

        #region Base Control Methods

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                ResolveSelectedNode();
                ConfigureAddButtons();
                RenderTree();
            }
        }

        #endregion

        #region Events

        protected void lbAddGroup_Click( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "GroupDetailPage", "CalculationGroupId", 0 );
        }

        protected void lbAddSubGroup_Click( object sender, EventArgs e )
        {
            int groupId = SelectedGroupId;
            if ( groupId > 0 )
            {
                NavigateToLinkedPage( "SubGroupDetailPage", "CalculationSubGroupId", 0, "CalculationGroupId", groupId );
            }
        }

        protected void lbAddCalculation_Click( object sender, EventArgs e )
        {
            int subGroupId = SelectedSubGroupId;
            if ( subGroupId > 0 )
            {
                NavigateToLinkedPage( "CalculationDetailPage", "CalculationId", 0, "CalculationSubGroupId", subGroupId );
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Determines the currently selected node from page parameters.
        /// </summary>
        private void ResolveSelectedNode()
        {
            int calcId = PageParameter( "CalculationId" ).AsInteger();
            int subGroupId = PageParameter( "CalculationSubGroupId" ).AsInteger();
            int groupId = PageParameter( "CalculationGroupId" ).AsInteger();

            if ( calcId > 0 )
            {
                SelectedType = "calc";
                SelectedId = calcId;

                // Resolve parent sub-group and group
                using ( var rockContext = new RockContext() )
                {
                    var calc = new CalculationService( rockContext ).Queryable()
                        .Where( c => c.Id == calcId )
                        .Select( c => new { c.CalculationSubGroupId, GroupId = c.CalculationSubGroup.CalculationGroupId } )
                        .FirstOrDefault();

                    if ( calc != null )
                    {
                        SelectedSubGroupId = calc.CalculationSubGroupId;
                        SelectedGroupId = calc.GroupId;
                    }
                }
            }
            else if ( subGroupId > 0 )
            {
                SelectedType = "subgroup";
                SelectedId = subGroupId;
                SelectedSubGroupId = subGroupId;

                // Resolve parent group
                using ( var rockContext = new RockContext() )
                {
                    SelectedGroupId = new CalculationSubGroupService( rockContext )
                        .GetSelect( subGroupId, sg => sg.CalculationGroupId );
                }
            }
            else if ( groupId > 0 )
            {
                SelectedType = "group";
                SelectedId = groupId;
                SelectedGroupId = groupId;
            }

            hfSelectedItemType.Value = SelectedType;
            hfSelectedItemId.Value = SelectedId.ToString();
        }

        /// <summary>
        /// Enables or disables the Add dropdown items based on the current selection.
        /// </summary>
        private void ConfigureAddButtons()
        {
            // "Add Group" is always enabled
            lbAddGroup.Enabled = true;

            // "Add Sub Group" is enabled when a group-level node is selected
            // (either directly on a group, or when viewing a sub-group/calc within a group)
            lbAddSubGroup.Enabled = SelectedGroupId > 0;

            // "Add Calculation" is enabled when a sub-group-level node is selected
            // (either directly on a sub-group, or when viewing a calc within a sub-group)
            lbAddCalculation.Enabled = SelectedSubGroupId > 0;
        }

        /// <summary>
        /// Renders the full tree of all Calculation Groups.
        /// </summary>
        private void RenderTree()
        {
            using ( var rockContext = new RockContext() )
            {
                var groups = new CalculationGroupService( rockContext ).Queryable()
                    .Include( g => g.CalculationSubGroups.Select( sg => sg.Calculations ) )
                    .OrderBy( g => g.Order )
                    .ThenBy( g => g.Name )
                    .ToList();

                if ( !groups.Any() )
                {
                    lTreeHtml.Text = "<div class='padding-all-md text-muted'>No calculation groups have been configured.</div>";
                    return;
                }

                var sb = new StringBuilder();
                sb.Append( "<ul class='list-unstyled' style='margin: 0;'>" );

                foreach ( var group in groups )
                {
                    RenderGroupNode( sb, group );
                }

                sb.Append( "</ul>" );
                lTreeHtml.Text = sb.ToString();
            }
        }

        private void RenderGroupNode( StringBuilder sb, CalculationGroup group )
        {
            bool isActive = SelectedType == "group" && SelectedId == group.Id;
            bool containsSelected = SelectedGroupId == group.Id;
            bool isExpanded = containsSelected;

            string groupUrl = LinkedPageUrl( "GroupDetailPage", new Dictionary<string, string>
            {
                { "CalculationGroupId", group.Id.ToString() }
            } );

            string activeStyle = isActive ? " background-color: #e8f0fe;" : "";
            string activeLinkStyle = isActive ? " color: #2196F3; font-weight: 600;" : "";
            string inactiveLabel = group.IsActive ? "" : " <span class='label label-danger' style='font-size: 10px;'>Inactive</span>";

            var subGroups = group.CalculationSubGroups
                .OrderBy( sg => sg.Order )
                .ThenBy( sg => sg.Name )
                .ToList();

            bool hasChildren = subGroups.Any();
            string caretIcon = hasChildren
                ? string.Format( "<a href='#' class='js-sync-tree-toggle' style='margin-right: 4px;'><i class='fa {0}' style='width: 12px;'></i></a>",
                    isExpanded ? "fa-caret-down" : "fa-caret-right" )
                : "<span style='display: inline-block; width: 16px;'></span>";

            sb.AppendFormat(
                "<li style='padding: 6px 12px;{0}'>{1}<a href='{2}' style='text-decoration: none;{3}'><i class='fa fa-sync' style='margin-right: 4px;'></i>{4}</a>{5}",
                activeStyle,
                caretIcon,
                HttpUtility.HtmlAttributeEncode( groupUrl ),
                activeLinkStyle,
                HttpUtility.HtmlEncode( group.Name ),
                inactiveLabel );

            if ( hasChildren )
            {
                string displayStyle = isExpanded ? "" : " style='display: none;'";
                sb.AppendFormat( "<ul class='list-unstyled' style='margin: 2px 0 0 0; padding-left: 16px;'{0}>", displayStyle );

                foreach ( var subGroup in subGroups )
                {
                    RenderSubGroupNode( sb, subGroup );
                }

                sb.Append( "</ul>" );
            }

            sb.Append( "</li>" );
        }

        private void RenderSubGroupNode( StringBuilder sb, CalculationSubGroup subGroup )
        {
            bool isActive = SelectedType == "subgroup" && SelectedId == subGroup.Id;
            bool containsSelected = SelectedSubGroupId == subGroup.Id;
            bool isExpanded = containsSelected;

            string sgUrl = LinkedPageUrl( "SubGroupDetailPage", new Dictionary<string, string>
            {
                { "CalculationSubGroupId", subGroup.Id.ToString() }
            } );

            string activeStyle = isActive ? " background-color: #e8f0fe;" : "";
            string activeLinkStyle = isActive ? " color: #2196F3; font-weight: 600;" : "";
            string inactiveLabel = subGroup.IsActive ? "" : " <span class='label label-danger' style='font-size: 10px;'>Inactive</span>";

            var calcs = subGroup.Calculations
                .OrderBy( c => c.Order )
                .ThenBy( c => c.Name )
                .ToList();

            bool hasChildren = calcs.Any();
            string caretIcon = hasChildren
                ? string.Format( "<a href='#' class='js-sync-tree-toggle' style='margin-right: 4px;'><i class='fa {0}' style='width: 12px;'></i></a>",
                    isExpanded ? "fa-caret-down" : "fa-caret-right" )
                : "<span style='display: inline-block; width: 16px;'></span>";

            sb.AppendFormat(
                "<li style='padding: 5px 12px;{0}'>{1}<a href='{2}' style='text-decoration: none;{3}'><i class='fa fa-layer-group' style='margin-right: 4px;'></i>{4}</a>{5}",
                activeStyle,
                caretIcon,
                HttpUtility.HtmlAttributeEncode( sgUrl ),
                activeLinkStyle,
                HttpUtility.HtmlEncode( subGroup.Name ),
                inactiveLabel );

            if ( hasChildren )
            {
                string displayStyle = isExpanded ? "" : " style='display: none;'";
                sb.AppendFormat( "<ul class='list-unstyled' style='margin: 2px 0 0 0; padding-left: 16px;'{0}>", displayStyle );

                foreach ( var calc in calcs )
                {
                    RenderCalculationNode( sb, calc );
                }

                sb.Append( "</ul>" );
            }

            sb.Append( "</li>" );
        }

        private void RenderCalculationNode( StringBuilder sb, Calculation calc )
        {
            bool isActive = SelectedType == "calc" && SelectedId == calc.Id;

            string calcUrl = LinkedPageUrl( "CalculationDetailPage", new Dictionary<string, string>
            {
                { "CalculationId", calc.Id.ToString() }
            } );

            string activeStyle = isActive ? " background-color: #e8f0fe;" : "";
            string activeLinkStyle = isActive ? " color: #2196F3; font-weight: 600;" : "";
            string inactiveLabel = calc.IsActive ? "" : " <span class='label label-danger' style='font-size: 10px;'>Inactive</span>";

            sb.AppendFormat(
                "<li style='padding: 4px 12px 4px 28px;{0}'><a href='{1}' style='text-decoration: none;{2}'><i class='fa fa-calculator' style='margin-right: 4px;'></i>{3}</a>{4}</li>",
                activeStyle,
                HttpUtility.HtmlAttributeEncode( calcUrl ),
                activeLinkStyle,
                HttpUtility.HtmlEncode( calc.Name ),
                inactiveLabel );
        }

        #endregion
    }
}
