using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web.UI.WebControls;

using com.razayya.RSVPReminders.Services;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web.Cache;
using Rock.Web.UI;

using RsvpGuid = com.razayya.RSVPReminders.SystemGuid;

namespace RockWeb.Plugins.com_razayya.RSVPReminders
{
    [DisplayName( "RSVP Group Exclusions" )]
    [Category( "Razayya > RSVP Reminders" )]
    [Description( "Lets group leaders skip meeting dates from the Group Toolbox so no meetings — and no RSVP emails, where the group sends them — happen on those dates. On the Group Toolbox it renders as a link to the dedicated Meeting Exclusions page; on that page it renders the full manager. Only shows for groups the current person can manage." )]

    [Rock.Attribute.GroupTypesField(
        "Group Types",
        "Group types whose leaders can manage meeting exclusions. Leave blank to use the Auto RSVP group types, which is how this block behaved before the setting existed.",
        required: false,
        order: 0,
        key: AttributeKey.GroupTypes )]

    public partial class RsvpGroupExclusions : RockBlock
    {
        private static class AttributeKey
        {
            public const string GroupTypes = "GroupTypes";
        }

        /// <summary>
        /// Whether the resolved group actually sends RSVP emails. Drives the wording only —
        /// skipping a date works the same either way, but promising a non-RSVP group that
        /// "RSVP emails won't go out" is meaningless to its leaders.
        /// </summary>
        private bool _groupSendsRsvpEmails;

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                BindAll();
            }
        }

        /// <summary>
        /// Resolves the route group and gates everything: GroupId param present, group has a
        /// schedule, current person can manage the group, and the group's type is one this
        /// block is configured for. Returns null (nothing renders) otherwise.
        ///
        /// Exclusions are native iCal EXDATEs on the group's own schedule, so nothing about
        /// the mechanism is RSVP-specific and the configured group types are the whole gate.
        /// With no group types configured this falls back to the original behaviour — the
        /// AutoRSVP group-type tree plus SendsRsvpEmails, the same identification the AutoRSVP
        /// job uses — so an unconfigured install behaves exactly as it did before.
        /// </summary>
        private Group ResolveAuthorizedGroup( RockContext rockContext )
        {
            var groupId = PageParameter( "GroupId" ).AsIntegerOrNull();
            if ( !groupId.HasValue || groupId.Value <= 0 )
            {
                return null;
            }

            var group = new GroupService( rockContext ).Queryable( "Schedule,GroupType" )
                .FirstOrDefault( g => g.Id == groupId.Value && g.IsActive && !g.IsArchived );
            if ( group == null || group.Schedule == null )
            {
                return null;
            }

            if ( !group.IsAuthorized( Authorization.EDIT, CurrentPerson ) )
            {
                return null;
            }

            var configuredGroupTypeGuids = GetConfiguredGroupTypeGuids();
            if ( configuredGroupTypeGuids.Any() )
            {
                if ( group.GroupType == null || !configuredGroupTypeGuids.Contains( group.GroupType.Guid ) )
                {
                    return null;
                }

                // Deliberately NOT gated on SendsRsvpEmails — group types outside the AutoRSVP
                // tree don't carry that attribute at all, and a leader blacking out a meeting
                // date is useful whether or not the group happens to send reminders.
                _groupSendsRsvpEmails = GroupSendsRsvpEmails( rockContext, group );
                return group;
            }

            var rsvpGroupType = new GroupTypeService( rockContext ).Get( RsvpGuid.GroupType.AUTO_RSVP_GROUP.AsGuid() );
            if ( rsvpGroupType == null )
            {
                return null;
            }

            var rsvpGroupTypeIds = rsvpGroupType.GetAllDependentGroupTypeIds( rockContext );
            rsvpGroupTypeIds.Add( rsvpGroupType.Id );
            if ( !rsvpGroupTypeIds.Contains( group.GroupTypeId ) )
            {
                return null;
            }

            if ( !GroupSendsRsvpEmails( rockContext, group ) )
            {
                return null;
            }

            _groupSendsRsvpEmails = true;
            return group;
        }

        /// <summary>
        /// Whether the group is flagged to send RSVP emails. Group types outside the AutoRSVP
        /// tree have no such attribute, in which case this is simply false.
        /// </summary>
        private static bool GroupSendsRsvpEmails( RockContext rockContext, Group group )
        {
            group.LoadAttributes( rockContext );
            return group.GetAttributeValue( RsvpGuid.GroupAttribute.SEND_RSVP_EMAILS.AsGuid() ).AsBoolean();
        }

        /// <summary>
        /// The group types this block is configured for.
        ///
        /// The Group Toolbox link and the manager on the Meeting Exclusions page are two
        /// separate instances of this block, and both gate on this setting. The manager is
        /// authoritative: the link instance always defers to it rather than to its own copy.
        /// Any other arrangement lets the two drift apart, and the failure mode is bad in both
        /// directions — a leader shown a "Skip Meeting Dates" button that opens an empty page,
        /// or no button at all on a group the manager would happily have managed.
        ///
        /// The block's own value is used only where there is no separate manager: the
        /// pre-006 layout, where this instance renders the manager inline on the toolbox.
        /// </summary>
        private List<Guid> GetConfiguredGroupTypeGuids()
        {
            var ownGuids = GetAttributeValue( AttributeKey.GroupTypes ).SplitDelimitedValues().AsGuidList();

            var exclusionsPage = PageCache.Get( RsvpGuid.Page.MEETING_EXCLUSIONS.AsGuid() );
            if ( exclusionsPage == null || exclusionsPage.Id == RockPage.PageId )
            {
                return ownGuids;
            }

            var blockType = BlockTypeCache.Get( RsvpGuid.BlockType.RSVP_GROUP_EXCLUSIONS.AsGuid() );
            var managerBlock = blockType == null
                ? null
                : exclusionsPage.Blocks.FirstOrDefault( b => b.BlockTypeId == blockType.Id );

            return managerBlock == null
                ? ownGuids
                : managerBlock.GetAttributeValue( AttributeKey.GroupTypes ).SplitDelimitedValues().AsGuidList();
        }

        private void BindAll()
        {
            pnlLink.Visible = false;
            pnlExclusions.Visible = false;

            using ( var rockContext = new RockContext() )
            {
                var group = ResolveAuthorizedGroup( rockContext );
                if ( group == null )
                {
                    return;
                }

                // Link mode when this instance sits on any page OTHER than the dedicated
                // Meeting Exclusions page (i.e., the Group Toolbox). Falls back to inline
                // manager mode if the dedicated page doesn't exist (pre-006 installs).
                var exclusionsPage = PageCache.Get( RsvpGuid.Page.MEETING_EXCLUSIONS.AsGuid() );
                if ( exclusionsPage != null && RockPage.PageId != exclusionsPage.Id )
                {
                    pnlLink.Visible = true;
                    hlManage.NavigateUrl = BuildPageUrl( exclusionsPage.Id, group.Id );
                    return;
                }

                pnlExclusions.Visible = true;

                lIntro.Text = _groupSendsRsvpEmails
                    ? "<p class=\"text-muted\">Skip a date your group won't be meeting and RSVP emails won't go out for it. You can un-skip a date any time before it arrives.</p>"
                    : "<p class=\"text-muted\">Skip a date your group won't be meeting and it will drop off the group's schedule. You can un-skip a date any time before it arrives.</p>";

                drpSkipRange.Help = _groupSendsRsvpEmails
                    ? "No RSVP emails will go out for anything scheduled between the two dates (inclusive). Leave the second date blank to skip a single date."
                    : "Nothing between the two dates (inclusive) will be treated as a meeting. Leave the second date blank to skip a single date.";

                var currentPage = PageCache.Get( RockPage.PageId );
                if ( currentPage != null && currentPage.ParentPageId.HasValue )
                {
                    hlBack.NavigateUrl = BuildPageUrl( currentPage.ParentPageId.Value, group.Id );
                    hlBack.Visible = true;
                }

                var exclusionDates = RsvpScheduleService.GetFutureExclusions( group.Schedule );
                var upcoming = RsvpScheduleService.GetUpcomingMeetingDates( group.Schedule, 12 )
                    .Select( d => d.Date )
                    .Where( d => !exclusionDates.Contains( d ) )
                    .Distinct()
                    .OrderBy( d => d )
                    .ToList();

                rptUpcoming.DataSource = upcoming;
                rptUpcoming.DataBind();
                lNoUpcoming.Visible = upcoming.Count == 0;

                var ranges = CollapseToRanges( exclusionDates );
                rptExclusions.DataSource = ranges;
                rptExclusions.DataBind();
                lNoExclusions.Visible = ranges.Count == 0;
            }
        }

        private static string BuildPageUrl( int pageId, int groupId )
        {
            var reference = new Rock.Web.PageReference( pageId );
            reference.Parameters = new Dictionary<string, string> { { "GroupId", groupId.ToString() } };
            return reference.BuildUrl();
        }

        protected void rptUpcoming_ItemCommand( object source, RepeaterCommandEventArgs e )
        {
            if ( e.CommandName == "Skip" )
            {
                var date = e.CommandArgument.ToString().AsDateTime();
                if ( date.HasValue )
                {
                    ApplyRangeChange( date.Value, date.Value, add: true );
                }
            }
        }

        protected void rptExclusions_ItemCommand( object source, RepeaterCommandEventArgs e )
        {
            if ( e.CommandName == "Remove" )
            {
                var parts = e.CommandArgument.ToString().Split( '|' );
                var start = parts.Length > 0 ? parts[0].AsDateTime() : null;
                var end = parts.Length > 1 ? parts[1].AsDateTime() : start;
                if ( start.HasValue && end.HasValue )
                {
                    ApplyRangeChange( start.Value, end.Value, add: false );
                }
            }
        }

        protected void lbAddRange_Click( object sender, EventArgs e )
        {
            var lower = drpSkipRange.LowerValue;
            var upper = drpSkipRange.UpperValue ?? lower;

            if ( !lower.HasValue )
            {
                ShowMessage( Rock.Web.UI.Controls.NotificationBoxType.Warning, "Pick at least a start date to skip." );
                BindAll();
                return;
            }
            if ( upper.Value.Date < lower.Value.Date )
            {
                ShowMessage( Rock.Web.UI.Controls.NotificationBoxType.Warning, "The end date must be on or after the start date." );
                BindAll();
                return;
            }
            if ( ( upper.Value.Date - lower.Value.Date ).TotalDays > 366 )
            {
                ShowMessage( Rock.Web.UI.Controls.NotificationBoxType.Warning, "Date ranges are limited to one year." );
                BindAll();
                return;
            }

            ApplyRangeChange( lower.Value.Date, upper.Value.Date, add: true );
            drpSkipRange.LowerValue = null;
            drpSkipRange.UpperValue = null;
        }

        /// <summary>
        /// Adds or removes the exclusion range (single dates are a one-day range),
        /// re-checking authorization server-side. The first add on a Weekly group
        /// transparently converts its schedule to Custom (iCal); Rock's Schedule
        /// SaveHook recomputes effective dates on save.
        /// </summary>
        private void ApplyRangeChange( DateTime start, DateTime end, bool add )
        {
            if ( start.Date < RockDateTime.Today )
            {
                ShowMessage( Rock.Web.UI.Controls.NotificationBoxType.Warning, "Only today or future dates can be changed." );
                BindAll();
                return;
            }

            using ( var rockContext = new RockContext() )
            {
                var group = ResolveAuthorizedGroup( rockContext );
                if ( group == null )
                {
                    pnlLink.Visible = false;
                    pnlExclusions.Visible = false;
                    return;
                }

                if ( add )
                {
                    var added = RsvpScheduleService.AddExclusionRange( group.Schedule, start, end );
                    rockContext.SaveChanges();
                    ShowMessage( Rock.Web.UI.Controls.NotificationBoxType.Success,
                        added > 0 ? string.Format( "Skipped {0}.", FormatRange( start, end ) )
                                  : "Those dates were already skipped." );
                }
                else
                {
                    var removed = RsvpScheduleService.RemoveExclusionRange( group.Schedule, start, end );
                    rockContext.SaveChanges();
                    ShowMessage( Rock.Web.UI.Controls.NotificationBoxType.Success,
                        removed > 0 ? string.Format( "Restored {0}.", FormatRange( start, end ) )
                                    : "Nothing to restore for those dates." );
                }
            }

            BindAll();
        }

        private static string FormatRange( DateTime start, DateTime end )
        {
            return start.Date == end.Date
                ? start.ToString( "MMMM d, yyyy" )
                : string.Format( "{0:MMMM d, yyyy} through {1:MMMM d, yyyy}", start, end );
        }

        private void ShowMessage( Rock.Web.UI.Controls.NotificationBoxType type, string text )
        {
            nbMessage.NotificationBoxType = type;
            nbMessage.Text = text;
            nbMessage.Visible = true;
        }

        #region Exclusion range display

        /// <summary>
        /// Collapses individual exclusion dates into consecutive-day ranges for display
        /// (mirrors how Rock's admin ScheduleBuilder presents exclusions), so a month-long
        /// blackout reads as one row with one Un-skip instead of thirty.
        /// </summary>
        private static List<ExclusionRange> CollapseToRanges( List<DateTime> dates )
        {
            var ranges = new List<ExclusionRange>();
            foreach ( var date in dates.Distinct().OrderBy( d => d ) )
            {
                if ( ranges.Count > 0 && ranges[ranges.Count - 1].End.AddDays( 1 ) == date )
                {
                    ranges[ranges.Count - 1].End = date;
                }
                else
                {
                    ranges.Add( new ExclusionRange { Start = date, End = date } );
                }
            }
            return ranges;
        }

        // Bind row for the skipped-dates repeater.
        protected class ExclusionRange
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }

            public string Label
            {
                get
                {
                    return Start == End
                        ? Start.ToString( "ddd, MMM d, yyyy" )
                        : string.Format( "{0:MMM d, yyyy} - {1:MMM d, yyyy}", Start, End );
                }
            }

            public string Arg
            {
                get
                {
                    return Start.ToString( "yyyy-MM-dd" ) + "|" + End.ToString( "yyyy-MM-dd" );
                }
            }
        }

        #endregion
    }
}
