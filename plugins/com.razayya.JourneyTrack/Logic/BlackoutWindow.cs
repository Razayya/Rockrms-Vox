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
    /// Each defined value is one named range: its Value is the label staff see, and its
    /// Start Date / End Date attributes (Month Day field type, stored "M/d") are the
    /// endpoints, both inclusive. A start later than its end wraps across year-end.
    ///
    /// The window start is the LATEST date D on or before AsOf such that the count of
    /// non-blackout days in [D, AsOf) — D included, AsOf excluded — reaches WithinDays.
    /// With no ranges configured this is exactly AsOf - WithinDays, so a blank setting is
    /// a true no-op. AsOf's own blackout status never affects the result, and the returned
    /// date always lands on a countable (non-blackout) day.
    ///
    /// dbo._com_razayya_JourneyTrack_ufnAttendanceWindowStart (plugin migration 018) mirrors
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
        /// (e.g. 7/5 => 705). StartMonthDay later than EndMonthDay wraps across year-end.
        /// </summary>
        public struct MonthDayRange
        {
            public int StartMonthDay;
            public int EndMonthDay;
        }

        /// <summary>
        /// Builds one range from a defined value's label and its Start Date / End Date
        /// attribute text. Throws on a blank or malformed endpoint rather than skipping
        /// the value — a silently-dropped range would shorten the window without anyone
        /// noticing, which is exactly the drift this feature exists to prevent.
        /// </summary>
        public static MonthDayRange ParseRange( string label, string startText, string endText )
        {
            var start = ParseMonthDay( startText );
            var end = ParseMonthDay( endText );

            if ( start == null || end == null )
            {
                throw new FormatException( $"Blackout range '{label}' needs a valid Start Date and End Date (month/day); got Start Date '{startText}' and End Date '{endText}'." );
            }

            return new MonthDayRange { StartMonthDay = start.Value, EndMonthDay = end.Value };
        }

        /// <summary>
        /// Parses a Month Day field value ("M/d", zero padding tolerated) into month*100+day,
        /// or null when it is blank or not a plausible month/day. Pure month-day parsing on
        /// purpose: Rock's MonthDayStringAsDateTime validates against the current year, which
        /// would reject 2/29 three years in four.
        /// </summary>
        public static int? ParseMonthDay( string text )
        {
            if ( string.IsNullOrWhiteSpace( text ) )
            {
                return null;
            }

            var parts = text.Trim().Split( '/' );
            if ( parts.Length != 2 )
            {
                return null;
            }

            var month = parts[0].AsIntegerOrNull();
            var day = parts[1].AsIntegerOrNull();

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
        /// Reads every active value of the defined type into ranges via its Start Date /
        /// End Date attributes. Throws on the first value that cannot be parsed.
        /// </summary>
        public static List<MonthDayRange> LoadRanges( DefinedTypeCache definedType )
        {
            return definedType.DefinedValues
                .Where( v => v.IsActive )
                .Select( v => ParseRange(
                    v.Value,
                    v.GetAttributeValue( AttributeKey.BlackoutStartDate ),
                    v.GetAttributeValue( AttributeKey.BlackoutEndDate ) ) )
                .ToList();
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

            var ranges = LoadRanges( definedType );

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
