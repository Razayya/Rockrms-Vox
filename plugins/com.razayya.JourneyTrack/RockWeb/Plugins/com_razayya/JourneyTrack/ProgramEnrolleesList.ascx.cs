using System;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Web.UI.WebControls;

using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_razayya.JourneyTrack
{
    [DisplayName( "Program Enrollees" )]
    [Category( "Razayya > JourneyTrack" )]
    [Description( "Shows the people currently enrolled in a Program. Manually add or unenroll." )]

    [LinkedPage( "Program Detail Page",
        Description = "Page to navigate back to for the Program.",
        IsRequired = false,
        Order = 0,
        Key = "ProgramDetailPage" )]

    public partial class ProgramEnrolleesList : RockBlock
    {
        private int ProgramId
        {
            get { return ViewState["ProgramId"] as int? ?? 0; }
            set { ViewState["ProgramId"] = value; }
        }

        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            gEnrollees.GridRebind += gEnrollees_GridRebind;
            gEnrollees.Actions.ShowAdd = false;
        }

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );
            if ( !Page.IsPostBack )
            {
                ProgramId = PageParameter( "JourneyProgramId" ).AsInteger();
                LoadProgramHeader();
                BindGrid();
            }
        }

        private void LoadProgramHeader()
        {
            using ( var rockContext = new RockContext() )
            {
                var program = new JourneyProgramService( rockContext ).Get( ProgramId );
                if ( program == null )
                {
                    nbResult.Text = "Program not found.";
                    nbResult.NotificationBoxType = NotificationBoxType.Warning;
                    nbResult.Visible = true;
                    return;
                }
                lProgramName.Text = " — " + program.Name;
                var activeCount = new JourneyProgramEnrollmentService( rockContext ).Queryable().AsNoTracking()
                    .Count( e => e.JourneyProgramId == ProgramId && e.IsActive );
                hlActiveCount.Text = activeCount.ToString( "N0" ) + " active";
            }
        }

        private void BindGrid()
        {
            using ( var rockContext = new RockContext() )
            {
                var q = new JourneyProgramEnrollmentService( rockContext ).Queryable().AsNoTracking()
                    .Where( e => e.JourneyProgramId == ProgramId );

                var statusFilter = ddlStatus.SelectedValue;
                if ( statusFilter == "active" )      q = q.Where( e => e.IsActive );
                else if ( statusFilter == "inactive" ) q = q.Where( e => !e.IsActive );

                var sourceFilter = tbSourceFilter.Text.Trim();
                if ( !string.IsNullOrEmpty( sourceFilter ) )
                {
                    q = q.Where( e => e.Source != null && e.Source.Contains( sourceFilter ) );
                }

                var rows = q.Select( e => new
                    {
                        e.Id,
                        PersonName = e.PersonAlias.Person.NickName + " " + e.PersonAlias.Person.LastName,
                        e.EnrolledDateTime,
                        e.Source,
                        e.IsActive,
                        e.UnenrolledDateTime
                    } )
                    .OrderByDescending( e => e.EnrolledDateTime )
                    .Take( 10000 )  // safety cap; grid pages within this
                    .ToList();

                var sortProperty = gEnrollees.SortProperty;
                if ( sortProperty != null )
                {
                    rows = sortProperty.Direction == System.Web.UI.WebControls.SortDirection.Ascending
                        ? rows.AsQueryable().OrderBy( sortProperty.Property ).ToList()
                        : rows.AsQueryable().OrderByDescending( sortProperty.Property ).ToList();
                }

                gEnrollees.DataSource = rows;
                gEnrollees.DataBind();
            }
        }

        protected void gEnrollees_GridRebind( object sender, GridRebindEventArgs e )
        {
            BindGrid();
        }

        protected void btnFilter_Click( object sender, EventArgs e )
        {
            BindGrid();
        }

        protected void ddlStatus_SelectedIndexChanged( object sender, EventArgs e )
        {
            BindGrid();
        }

        protected void btnEnroll_Click( object sender, EventArgs e )
        {
            var personId = ppNewEnrollee.PersonId;
            if ( !personId.HasValue )
            {
                nbResult.Text = "Please pick a person.";
                nbResult.NotificationBoxType = NotificationBoxType.Warning;
                nbResult.Visible = true;
                return;
            }

            using ( var rockContext = new RockContext() )
            {
                var aliasId = new PersonAliasService( rockContext ).Queryable()
                    .Where( pa => pa.PersonId == personId.Value && pa.AliasPersonId == personId.Value )
                    .Select( pa => ( int? ) pa.Id ).FirstOrDefault();
                if ( !aliasId.HasValue )
                {
                    nbResult.Text = "Could not resolve the person's primary alias.";
                    nbResult.NotificationBoxType = NotificationBoxType.Danger;
                    nbResult.Visible = true;
                    return;
                }

                var enrollmentService = new JourneyProgramEnrollmentService( rockContext );
                var existing = enrollmentService.Queryable()
                    .Where( en => en.JourneyProgramId == ProgramId && en.PersonAlias.PersonId == personId.Value )
                    .OrderByDescending( en => en.Id )
                    .FirstOrDefault();

                if ( existing == null )
                {
                    enrollmentService.Add( new JourneyProgramEnrollment
                    {
                        JourneyProgramId = ProgramId,
                        PersonAliasId = aliasId.Value,
                        EnrolledDateTime = RockDateTime.Now,
                        IsActive = true,
                        Source = "Manual",
                        EnrolledByPersonAliasId = CurrentPersonAliasId
                    } );
                    nbResult.Text = "Enrolled.";
                    nbResult.NotificationBoxType = NotificationBoxType.Success;
                }
                else if ( !existing.IsActive )
                {
                    existing.IsActive = true;
                    existing.UnenrolledDateTime = null;
                    existing.ModifiedDateTime = RockDateTime.Now;
                    nbResult.Text = "Re-activated existing enrollment.";
                    nbResult.NotificationBoxType = NotificationBoxType.Success;
                }
                else
                {
                    nbResult.Text = "Person is already enrolled.";
                    nbResult.NotificationBoxType = NotificationBoxType.Info;
                }
                rockContext.SaveChanges();
                nbResult.Visible = true;
            }

            ppNewEnrollee.SetValue( null );
            LoadProgramHeader();
            BindGrid();
        }

        protected void btnUnenroll_Click( object sender, EventArgs e )
        {
            var btn = sender as LinkButton;
            if ( btn == null ) return;
            var enrollmentId = btn.CommandArgument.AsInteger();
            if ( enrollmentId <= 0 ) return;

            using ( var rockContext = new RockContext() )
            {
                var row = new JourneyProgramEnrollmentService( rockContext ).Get( enrollmentId );
                if ( row != null && row.IsActive )
                {
                    row.IsActive = false;
                    row.UnenrolledDateTime = RockDateTime.Now;
                    row.ModifiedDateTime = RockDateTime.Now;
                    rockContext.SaveChanges();
                    nbResult.Text = "Unenrolled.";
                    nbResult.NotificationBoxType = NotificationBoxType.Success;
                    nbResult.Visible = true;
                }
            }
            LoadProgramHeader();
            BindGrid();
        }

        protected void btnBack_Click( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "ProgramDetailPage", "JourneyProgramId", ProgramId );
        }
    }
}
