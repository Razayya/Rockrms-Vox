using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.JourneyTrack.Constants;
using com.razayya.JourneyTrack.Logic;
using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;

namespace com.razayya.JourneyTrack.CalculationTypes
{
    /// <summary>
    /// Evaluates whether persons attended any group of the selected group type(s)
    /// a minimum number of times within a given number of days. For attendance scoped
    /// to specific, individually-chosen groups rather than whole group types, see
    /// GroupAttendanceCalculation.
    /// </summary>
    [Description( "Evaluates attendance against group type(s), a minimum count, and a date range." )]

    [GroupTypesField( "Group Types",
        Description = "The group types to check attendance for.",
        IsRequired = true,
        Order = 0,
        Key = AttributeKey.GroupTypes )]

    [IntegerField( "Minimum Count",
        Description = "The minimum number of times a person must have attended.",
        IsRequired = true,
        DefaultIntegerValue = 1,
        Order = 1,
        Key = AttributeKey.MinimumCount )]

    [IntegerField( "Within Days",
        Description = "The number of days back from today to check for attendance.",
        IsRequired = true,
        DefaultIntegerValue = 90,
        Order = 2,
        Key = AttributeKey.WithinDays )]

    [DefinedTypeField( "Blackout Ranges",
        Description = "Optional. A defined type whose active values are recurring annual date ranges (MM-dd|MM-dd, both ends inclusive; a start later than its end wraps across year-end). Days inside a range do not consume the Within Days budget, so the window stretches across planned breaks. Attendance during a range still counts. Blank keeps the flat window.",
        IsRequired = false,
        Order = 3,
        Key = AttributeKey.BlackoutRanges )]

    public class AttendanceCalculation : JourneyCalculationTypeComponent
    {
        /// <inheritdoc/>
        public override string Title => "Group Type Attendance";

        /// <inheritdoc/>
        public override string IconCssClass => "fa fa-calendar-check";

        /// <inheritdoc/>
        // Per-(groupTypeGuids set, windowStart) cache. Stores all candidate persons +
        // their attendance counts; the per-calc MinimumCount filter is applied at lookup.
        // Keyed by the RESOLVED window start (not WithinDays) so blackout stretching is
        // part of the identity; calcs with identical config still share one entry.
        private struct AttendanceSummary
        {
            public int AttendanceCount;
            public System.DateTime FirstAttendanceDate;
            public System.DateTime LastAttendanceDate;
        }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (System.DateTime CachedAt, Dictionary<int, AttendanceSummary> Map)> _attCache
            = new System.Collections.Concurrent.ConcurrentDictionary<string, (System.DateTime, Dictionary<int, AttendanceSummary>)>();
        private static readonly System.TimeSpan _cacheTtl = System.TimeSpan.FromSeconds( 30 );

        private static Dictionary<int, AttendanceSummary> GetAttendanceSummaries( List<System.Guid> groupTypeGuids, System.DateTime windowStart, RockContext rockContext )
        {
            var key = string.Join( ",", groupTypeGuids.OrderBy( g => g ) ) + "|" + windowStart.ToString( "yyyy-MM-dd" );
            if ( _attCache.TryGetValue( key, out var cached )
                && ( System.DateTime.UtcNow - cached.CachedAt ) < _cacheTtl )
            {
                return cached.Map;
            }

            var sinceDate = windowStart;
            var rows = new AttendanceService( rockContext ).Queryable().AsNoTracking()
                .Where( a =>
                    a.DidAttend == true &&
                    a.StartDateTime >= sinceDate &&
                    a.Occurrence.Group != null &&
                    groupTypeGuids.Contains( a.Occurrence.Group.GroupType.Guid ) &&
                    a.PersonAlias != null )
                .GroupBy( a => a.PersonAlias.PersonId )
                .Select( g => new
                {
                    PersonId = g.Key,
                    AttendanceCount = g.Count(),
                    FirstAttendanceDate = g.Min( a => a.StartDateTime ),
                    LastAttendanceDate = g.Max( a => a.StartDateTime )
                } )
                .ToList();

            var map = rows.ToDictionary(
                r => r.PersonId,
                r => new AttendanceSummary { AttendanceCount = r.AttendanceCount, FirstAttendanceDate = r.FirstAttendanceDate, LastAttendanceDate = r.LastAttendanceDate } );

            _attCache[key] = ( System.DateTime.UtcNow, map );
            return map;
        }

        /// <summary>
        /// Single-person summary for render-path evaluations: a fresh shared entry
        /// is a free hit; otherwise query only this person's attendance rows
        /// instead of building the all-persons map. The shared cache is not
        /// written here — population runs keep their own rebuild cadence.
        /// </summary>
        private static AttendanceSummary? GetPersonAttendanceSummary( List<System.Guid> groupTypeGuids, System.DateTime windowStart, int personId, RockContext rockContext )
        {
            var key = string.Join( ",", groupTypeGuids.OrderBy( g => g ) ) + "|" + windowStart.ToString( "yyyy-MM-dd" );
            if ( _attCache.TryGetValue( key, out var cached )
                && ( System.DateTime.UtcNow - cached.CachedAt ) < _cacheTtl )
            {
                return cached.Map.TryGetValue( personId, out var hit ) ? hit : ( AttendanceSummary? ) null;
            }

            var sinceDate = windowStart;
            var row = new AttendanceService( rockContext ).Queryable().AsNoTracking()
                .Where( a =>
                    a.DidAttend == true &&
                    a.StartDateTime >= sinceDate &&
                    a.Occurrence.Group != null &&
                    groupTypeGuids.Contains( a.Occurrence.Group.GroupType.Guid ) &&
                    a.PersonAlias != null &&
                    a.PersonAlias.PersonId == personId )
                .GroupBy( a => a.PersonAlias.PersonId )
                .Select( g => new
                {
                    AttendanceCount = g.Count(),
                    FirstAttendanceDate = g.Min( a => a.StartDateTime ),
                    LastAttendanceDate = g.Max( a => a.StartDateTime )
                } )
                .FirstOrDefault();

            if ( row == null )
            {
                return null;
            }

            return new AttendanceSummary { AttendanceCount = row.AttendanceCount, FirstAttendanceDate = row.FirstAttendanceDate, LastAttendanceDate = row.LastAttendanceDate };
        }

        public override Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            JourneyCalculation calc,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            var groupTypeGuids = calc.GetAttributeValue( AttributeKey.GroupTypes )
                .SplitDelimitedValues()
                .AsGuidList();
            var minimumCount = calc.GetAttributeValue( AttributeKey.MinimumCount ).AsIntegerOrNull() ?? 1;
            var withinDays = calc.GetAttributeValue( AttributeKey.WithinDays ).AsIntegerOrNull() ?? 90;

            if ( !groupTypeGuids.Any() ) return results;
            if ( populationPersonIds == null || populationPersonIds.Count == 0 ) return results;

            var asOf = RockDateTime.Today;
            var windowStart = BlackoutWindow.ResolveWindowStart( calc, withinDays, asOf );
            var effectiveWithinDays = ( int ) ( asOf - windowStart ).TotalDays;

            // Render-path fast path: a single-person evaluation queries only that
            // person's attendance rather than the all-persons summary map.
            if ( populationPersonIds.Count == 1 )
            {
                var singlePersonId = populationPersonIds.First();
                var personSummary = GetPersonAttendanceSummary( groupTypeGuids, windowStart, singlePersonId, rockContext );
                if ( personSummary.HasValue && personSummary.Value.AttendanceCount >= minimumCount )
                {
                    results[singlePersonId] = new Dictionary<string, object>
                    {
                        { "Matched", true },
                        { "AttendanceCount", personSummary.Value.AttendanceCount },
                        { "FirstAttendanceDate", personSummary.Value.FirstAttendanceDate },
                        { "LastAttendanceDate", personSummary.Value.LastAttendanceDate },
                        { "WindowStartDate", windowStart },
                        { "EffectiveWithinDays", effectiveWithinDays }
                    };
                }
                return results;
            }

            var attMap = GetAttendanceSummaries( groupTypeGuids, windowStart, rockContext );
            if ( attMap.Count == 0 ) return results;

            foreach ( var personId in populationPersonIds )
            {
                if ( !attMap.TryGetValue( personId, out var summary ) ) continue;
                if ( summary.AttendanceCount < minimumCount ) continue;

                results[personId] = new Dictionary<string, object>
                {
                    { "Matched", true },
                    { "AttendanceCount", summary.AttendanceCount },
                    { "FirstAttendanceDate", summary.FirstAttendanceDate },
                    { "LastAttendanceDate", summary.LastAttendanceDate },
                    { "WindowStartDate", windowStart },
                    { "EffectiveWithinDays", effectiveWithinDays }
                };
            }

            return results;
        }

        /// <inheritdoc/>
        // The summary cache holds counts for everyone with >=1 attendance — the
        // MinimumCount filter is only applied at lookup — so partial progress
        // ("3 of 4") is a straight map lookup, no extra query.
        public override Dictionary<string, object> DescribeProgress(
            RockContext rockContext,
            JourneyCalculation calc,
            int personId )
        {
            var groupTypeGuids = calc.GetAttributeValue( AttributeKey.GroupTypes )
                .SplitDelimitedValues()
                .AsGuidList();
            var minimumCount = calc.GetAttributeValue( AttributeKey.MinimumCount ).AsIntegerOrNull() ?? 1;
            var withinDays = calc.GetAttributeValue( AttributeKey.WithinDays ).AsIntegerOrNull() ?? 90;

            var asOf = RockDateTime.Today;
            var windowStart = BlackoutWindow.ResolveWindowStart( calc, withinDays, asOf );
            var effectiveWithinDays = ( int ) ( asOf - windowStart ).TotalDays;

            int count = 0;
            System.DateTime? firstAttendance = null;
            System.DateTime? lastAttendance = null;

            if ( groupTypeGuids.Any() )
            {
                var personSummary = GetPersonAttendanceSummary( groupTypeGuids, windowStart, personId, rockContext );
                if ( personSummary.HasValue )
                {
                    count = personSummary.Value.AttendanceCount;
                    firstAttendance = personSummary.Value.FirstAttendanceDate;
                    lastAttendance = personSummary.Value.LastAttendanceDate;
                }
            }

            return new Dictionary<string, object>
            {
                { "Matched", count >= minimumCount },
                { "Current", ( decimal ) count },
                { "Target", ( decimal ) minimumCount },
                { "AttendanceCount", count },
                { "Remaining", System.Math.Max( 0, minimumCount - count ) },
                { "WithinDays", withinDays },
                { "EffectiveWithinDays", effectiveWithinDays },
                { "WindowStartDate", windowStart },
                { "FirstAttendanceDate", firstAttendance },
                { "LastAttendanceDate", lastAttendance }
            };
        }

        /// <inheritdoc/>
        public override List<MergeFieldInfo> GetMergeFields()
        {
            return new List<MergeFieldInfo>
            {
                new MergeFieldInfo { Name = "Matched", Description = "True if attendance criteria were met.", DataType = "Boolean" },
                new MergeFieldInfo { Name = "AttendanceCount", Description = "Number of attendances in the date range.", DataType = "Integer" },
                new MergeFieldInfo { Name = "FirstAttendanceDate", Description = "Earliest attendance date in the date range.", DataType = "DateTime" },
                new MergeFieldInfo { Name = "LastAttendanceDate", Description = "Most recent attendance date.", DataType = "DateTime" },
                new MergeFieldInfo { Name = "WindowStartDate", Description = "Start of the effective attendance window, stretched past any blackout ranges.", DataType = "DateTime" },
                new MergeFieldInfo { Name = "EffectiveWithinDays", Description = "Calendar length of the effective window; equals Within Days when no blackout overlaps it.", DataType = "Integer" }
            };
        }
    }
}
