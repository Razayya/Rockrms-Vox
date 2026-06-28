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
            sb.Append( JourneyCss() );
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
            // Pull both the raw Value (for the presence test) and the PersistedTextValue (the
            // field-type-formatted text Rock stores) so the cell can display generically by
            // presence, not by field type — sidesteps the Date-attr-holding-"True" render gap.
            var avValue = new Dictionary<int, string>();
            var avPersisted = new Dictionary<int, string>();
            if ( sinkAttrIds.Count > 0 )
            {
                var avRows = new AttributeValueService( rockContext ).Queryable().AsNoTracking()
                    .Where( av => sinkAttrIds.Contains( av.AttributeId ) && av.EntityId == Person.Id )
                    .Select( av => new { av.AttributeId, av.Value, av.PersistedTextValue } )
                    .ToList();
                foreach ( var grp in avRows.GroupBy( av => av.AttributeId ) )
                {
                    var first = grp.First();
                    avValue[grp.Key] = first.Value;
                    avPersisted[grp.Key] = first.PersistedTextValue;
                }
            }

            var calcsByStage = calcs.GroupBy( c => c.StageId ).ToDictionary( g => g.Key, g => g.ToList() );

            var sb = new StringBuilder();
            sb.Append( "<div class='jp-drawers'>" );
            foreach ( var stage in progress.Stages )
            {
                if ( !calcsByStage.TryGetValue( stage.StageId, out var stageCalcs ) || stageCalcs.Count == 0 )
                {
                    continue;
                }

                string badgeClass, badgeText;
                if ( stage.Passed ) { badgeClass = "passed"; badgeText = "Passed"; }
                else if ( stage.IsCurrent ) { badgeClass = "current"; badgeText = "Current"; }
                else { badgeClass = "pending"; badgeText = "Pending"; }

                var openAttr = stage.IsCurrent ? " open" : string.Empty;

                sb.AppendFormat(
                    "<details class='jp-drawer'{0}>" +
                    "<summary><span class='jp-drawer-name'>{1}</span><span class='jp-badge {2}'>{3}</span></summary>" +
                    "<div class='jp-drawer-body'>" +
                    "<table class='jp-table'><thead><tr><th>Calculation</th><th>Type</th><th>Target Attribute</th><th class='jp-th-val'>Current Value</th></tr></thead><tbody>",
                    openAttr,
                    System.Web.HttpUtility.HtmlEncode( stage.StageName ?? string.Empty ),
                    badgeClass, badgeText );

                foreach ( var calc in stageCalcs )
                {
                    var calcTypeFriendly = SimplifyCalcTypeName( calc.CalculationTypeEntityType?.Name );
                    var targetCell = calc.PersonAttributeId.HasValue
                        ? "<span class='jp-target'>" + System.Web.HttpUtility.HtmlEncode( calc.PersonAttribute?.Name ?? string.Empty ) + "</span>"
                        : "<span class='jp-target transient'>(transient)</span>";

                    // Display the value of the attribute being written to: blank stays blank; a present
                    // value (incl. "True"/"False") shows the attribute's PersistedTextValue — generic on
                    // presence, not field type (falls back to the raw value if no persisted text yet).
                    string valueCell = string.Empty;
                    if ( calc.PersonAttributeId.HasValue
                        && avValue.TryGetValue( calc.PersonAttributeId.Value, out var v )
                        && !string.IsNullOrWhiteSpace( v ) )
                    {
                        avPersisted.TryGetValue( calc.PersonAttributeId.Value, out var pt );
                        var text = !string.IsNullOrWhiteSpace( pt ) ? pt : v;
                        valueCell = "<span class='jp-val'>" + System.Web.HttpUtility.HtmlEncode( text ) + "</span>";
                    }

                    sb.AppendFormat(
                        "<tr><td class='jp-calc'>{0}</td><td><span class='jp-type'>{1}</span></td><td>{2}</td><td class='jp-td-val'>{3}</td></tr>",
                        System.Web.HttpUtility.HtmlEncode( calc.Name ?? string.Empty ),
                        System.Web.HttpUtility.HtmlEncode( calcTypeFriendly ),
                        targetCell,
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

            var passedCount = progress.Stages.Count( s => s.Passed );
            var sb = new StringBuilder();
            sb.Append( "<div class='jp-progress'>" );
            sb.AppendFormat(
                "<div class='jp-progress-head'><span class='jp-prog-name'>{0}</span>" +
                "<span class='jp-prog-status'>{1}</span></div>",
                System.Web.HttpUtility.HtmlEncode( progress.ProgramName ?? string.Empty ),
                progress.AllPassed ? "Completed" : string.Format( "{0} of {1} stages", passedCount, progress.Stages.Count ) );

            sb.Append( "<div class='jp-bar'>" );
            foreach ( var stage in progress.Stages )
            {
                string cls, label;
                if ( stage.Passed ) { cls = "passed"; label = "&#10003;"; }
                else if ( stage.IsCurrent ) { cls = "current"; label = "&#9679;"; }
                else { cls = "pending"; label = "&#9675;"; }

                sb.AppendFormat(
                    "<div class='jp-seg {0}' title='{1}'><span class='jp-seg-icon'>{2}</span><span class='jp-seg-name'>{3}</span></div>",
                    cls,
                    System.Web.HttpUtility.HtmlEncode( stage.StageName ?? string.Empty ),
                    label,
                    System.Web.HttpUtility.HtmlEncode( stage.StageName ?? string.Empty ) );
            }
            sb.Append( "</div></div>" );
            return sb.ToString();
        }

        // Scoped styles for the progress bar + stage drawers (emitted once per render).
        private static string JourneyCss()
        {
            return @"<style>
.jp-progress{margin:0 0 1.25rem;}
.jp-progress-head{display:flex;justify-content:space-between;align-items:baseline;margin-bottom:.5rem;gap:.5rem;}
.jp-prog-name{font-weight:700;font-size:1.05rem;}
.jp-prog-status{font-size:.78rem;color:#6b7280;font-weight:600;white-space:nowrap;}
.jp-bar{display:flex;gap:.3rem;}
.jp-seg{flex:1;min-width:0;text-align:center;padding:.45rem .35rem;border-radius:6px;border:1px solid #d1d5db;background:#f3f4f6;color:#9ca3af;}
.jp-seg.passed{background:#ecfdf5;border-color:#16a34a;color:#166534;}
.jp-seg.current{background:#fffbeb;border-color:#f59e0b;color:#92400e;}
.jp-seg-icon{display:block;font-size:1.05rem;line-height:1;}
.jp-seg-name{display:block;margin-top:.2rem;font-size:.72rem;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
.jp-drawers{margin-top:1rem;}
.jp-drawer{border:1px solid #e5e7eb;border-radius:8px;margin-bottom:.55rem;overflow:hidden;background:#fff;}
.jp-drawer>summary{padding:.65rem 1rem;cursor:pointer;display:flex;justify-content:space-between;align-items:center;gap:.5rem;background:#f9fafb;}
.jp-drawer[open]>summary{border-bottom:1px solid #e5e7eb;}
.jp-drawer-name{font-weight:600;}
.jp-badge{font-size:.68rem;font-weight:700;padding:.15rem .55rem;border-radius:999px;text-transform:uppercase;letter-spacing:.02em;white-space:nowrap;}
.jp-badge.passed{background:#dcfce7;color:#166534;}
.jp-badge.current{background:#fef3c7;color:#92400e;}
.jp-badge.pending{background:#eef2f7;color:#6b7280;}
.jp-drawer-body{padding:.25rem .5rem .5rem;}
.jp-table{width:100%;border-collapse:collapse;font-size:.85rem;}
.jp-table th{text-align:left;padding:.45rem .65rem;border-bottom:2px solid #eef2f7;color:#9ca3af;font-weight:700;text-transform:uppercase;font-size:.66rem;letter-spacing:.03em;}
.jp-table td{padding:.45rem .65rem;border-bottom:1px solid #f3f4f6;vertical-align:top;}
.jp-table tbody tr:last-child td{border-bottom:none;}
.jp-table tbody tr:hover{background:#f9fafb;}
.jp-th-val,.jp-td-val{text-align:right;white-space:nowrap;}
.jp-calc{font-weight:600;color:#1f2937;}
.jp-type{color:#9ca3af;font-size:.78rem;}
.jp-target{color:#374151;}
.jp-target.transient{color:#9ca3af;font-style:italic;}
.jp-val{font-weight:700;color:#111827;}
</style>";
        }
    }
}
