using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;

using com.razayya.JourneyTrack.CalculationTypes;
using com.razayya.JourneyTrack.Data;
using com.razayya.JourneyTrack.Logic;
using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web.Cache;
using Rock.Web.UI;

using ComparisonType = com.razayya.JourneyTrack.Model.ComparisonType;

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

    [SecurityAction( SecurityActionKey.ManageSkips, "The roles and/or users that can manually skip or restore Pathway steps for the displayed person." )]

    public partial class PersonJourneyProgress : PersonBlock, IPostBackEventHandler
    {
        private static class AttributeKey
        {
            public const string JourneyProgramGuids = "JourneyProgramGuids";
            public const string EmptyMessage = "EmptyMessage";
        }

        public static class SecurityActionKey
        {
            public const string ManageSkips = "ManageSkips";
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

        #region Manual skip actions (7038)

        /// <summary>
        /// Routes the drawer's Skip / Restore / Skip-remaining links (rendered as
        /// __doPostBack hyperlinks inside the literal) to their handlers. Arguments:
        /// "skip:&lt;calcId&gt;", "skipstage:&lt;stageId&gt;", "restore:&lt;calcId&gt;".
        /// </summary>
        public void RaisePostBackEvent( string eventArgument )
        {
            if ( Person == null || !IsUserAuthorized( SecurityActionKey.ManageSkips ) )
            {
                return;
            }

            var parts = ( eventArgument ?? string.Empty ).Split( new[] { ':' }, 2 );
            if ( parts.Length != 2 )
            {
                return;
            }

            var targetId = parts[1].AsInteger();
            if ( targetId <= 0 )
            {
                return;
            }

            using ( var rockContext = new RockContext() )
            {
                if ( parts[0] == "skip" )
                {
                    var calc = new JourneyCalculationService( rockContext ).Get( targetId );
                    if ( calc == null )
                    {
                        return;
                    }
                    ViewState["PendingSkipCalcId"] = calc.Id;
                    ViewState["PendingSkipStageId"] = null;
                    lSkipPrompt.Text = string.Format(
                        "<p>Skip <strong>{0}</strong> for {1}? The step will count as passed without being completed, and can be restored later.</p>",
                        System.Web.HttpUtility.HtmlEncode( calc.Name ?? string.Empty ),
                        System.Web.HttpUtility.HtmlEncode( Person.NickName ?? string.Empty ) );
                    tbSkipNote.Text = string.Empty;
                    mdSkip.Show();
                }
                else if ( parts[0] == "skipstage" )
                {
                    var stage = new StageService( rockContext ).Get( targetId );
                    if ( stage == null )
                    {
                        return;
                    }
                    ViewState["PendingSkipStageId"] = stage.Id;
                    ViewState["PendingSkipCalcId"] = null;
                    lSkipPrompt.Text = string.Format(
                        "<p>Skip all remaining steps in <strong>{0}</strong> for {1}? Each incomplete step will count as passed without being completed, and can be restored individually later.</p>",
                        System.Web.HttpUtility.HtmlEncode( stage.Name ?? string.Empty ),
                        System.Web.HttpUtility.HtmlEncode( Person.NickName ?? string.Empty ) );
                    tbSkipNote.Text = string.Empty;
                    mdSkip.Show();
                }
                else if ( parts[0] == "restore" )
                {
                    RestoreSkips( targetId, rockContext );
                }
            }
        }

        /// <summary>
        /// Creates the pending skip row(s) from the modal. Single-calc skips create one
        /// row; stage skips create a row per active calc that isn't already skipped and
        /// whose sink is still blank (a present sink value means the step is effectively
        /// complete). Then re-syncs the stage so the drawer + persisted stage status
        /// reflect the change immediately.
        /// </summary>
        protected void mdSkip_SaveClick( object sender, EventArgs e )
        {
            var pendingCalcId = ViewState["PendingSkipCalcId"] as int?;
            var pendingStageId = ViewState["PendingSkipStageId"] as int?;
            ViewState["PendingSkipCalcId"] = null;
            ViewState["PendingSkipStageId"] = null;
            mdSkip.Hide();

            if ( Person == null || !IsUserAuthorized( SecurityActionKey.ManageSkips )
                || ( !pendingCalcId.HasValue && !pendingStageId.HasValue ) )
            {
                Render();
                return;
            }

            int? syncStageId = null;
            using ( var rockContext = new RockContext() )
            {
                var aliasId = new PersonAliasService( rockContext ).Queryable()
                    .Where( pa => pa.PersonId == Person.Id && pa.AliasPersonId == Person.Id )
                    .Select( pa => ( int? ) pa.Id ).FirstOrDefault();
                if ( !aliasId.HasValue )
                {
                    nbMessage.NotificationBoxType = Rock.Web.UI.Controls.NotificationBoxType.Danger;
                    nbMessage.Text = "Could not resolve a primary PersonAlias for the displayed person.";
                    nbMessage.Visible = true;
                    return;
                }

                var skipService = new JourneyCalculationSkipService( rockContext );
                var calcService = new JourneyCalculationService( rockContext );
                var note = string.IsNullOrWhiteSpace( tbSkipNote.Text ) ? null : tbSkipNote.Text.Trim();

                if ( pendingCalcId.HasValue )
                {
                    var calc = calcService.Get( pendingCalcId.Value );
                    if ( calc == null )
                    {
                        Render();
                        return;
                    }
                    syncStageId = calc.StageId;

                    bool alreadySkipped = skipService.Queryable().AsNoTracking()
                        .Any( s => s.JourneyCalculationId == calc.Id && s.IsActive && s.PersonAlias.PersonId == Person.Id );
                    if ( !alreadySkipped )
                    {
                        skipService.Add( new JourneyCalculationSkip
                        {
                            JourneyCalculationId = calc.Id,
                            PersonAliasId = aliasId.Value,
                            IsActive = true,
                            Note = note
                        } );
                    }
                }
                else
                {
                    syncStageId = pendingStageId.Value;
                    var stageCalcs = calcService.Queryable().AsNoTracking()
                        .Where( c => c.StageId == pendingStageId.Value && c.IsActive )
                        .Select( c => new { c.Id, c.PersonAttributeId } )
                        .ToList();
                    var stageCalcIds = stageCalcs.Select( c => c.Id ).ToList();

                    var alreadySkippedIds = new HashSet<int>( skipService.Queryable().AsNoTracking()
                        .Where( s => s.IsActive && stageCalcIds.Contains( s.JourneyCalculationId ) && s.PersonAlias.PersonId == Person.Id )
                        .Select( s => s.JourneyCalculationId )
                        .ToList() );

                    var sinkAttrIds = stageCalcs.Where( c => c.PersonAttributeId.HasValue )
                        .Select( c => c.PersonAttributeId.Value ).Distinct().ToList();
                    var presentAttrIds = new HashSet<int>();
                    if ( sinkAttrIds.Count > 0 )
                    {
                        presentAttrIds = new HashSet<int>( new AttributeValueService( rockContext ).Queryable().AsNoTracking()
                            .Where( av => sinkAttrIds.Contains( av.AttributeId ) && av.EntityId == Person.Id && !string.IsNullOrEmpty( av.Value ) )
                            .Select( av => av.AttributeId )
                            .ToList() );
                    }

                    foreach ( var calc in stageCalcs )
                    {
                        if ( alreadySkippedIds.Contains( calc.Id ) )
                        {
                            continue;
                        }
                        if ( calc.PersonAttributeId.HasValue && presentAttrIds.Contains( calc.PersonAttributeId.Value ) )
                        {
                            continue;
                        }
                        skipService.Add( new JourneyCalculationSkip
                        {
                            JourneyCalculationId = calc.Id,
                            PersonAliasId = aliasId.Value,
                            IsActive = true,
                            Note = note
                        } );
                    }
                }

                rockContext.SaveChanges();
            }

            if ( syncStageId.HasValue )
            {
                new JourneyTrackService().ProcessStageForPerson( syncStageId.Value, Person.Id );
            }

            Render();
        }

        /// <summary>
        /// Deactivates the person's active skip row(s) for the calc (audit preserved via
        /// RemovedDateTime / RemovedByPersonAliasId), then re-syncs the stage so the pass
        /// state regresses immediately if the skip was load-bearing.
        /// </summary>
        private void RestoreSkips( int calcId, RockContext rockContext )
        {
            var skipService = new JourneyCalculationSkipService( rockContext );
            var rows = skipService.Queryable()
                .Where( s => s.JourneyCalculationId == calcId && s.IsActive && s.PersonAlias.PersonId == Person.Id )
                .ToList();

            var stageId = new JourneyCalculationService( rockContext ).Queryable().AsNoTracking()
                .Where( c => c.Id == calcId )
                .Select( c => ( int? ) c.StageId )
                .FirstOrDefault();

            if ( rows.Count > 0 )
            {
                foreach ( var row in rows )
                {
                    row.IsActive = false;
                    row.RemovedDateTime = RockDateTime.Now;
                    row.RemovedByPersonAliasId = CurrentPersonAliasId;
                }
                rockContext.SaveChanges();

                if ( stageId.HasValue )
                {
                    new JourneyTrackService().ProcessStageForPerson( stageId.Value, Person.Id );
                }
            }

            Render();
        }

        // Active manual-skip display info for the displayed person, keyed by calc Id.
        private class SkipInfo
        {
            public string ByName { get; set; }
            public DateTime? CreatedDateTime { get; set; }
            public string Note { get; set; }
        }

        #endregion

        #region Skip rule description (Auto-Skipped tooltip)

        /// <summary>
        /// Renders a Skip If condition tree as a short human-readable phrase for the
        /// Auto-Skipped chip tooltip, e.g. "Connection Status is Member". Returns null
        /// on empty/unparseable JSON — callers fall back to generic wording.
        /// </summary>
        private string DescribeSkipRule( string skipFilterJson, bool matchAll, RockContext rockContext )
        {
            try
            {
                var tree = LogicTree.Parse<FilterCondition>( skipFilterJson, matchAll ? LogicGroupType.All : LogicGroupType.Any );
                if ( LogicTree.IsEmpty( tree ) )
                {
                    return null;
                }

                var text = DescribeRuleNode( tree, rockContext );
                if ( string.IsNullOrWhiteSpace( text ) )
                {
                    return null;
                }
                // The generator fully wraps a multi-part root — strip that outermost pair.
                if ( text.StartsWith( "(" ) && text.EndsWith( ")" ) )
                {
                    text = text.Substring( 1, text.Length - 2 );
                }
                return text;
            }
            catch
            {
                return null;
            }
        }

        private string DescribeRuleNode( LogicNode<FilterCondition> node, RockContext rockContext )
        {
            if ( node == null )
            {
                return string.Empty;
            }
            if ( node.Leaf != null )
            {
                return DescribeRuleLeaf( node.Leaf, rockContext );
            }

            var parts = ( node.Children ?? new List<LogicNode<FilterCondition>>() )
                .Select( c => DescribeRuleNode( c, rockContext ) )
                .Where( s => !string.IsNullOrWhiteSpace( s ) )
                .ToList();
            if ( parts.Count == 0 )
            {
                return string.Empty;
            }

            switch ( node.Type ?? LogicGroupType.All )
            {
                case LogicGroupType.Any:
                    return parts.Count == 1 ? parts[0] : "(" + string.Join( " OR ", parts ) + ")";
                case LogicGroupType.AllFalse:
                    return "NONE OF (" + string.Join( "; ", parts ) + ")";
                case LogicGroupType.AnyFalse:
                    return "NOT ALL OF (" + string.Join( "; ", parts ) + ")";
                default:
                    return parts.Count == 1 ? parts[0] : "(" + string.Join( " AND ", parts ) + ")";
            }
        }

        private string DescribeRuleLeaf( FilterCondition condition, RockContext rockContext )
        {
            if ( condition == null || string.IsNullOrWhiteSpace( condition.Key ) )
            {
                return string.Empty;
            }

            string label;
            if ( condition.Source == FilterSource.Attribute )
            {
                var personEntityTypeId = EntityTypeCache.Get( typeof( Person ) ).Id;
                label = new AttributeService( rockContext ).GetByEntityTypeId( personEntityTypeId )
                    .Where( a => a.Key == condition.Key )
                    .Select( a => a.Name )
                    .FirstOrDefault() ?? SpaceCamelCase( condition.Key );
            }
            else
            {
                // Person property: "ConnectionStatusValueId" reads as "Connection Status".
                var key = condition.Key;
                if ( key.EndsWith( "ValueId", StringComparison.OrdinalIgnoreCase ) )
                {
                    key = key.Substring( 0, key.Length - "ValueId".Length );
                }
                else if ( key.Length > 2 && key.EndsWith( "Id", StringComparison.Ordinal ) )
                {
                    key = key.Substring( 0, key.Length - 2 );
                }
                label = SpaceCamelCase( key );
            }

            switch ( condition.Comparison )
            {
                case ComparisonType.IsNotBlank:
                    return label + " has a value";
                case ComparisonType.IsBlank:
                    return label + " is blank";
            }

            string op;
            switch ( condition.Comparison )
            {
                case ComparisonType.EqualTo: op = "is"; break;
                case ComparisonType.NotEqualTo: op = "is not"; break;
                case ComparisonType.GreaterThan: op = "is greater than"; break;
                case ComparisonType.LessThan: op = "is less than"; break;
                case ComparisonType.GreaterThanOrEqualTo: op = "is at least"; break;
                case ComparisonType.LessThanOrEqualTo: op = "is at most"; break;
                case ComparisonType.Contains: op = "contains"; break;
                default: op = "matches"; break;
            }

            return label + " " + op + " " + ResolveRuleValue( condition );
        }

        /// <summary>
        /// Resolves a rule condition's stored value to display text: Id-bearing person
        /// properties resolve through the caches (Defined Value / Campus), attribute
        /// values holding a Defined Value guid resolve to its text, everything else raw.
        /// </summary>
        private static string ResolveRuleValue( FilterCondition condition )
        {
            var raw = condition.Value ?? string.Empty;

            if ( condition.Source == FilterSource.Property )
            {
                if ( condition.Key.EndsWith( "ValueId", StringComparison.OrdinalIgnoreCase ) && raw.AsIntegerOrNull().HasValue )
                {
                    var dv = DefinedValueCache.Get( raw.AsInteger() );
                    if ( dv != null )
                    {
                        return dv.Value;
                    }
                }
                if ( string.Equals( condition.Key, "CampusId", StringComparison.OrdinalIgnoreCase ) && raw.AsIntegerOrNull().HasValue )
                {
                    var campus = CampusCache.Get( raw.AsInteger() );
                    if ( campus != null )
                    {
                        return campus.Name;
                    }
                }
            }
            else
            {
                var guid = raw.AsGuidOrNull();
                if ( guid.HasValue )
                {
                    var dv = DefinedValueCache.Get( guid.Value );
                    if ( dv != null )
                    {
                        return dv.Value;
                    }
                }
            }

            return raw;
        }

        private static string SpaceCamelCase( string value )
        {
            return System.Text.RegularExpressions.Regex.Replace( value ?? string.Empty, "(?<=[a-z])([A-Z])", " $1" );
        }

        #endregion

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

            // Manual-skip context: the viewer's ManageSkips grant plus the displayed
            // person's active skips across the program's calcs (one query).
            bool canManage = IsUserAuthorized( SecurityActionKey.ManageSkips );
            var manualSkips = new Dictionary<int, SkipInfo>();
            if ( calcs.Count > 0 )
            {
                var calcIdList = calcs.Select( c => c.Id ).ToList();
                var skipRows = new JourneyCalculationSkipService( rockContext ).Queryable().AsNoTracking()
                    .Where( s => s.IsActive && calcIdList.Contains( s.JourneyCalculationId ) && s.PersonAlias.PersonId == Person.Id )
                    .Select( s => new
                    {
                        s.JourneyCalculationId,
                        s.Note,
                        s.CreatedDateTime,
                        ByNick = s.CreatedByPersonAlias.Person.NickName,
                        ByLast = s.CreatedByPersonAlias.Person.LastName
                    } )
                    .ToList();
                foreach ( var row in skipRows )
                {
                    manualSkips[row.JourneyCalculationId] = new SkipInfo
                    {
                        ByName = ( ( row.ByNick ?? string.Empty ) + " " + ( row.ByLast ?? string.Empty ) ).Trim(),
                        CreatedDateTime = row.CreatedDateTime,
                        Note = row.Note
                    };
                }
            }

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

                string stageActionsHtml = string.Empty;
                if ( canManage && !stage.Passed )
                {
                    stageActionsHtml = "<div class='jp-stage-actions'><a href=\"" +
                        Page.ClientScript.GetPostBackClientHyperlink( this, "skipstage:" + stage.StageId ) +
                        "\">Skip remaining steps</a></div>";
                }

                sb.AppendFormat(
                    "<details class='jp-drawer'{0}>" +
                    "<summary><span class='jp-drawer-name'>{1}</span><span class='jp-badge {2}'>{3}</span></summary>" +
                    "<div class='jp-drawer-body'>{4}" +
                    "<table class='jp-table'><thead><tr><th>Calculation</th><th>Type</th><th>Target Attribute</th><th class='jp-th-val'>Current Value</th>{5}</tr></thead><tbody>",
                    openAttr,
                    System.Web.HttpUtility.HtmlEncode( stage.StageName ?? string.Empty ),
                    badgeClass, badgeText,
                    stageActionsHtml,
                    canManage ? "<th class='jp-th-act'></th>" : string.Empty );

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

                    // Manual skip (7038) takes display precedence — the chip's tooltip carries
                    // the who/when/note audit, and the row offers Restore instead of Skip.
                    manualSkips.TryGetValue( calc.Id, out var manualSkip );
                    if ( manualSkip != null )
                    {
                        var tooltip = "Skipped";
                        if ( !string.IsNullOrWhiteSpace( manualSkip.ByName ) )
                        {
                            tooltip += " by " + manualSkip.ByName;
                        }
                        if ( manualSkip.CreatedDateTime.HasValue )
                        {
                            tooltip += " on " + manualSkip.CreatedDateTime.Value.ToShortDateString()
                                + " at " + manualSkip.CreatedDateTime.Value.ToShortTimeString();
                        }
                        if ( !string.IsNullOrWhiteSpace( manualSkip.Note ) )
                        {
                            tooltip += " - " + manualSkip.Note;
                        }
                        valueCell = "<span class='jp-skipped' title=\"" + System.Web.HttpUtility.HtmlAttributeEncode( tooltip ) + "\">Skipped</span>";
                    }

                    // Rule-based skip: the person matches this calc's "Skip If" filter. Rendered
                    // as a distinct "Auto-Skipped" chip (vs. the manual amber "Skipped") because
                    // there's no per-person record to restore — undoing it means editing the
                    // step's Skip Logic or the person no longer matching. Presence-gated:
                    // unconfigured calcs do no work here.
                    if ( string.IsNullOrWhiteSpace( valueCell ) && !string.IsNullOrWhiteSpace( calc.SkipFilterJson ) )
                    {
                        var skipMatch = com.razayya.JourneyTrack.CalculationTypes.PersonFilterCalculation.EvaluatePopulation(
                            calc.SkipFilterJson, calc.SkipFilterMatchAll, new System.Collections.Generic.HashSet<int> { Person.Id }, rockContext );
                        if ( skipMatch.Contains( Person.Id ) )
                        {
                            var rule = DescribeSkipRule( calc.SkipFilterJson, calc.SkipFilterMatchAll, rockContext );
                            var autoTooltip = rule != null
                                ? "Skipped by rule: " + rule
                                : "Skipped by this step's Skip If rule";
                            valueCell = "<span class='jp-autoskipped' title=\"" + System.Web.HttpUtility.HtmlAttributeEncode( autoTooltip ) + "\">Auto-Skipped</span>";
                        }
                    }

                    if ( string.IsNullOrWhiteSpace( valueCell )
                        && calc.PersonAttributeId.HasValue
                        && avValue.TryGetValue( calc.PersonAttributeId.Value, out var v )
                        && !string.IsNullOrWhiteSpace( v ) )
                    {
                        avPersisted.TryGetValue( calc.PersonAttributeId.Value, out var pt );
                        var text = !string.IsNullOrWhiteSpace( pt ) ? pt : v;
                        valueCell = "<span class='jp-val'>" + System.Web.HttpUtility.HtmlEncode( text ) + "</span>";
                    }

                    string actionCell = string.Empty;
                    if ( canManage )
                    {
                        if ( manualSkip != null )
                        {
                            actionCell = "<a class='jp-action' href=\"" +
                                Page.ClientScript.GetPostBackClientHyperlink( this, "restore:" + calc.Id ) +
                                "\" onclick=\"return confirm('Restore this step? It will need to be completed normally.');\">Restore</a>";
                        }
                        else if ( string.IsNullOrWhiteSpace( valueCell ) )
                        {
                            actionCell = "<a class='jp-action' href=\"" +
                                Page.ClientScript.GetPostBackClientHyperlink( this, "skip:" + calc.Id ) + "\">Skip</a>";
                        }
                    }

                    sb.AppendFormat(
                        "<tr><td class='jp-calc'>{0}</td><td><span class='jp-type'>{1}</span></td><td>{2}</td><td class='jp-td-val'>{3}</td>{4}</tr>",
                        System.Web.HttpUtility.HtmlEncode( calc.Name ?? string.Empty ),
                        System.Web.HttpUtility.HtmlEncode( calcTypeFriendly ),
                        targetCell,
                        valueCell,
                        canManage ? "<td class='jp-td-act'>" + actionCell + "</td>" : string.Empty );
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
.jp-skipped{display:inline-block;font-size:.66rem;font-weight:700;padding:.15rem .55rem;border-radius:999px;text-transform:uppercase;letter-spacing:.03em;background:#fde68a;color:#92400e;}
.jp-autoskipped{display:inline-block;font-size:.66rem;font-weight:700;padding:.15rem .55rem;border-radius:999px;text-transform:uppercase;letter-spacing:.03em;background:#dbeafe;color:#1e40af;}
.jp-th-act,.jp-td-act{text-align:right;white-space:nowrap;}
.jp-action{font-size:.72rem;font-weight:700;}
.jp-stage-actions{text-align:right;padding:.5rem .65rem 0;font-size:.75rem;}
</style>";
        }
    }
}
