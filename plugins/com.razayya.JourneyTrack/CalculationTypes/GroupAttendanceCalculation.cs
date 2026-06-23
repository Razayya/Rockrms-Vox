using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.JourneyTrack.Constants;
using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;

namespace com.razayya.JourneyTrack.CalculationTypes
{
    /// <summary>
    /// Evaluates whether persons attended specific, individually-chosen groups a minimum
    /// number of times within a given number of days. Unlike Group Type Attendance (which
    /// matches any group belonging to a whole group type), this matches attendance only in
    /// the explicitly selected groups. The group selection is captured by the cascading
    /// Group Type → Groups picker on the detail page and stored as a list of Group Guids.
    /// </summary>
    [Description( "Evaluates attendance against specific groups, a minimum count, and a date range." )]

    [TextField( "Groups",
        Description = "The specific groups whose attendance is checked. Managed by the Group Type / Groups picker; stored as a comma-delimited list of Group Guids.",
        IsRequired = false,
        Order = 0,
        Key = AttributeKey.Groups_GroupAttendance )]

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

    public class GroupAttendanceCalculation : JourneyCalculationTypeComponent
    {
        /// <inheritdoc/>
        public override string Title => "Group Attendance";

        /// <inheritdoc/>
        public override string IconCssClass => "fa fa-calendar-check";

        // Per-(group guids set, withinDays) cache. Stores all candidate persons +
        // their attendance counts; the per-calc MinimumCount filter is applied at lookup.
        private struct AttendanceSummary
        {
            public int AttendanceCount;
            public System.DateTime LastAttendanceDate;
        }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (System.DateTime CachedAt, Dictionary<int, AttendanceSummary> Map)> _attCache
            = new System.Collections.Concurrent.ConcurrentDictionary<string, (System.DateTime, Dictionary<int, AttendanceSummary>)>();
        private static readonly System.TimeSpan _cacheTtl = System.TimeSpan.FromSeconds( 30 );

        private static Dictionary<int, AttendanceSummary> GetAttendanceSummaries( List<System.Guid> groupGuids, int withinDays, RockContext rockContext )
        {
            var key = string.Join( ",", groupGuids.OrderBy( g => g ) ) + "|" + withinDays;
            if ( _attCache.TryGetValue( key, out var cached )
                && ( System.DateTime.UtcNow - cached.CachedAt ) < _cacheTtl )
            {
                return cached.Map;
            }

            var sinceDate = RockDateTime.Today.AddDays( -withinDays );
            var rows = new AttendanceService( rockContext ).Queryable().AsNoTracking()
                .Where( a =>
                    a.DidAttend == true &&
                    a.StartDateTime >= sinceDate &&
                    a.Occurrence.Group != null &&
                    groupGuids.Contains( a.Occurrence.Group.Guid ) &&
                    a.PersonAlias != null )
                .GroupBy( a => a.PersonAlias.PersonId )
                .Select( g => new
                {
                    PersonId = g.Key,
                    AttendanceCount = g.Count(),
                    LastAttendanceDate = g.Max( a => a.StartDateTime )
                } )
                .ToList();

            var map = rows.ToDictionary(
                r => r.PersonId,
                r => new AttendanceSummary { AttendanceCount = r.AttendanceCount, LastAttendanceDate = r.LastAttendanceDate } );

            _attCache[key] = ( System.DateTime.UtcNow, map );
            return map;
        }

        /// <inheritdoc/>
        public override Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            JourneyCalculation calc,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            var groupGuids = calc.GetAttributeValue( AttributeKey.Groups_GroupAttendance )
                .SplitDelimitedValues()
                .AsGuidList();
            var minimumCount = calc.GetAttributeValue( AttributeKey.MinimumCount ).AsIntegerOrNull() ?? 1;
            var withinDays = calc.GetAttributeValue( AttributeKey.WithinDays ).AsIntegerOrNull() ?? 90;

            if ( !groupGuids.Any() ) return results;
            if ( populationPersonIds == null || populationPersonIds.Count == 0 ) return results;

            var attMap = GetAttendanceSummaries( groupGuids, withinDays, rockContext );
            if ( attMap.Count == 0 ) return results;

            foreach ( var personId in populationPersonIds )
            {
                if ( !attMap.TryGetValue( personId, out var summary ) ) continue;
                if ( summary.AttendanceCount < minimumCount ) continue;

                results[personId] = new Dictionary<string, object>
                {
                    { "Matched", true },
                    { "AttendanceCount", summary.AttendanceCount },
                    { "LastAttendanceDate", summary.LastAttendanceDate }
                };
            }

            return results;
        }

        /// <inheritdoc/>
        public override List<MergeFieldInfo> GetMergeFields()
        {
            return new List<MergeFieldInfo>
            {
                new MergeFieldInfo { Name = "Matched", Description = "True if attendance criteria were met.", DataType = "Boolean" },
                new MergeFieldInfo { Name = "AttendanceCount", Description = "Number of attendances in the date range.", DataType = "Integer" },
                new MergeFieldInfo { Name = "LastAttendanceDate", Description = "Most recent attendance date.", DataType = "DateTime" }
            };
        }
    }
}
