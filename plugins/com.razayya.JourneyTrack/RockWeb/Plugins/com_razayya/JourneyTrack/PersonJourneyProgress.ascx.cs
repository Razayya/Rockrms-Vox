using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Web.UI.WebControls;

using com.razayya.JourneyTrack.Data;
using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Web.UI;

namespace RockWeb.Plugins.com_razayya.JourneyTrack
{
    [DisplayName( "Person Journey Progress" )]
    [Category( "Razayya > JourneyTrack" )]
    [Description( "Shows a person's progress through one or more Journey Programs as a horizontal stage bar. Drop this on a Person profile page." )]

    [CustomDropdownListField( "Journey Program",
        description: "The Journey Program to render progress for on this block. Reads program list from the JourneyTrack database; only active programs appear.",
        listSource: "SELECT CAST([Guid] AS NVARCHAR(50)) AS [Value], [Name] AS [Text] FROM _com_razayya_JourneyTrack_JourneyProgram WHERE IsActive = 1 ORDER BY [Order], [Name]",
        IsRequired = true,
        Order = 0,
        Key = AttributeKey.JourneyProgramGuids )]

    [TextField( "Empty Message",
        Description = "Text shown when the person has no progress in any configured Journey Program.",
        IsRequired = false,
        DefaultValue = "No journey progress yet.",
        Order = 1,
        Key = AttributeKey.EmptyMessage )]

    public partial class PersonJourneyProgress : PersonBlock
    {
        private static class AttributeKey
        {
            public const string JourneyProgramGuids = "JourneyProgramGuids";
            public const string EmptyMessage = "EmptyMessage";
        }

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( Page.IsPostBack )
            {
                return;
            }

            Render();
        }

        private void Render()
        {
            // Reset
            nbMessage.Visible = false;
            lOutput.Text = string.Empty;
            rEnrollPrompts.DataSource = null;
            rEnrollPrompts.DataBind();

            if ( Person == null )
            {
                nbMessage.NotificationBoxType = Rock.Web.UI.Controls.NotificationBoxType.Info;
                nbMessage.Text = "No person context.";
                nbMessage.Visible = true;
                return;
            }

            var raw = GetAttributeValue( AttributeKey.JourneyProgramGuids );
            var programGuids = ( raw ?? string.Empty )
                .Split( new[] { ',', ';', '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries )
                .Select( s => s.AsGuidOrNull() )
                .Where( g => g.HasValue )
                .Select( g => g.Value )
                .ToList();

            if ( programGuids.Count == 0 )
            {
                nbMessage.NotificationBoxType = Rock.Web.UI.Controls.NotificationBoxType.Warning;
                nbMessage.Text = "Block is not configured with any Journey Program Guids.";
                nbMessage.Visible = true;
                return;
            }

            var service = new JourneyTrackService();
            var sb = new StringBuilder();
            int rendered = 0;
            var enrollPrompts = new List<EnrollPrompt>();

            using ( var rockContext = new RockContext() )
            {
                var programService = new JourneyProgramService( rockContext );
                var enrollmentService = new JourneyProgramEnrollmentService( rockContext );

                foreach ( var guid in programGuids )
                {
                    var program = programService.Get( guid );
                    if ( program == null )
                    {
                        continue;
                    }

                    // Programs that require enrollment surface an enroll card when the
                    // displayed person isn't enrolled. Programs without RequiresEnrollment
                    // always render the progress bar (legacy "everyone is in scope" mode).
                    if ( program.RequiresEnrollment )
                    {
                        var isEnrolled = enrollmentService.Queryable().AsNoTracking()
                            .Any( e => e.JourneyProgramId == program.Id
                                && e.IsActive
                                && e.PersonAlias.PersonId == Person.Id );
                        if ( !isEnrolled )
                        {
                            enrollPrompts.Add( new EnrollPrompt { ProgramId = program.Id, ProgramName = program.Name } );
                            continue;
                        }
                    }

                    var progress = service.GetProgramProgressForPerson( program.Id, Person.Id );
                    sb.Append( RenderProgressBar( progress ) );
                    rendered++;
                }
            }

            lOutput.Text = sb.ToString();

            if ( enrollPrompts.Count > 0 )
            {
                rEnrollPrompts.DataSource = enrollPrompts;
                rEnrollPrompts.DataBind();
            }
            else if ( rendered == 0 )
            {
                // No programs to render at all + no enroll prompts → static empty message.
                nbMessage.NotificationBoxType = Rock.Web.UI.Controls.NotificationBoxType.Info;
                nbMessage.Text = GetAttributeValue( AttributeKey.EmptyMessage );
                nbMessage.Visible = true;
            }
        }

        protected void rEnrollPrompts_ItemCommand( object source, RepeaterCommandEventArgs e )
        {
            if ( e.CommandName != "Enroll" ) return;
            var programId = e.CommandArgument.ToString().AsInteger();
            if ( programId <= 0 || Person == null ) return;

            using ( var rockContext = new RockContext() )
            {
                var aliasId = new Rock.Model.PersonAliasService( rockContext ).Queryable()
                    .Where( pa => pa.PersonId == Person.Id && pa.AliasPersonId == Person.Id )
                    .Select( pa => ( int? ) pa.Id ).FirstOrDefault();
                if ( !aliasId.HasValue )
                {
                    nbMessage.NotificationBoxType = Rock.Web.UI.Controls.NotificationBoxType.Danger;
                    nbMessage.Text = "Could not resolve a primary PersonAlias for the displayed person.";
                    nbMessage.Visible = true;
                    return;
                }

                var enrollmentService = new JourneyProgramEnrollmentService( rockContext );
                var existing = enrollmentService.Queryable()
                    .Where( en => en.JourneyProgramId == programId && en.PersonAlias.PersonId == Person.Id )
                    .OrderByDescending( en => en.Id )
                    .FirstOrDefault();

                if ( existing == null )
                {
                    enrollmentService.Add( new JourneyProgramEnrollment
                    {
                        JourneyProgramId = programId,
                        PersonAliasId = aliasId.Value,
                        EnrolledDateTime = RockDateTime.Now,
                        IsActive = true,
                        Source = "ProfileBlock",
                        EnrolledByPersonAliasId = CurrentPersonAliasId
                    } );
                }
                else if ( !existing.IsActive )
                {
                    existing.IsActive = true;
                    existing.UnenrolledDateTime = null;
                    existing.ModifiedDateTime = RockDateTime.Now;
                }
                rockContext.SaveChanges();
            }

            Render();
        }

        // Lightweight bind row for the enroll-prompt repeater.
        private class EnrollPrompt
        {
            public int ProgramId { get; set; }
            public string ProgramName { get; set; }
        }

        private string RenderProgressBar( ProgramProgressResult progress )
        {
            if ( progress == null || progress.NotFound )
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append( "<div class='journey-progress' style='margin-bottom:1.25rem;'>" );
            sb.AppendFormat(
                "<div class='journey-progress-header' style='display:flex;justify-content:space-between;align-items:baseline;margin-bottom:.4rem;'>" +
                "<strong>{0}</strong>" +
                "<span class='text-muted' style='font-size:.85em;'>{1}</span></div>",
                System.Web.HttpUtility.HtmlEncode( progress.ProgramName ?? string.Empty ),
                progress.AllPassed ? "Completed" : string.Format( "{0}/{1} stages",
                    progress.Stages.Count( s => s.Passed ),
                    progress.Stages.Count ) );

            sb.Append( "<div class='journey-progress-bar' style='display:flex;gap:.25rem;'>" );
            foreach ( var stage in progress.Stages )
            {
                string bg, fg, border, label;
                if ( stage.Passed )
                {
                    bg = "#16a34a"; fg = "#ffffff"; border = "#16a34a"; label = "&#10003;";
                }
                else if ( stage.IsCurrent )
                {
                    bg = "#fef3c7"; fg = "#92400e"; border = "#f59e0b"; label = "&#9679;";
                }
                else
                {
                    bg = "#f3f4f6"; fg = "#9ca3af"; border = "#d1d5db"; label = "&#9675;";
                }

                sb.AppendFormat(
                    "<div title='{0}' style='flex:1;padding:.45rem .5rem;text-align:center;border:1px solid {1};border-radius:4px;background:{2};color:{3};font-size:.8em;line-height:1.1;'>" +
                    "<div style='font-size:1.1em;'>{4}</div>" +
                    "<div style='margin-top:.15rem;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;'>{5}</div>" +
                    "</div>",
                    System.Web.HttpUtility.HtmlEncode( stage.StageName ?? string.Empty ),
                    border, bg, fg, label,
                    System.Web.HttpUtility.HtmlEncode( stage.StageName ?? string.Empty ) );
            }
            sb.Append( "</div></div>" );
            return sb.ToString();
        }
    }
}
