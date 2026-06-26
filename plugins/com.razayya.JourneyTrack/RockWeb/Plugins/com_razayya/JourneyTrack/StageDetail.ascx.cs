using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.JourneyTrack.CalculationTypes;
using com.razayya.JourneyTrack.Data;
using com.razayya.JourneyTrack.Model;
using com.razayya.JourneyTrack.UI;

using Rock;
using Rock.Attribute;
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_razayya.JourneyTrack
{
    [DisplayName( "JourneyStage Detail" )]
    [Category( "Razayya > JourneyTrack" )]
    [Description( "Displays details for a JourneyStage and its Calculations." )]

    [LinkedPage( "JourneyCalculation Detail Page",
        Description = "Page to navigate to for JourneyCalculation details.",
        IsRequired = true,
        Order = 0,
        Key = "JourneyCalculationDetailPage" )]

    [LinkedPage( "Parent Page",
        Description = "Page to navigate back to the parent Journey Program.",
        IsRequired = false,
        Order = 1,
        Key = "ParentPage" )]

    public partial class StageDetail : RockBlock
    {
        #region Properties

        private int SubGroupId
        {
            get { return ViewState["SubGroupId"] as int? ?? 0; }
            set { ViewState["SubGroupId"] = value; }
        }

        private int ParentGroupId
        {
            get { return ViewState["ParentGroupId"] as int? ?? 0; }
            set { ViewState["ParentGroupId"] = value; }
        }

        #endregion

        #region Base Control Methods

        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            gCalculations.DataKeyNames = new string[] { "Id" };
            gCalculations.Actions.ShowAdd = true;
            gCalculations.Actions.AddClick += gCalculations_Add;
            gCalculations.GridReorder += gCalculations_GridReorder;
            gCalculations.GridRebind += gCalculations_GridRebind;
        }

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                int subGroupId = PageParameter( "StageId" ).AsInteger();
                int groupId = PageParameter( "JourneyProgramId" ).AsInteger();
                ParentGroupId = groupId;
                ShowDetail( subGroupId );
            }
        }

        #endregion

        #region Events

        protected void btnEdit_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var subGroup = new StageService( rockContext ).Get( SubGroupId );
                ShowEditDetails( subGroup );
            }
        }

        protected void btnSave_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new StageService( rockContext );
                Stage subGroup;

                if ( SubGroupId != 0 )
                {
                    subGroup = service.Get( SubGroupId );
                }
                else
                {
                    subGroup = new Stage();
                    subGroup.JourneyProgramId = ParentGroupId;
                    service.Add( subGroup );
                }

                subGroup.Name = tbName.Text;
                subGroup.Description = tbDescription.Text;
                subGroup.IsActive = cbIsActive.Checked;
                subGroup.PrerequisiteStageIds = string.Join( ",", cblPrerequisites.SelectedValues );
                subGroup.AdditionalDataViewId = dvpAdditionalDataView.SelectedValueAsInt();
                subGroup.OnCompleteSystemCommunicationId = ddlOnCompleteCommunication.SelectedValueAsInt();
                subGroup.LogicTreeJson = seLogic.Value;

                if ( !subGroup.IsValid )
                {
                    return;
                }

                rockContext.SaveChanges();

                SubGroupId = subGroup.Id;
                ParentGroupId = subGroup.JourneyProgramId;
            }

            ShowDetail( SubGroupId );
        }

        protected void btnCancel_Click( object sender, EventArgs e )
        {
            if ( SubGroupId == 0 )
            {
                NavigateToParentPage();
            }
            else
            {
                ShowDetail( SubGroupId );
            }
        }

        protected void btnCopy_Click( object sender, EventArgs e )
        {
            var service = new ImportExportService();
            int newId = service.CopySubGroup( SubGroupId );
            if ( newId > 0 )
            {
                NavigateToCurrentPageReference( new Dictionary<string, string> { { "StageId", newId.ToString() } } );
            }
        }

        protected void btnBack_Click( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "ParentPage", "JourneyProgramId", ParentGroupId );
        }

        protected void gCalculations_Add( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "JourneyCalculationDetailPage", "JourneyCalculationId", 0, "StageId", SubGroupId );
        }

        protected void gCalculations_RowSelected( object sender, RowEventArgs e )
        {
            NavigateToLinkedPage( "JourneyCalculationDetailPage", "JourneyCalculationId", e.RowKeyId );
        }

        protected void gCalculations_Delete( object sender, RowEventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new JourneyCalculationService( rockContext );
                var calc = service.Get( e.RowKeyId );
                if ( calc != null )
                {
                    service.Delete( calc );
                    rockContext.SaveChanges();
                }
            }

            BindCalculationsGrid();
        }

        protected void gCalculations_GridReorder( object sender, GridReorderEventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new JourneyCalculationService( rockContext );
                var calcs = service.Queryable()
                    .Where( c => c.StageId == SubGroupId )
                    .OrderBy( c => c.Order )
                    .ThenBy( c => c.Name )
                    .ToList();
                service.Reorder( calcs, e.OldIndex, e.NewIndex );
                rockContext.SaveChanges();
            }

            BindCalculationsGrid();
        }

        protected void gCalculations_GridRebind( object sender, GridRebindEventArgs e )
        {
            BindCalculationsGrid();
        }

        protected void btnManageMediaGroups_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var stage = new StageService( rockContext ).Get( SubGroupId );
                if ( stage == null )
                {
                    return;
                }

                // StageId must be set before Value so the editor can resolve calc names.
                mgEditor.StageId = stage.Id;
                mgEditor.Value = stage.MediaGroupsJson;
            }

            nbMediaGroups.Visible = false;
            mdMediaGroups.Show();
        }

        protected void mdMediaGroups_SaveClick( object sender, EventArgs e )
        {
            var errors = mgEditor.GetValidationErrors();
            if ( errors.Any() )
            {
                nbMediaGroups.Text = string.Join( "<br/>", errors );
                nbMediaGroups.Visible = true;
                mdMediaGroups.Show();
                return;
            }

            using ( var rockContext = new RockContext() )
            {
                var stage = new StageService( rockContext ).Get( SubGroupId );
                if ( stage != null )
                {
                    stage.MediaGroupsJson = mgEditor.Value;
                    rockContext.SaveChanges();
                }
            }

            mdMediaGroups.Hide();
            ShowDetail( SubGroupId );
        }

        #endregion

        #region Methods

        private void ShowDetail( int subGroupId )
        {
            pnlDetails.Visible = true;

            Stage subGroup = null;

            if ( subGroupId > 0 )
            {
                using ( var rockContext = new RockContext() )
                {
                    subGroup = new StageService( rockContext ).Queryable()
                        .Include( sg => sg.JourneyProgram )
                        .FirstOrDefault( sg => sg.Id == subGroupId );
                }
            }

            if ( subGroup == null )
            {
                subGroup = new Stage { IsActive = true, JourneyProgramId = ParentGroupId };
                lTitle.Text = ActionTitle.Add( "JourneyStage" ).FormatAsHtmlTitle();
                ShowEditDetails( subGroup );
                return;
            }

            SubGroupId = subGroup.Id;
            ParentGroupId = subGroup.JourneyProgramId;

            lTitle.Text = subGroup.Name.FormatAsHtmlTitle();
            hlInactive.Visible = !subGroup.IsActive;
            hlGroupName.Text = subGroup.JourneyProgram?.Name ?? string.Empty;

            lDescription.Text = subGroup.Description;

            string summary = string.Empty;

            var prereqIds = ( subGroup.PrerequisiteStageIds ?? string.Empty )
                .Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries )
                .Select( s => s.Trim().AsInteger() )
                .Where( id => id > 0 )
                .ToList();

            if ( prereqIds.Any() )
            {
                using ( var ctx = new RockContext() )
                {
                    var names = new StageService( ctx ).Queryable()
                        .Where( sg => prereqIds.Contains( sg.Id ) )
                        .Select( sg => sg.Name )
                        .ToList();
                    summary += string.Format( "<dt>Prerequisites</dt><dd>{0}</dd>", string.Join( ", ", names ) );
                }
            }
            else
            {
                summary += "<dt>Prerequisites</dt><dd>None (uses full base population)</dd>";
            }

            if ( subGroup.AdditionalDataViewId.HasValue )
            {
                using ( var ctx = new RockContext() )
                {
                    var dv = new DataViewService( ctx ).Get( subGroup.AdditionalDataViewId.Value );
                    if ( dv != null )
                    {
                        summary += string.Format( "<dt>Additional Data View</dt><dd>{0}</dd>", dv.Name );
                    }
                }
            }

            lSubGroupSummary.Text = summary;

            pnlView.Visible = true;
            pnlEdit.Visible = false;

            BindCalculationsGrid();

            // Media Groups surface — only meaningful once a Stage has more than one video.
            // The action button lives in the .actions bar (between Edit and Copy); the summary
            // panel stays below the Calculations grid. Both gate on the same >1-media-calc test.
            int mediaCalcCount = CountActiveMediaCalcs( subGroup.Id );
            btnManageMediaGroups.Visible = mediaCalcCount > 1;
            pnlMediaGroups.Visible = mediaCalcCount > 1;
            if ( pnlMediaGroups.Visible )
            {
                lMediaGroupsSummary.Text = JourneyTrackUiHelper.FormatMediaGroupsSummaryHtml( subGroup.MediaGroupsJson, subGroup.Id );
            }
        }

        private void ShowEditDetails( Stage subGroup )
        {
            pnlView.Visible = false;
            pnlEdit.Visible = true;

            tbName.Text = subGroup.Name;
            tbDescription.Text = subGroup.Description;
            cbIsActive.Checked = subGroup.IsActive;
            dvpAdditionalDataView.SetValue( subGroup.AdditionalDataViewId );
            JourneyTrackUiHelper.PopulateSystemCommunicationPicker( ddlOnCompleteCommunication, subGroup.OnCompleteSystemCommunicationId );

            // Populate prerequisite picker with sibling sub-groups (excluding self)
            cblPrerequisites.Items.Clear();
            using ( var rockContext = new RockContext() )
            {
                var siblings = new StageService( rockContext ).Queryable()
                    .Where( sg => sg.JourneyProgramId == subGroup.JourneyProgramId && sg.Id != subGroup.Id )
                    .OrderBy( sg => sg.Order )
                    .ThenBy( sg => sg.Name )
                    .Select( sg => new { sg.Id, sg.Name } )
                    .ToList();

                foreach ( var sibling in siblings )
                {
                    cblPrerequisites.Items.Add( new System.Web.UI.WebControls.ListItem( sibling.Name, sibling.Id.ToString() ) );
                }
            }

            // Set selected prerequisites
            var selectedIds = ( subGroup.PrerequisiteStageIds ?? string.Empty )
                .Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries )
                .Select( s => s.Trim() )
                .ToList();
            cblPrerequisites.SetValues( selectedIds );

            // Stage logic tree (leaves reference this Stage's calcs). StageId must be
            // set before Value so the leaf dropdowns can resolve calc names.
            seLogic.StageId = subGroup.Id;
            seLogic.Value = subGroup.LogicTreeJson;
        }

        private void BindCalculationsGrid()
        {
            using ( var rockContext = new RockContext() )
            {
                var calcs = new JourneyCalculationService( rockContext ).Queryable().AsNoTracking()
                    .Where( c => c.StageId == SubGroupId )
                    .OrderBy( c => c.Order )
                    .ThenBy( c => c.Name )
                    .Select( c => new
                    {
                        c.Id,
                        c.Name,
                        c.IsActive,
                        CalculationType = c.CalculationTypeEntityType.FriendlyName,
                        TargetAttribute = c.PersonAttribute.Name
                    } )
                    .ToList();

                gCalculations.DataSource = calcs;
                gCalculations.DataBind();
            }
        }

        private int CountActiveMediaCalcs( int stageId )
        {
            if ( stageId <= 0 )
            {
                return 0;
            }

            var mediaTypeName = typeof( MediaWatchedCalculation ).FullName;
            using ( var rockContext = new RockContext() )
            {
                return new JourneyCalculationService( rockContext ).Queryable().AsNoTracking()
                    .Count( c => c.StageId == stageId
                        && c.IsActive
                        && c.CalculationTypeEntityType.Name == mediaTypeName );
            }
        }

        #endregion
    }
}
