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
using Rock.Model;
using Rock.Web.Cache;
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
                    sb.Append( RenderStageDrawers( program.Id, progress, rockContext ) );
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

        /// <summary>
        /// Per-stage expandable drawers listing every active calculation in the
        /// Stage and the current value of its target Person Attribute for the
        /// displayed person. Transient calcs (no sink) show "—". Drawers default
        /// to closed except for the current Stage, which opens by default.
        /// </summary>
        private string RenderStageDrawers( int programId, ProgramProgressResult progress, RockContext rockContext )
        {
            if ( progress == null || progress.NotFound || progress.Stages.Count == 0 )
            {
                return string.Empty;
            }

            // Pull every active calc in the program in one round-trip, with PersonAttribute eager-loaded.
            var calcs = new JourneyCalculationService( rockContext ).Queryable().AsNoTracking()
                .Include( c => c.PersonAttribute )
                .Include( c => c.CalculationTypeEntityType )
                .Where( c => c.Stage.JourneyProgramId == programId && c.IsActive )
                .OrderBy( c => c.StageId )
                .ThenBy( c => c.Order )
                .ThenBy( c => c.Name )
                .ToList();

            // Pre-fetch existing AVs for every sink attribute used by these calcs, for this person, in one query.
            var sinkAttrIds = calcs.Where( c => c.PersonAttributeId.HasValue )
                .Select( c => c.PersonAttributeId.Value )
                .Distinct().ToList();
            var avLookup = new Dictionary<int, string>();
            if ( sinkAttrIds.Count > 0 )
            {
                avLookup = new AttributeValueService( rockContext ).Queryable().AsNoTracking()
                    .Where( av => sinkAttrIds.Contains( av.AttributeId )
                        && av.EntityId == Person.Id )
                    .Select( av => new { av.AttributeId, av.Value } )
                    .ToList()
                    .GroupBy( av => av.AttributeId )
                    .ToDictionary( g => g.Key, g => g.First().Value );
            }

            var calcsByStage = calcs.GroupBy( c => c.StageId ).ToDictionary( g => g.Key, g => g.ToList() );

            var sb = new StringBuilder();
            sb.Append( "<div class='journey-stage-drawers' style='margin-top:1rem;'>" );
            foreach ( var stage in progress.Stages )
            {
                if ( !calcsByStage.TryGetValue( stage.StageId, out var stageCalcs ) || stageCalcs.Count == 0 )
                {
                    continue;
                }

                string statusBadge;
                if ( stage.Passed )
                {
                    statusBadge = "<span class='label' style='background:#16a34a;color:#fff;'>Passed</span>";
                }
                else if ( stage.IsCurrent )
                {
                    statusBadge = "<span class='label' style='background:#f59e0b;color:#fff;'>Current</span>";
                }
                else
                {
                    statusBadge = "<span class='label' style='background:#e5e7eb;color:#374151;'>Pending</span>";
                }

                var openAttr = stage.IsCurrent ? " open" : string.Empty;

                sb.AppendFormat(
                    "<details{0} style='margin-bottom:.5rem;border:1px solid #e5e7eb;border-radius:6px;'>" +
                    "<summary style='padding:.6rem .85rem;cursor:pointer;display:flex;justify-content:space-between;align-items:center;background:#f9fafb;border-radius:6px 6px 0 0;'>" +
                    "<span><strong>{1}</strong></span>{2}</summary>" +
                    "<div style='padding:.5rem .85rem;'>" +
                    "<table class='table table-condensed' style='margin-bottom:0;'><thead><tr><th>Calculation</th><th>Type</th><th>Target Attribute</th><th>Current Value</th></tr></thead><tbody>",
                    openAttr,
                    System.Web.HttpUtility.HtmlEncode( stage.StageName ?? string.Empty ),
                    statusBadge );

                foreach ( var calc in stageCalcs )
                {
                    var calcTypeFriendly = SimplifyCalcTypeName( calc.CalculationTypeEntityType?.Name );
                    var targetName = calc.PersonAttribute != null
                        ? calc.PersonAttribute.Name
                        : "<em class='text-muted'>(transient)</em>";

                    string valueCell;
                    if ( calc.PersonAttributeId.HasValue
                        && avLookup.TryGetValue( calc.PersonAttributeId.Value, out var v )
                        && !string.IsNullOrWhiteSpace( v ) )
                    {
                        // Use AttributeCache to reach the FieldType helper (calc.PersonAttribute
                        // is the EF entity, which exposes the FieldType entity but not the IFieldType impl).
                        var attrCache = AttributeCache.Get( calc.PersonAttributeId.Value );
                        valueCell = attrCache != null
                            ? attrCache.FieldType.Field.FormatValueAsHtml( null, attrCache.EntityTypeId, Person.Id, v, attrCache.QualifierValues, false )
                            : System.Web.HttpUtility.HtmlEncode( v );
                    }
                    else if ( calc.PersonAttributeId.HasValue )
                    {
                        valueCell = "<span class='text-muted'>—</span>";
                    }
                    else
                    {
                        valueCell = "<span class='text-muted'>—</span>";
                    }

                    sb.AppendFormat(
                        "<tr><td>{0}</td><td><span class='text-muted small'>{1}</span></td><td>{2}</td><td>{3}</td></tr>",
                        System.Web.HttpUtility.HtmlEncode( calc.Name ?? string.Empty ),
                        System.Web.HttpUtility.HtmlEncode( calcTypeFriendly ),
                        targetName.StartsWith( "<" ) ? targetName : System.Web.HttpUtility.HtmlEncode( targetName ),
                        valueCell );
                }

                sb.Append( "</tbody></table></div></details>" );
            }
            sb.Append( "</div>" );
            return sb.ToString();
        }

        private static string SimplifyCalcTypeName( string fullName )
        {
            if ( string.IsNullOrWhiteSpace( fullName ) ) return string.Empty;
            var lastDot = fullName.LastIndexOf( '.' );
            var shortName = lastDot >= 0 ? fullName.Substring( lastDot + 1 ) : fullName;
            // Strip trailing "Calculation"
            if ( shortName.EndsWith( "Calculation", StringComparison.Ordinal ) )
            {
                shortName = shortName.Substring( 0, shortName.Length - "Calculation".Length );
            }
            // CamelCase → spaced
            return System.Text.RegularExpressions.Regex.Replace( shortName, "(?<=[a-z])([A-Z])", " $1" );
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
