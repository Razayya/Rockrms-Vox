using System;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Web.UI.WebControls;

using com.razayya.JourneyTrack.Data;
using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Data;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_razayya.JourneyTrack
{
    [DisplayName( "JourneyCalculation Run List" )]
    [Category( "Razayya > JourneyTrack" )]
    [Description( "Displays the run history for attribute sync calculations." )]

    public partial class JourneyCalculationRunList : RockBlock
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

        protected void btnRetry_Click( object sender, EventArgs e )
        {
            var btn = sender as LinkButton;
            int runId = btn.CommandArgument.AsInteger();

            if ( runId == 0 )
            {
                return;
            }

            int calculationId;
            using ( var rockContext = new RockContext() )
            {
                var run = new JourneyCalculationRunService( rockContext ).Get( runId );
                if ( run == null )
                {
                    nbResult.NotificationBoxType = NotificationBoxType.Warning;
                    nbResult.Text = "Run record not found.";
                    nbResult.Visible = true;
                    return;
                }

                calculationId = run.JourneyCalculationId;
            }

            var service = new JourneyTrackService { RunByPersonAliasId = CurrentPersonAliasId };
            var result = service.ProcessCalculation( calculationId );

            if ( result.Errors.Any() )
            {
                nbResult.NotificationBoxType = NotificationBoxType.Warning;
                nbResult.Text = string.Format(
                    "Retry completed with errors. {0} updated, {1} skipped, {2} error(s): {3}",
                    result.Updated, result.Skipped, result.Errors.Count, string.Join( "; ", result.Errors ) );
            }
            else
            {
                nbResult.NotificationBoxType = NotificationBoxType.Success;
                nbResult.Text = string.Format( "Retry completed. {0} updated, {1} skipped.", result.Updated, result.Skipped );
            }

            nbResult.Visible = true;
            BindGrid();
        }

        #endregion

        #region Methods

        private void PopulateCalculationFilter()
        {
            using ( var rockContext = new RockContext() )
            {
                var calculations = new JourneyCalculationService( rockContext ).Queryable().AsNoTracking()
                    .OrderBy( c => c.Stage.JourneyProgram.Name )
                    .ThenBy( c => c.Stage.Name )
                    .ThenBy( c => c.Name )
                    .Select( c => new
                    {
                        c.Id,
                        DisplayName = c.Stage.JourneyProgram.Name + " > " + c.Stage.Name + " > " + c.Name
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
                var query = new JourneyCalculationRunService( rockContext ).Queryable().AsNoTracking()
                    .Include( r => r.JourneyCalculation )
                    .Include( r => r.RunByPersonAlias.Person );

                // Filter by JourneyCalculation
                var selectedCalcId = ddlCalculation.SelectedValue.AsIntegerOrNull();
                if ( selectedCalcId.HasValue )
                {
                    query = query.Where( r => r.JourneyCalculationId == selectedCalcId.Value );
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
                        CalculationName = r.JourneyCalculation.Name,
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
