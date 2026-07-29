using System;
using System.Collections.Generic;
using System.Linq;

using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

using Rock;
using Rock.Model;

namespace com.razayya.RSVPReminders.Services
{
    /// <summary>
    /// Pure schedule/iCal logic for leader-managed meeting exclusions (6919): lazy
    /// Weekly-to-Custom conversion plus add/remove/list of single-date EXDATE
    /// exclusions on a group's own inline schedule. The AutoRSVP job keys every send
    /// on occurrences from GetScheduledStartTimes, so an excluded meeting simply
    /// vanishes — all its lead-time reminders die with it, with no job changes.
    /// Persistence stays with the caller's RockContext; Rock's Schedule SaveHook
    /// recomputes effective dates on save.
    /// </summary>
    public static class RsvpScheduleService
    {
        /// <summary>
        /// Idempotently converts a simple Weekly schedule to a Custom (iCal) schedule so
        /// exclusions become possible AND GetScheduledStartTimes actually returns
        /// occurrences (a bare Weekly schedule yields none, which silently disables the
        /// RSVP job for the group). No-op when the schedule already has iCal content or
        /// has no weekly day configured.
        /// </summary>
        public static void EnsureCustomSchedule( Schedule schedule )
        {
            if ( schedule == null
                || !string.IsNullOrWhiteSpace( schedule.iCalendarContent )
                || !schedule.WeeklyDayOfWeek.HasValue )
            {
                return;
            }

            var dayOfWeek = schedule.WeeklyDayOfWeek.Value;
            var timeOfDay = schedule.WeeklyTimeOfDay ?? TimeSpan.Zero;

            // Anchor DTSTART on the most recent past date matching the weekday so future
            // occurrences generate. The iCal "DTSTART is always an occurrence" quirk then
            // lands in the past, outside any reminder window, so it is harmless.
            var anchorDate = RockDateTime.Today;
            while ( anchorDate.DayOfWeek != dayOfWeek )
            {
                anchorDate = anchorDate.AddDays( -1 );
            }

            var dtStart = new CalDateTime( anchorDate.Add( timeOfDay ) );
            dtStart.HasTime = true;

            var calendarEvent = new CalendarEvent
            {
                DtStart = dtStart
            };

            var recurrence = new RecurrencePattern( FrequencyType.Weekly )
            {
                Interval = 1
            };
            recurrence.ByDay.Add( new WeekDay( dayOfWeek ) );
            calendarEvent.RecurrenceRules.Add( recurrence );

            schedule.iCalendarContent = InetCalendarHelper.SerializeToCalendarString( calendarEvent );
            schedule.WeeklyDayOfWeek = null;
            schedule.WeeklyTimeOfDay = null;
        }

        /// <summary>
        /// Adds a single-date exclusion (EXDATE) to the schedule, converting Weekly to
        /// Custom first when needed. Idempotent per date.
        /// </summary>
        public static void AddExclusion( Schedule schedule, DateTime date )
        {
            EnsureCustomSchedule( schedule );

            var calendarEvent = InetCalendarHelper.CreateCalendarEvent( schedule.iCalendarContent );
            if ( calendarEvent == null )
            {
                return;
            }

            var dates = ReadExclusionDates( calendarEvent );
            if ( !dates.Contains( date.Date ) )
            {
                dates.Add( date.Date );
            }

            WriteExclusionDates( calendarEvent, dates );
            schedule.iCalendarContent = InetCalendarHelper.SerializeToCalendarString( calendarEvent );
        }

        /// <summary>
        /// Adds exclusions for every meeting occurrence inside the (inclusive) date
        /// range, converting Weekly to Custom first when needed. Only real occurrence
        /// dates are excluded — unlike Rock's admin ScheduleBuilder, which EXDATEs every
        /// calendar day in a range — so the leader's skipped-dates list stays a list of
        /// actual meetings. Returns the number of newly-excluded dates.
        /// </summary>
        public static int AddExclusionRange( Schedule schedule, DateTime start, DateTime end )
        {
            EnsureCustomSchedule( schedule );

            var calendarEvent = InetCalendarHelper.CreateCalendarEvent( schedule.iCalendarContent );
            if ( calendarEvent == null )
            {
                return 0;
            }

            var occurrenceDates = schedule.GetScheduledStartTimes( start.Date, end.Date.AddDays( 1 ).AddSeconds( -1 ) )
                .Select( d => d.Date )
                .Distinct()
                .ToList();
            if ( occurrenceDates.Count == 0 )
            {
                return 0;
            }

            var dates = ReadExclusionDates( calendarEvent );
            int added = 0;
            foreach ( var date in occurrenceDates )
            {
                if ( !dates.Contains( date ) )
                {
                    dates.Add( date );
                    added++;
                }
            }

            if ( added > 0 )
            {
                WriteExclusionDates( calendarEvent, dates );
                schedule.iCalendarContent = InetCalendarHelper.SerializeToCalendarString( calendarEvent );
            }
            return added;
        }

        /// <summary>
        /// Removes a single-date exclusion from the schedule. No-op when the date isn't
        /// excluded or the schedule has no iCal content.
        /// </summary>
        public static void RemoveExclusion( Schedule schedule, DateTime date )
        {
            if ( schedule == null || string.IsNullOrWhiteSpace( schedule.iCalendarContent ) )
            {
                return;
            }

            var calendarEvent = InetCalendarHelper.CreateCalendarEvent( schedule.iCalendarContent );
            if ( calendarEvent == null )
            {
                return;
            }

            var dates = ReadExclusionDates( calendarEvent );
            if ( dates.Remove( date.Date ) )
            {
                WriteExclusionDates( calendarEvent, dates );
                schedule.iCalendarContent = InetCalendarHelper.SerializeToCalendarString( calendarEvent );
            }
        }

        /// <summary>
        /// Future (today or later) exclusion dates currently on the schedule.
        /// </summary>
        public static List<DateTime> GetFutureExclusions( Schedule schedule )
        {
            if ( schedule == null || string.IsNullOrWhiteSpace( schedule.iCalendarContent ) )
            {
                return new List<DateTime>();
            }

            var calendarEvent = InetCalendarHelper.CreateCalendarEvent( schedule.iCalendarContent );
            if ( calendarEvent == null )
            {
                return new List<DateTime>();
            }

            return ReadExclusionDates( calendarEvent )
                .Where( d => d >= RockDateTime.Today )
                .OrderBy( d => d )
                .ToList();
        }

        /// <summary>
        /// Upcoming meeting dates over the next <paramref name="weeks"/> weeks. Custom
        /// schedules come from GetScheduledStartTimes (which already honors exclusions);
        /// unconverted Weekly schedules compute the next weekly dates directly so the
        /// picker shows real dates before any conversion has happened.
        /// </summary>
        public static List<DateTime> GetUpcomingMeetingDates( Schedule schedule, int weeks )
        {
            var start = RockDateTime.Today;
            var end = start.AddDays( weeks * 7 );

            if ( schedule == null )
            {
                return new List<DateTime>();
            }

            if ( !string.IsNullOrWhiteSpace( schedule.iCalendarContent ) )
            {
                return schedule.GetScheduledStartTimes( start, end );
            }

            var dates = new List<DateTime>();
            if ( schedule.WeeklyDayOfWeek.HasValue )
            {
                var timeOfDay = schedule.WeeklyTimeOfDay ?? TimeSpan.Zero;
                var next = start;
                while ( next.DayOfWeek != schedule.WeeklyDayOfWeek.Value )
                {
                    next = next.AddDays( 1 );
                }
                while ( next < end )
                {
                    dates.Add( next.Add( timeOfDay ) );
                    next = next.AddDays( 7 );
                }
            }
            return dates;
        }

        /// <summary>
        /// Reads the event's exclusion dates as distinct, ordered date-only values.
        /// Merges ALL ExceptionDates period lists (hand-crafted iCal can carry several
        /// EXDATE lines) — WriteExclusionDates consolidates back to one list, so reading
        /// only the first would silently drop the rest on the first write.
        /// </summary>
        private static List<DateTime> ReadExclusionDates( CalendarEvent calendarEvent )
        {
            var dates = new List<DateTime>();
            if ( calendarEvent.ExceptionDates != null )
            {
                dates.AddRange( calendarEvent.ExceptionDates
                    .SelectMany( periodList => periodList )
                    .Select( p => p.StartTime.Value.Date ) );
            }
            return dates.Distinct().OrderBy( d => d ).ToList();
        }

        /// <summary>
        /// Rebuilds the event's ExceptionDates as a single PeriodList of single-date
        /// periods (mirrors Rock's ScheduleBuilder shape).
        /// </summary>
        private static void WriteExclusionDates( CalendarEvent calendarEvent, List<DateTime> dates )
        {
            calendarEvent.ExceptionDates.Clear();
            if ( dates.Count > 0 )
            {
                var periodList = new PeriodList();
                foreach ( var date in dates.Distinct().OrderBy( d => d ) )
                {
                    periodList.Add( new Period( new CalDateTime( date ) ) );
                }
                calendarEvent.ExceptionDates.Add( periodList );
            }
        }
    }
}
