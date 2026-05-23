using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.JourneyTrack.Data;
using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Attribute;
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_razayya.JourneyTrack
{
    [DisplayName( "Journey Program Detail" )]
    [Category( "Razayya > JourneyTrack" )]
    [Description( "Displays details for a Journey Program and its Sub Groups." )]

    [LinkedPage( "Stage Detail Page",
        Description = "Page to navigate to for Stage details.",
        IsRequired = true,
        Order = 0,
        Key = "SubGroupDetailPage" )]

    [LinkedPage( "Enrollees Page",
        Description = "Page that shows the active enrollees for this Program.",
        IsRequired = false,
        Order = 1,
        Key = "EnrolleesPage" )]

    public partial class JourneyProgramDetail : RockBlock
    {
        #region Properties

        private int JourneyProgramId
        {
            get { return ViewState["JourneyProgramId"] as int? ?? 0; }
            set { ViewState["JourneyProgramId"] = value; }
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
                int groupId = PageParameter( "JourneyProgramId" ).AsInteger();
                ShowDetail( groupId );
            }
        }

        #endregion

        #region Events

        protected void btnEdit_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var group = new JourneyProgramService( rockContext ).Get( JourneyProgramId );
                ShowEditDetails( group );
            }
        }

        protected void btnSave_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new JourneyProgramService( rockContext );
                JourneyProgram group;

                if ( JourneyProgramId != 0 )
                {
                    group = service.Get( JourneyProgramId );
                }
                else
                {
                    group = new JourneyProgram();
                    service.Add( group );
                }

                group.Name = tbName.Text;
                group.Description = tbDescription.Text;
                group.IsActive = cbIsActive.Checked;
                group.RequiresEnrollment = cbRequiresEnrollment.Checked;
                group.AutoEnrollFromPopulation = cbAutoEnroll.Checked;
                group.AutoUnenrollOnPopulationLeave = cbAutoUnenroll.Checked;
                group.RecordStatusValueId = dvpRecordStatus.SelectedValueAsInt();
                group.ConnectionStatusValueId = dvpConnectionStatus.SelectedValueAsInt();
                group.CampusId = cpCampus.SelectedValueAsInt();
                group.DataViewId = dvpDataView.SelectedValueAsInt();

                if ( !group.IsValid )
                {
                    return;
                }

                rockContext.SaveChanges();

                // Save attribute values (PersonAttributeCategories)
                group.LoadAttributes( rockContext );
                var selectedCategoryGuids = cpAttributeCategories.SelectedValuesAsInt()
                    .Select( id => CategoryCache.Get( id ) )
                    .Where( c => c != null )
                    .Select( c => c.Guid.ToString() );
                group.SetAttributeValue( "PersonAttributeCategories", string.Join( ",", selectedCategoryGuids ) );
                group.SaveAttributeValues( rockContext );

                JourneyProgramId = group.Id;
            }

            ShowDetail( JourneyProgramId );
        }

        protected void btnCancel_Click( object sender, EventArgs e )
        {
            if ( JourneyProgramId == 0 )
            {
                NavigateToParentPage();
            }
            else
            {
                ShowDetail( JourneyProgramId );
            }
        }

        protected void cbRequiresEnrollment_CheckedChanged( object sender, EventArgs e )
        {
            UpdatePopulationHeading();
        }

        private void UpdatePopulationHeading()
        {
            if ( cbRequiresEnrollment.Checked && cbAutoEnroll.Checked )
            {
                lPopulationHeading.Text = "Auto-Enroll Spec";
                lPopulationHelp.Text = "Anyone matching these filters will be auto-enrolled by the nightly job (and the 'Reconcile Now' button). Leave blank to require manual enrollment only.";
            }
            else if ( cbRequiresEnrollment.Checked )
            {
                lPopulationHeading.Text = "Population Filters (not used at runtime)";
                lPopulationHelp.Text = "Requires Enrollment is on — these filters become the auto-enroll spec only if Auto-Enroll from Population is also enabled. Otherwise the engine reads enrollments directly.";
            }
            else
            {
                lPopulationHeading.Text = "Population Filters";
                lPopulationHelp.Text = "Define the base population of people this group will evaluate. Leave blank to include everyone.";
            }
        }

        protected void btnEnrollees_Click( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "EnrolleesPage", "JourneyProgramId", JourneyProgramId );
        }

        protected void btnReconcile_Click( object sender, EventArgs e )
        {
            try
            {
                var rec = new JourneyTrackService().ReconcileEnrollments( JourneyProgramId );
                nbWarning.NotificationBoxType = NotificationBoxType.Success;
                nbWarning.Text = string.Format(
                    "Reconciliation complete: <strong>{0}</strong> added, <strong>{1}</strong> reactivated, <strong>{2}</strong> soft-unenrolled. <strong>{3}</strong> active enrollees.",
                    rec.Added, rec.Reactivated, rec.SoftUnenrolled, rec.ActiveAfter );
                nbWarning.Visible = true;
                ShowDetail( JourneyProgramId );
            }
            catch ( Exception ex )
            {
                nbWarning.NotificationBoxType = NotificationBoxType.Danger;
                nbWarning.Text = "Reconciliation failed: " + ex.Message;
                nbWarning.Visible = true;
            }
        }

        protected void btnCopy_Click( object sender, EventArgs e )
        {
            var service = new ImportExportService();
            int newId = service.CopyGroup( JourneyProgramId );
            if ( newId > 0 )
            {
                NavigateToCurrentPageReference( new Dictionary<string, string> { { "JourneyProgramId", newId.ToString() } } );
            }
        }

        protected void btnExport_Click( object sender, EventArgs e )
        {
            var service = new ImportExportService();
            string json = service.ExportGroup( JourneyProgramId );
            if ( !string.IsNullOrEmpty( json ) )
            {
                string groupName;
                using ( var rockContext = new RockContext() )
                {
                    groupName = new JourneyProgramService( rockContext ).GetSelect( JourneyProgramId, g => g.Name ) ?? "Export";
                }

                var safeFileName = groupName.Replace( " ", "_" ).RemoveSpecialCharacters();
                Page.Response.Clear();
                Page.Response.ContentType = "application/json";
                Page.Response.AddHeader( "content-disposition", $"attachment; filename=SyncEngine_Group_{safeFileName}.json" );
                Page.Response.Write( json );
                Page.Response.End();
            }
        }

        protected void gSubGroups_Add( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "SubGroupDetailPage", "StageId", 0, "JourneyProgramId", JourneyProgramId );
        }

        protected void gSubGroups_RowSelected( object sender, RowEventArgs e )
        {
            NavigateToLinkedPage( "SubGroupDetailPage", "StageId", e.RowKeyId );
        }

        protected void gSubGroups_Delete( object sender, RowEventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new StageService( rockContext );
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
                var service = new StageService( rockContext );
                var subGroups = service.Queryable()
                    .Where( sg => sg.JourneyProgramId == JourneyProgramId )
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

            JourneyProgram group = null;

            if ( groupId > 0 )
            {
                using ( var rockContext = new RockContext() )
                {
                    group = new JourneyProgramService( rockContext ).Get( groupId );
                }
            }

            if ( group == null )
            {
                group = new JourneyProgram { IsActive = true };
                lTitle.Text = ActionTitle.Add( "Journey Program" ).FormatAsHtmlTitle();
                ShowEditDetails( group );
                return;
            }

            JourneyProgramId = group.Id;

            lTitle.Text = group.Name.FormatAsHtmlTitle();
            hlInactive.Visible = !group.IsActive;
            hlLastRun.Text = group.LastRunDateTime.HasValue
                ? string.Format( "Last Run: {0}", group.LastRunDateTime.Value.ToShortDateString() )
                : "Never Run";

            lDescription.Text = group.Description;

            // Enrollment summary (visible when RequiresEnrollment is on)
            string populationHtml = string.Empty;
            if ( group.RequiresEnrollment )
            {
                int activeEnrollees;
                using ( var ctx = new RockContext() )
                {
                    activeEnrollees = new JourneyProgramEnrollmentService( ctx ).Queryable().AsNoTracking()
                        .Count( e => e.JourneyProgramId == group.Id && e.IsActive );
                }
                populationHtml += string.Format( "<dt>Enrollees</dt><dd>{0} active</dd>", activeEnrollees );
                var modeLabel = group.AutoEnrollFromPopulation
                    ? ( group.AutoUnenrollOnPopulationLeave ? "Auto-Enroll &amp; Auto-Unenroll" : "Auto-Enroll" )
                    : "Manual only";
                populationHtml += string.Format( "<dt>Enrollment Mode</dt><dd>{0}</dd>", modeLabel );
                btnEnrollees.Visible = true;
                btnReconcile.Visible = group.AutoEnrollFromPopulation;
            }
            else
            {
                btnEnrollees.Visible = false;
                btnReconcile.Visible = false;
            }

            // Population summary
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

            using ( var attrCtx = new RockContext() )
            {
                group.LoadAttributes( attrCtx );
                var categoryGuids = group.GetAttributeValue( "PersonAttributeCategories" );
                if ( !string.IsNullOrWhiteSpace( categoryGuids ) )
                {
                    var categoryNames = categoryGuids.SplitDelimitedValues()
                        .Select( g => CategoryCache.Get( g.AsGuid() ) )
                        .Where( c => c != null )
                        .Select( c => c.Name )
                        .OrderBy( n => n );
                    populationHtml += string.Format( "<dt>Target Attribute Categories</dt><dd>{0}</dd>",
                        string.Join( ", ", categoryNames ) );
                }
            }

            lPopulationSummary.Text = populationHtml;

            btnSecurity.Visible = group.IsAuthorized( Authorization.ADMINISTRATE, CurrentPerson );
            btnSecurity.EntityId = group.Id;
            btnSecurity.Title = group.Name;

            pnlView.Visible = true;
            pnlEdit.Visible = false;

            BindSubGroupsGrid();
        }

        private void ShowEditDetails( JourneyProgram group )
        {
            pnlView.Visible = false;
            pnlEdit.Visible = true;

            tbName.Text = group.Name;
            tbDescription.Text = group.Description;
            cbIsActive.Checked = group.IsActive;
            cbRequiresEnrollment.Checked = group.RequiresEnrollment;
            cbAutoEnroll.Checked = group.AutoEnrollFromPopulation;
            cbAutoUnenroll.Checked = group.AutoUnenrollOnPopulationLeave;
            UpdatePopulationHeading();

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

            // Configure the category picker for Person Attribute categories
            var attributeEntityTypeId = EntityTypeCache.Get( typeof( Rock.Model.Attribute ) ).Id;
            cpAttributeCategories.EntityTypeId = attributeEntityTypeId;

            using ( var attrCtx = new RockContext() )
            {
                group.LoadAttributes( attrCtx );
                var categoryGuids = group.GetAttributeValue( "PersonAttributeCategories" );
                if ( !string.IsNullOrWhiteSpace( categoryGuids ) )
                {
                    var categoryIds = categoryGuids.SplitDelimitedValues()
                        .Select( g => CategoryCache.Get( g.AsGuid() ) )
                        .Where( c => c != null )
                        .Select( c => c.Id )
                        .ToList();
                    cpAttributeCategories.SetValues( categoryIds );
                }
            }
        }

        private void BindSubGroupsGrid()
        {
            using ( var rockContext = new RockContext() )
            {
                var subGroups = new StageService( rockContext ).Queryable().AsNoTracking()
                    .Where( sg => sg.JourneyProgramId == JourneyProgramId )
                    .OrderBy( sg => sg.Order )
                    .ThenBy( sg => sg.Name )
                    .Select( sg => new
                    {
                        sg.Id,
                        sg.Name,
                        sg.IsActive,
                        HasPrerequisites = !string.IsNullOrEmpty( sg.PrerequisiteStageIds ),
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
