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

            // WeeklyDayOfWeek/WeeklyTimeOfDay are deliberately LEFT POPULATED. Rock itself
            // reads the iCal event first and only falls back to these columns when there is
            // none (Schedule.Logic ScheduleType/HasSchedule/ToFriendlyScheduleText), so they
            // are inert to Rock once iCalendarContent is set. Vox surfaces are not so lucky:
            // the Men's/Women's/Spanish Ministry and VoxKids group cards gate their meeting
            // day on "{% if group.Schedule.WeeklyDayOfWeek != null %}", and the Campus
            // Breakdown / Attendance Report Card / Attendance Dashboard / Groups Audit
            // reports join DaysOfWeek on this column. Clearing it here would silently blank
            // the meeting day on all of them the first time a leader skips a date.
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
        /// Adds an exclusion for EVERY calendar day in the (inclusive) range — the same
        /// shape Rock's admin ScheduleBuilder writes — so leaders can black out a
        /// stretch regardless of whether occurrences can be enumerated that far out,
        /// and the blackout survives later schedule changes inside the range.
        /// Returns the number of newly-excluded days.
        /// </summary>
        public static int AddExclusionRange( Schedule schedule, DateTime start, DateTime end )
        {
            EnsureCustomSchedule( schedule );

            var calendarEvent = InetCalendarHelper.CreateCalendarEvent( schedule.iCalendarContent );
            if ( calendarEvent == null )
            {
                return 0;
            }

            var dates = ReadExclusionDates( calendarEvent );
            int added = 0;
            for ( var date = start.Date; date <= end.Date; date = date.AddDays( 1 ) )
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
        /// Removes every exclusion date inside the (inclusive) range. Returns the
        /// number of dates removed.
        /// </summary>
        public static int RemoveExclusionRange( Schedule schedule, DateTime start, DateTime end )
        {
            if ( schedule == null || string.IsNullOrWhiteSpace( schedule.iCalendarContent ) )
            {
                return 0;
            }

            var calendarEvent = InetCalendarHelper.CreateCalendarEvent( schedule.iCalendarContent );
            if ( calendarEvent == null )
            {
                return 0;
            }

            var dates = ReadExclusionDates( calendarEvent );
            var removed = dates.RemoveAll( d => d >= start.Date && d <= end.Date );
            if ( removed > 0 )
            {
                WriteExclusionDates( calendarEvent, dates );
                schedule.iCalendarContent = InetCalendarHelper.SerializeToCalendarString( calendarEvent );
            }
            return removed;
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

        #region Meeting day & time (7478)

        /// <summary>
        /// What the leader-facing editor can show and change about a schedule's
        /// meeting day and time.
        /// </summary>
        public class MeetingDayInfo
        {
            public DayOfWeek? DayOfWeek { get; set; }
            public TimeSpan? TimeOfDay { get; set; }

            /// <summary>
            /// True when the schedule is a plain weekly one (columns only, or an iCal
            /// event with a single every-week BYDAY) that the editor can rewrite.
            /// </summary>
            public bool CanEdit { get; set; }

            /// <summary>
            /// Rock's friendly description of the schedule, for read-only display.
            /// </summary>
            public string Summary { get; set; }
        }

        /// <summary>
        /// Reads the meeting day and time the leader editor should show. Weekly-column
        /// schedules read the columns; simple weekly iCal schedules read BYDAY (falling
        /// back to DTSTART's weekday, which is what Rock's ScheduleBuilder writes when no
        /// day is ticked) and DTSTART's time. Anything else (every-other-week, monthly,
        /// specific dates, one-off) is reported read-only.
        /// </summary>
        public static MeetingDayInfo GetMeetingDay( Schedule schedule )
        {
            var info = new MeetingDayInfo();
            if ( schedule == null )
            {
                return info;
            }

            info.Summary = schedule.ToFriendlyScheduleText();

            if ( string.IsNullOrWhiteSpace( schedule.iCalendarContent ) )
            {
                info.DayOfWeek = schedule.WeeklyDayOfWeek;
                info.TimeOfDay = schedule.WeeklyTimeOfDay;
                info.CanEdit = true;
                return info;
            }

            var calendarEvent = InetCalendarHelper.CreateCalendarEvent( schedule.iCalendarContent );
            var rule = GetSimpleWeeklyRule( calendarEvent );
            if ( rule == null )
            {
                return info;
            }

            info.DayOfWeek = rule.ByDay.Count == 1 ? rule.ByDay[0].DayOfWeek : calendarEvent.DtStart.Value.DayOfWeek;
            info.TimeOfDay = calendarEvent.DtStart.HasTime ? calendarEvent.DtStart.Value.TimeOfDay : ( TimeSpan? ) null;
            info.CanEdit = true;
            return info;
        }

        /// <summary>
        /// Sets the meeting day and time. Weekly-column schedules get the columns set,
        /// exactly as core Group Detail Lava does. Simple weekly iCal schedules get
        /// DTSTART moved to the new weekday WITHIN ITS OWN WEEK (so COUNT- and
        /// UNTIL-bounded series such as Short Term groups keep their length and end),
        /// DTEND moved with it (Rock's builder writes DTEND one second after DTSTART, and
        /// a stale DTEND before the new DTSTART would give the event a negative duration
        /// and no occurrences), BYDAY rewritten, every EXDATE preserved, and the weekly
        /// columns synced so the mobile Group Finder and the Vox group cards keep
        /// matching. A blank time keeps the schedule's current time.
        /// Returns false, changing nothing, when the schedule isn't one the editor owns.
        /// </summary>
        public static bool SetMeetingDay( Schedule schedule, DayOfWeek dayOfWeek, TimeSpan? timeOfDay )
        {
            if ( schedule == null )
            {
                return false;
            }

            if ( string.IsNullOrWhiteSpace( schedule.iCalendarContent ) )
            {
                schedule.WeeklyDayOfWeek = dayOfWeek;
                schedule.WeeklyTimeOfDay = timeOfDay;
                return true;
            }

            var calendarEvent = InetCalendarHelper.CreateCalendarEvent( schedule.iCalendarContent );
            var rule = GetSimpleWeeklyRule( calendarEvent );
            if ( rule == null )
            {
                return false;
            }

            var oldStart = calendarEvent.DtStart.Value;
            var newTime = timeOfDay ?? oldStart.TimeOfDay;
            var newStart = oldStart.Date
                .AddDays( ( int ) dayOfWeek - ( int ) oldStart.DayOfWeek )
                .Add( newTime );

            TimeSpan? duration = null;
            if ( calendarEvent.DtEnd != null )
            {
                duration = calendarEvent.DtEnd.Value - oldStart;
                if ( duration.Value < TimeSpan.FromSeconds( 1 ) )
                {
                    duration = TimeSpan.FromSeconds( 1 );
                }
            }

            var dtStart = new CalDateTime( newStart );
            dtStart.HasTime = true;
            calendarEvent.DtStart = dtStart;

            if ( duration.HasValue )
            {
                var dtEnd = new CalDateTime( newStart.Add( duration.Value ) );
                dtEnd.HasTime = true;
                calendarEvent.DtEnd = dtEnd;
            }

            rule.ByDay.Clear();
            rule.ByDay.Add( new WeekDay( dayOfWeek ) );

            schedule.iCalendarContent = InetCalendarHelper.SerializeToCalendarString( calendarEvent );
            schedule.WeeklyDayOfWeek = dayOfWeek;
            schedule.WeeklyTimeOfDay = newTime;
            return true;
        }

        /// <summary>
        /// The event's single every-week recurrence rule, or null when the event is
        /// anything the editor must leave alone: no DTSTART, no rule or several, a
        /// non-weekly frequency, an INTERVAL above one, more than one BYDAY, an ordinal
        /// BYDAY, any other BY* part, RDATEs, or exception rules. Parsed, not string
        /// matched, because "FREQ=WEEKLY" also appears in every-other-week rules.
        /// </summary>
        private static RecurrencePattern GetSimpleWeeklyRule( CalendarEvent calendarEvent )
        {
            if ( calendarEvent == null || calendarEvent.DtStart == null )
            {
                return null;
            }

            if ( calendarEvent.RecurrenceRules == null || calendarEvent.RecurrenceRules.Count != 1 )
            {
                return null;
            }

            if ( calendarEvent.RecurrenceDates != null && calendarEvent.RecurrenceDates.Any( periodList => periodList.Any() ) )
            {
                return null;
            }

            if ( calendarEvent.ExceptionRules != null && calendarEvent.ExceptionRules.Any() )
            {
                return null;
            }

            var rule = calendarEvent.RecurrenceRules[0];
            if ( rule.Frequency != FrequencyType.Weekly || rule.Interval != 1 )
            {
                return null;
            }

            if ( rule.ByDay.Count > 1 )
            {
                return null;
            }

            // Ical.Net reports "no ordinal" as int.MinValue; anything else (e.g. 2TU) is monthly-style.
            if ( rule.ByDay.Count == 1 && rule.ByDay[0].Offset != int.MinValue && rule.ByDay[0].Offset != 0 )
            {
                return null;
            }

            if ( rule.ByMonth.Any() || rule.ByMonthDay.Any() || rule.ByYearDay.Any() || rule.ByWeekNo.Any()
                || rule.ByHour.Any() || rule.ByMinute.Any() || rule.BySecond.Any() || rule.BySetPosition.Any() )
            {
                return null;
            }

            return rule;
        }

        #endregion

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
