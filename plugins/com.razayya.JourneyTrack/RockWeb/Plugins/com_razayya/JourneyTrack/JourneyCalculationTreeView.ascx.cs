using System;
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
    [DisplayName( "JourneyCalculation Tree View" )]
    [Category( "Razayya > JourneyTrack" )]
    [Description( "Displays a navigable tree of a Journey Program's hierarchy." )]

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

    public partial class JourneyCalculationTreeView : RockBlock
    {
        #region Base Control Methods

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                RenderTree();
            }
        }

        #endregion

        #region Methods

        private void RenderTree()
        {
            int groupId = ResolveGroupId();
            if ( groupId == 0 )
            {
                lTreeHtml.Text = "<div class='padding-all-md text-muted'>No group context found.</div>";
                return;
            }

            using ( var rockContext = new RockContext() )
            {
                var group = new JourneyProgramService( rockContext ).Queryable()
                    .Include( g => g.Stages.Select( sg => sg.Calculations ) )
                    .FirstOrDefault( g => g.Id == groupId );

                if ( group == null )
                {
                    lTreeHtml.Text = "<div class='padding-all-md text-muted'>Group not found.</div>";
                    return;
                }

                lGroupName.Text = group.Name;

                // Determine what's currently selected
                string activeType = null;
                int activeId = 0;

                int calcId = PageParameter( "JourneyCalculationId" ).AsInteger();
                int subGroupId = PageParameter( "StageId" ).AsInteger();
                int paramGroupId = PageParameter( "JourneyProgramId" ).AsInteger();

                if ( calcId > 0 )
                {
                    activeType = "calc";
                    activeId = calcId;
                }
                else if ( subGroupId > 0 )
                {
                    activeType = "subgroup";
                    activeId = subGroupId;
                }
                else if ( paramGroupId > 0 )
                {
                    activeType = "group";
                    activeId = paramGroupId;
                }

                var sb = new StringBuilder();
                sb.Append( "<ul class='list-unstyled' style='margin: 0;'>" );

                // Group node
                string groupUrl = LinkedPageUrl( "GroupDetailPage", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "JourneyProgramId", group.Id.ToString() }
                } );
                bool groupActive = activeType == "group" && activeId == group.Id;
                sb.AppendFormat(
                    "<li style='padding: 8px 16px;{0}'><a href='{1}' style='text-decoration: none;{2}'><i class='fa fa-sync'></i> <strong>{3}</strong></a>",
                    groupActive ? " background-color: #e8f0fe;" : "",
                    HttpUtility.HtmlAttributeEncode( groupUrl ),
                    groupActive ? " color: #2196F3;" : "",
                    HttpUtility.HtmlEncode( group.Name ) );

                // Sub Groups
                var subGroups = group.Stages
                    .OrderBy( sg => sg.Order )
                    .ThenBy( sg => sg.Name )
                    .ToList();

                if ( subGroups.Any() )
                {
                    sb.Append( "<ul class='list-unstyled' style='margin: 4px 0 0 0;'>" );

                    foreach ( var subGroup in subGroups )
                    {
                        bool sgActive = activeType == "subgroup" && activeId == subGroup.Id;
                        bool sgContainsActive = activeType == "calc" && subGroup.Calculations.Any( c => c.Id == activeId );

                        string sgUrl = LinkedPageUrl( "SubGroupDetailPage", new System.Collections.Generic.Dictionary<string, string>
                        {
                            { "StageId", subGroup.Id.ToString() }
                        } );

                        string sgStyle = sgActive ? " background-color: #e8f0fe;" : "";
                        string sgLinkStyle = sgActive ? " color: #2196F3;" : "";
                        string inactiveLabel = subGroup.IsActive ? "" : " <span class='label label-danger label-xs'>Inactive</span>";

                        sb.AppendFormat(
                            "<li style='padding: 6px 16px;{0}'><a href='{1}' style='text-decoration: none;{2}'><i class='fa fa-layer-group'></i> {3}</a>{4}",
                            sgStyle,
                            HttpUtility.HtmlAttributeEncode( sgUrl ),
                            sgLinkStyle,
                            HttpUtility.HtmlEncode( subGroup.Name ),
                            inactiveLabel );

                        // Calculations
                        var calcs = subGroup.Calculations
                            .OrderBy( c => c.Order )
                            .ThenBy( c => c.Name )
                            .ToList();

                        if ( calcs.Any() )
                        {
                            sb.Append( "<ul class='list-unstyled' style='margin: 4px 0 0 0;'>" );

                            foreach ( var calc in calcs )
                            {
                                bool calcActive = activeType == "calc" && activeId == calc.Id;

                                string calcUrl = LinkedPageUrl( "JourneyCalculationDetailPage", new System.Collections.Generic.Dictionary<string, string>
                                {
                                    { "JourneyCalculationId", calc.Id.ToString() }
                                } );

                                string calcStyle = calcActive ? " background-color: #e8f0fe;" : "";
                                string calcLinkStyle = calcActive ? " color: #2196F3;" : "";
                                string calcInactiveLabel = calc.IsActive ? "" : " <span class='label label-danger label-xs'>Inactive</span>";

                                sb.AppendFormat(
                                    "<li style='padding: 4px 16px;{0}'><a href='{1}' style='text-decoration: none;{2}'><i class='fa fa-calculator'></i> {3}</a>{4}</li>",
                                    calcStyle,
                                    HttpUtility.HtmlAttributeEncode( calcUrl ),
                                    calcLinkStyle,
                                    HttpUtility.HtmlEncode( calc.Name ),
                                    calcInactiveLabel );
                            }

                            sb.Append( "</ul>" );
                        }

                        sb.Append( "</li>" );
                    }

                    sb.Append( "</ul>" );
                }

                sb.Append( "</li></ul>" );

                lTreeHtml.Text = sb.ToString();
            }
        }

        /// <summary>
        /// Resolves the JourneyProgramId from any level of the hierarchy based on page parameters.
        /// </summary>
        private int ResolveGroupId()
        {
            int groupId = PageParameter( "JourneyProgramId" ).AsInteger();
            if ( groupId > 0 )
            {
                return groupId;
            }

            int subGroupId = PageParameter( "StageId" ).AsInteger();
            if ( subGroupId > 0 )
            {
                using ( var rockContext = new RockContext() )
                {
                    return new StageService( rockContext )
                        .GetSelect( subGroupId, sg => sg.JourneyProgramId );
                }
            }

            int calcId = PageParameter( "JourneyCalculationId" ).AsInteger();
            if ( calcId > 0 )
            {
                using ( var rockContext = new RockContext() )
                {
                    return new JourneyCalculationService( rockContext ).Queryable()
                        .Where( c => c.Id == calcId )
                        .Select( c => c.Stage.JourneyProgramId )
                        .FirstOrDefault();
                }
            }

            return 0;
        }

        #endregion
    }
}
