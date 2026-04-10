using System;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.CustomPersonAttributeSyncEngine.Model;

using Rock;
using Rock.Attribute;
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_razayya.CustomPersonAttributeSyncEngine
{
    [DisplayName( "Calculation Group Detail" )]
    [Category( "Razayya > Attribute Sync Engine" )]
    [Description( "Displays details for a Calculation Group and its Sub Groups." )]

    [LinkedPage( "Sub Group Detail Page",
        Description = "Page to navigate to for Sub Group details.",
        IsRequired = true,
        Order = 0,
        Key = "SubGroupDetailPage" )]

    public partial class CalculationGroupDetail : RockBlock
    {
        #region Properties

        private int CalculationGroupId
        {
            get { return ViewState["CalculationGroupId"] as int? ?? 0; }
            set { ViewState["CalculationGroupId"] = value; }
        }

        #endregion

        #region Base Control Methods

        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            gSubGroups.DataKeyNames = new string[] { "Id" };
            gSubGroups.Actions.ShowAdd = true;
            gSubGroups.Actions.AddClick += gSubGroups_Add;
            gSubGroups.GridReorder += gSubGroups_GridReorder;
            gSubGroups.GridRebind += gSubGroups_GridRebind;
        }

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                int groupId = PageParameter( "CalculationGroupId" ).AsInteger();
                ShowDetail( groupId );
            }
        }

        #endregion

        #region Events

        protected void btnEdit_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var group = new CalculationGroupService( rockContext ).Get( CalculationGroupId );
                ShowEditDetails( group );
            }
        }

        protected void btnSave_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new CalculationGroupService( rockContext );
                CalculationGroup group;

                if ( CalculationGroupId != 0 )
                {
                    group = service.Get( CalculationGroupId );
                }
                else
                {
                    group = new CalculationGroup();
                    service.Add( group );
                }

                group.Name = tbName.Text;
                group.Description = tbDescription.Text;
                group.IsActive = cbIsActive.Checked;
                group.RecordStatusValueId = dvpRecordStatus.SelectedValueAsInt();
                group.ConnectionStatusValueId = dvpConnectionStatus.SelectedValueAsInt();
                group.CampusId = cpCampus.SelectedValueAsInt();
                group.DataViewId = dvpDataView.SelectedValueAsInt();

                if ( !group.IsValid )
                {
                    return;
                }

                rockContext.SaveChanges();

                CalculationGroupId = group.Id;
            }

            ShowDetail( CalculationGroupId );
        }

        protected void btnCancel_Click( object sender, EventArgs e )
        {
            if ( CalculationGroupId == 0 )
            {
                NavigateToParentPage();
            }
            else
            {
                ShowDetail( CalculationGroupId );
            }
        }

        protected void gSubGroups_Add( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "SubGroupDetailPage", "CalculationSubGroupId", 0, "CalculationGroupId", CalculationGroupId );
        }

        protected void gSubGroups_RowSelected( object sender, RowEventArgs e )
        {
            NavigateToLinkedPage( "SubGroupDetailPage", "CalculationSubGroupId", e.RowKeyId );
        }

        protected void gSubGroups_Delete( object sender, RowEventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new CalculationSubGroupService( rockContext );
                var subGroup = service.Get( e.RowKeyId );
                if ( subGroup != null )
                {
                    service.Delete( subGroup );
                    rockContext.SaveChanges();
                }
            }

            BindSubGroupsGrid();
        }

        protected void gSubGroups_GridReorder( object sender, GridReorderEventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new CalculationSubGroupService( rockContext );
                var subGroups = service.Queryable()
                    .Where( sg => sg.CalculationGroupId == CalculationGroupId )
                    .OrderBy( sg => sg.Order )
                    .ThenBy( sg => sg.Name )
                    .ToList();
                service.Reorder( subGroups, e.OldIndex, e.NewIndex );
                rockContext.SaveChanges();
            }

            BindSubGroupsGrid();
        }

        protected void gSubGroups_GridRebind( object sender, GridRebindEventArgs e )
        {
            BindSubGroupsGrid();
        }

        #endregion

        #region Methods

        private void ShowDetail( int groupId )
        {
            pnlDetails.Visible = true;

            CalculationGroup group = null;

            if ( groupId > 0 )
            {
                using ( var rockContext = new RockContext() )
                {
                    group = new CalculationGroupService( rockContext ).Get( groupId );
                }
            }

            if ( group == null )
            {
                group = new CalculationGroup { IsActive = true };
                lTitle.Text = ActionTitle.Add( "Calculation Group" ).FormatAsHtmlTitle();
                ShowEditDetails( group );
                return;
            }

            CalculationGroupId = group.Id;

            lTitle.Text = group.Name.FormatAsHtmlTitle();
            hlInactive.Visible = !group.IsActive;
            hlLastRun.Text = group.LastRunDateTime.HasValue
                ? string.Format( "Last Run: {0}", group.LastRunDateTime.Value.ToShortDateString() )
                : "Never Run";

            lDescription.Text = group.Description;

            // Population summary
            string populationHtml = string.Empty;
            if ( group.RecordStatusValueId.HasValue )
            {
                populationHtml += string.Format( "<dt>Record Status</dt><dd>{0}</dd>", DefinedValueCache.Get( group.RecordStatusValueId.Value )?.Value );
            }
            if ( group.ConnectionStatusValueId.HasValue )
            {
                populationHtml += string.Format( "<dt>Connection Status</dt><dd>{0}</dd>", DefinedValueCache.Get( group.ConnectionStatusValueId.Value )?.Value );
            }
            if ( group.CampusId.HasValue )
            {
                populationHtml += string.Format( "<dt>Campus</dt><dd>{0}</dd>", CampusCache.Get( group.CampusId.Value )?.Name );
            }
            if ( group.DataViewId.HasValue )
            {
                using ( var ctx = new RockContext() )
                {
                    var dv = new DataViewService( ctx ).Get( group.DataViewId.Value );
                    if ( dv != null )
                    {
                        populationHtml += string.Format( "<dt>Data View</dt><dd>{0}</dd>", dv.Name );
                    }
                }
            }
            if ( string.IsNullOrEmpty( populationHtml ) )
            {
                populationHtml = "<dt>Population</dt><dd>All People</dd>";
            }
            lPopulationSummary.Text = populationHtml;

            btnSecurity.Visible = group.IsAuthorized( Authorization.ADMINISTRATE, CurrentPerson );
            btnSecurity.EntityId = group.Id;
            btnSecurity.Title = group.Name;

            pnlView.Visible = true;
            pnlEdit.Visible = false;

            BindSubGroupsGrid();
        }

        private void ShowEditDetails( CalculationGroup group )
        {
            pnlView.Visible = false;
            pnlEdit.Visible = true;

            tbName.Text = group.Name;
            tbDescription.Text = group.Description;
            cbIsActive.Checked = group.IsActive;

            // Populate DefinedType pickers
            var recordStatusDefinedTypeId = DefinedTypeCache.Get( Rock.SystemGuid.DefinedType.PERSON_RECORD_STATUS.AsGuid() )?.Id;
            if ( recordStatusDefinedTypeId.HasValue )
            {
                dvpRecordStatus.DefinedTypeId = recordStatusDefinedTypeId.Value;
            }
            dvpRecordStatus.SetValue( group.RecordStatusValueId );

            var connectionStatusDefinedTypeId = DefinedTypeCache.Get( Rock.SystemGuid.DefinedType.PERSON_CONNECTION_STATUS.AsGuid() )?.Id;
            if ( connectionStatusDefinedTypeId.HasValue )
            {
                dvpConnectionStatus.DefinedTypeId = connectionStatusDefinedTypeId.Value;
            }
            dvpConnectionStatus.SetValue( group.ConnectionStatusValueId );

            cpCampus.SetValue( group.CampusId );
            dvpDataView.SetValue( group.DataViewId );
        }

        private void BindSubGroupsGrid()
        {
            using ( var rockContext = new RockContext() )
            {
                var subGroups = new CalculationSubGroupService( rockContext ).Queryable().AsNoTracking()
                    .Where( sg => sg.CalculationGroupId == CalculationGroupId )
                    .OrderBy( sg => sg.Order )
                    .ThenBy( sg => sg.Name )
                    .Select( sg => new
                    {
                        sg.Id,
                        sg.Name,
                        sg.IsActive,
                        sg.ScopeToPreviousSubGroup,
                        CalculationCount = sg.Calculations.Count()
                    } )
                    .ToList();

                gSubGroups.DataSource = subGroups;
                gSubGroups.DataBind();
            }
        }

        #endregion
    }
}
