using System;
using System.Collections.Generic;
using System.Linq;

using com.razayya.JourneyTrack.Constants;
using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Web.Cache;

namespace com.razayya.JourneyTrack.Logic
{
    /// <summary>
    /// Resolves the effective attendance window start when a calculation's Blackout Ranges
    /// setting points at a defined type of recurring annual date ranges. Days inside a
    /// blackout range do not consume the WithinDays budget, so the window stretches across
    /// planned breaks (group sabbatical, Christmas) instead of silently failing people —
    /// attendance recorded inside a blackout still counts, only the clock stops.
    ///
    /// The window start is the LATEST date D on or before AsOf such that the count of
    /// non-blackout days in [D, AsOf) — D included, AsOf excluded — reaches WithinDays.
    /// With no ranges configured this is exactly AsOf - WithinDays, so a blank setting is
    /// a true no-op. AsOf's own blackout status never affects the result, and the returned
    /// date always lands on a countable (non-blackout) day.
    ///
    /// dbo._com_razayya_JourneyTrack_ufnAttendanceWindowStart (plugin migration 017) mirrors
    /// this algorithm for the T-SQL surfaces (mobile pages, dashboards). The two MUST stay
    /// in lockstep — change both or neither.
    /// </summary>
    public static class BlackoutWindow
    {
        /// <summary>
        /// Walk-back safety bound beyond the WithinDays budget. A configuration whose
        /// blackout covers essentially the whole year clamps here instead of walking
        /// forever; the SQL function clamps at the same bound.
        /// </summary>
        public const int MaxWalkbackPadDays = 400;

        /// <summary>
        /// One recurring annual range, both endpoints inclusive, encoded as month*100+day
        /// (e.g. 07-05 => 705). StartMonthDay later than EndMonthDay wraps across year-end.
        /// </summary>
        public struct MonthDayRange
        {
            public int StartMonthDay;
            public int EndMonthDay;
        }

        /// <summary>
        /// Parses defined value strings in the pinned MM-dd|MM-dd format (zero-padded,
        /// 11 characters, both ends inclusive). Throws on any malformed value rather than
        /// skipping it — a silently-dropped range would shorten the window without anyone
        /// noticing, which is exactly the drift this feature exists to prevent.
        /// </summary>
        public static List<MonthDayRange> ParseRanges( IEnumerable<string> values )
        {
            var ranges = new List<MonthDayRange>();

            foreach ( var value in values ?? Enumerable.Empty<string>() )
            {
                var start = ParseMonthDay( value, 0 );
                var end = ParseMonthDay( value, 6 );

                if ( value == null || value.Length != 11 || value[2] != '-' || value[5] != '|' || value[8] != '-'
                    || start == null || end == null )
                {
                    throw new FormatException( $"Blackout range value '{value}' is not in the required MM-dd|MM-dd format." );
                }

                ranges.Add( new MonthDayRange { StartMonthDay = start.Value, EndMonthDay = end.Value } );
            }

            return ranges;
        }

        private static int? ParseMonthDay( string value, int offset )
        {
            if ( value == null || value.Length < offset + 5 )
            {
                return null;
            }

            var month = value.Substring( offset, 2 ).AsIntegerOrNull();
            var day = value.Substring( offset + 3, 2 ).AsIntegerOrNull();

            if ( month == null || day == null || month < 1 || month > 12 || day < 1 || day > 31 )
            {
                return null;
            }

            return month.Value * 100 + day.Value;
        }

        /// <summary>
        /// True when the date's month-day falls inside any of the ranges (union semantics;
        /// overlapping ranges are fine). Pure month-day comparison, so Feb 29 needs no
        /// special casing in non-leap years.
        /// </summary>
        public static bool IsBlackoutDay( DateTime date, IReadOnlyList<MonthDayRange> ranges )
        {
            int monthDay = date.Month * 100 + date.Day;

            for ( int i = 0; i < ranges.Count; i++ )
            {
                var range = ranges[i];
                bool inRange = range.StartMonthDay <= range.EndMonthDay
                    ? monthDay >= range.StartMonthDay && monthDay <= range.EndMonthDay
                    : monthDay >= range.StartMonthDay || monthDay <= range.EndMonthDay;

                if ( inRange )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Computes the effective window start per the algorithm in the class summary.
        /// <paramref name="clamped"/> is true when the walk hit the safety bound before
        /// filling the budget (pathological configuration).
        /// </summary>
        public static DateTime GetWindowStart( DateTime asOf, int withinDays, IReadOnlyList<MonthDayRange> ranges, out bool clamped )
        {
            clamped = false;

            if ( withinDays <= 0 )
            {
                return asOf.Date;
            }

            if ( ranges == null || ranges.Count == 0 )
            {
                return asOf.Date.AddDays( -withinDays );
            }

            var day = asOf.Date;
            int counted = 0;
            int walked = 0;
            int maxWalk = withinDays + MaxWalkbackPadDays;

            while ( counted < withinDays && walked < maxWalk )
            {
                day = day.AddDays( -1 );
                walked++;

                if ( !IsBlackoutDay( day, ranges ) )
                {
                    counted++;
                }
            }

            clamped = counted < withinDays;
            return day;
        }

        /// <summary>
        /// Resolves the window start for a calculation from its stored Blackout Ranges
        /// setting (a defined type guid; blank means the flat window). A configured guid
        /// that no longer resolves to a defined type throws rather than silently reverting
        /// to the flat window: the engine run fails visibly and sinks stay untouched, which
        /// beats months of quietly-wrong gates. Attributes must already be loaded.
        /// </summary>
        public static DateTime ResolveWindowStart( JourneyCalculation calc, int withinDays, DateTime asOf )
        {
            var definedTypeGuid = calc.GetAttributeValue( AttributeKey.BlackoutRanges ).AsGuidOrNull();
            if ( definedTypeGuid == null )
            {
                return asOf.Date.AddDays( -withinDays );
            }

            var definedType = DefinedTypeCache.Get( definedTypeGuid.Value );
            if ( definedType == null )
            {
                throw new InvalidOperationException( $"JourneyCalculation {calc.Id} has Blackout Ranges pointing at defined type '{definedTypeGuid}', which does not exist." );
            }

            var ranges = ParseRanges( definedType.DefinedValues
                .Where( v => v.IsActive )
                .Select( v => v.Value ) );

            var windowStart = GetWindowStart( asOf, withinDays, ranges, out bool clamped );

            if ( clamped )
            {
                // Loud on purpose: this only fires when the blackout config swallows nearly
                // the whole year, and it fires on every evaluation until someone fixes it.
                Rock.Model.ExceptionLogService.LogException( new InvalidOperationException(
                    $"JourneyCalculation {calc.Id} blackout ranges left fewer than {withinDays} countable days in {withinDays + MaxWalkbackPadDays} calendar days; window start clamped to {windowStart:yyyy-MM-dd}." ) );
            }

            return windowStart;
        }
    }
}
