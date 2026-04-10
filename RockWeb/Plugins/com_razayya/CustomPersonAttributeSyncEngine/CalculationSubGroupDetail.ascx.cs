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
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_razayya.CustomPersonAttributeSyncEngine
{
    [DisplayName( "Calculation Sub Group Detail" )]
    [Category( "Razayya > Attribute Sync Engine" )]
    [Description( "Displays details for a Calculation Sub Group and its Calculations." )]

    [LinkedPage( "Calculation Detail Page",
        Description = "Page to navigate to for Calculation details.",
        IsRequired = true,
        Order = 0,
        Key = "CalculationDetailPage" )]

    [LinkedPage( "Parent Page",
        Description = "Page to navigate back to the parent Calculation Group.",
        IsRequired = false,
        Order = 1,
        Key = "ParentPage" )]

    public partial class CalculationSubGroupDetail : RockBlock
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
                int subGroupId = PageParameter( "CalculationSubGroupId" ).AsInteger();
                int groupId = PageParameter( "CalculationGroupId" ).AsInteger();
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
                var subGroup = new CalculationSubGroupService( rockContext ).Get( SubGroupId );
                ShowEditDetails( subGroup );
            }
        }

        protected void btnSave_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new CalculationSubGroupService( rockContext );
                CalculationSubGroup subGroup;

                if ( SubGroupId != 0 )
                {
                    subGroup = service.Get( SubGroupId );
                }
                else
                {
                    subGroup = new CalculationSubGroup();
                    subGroup.CalculationGroupId = ParentGroupId;
                    service.Add( subGroup );
                }

                subGroup.Name = tbName.Text;
                subGroup.Description = tbDescription.Text;
                subGroup.IsActive = cbIsActive.Checked;
                subGroup.ScopeToPreviousSubGroup = cbScopeToPrevious.Checked;
                subGroup.AdditionalDataViewId = dvpAdditionalDataView.SelectedValueAsInt();

                if ( !subGroup.IsValid )
                {
                    return;
                }

                rockContext.SaveChanges();

                SubGroupId = subGroup.Id;
                ParentGroupId = subGroup.CalculationGroupId;
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

        protected void btnBack_Click( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "ParentPage", "CalculationGroupId", ParentGroupId );
        }

        protected void gCalculations_Add( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "CalculationDetailPage", "CalculationId", 0, "CalculationSubGroupId", SubGroupId );
        }

        protected void gCalculations_RowSelected( object sender, RowEventArgs e )
        {
            NavigateToLinkedPage( "CalculationDetailPage", "CalculationId", e.RowKeyId );
        }

        protected void gCalculations_Delete( object sender, RowEventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new CalculationService( rockContext );
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
                var service = new CalculationService( rockContext );
                var calcs = service.Queryable()
                    .Where( c => c.CalculationSubGroupId == SubGroupId )
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

        #endregion

        #region Methods

        private void ShowDetail( int subGroupId )
        {
            pnlDetails.Visible = true;

            CalculationSubGroup subGroup = null;

            if ( subGroupId > 0 )
            {
                using ( var rockContext = new RockContext() )
                {
                    subGroup = new CalculationSubGroupService( rockContext ).Queryable()
                        .Include( sg => sg.CalculationGroup )
                        .FirstOrDefault( sg => sg.Id == subGroupId );
                }
            }

            if ( subGroup == null )
            {
                subGroup = new CalculationSubGroup { IsActive = true, ScopeToPreviousSubGroup = true, CalculationGroupId = ParentGroupId };
                lTitle.Text = ActionTitle.Add( "Calculation Sub Group" ).FormatAsHtmlTitle();
                ShowEditDetails( subGroup );
                return;
            }

            SubGroupId = subGroup.Id;
            ParentGroupId = subGroup.CalculationGroupId;

            lTitle.Text = subGroup.Name.FormatAsHtmlTitle();
            hlInactive.Visible = !subGroup.IsActive;
            hlGroupName.Text = subGroup.CalculationGroup?.Name ?? string.Empty;

            lDescription.Text = subGroup.Description;

            string summary = string.Empty;
            summary += string.Format( "<dt>Scope to Previous</dt><dd>{0}</dd>", subGroup.ScopeToPreviousSubGroup ? "Yes" : "No" );

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
        }

        private void ShowEditDetails( CalculationSubGroup subGroup )
        {
            pnlView.Visible = false;
            pnlEdit.Visible = true;

            tbName.Text = subGroup.Name;
            tbDescription.Text = subGroup.Description;
            cbIsActive.Checked = subGroup.IsActive;
            cbScopeToPrevious.Checked = subGroup.ScopeToPreviousSubGroup;
            dvpAdditionalDataView.SetValue( subGroup.AdditionalDataViewId );
        }

        private void BindCalculationsGrid()
        {
            using ( var rockContext = new RockContext() )
            {
                var calcs = new CalculationService( rockContext ).Queryable().AsNoTracking()
                    .Where( c => c.CalculationSubGroupId == SubGroupId )
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

        #endregion
    }
}
