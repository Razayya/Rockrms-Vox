using System;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.CustomPersonAttributeSyncEngine.Data;
using com.razayya.CustomPersonAttributeSyncEngine.Model;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_razayya.CustomPersonAttributeSyncEngine
{
    [DisplayName( "Calculation Group List" )]
    [Category( "Razayya > Attribute Sync Engine" )]
    [Description( "Lists Calculation Groups for the Attribute Sync Engine." )]

    [LinkedPage( "Detail Page",
        Description = "Page to navigate to for Calculation Group details.",
        IsRequired = true,
        Order = 0,
        Key = "DetailPage" )]

    public partial class CalculationGroupList : RockBlock, ICustomGridColumns
    {
        #region Base Control Methods

        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            gList.DataKeyNames = new string[] { "Id" };
            gList.Actions.ShowAdd = true;
            gList.Actions.AddClick += gList_Add;
            gList.GridReorder += gList_GridReorder;
            gList.GridRebind += gList_GridRebind;
            gList.IsDeleteEnabled = UserCanAdministrate;
        }

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                BindGrid();
            }
        }

        #endregion

        #region Events

        protected void gList_Add( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "DetailPage", "CalculationGroupId", 0 );
        }

        protected void gList_RowSelected( object sender, RowEventArgs e )
        {
            NavigateToLinkedPage( "DetailPage", "CalculationGroupId", e.RowKeyId );
        }

        protected void gList_Delete( object sender, RowEventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new CalculationGroupService( rockContext );
                var group = service.Get( e.RowKeyId );

                if ( group != null )
                {
                    if ( !group.IsAuthorized( Authorization.EDIT, CurrentPerson ) )
                    {
                        mdGridWarning.Show( "You are not authorized to delete this item.", ModalAlertType.Warning );
                        return;
                    }

                    service.Delete( group );
                    rockContext.SaveChanges();
                }
            }

            BindGrid();
        }

        protected void gList_GridReorder( object sender, GridReorderEventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new CalculationGroupService( rockContext );
                var groups = service.Queryable().OrderBy( g => g.Order ).ThenBy( g => g.Name ).ToList();
                service.Reorder( groups, e.OldIndex, e.NewIndex );
                rockContext.SaveChanges();
            }

            BindGrid();
        }

        protected void gList_GridRebind( object sender, GridRebindEventArgs e )
        {
            BindGrid();
        }

        protected void btnImport_Click( object sender, EventArgs e )
        {
            ceImportJson.Text = string.Empty;
            nbImportWarning.Visible = false;
            mdImport.Show();
        }

        protected void mdImport_SaveClick( object sender, EventArgs e )
        {
            var json = ceImportJson.Text;
            if ( string.IsNullOrWhiteSpace( json ) )
            {
                nbImportWarning.Text = "Please paste a JSON configuration.";
                nbImportWarning.Visible = true;
                return;
            }

            var service = new ImportExportService();
            var result = service.ImportGroup( json );

            if ( result.Errors.Count > 0 )
            {
                nbImportWarning.Text = string.Join( "<br/>", result.Errors );
                nbImportWarning.Visible = true;
                return;
            }

            mdImport.Hide();

            if ( result.Warnings.Count > 0 )
            {
                mdGridWarning.Show(
                    string.Format( "Imported {0} group, {1} sub-group(s), {2} calculation(s) with warnings:<br/>{3}",
                        result.GroupsCreated, result.SubGroupsCreated, result.CalculationsCreated,
                        string.Join( "<br/>", result.Warnings ) ),
                    ModalAlertType.Information );
            }
            else
            {
                mdGridWarning.Show(
                    string.Format( "Successfully imported {0} group, {1} sub-group(s), {2} calculation(s).",
                        result.GroupsCreated, result.SubGroupsCreated, result.CalculationsCreated ),
                    ModalAlertType.Information );
            }

            BindGrid();
        }

        #endregion

        #region Methods

        private void BindGrid()
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new CalculationGroupService( rockContext );
                var query = service.Queryable().AsNoTracking()
                    .Select( g => new
                    {
                        g.Id,
                        g.Name,
                        g.Description,
                        g.IsActive,
                        g.Order,
                        g.LastRunDateTime,
                        SubGroupCount = g.CalculationSubGroups.Count()
                    } )
                    .OrderBy( g => g.Order )
                    .ThenBy( g => g.Name );

                gList.DataSource = query.ToList();
                gList.DataBind();
            }
        }

        #endregion
    }
}
