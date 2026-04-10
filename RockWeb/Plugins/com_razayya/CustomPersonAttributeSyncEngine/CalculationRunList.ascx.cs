using System;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.CustomPersonAttributeSyncEngine.Model;

using Rock;
using Rock.Data;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_razayya.CustomPersonAttributeSyncEngine
{
    [DisplayName( "Calculation Run List" )]
    [Category( "Razayya > Attribute Sync Engine" )]
    [Description( "Displays the run history for attribute sync calculations." )]

    public partial class CalculationRunList : RockBlock
    {
        #region Base Control Methods

        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            gRunHistory.DataKeyNames = new string[] { "Id" };
            gRunHistory.GridRebind += gRunHistory_GridRebind;
            gRunHistory.Actions.ShowAdd = false;
        }

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                PopulateCalculationFilter();
                BindGrid();
            }
        }

        #endregion

        #region Events

        protected void ddlCalculation_SelectedIndexChanged( object sender, EventArgs e )
        {
            BindGrid();
        }

        protected void btnFilter_Click( object sender, EventArgs e )
        {
            BindGrid();
        }

        protected void gRunHistory_GridRebind( object sender, GridRebindEventArgs e )
        {
            BindGrid();
        }

        #endregion

        #region Methods

        private void PopulateCalculationFilter()
        {
            using ( var rockContext = new RockContext() )
            {
                var calculations = new CalculationService( rockContext ).Queryable().AsNoTracking()
                    .OrderBy( c => c.CalculationSubGroup.CalculationGroup.Name )
                    .ThenBy( c => c.CalculationSubGroup.Name )
                    .ThenBy( c => c.Name )
                    .Select( c => new
                    {
                        c.Id,
                        DisplayName = c.CalculationSubGroup.CalculationGroup.Name + " > " + c.CalculationSubGroup.Name + " > " + c.Name
                    } )
                    .ToList();

                ddlCalculation.Items.Clear();
                ddlCalculation.Items.Add( new System.Web.UI.WebControls.ListItem( "All", "" ) );
                foreach ( var calc in calculations )
                {
                    ddlCalculation.Items.Add( new System.Web.UI.WebControls.ListItem( calc.DisplayName, calc.Id.ToString() ) );
                }
            }
        }

        private void BindGrid()
        {
            using ( var rockContext = new RockContext() )
            {
                var query = new CalculationRunService( rockContext ).Queryable().AsNoTracking()
                    .Include( r => r.Calculation )
                    .Include( r => r.RunByPersonAlias.Person );

                // Filter by calculation
                var selectedCalcId = ddlCalculation.SelectedValue.AsIntegerOrNull();
                if ( selectedCalcId.HasValue )
                {
                    query = query.Where( r => r.CalculationId == selectedCalcId.Value );
                }

                // Filter by date range
                var dateRange = SlidingDateRangePicker.CalculateDateRangeFromDelimitedValues( drpDateRange.DelimitedValues );
                if ( dateRange.Start.HasValue )
                {
                    query = query.Where( r => r.RunDateTime >= dateRange.Start.Value );
                }
                if ( dateRange.End.HasValue )
                {
                    query = query.Where( r => r.RunDateTime < dateRange.End.Value );
                }

                // Filter by status
                var statusFilter = ddlStatus.SelectedValue.AsBooleanOrNull();
                if ( statusFilter.HasValue )
                {
                    query = query.Where( r => r.WasSuccessful == statusFilter.Value );
                }

                var gridData = query
                    .OrderByDescending( r => r.RunDateTime )
                    .Select( r => new
                    {
                        r.Id,
                        CalculationName = r.Calculation.Name,
                        r.RunDateTime,
                        r.CompletedDateTime,
                        RunByPersonName = r.RunByPersonAlias != null
                            ? r.RunByPersonAlias.Person.NickName + " " + r.RunByPersonAlias.Person.LastName
                            : "Job",
                        r.PopulationCount,
                        r.MatchedCount,
                        r.UpdatedCount,
                        r.SkippedCount,
                        r.ErrorCount,
                        r.WasSuccessful
                    } )
                    .ToList();

                gRunHistory.DataSource = gridData;
                gRunHistory.DataBind();
            }
        }

        #endregion
    }
}
