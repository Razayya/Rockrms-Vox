using System;
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
    [Description( "Lets group leaders skip upcoming meeting dates so RSVP emails aren't sent for those occurrences. Renders only for AutoRSVP-enabled groups the current person can manage. Drop on the Group Toolbox page." )]

    public partial class RsvpGroupExclusions : RockBlock
    {
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                BindAll();
            }
        }

        /// <summary>
        /// Resolves the route group and gates the whole panel: GroupId param present,
        /// group has a schedule, current person can manage the group, and the group is
        /// AutoRSVP-enabled (RSVP group-type tree + SendsRsvpEmails attribute) — the
        /// same identification the AutoRSVP job uses. Returns null (panel hidden) otherwise.
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

            group.LoadAttributes( rockContext );
            if ( !group.GetAttributeValue( RsvpGuid.GroupAttribute.SEND_RSVP_EMAILS.AsGuid() ).AsBoolean() )
            {
                return null;
            }

            return group;
        }

        private void BindAll()
        {
            using ( var rockContext = new RockContext() )
            {
                var group = ResolveAuthorizedGroup( rockContext );
                if ( group == null )
                {
                    pnlExclusions.Visible = false;
                    return;
                }

                pnlExclusions.Visible = true;

                var exclusions = RsvpScheduleService.GetFutureExclusions( group.Schedule );
                var upcoming = RsvpScheduleService.GetUpcomingMeetingDates( group.Schedule, 12 )
                    .Select( d => d.Date )
                    .Where( d => !exclusions.Contains( d ) )
                    .Distinct()
                    .OrderBy( d => d )
                    .ToList();

                rptUpcoming.DataSource = upcoming;
                rptUpcoming.DataBind();
                lNoUpcoming.Visible = upcoming.Count == 0;

                rptExclusions.DataSource = exclusions;
                rptExclusions.DataBind();
                lNoExclusions.Visible = exclusions.Count == 0;
            }
        }

        protected void rptUpcoming_ItemCommand( object source, RepeaterCommandEventArgs e )
        {
            if ( e.CommandName == "Skip" )
            {
                ApplyExclusionChange( e.CommandArgument.ToString(), add: true );
            }
        }

        protected void rptExclusions_ItemCommand( object source, RepeaterCommandEventArgs e )
        {
            if ( e.CommandName == "Remove" )
            {
                ApplyExclusionChange( e.CommandArgument.ToString(), add: false );
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
            if ( lower.Value.Date < RockDateTime.Today )
            {
                ShowMessage( Rock.Web.UI.Controls.NotificationBoxType.Warning, "Only today or future dates can be skipped." );
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

            using ( var rockContext = new RockContext() )
            {
                var group = ResolveAuthorizedGroup( rockContext );
                if ( group == null )
                {
                    pnlExclusions.Visible = false;
                    return;
                }

                var added = RsvpScheduleService.AddExclusionRange( group.Schedule, lower.Value.Date, upper.Value.Date );
                rockContext.SaveChanges();

                if ( added > 0 )
                {
                    ShowMessage( Rock.Web.UI.Controls.NotificationBoxType.Success,
                        string.Format( "Skipped {0} meeting{1} between {2:MMMM d, yyyy} and {3:MMMM d, yyyy}.",
                            added, added == 1 ? "" : "s", lower.Value, upper.Value ) );
                }
                else
                {
                    ShowMessage( Rock.Web.UI.Controls.NotificationBoxType.Warning,
                        "No meetings found in that date range (or they were already skipped)." );
                }
            }

            drpSkipRange.LowerValue = null;
            drpSkipRange.UpperValue = null;
            BindAll();
        }

        /// <summary>
        /// Adds or removes one exclusion date, re-checking authorization server-side.
        /// The first add on a Weekly group transparently converts its schedule to
        /// Custom (iCal); Rock's Schedule SaveHook recomputes effective dates on save.
        /// </summary>
        private void ApplyExclusionChange( string dateArg, bool add )
        {
            var date = dateArg.AsDateTime();
            if ( !date.HasValue || date.Value.Date < RockDateTime.Today )
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
                    pnlExclusions.Visible = false;
                    return;
                }

                if ( add )
                {
                    RsvpScheduleService.AddExclusion( group.Schedule, date.Value );
                }
                else
                {
                    RsvpScheduleService.RemoveExclusion( group.Schedule, date.Value );
                }
                rockContext.SaveChanges();

                ShowMessage( Rock.Web.UI.Controls.NotificationBoxType.Success,
                    string.Format( "{0} {1:MMMM d, yyyy}.", add ? "Skipped" : "Restored", date.Value ) );
            }

            BindAll();
        }

        private void ShowMessage( Rock.Web.UI.Controls.NotificationBoxType type, string text )
        {
            nbMessage.NotificationBoxType = type;
            nbMessage.Text = text;
            nbMessage.Visible = true;
        }
    }
}
