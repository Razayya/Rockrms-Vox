using System;
using System.ComponentModel;
using System.Linq;
using System.Text;

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

    [TextField( "Journey Program Guids",
        Description = "Comma-delimited list of JourneyProgram Guids to render for the displayed person. Each renders as its own progress bar with one segment per Stage.",
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

            using ( var rockContext = new RockContext() )
            {
                var programService = new JourneyProgramService( rockContext );
                foreach ( var guid in programGuids )
                {
                    var program = programService.Get( guid );
                    if ( program == null )
                    {
                        continue;
                    }

                    var progress = service.GetProgramProgressForPerson( program.Id, Person.Id );
                    sb.Append( RenderProgressBar( progress ) );
                    rendered++;
                }
            }

            if ( rendered == 0 )
            {
                nbMessage.NotificationBoxType = Rock.Web.UI.Controls.NotificationBoxType.Info;
                nbMessage.Text = GetAttributeValue( AttributeKey.EmptyMessage );
                nbMessage.Visible = true;
                return;
            }

            lOutput.Text = sb.ToString();
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
