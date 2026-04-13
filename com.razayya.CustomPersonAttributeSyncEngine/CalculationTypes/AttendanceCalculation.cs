using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.CustomPersonAttributeSyncEngine.Constants;
using com.razayya.CustomPersonAttributeSyncEngine.Model;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;

namespace com.razayya.CustomPersonAttributeSyncEngine.CalculationTypes
{
    /// <summary>
    /// Evaluates whether persons attended groups of a specific type
    /// a minimum number of times within a given number of days.
    /// </summary>
    [Description( "Evaluates attendance against group type, minimum count, and date range criteria." )]

    [GroupTypeField( "Group Type",
        Description = "The group type to check attendance for.",
        IsRequired = true,
        Order = 0,
        Key = AttributeKey.GroupType )]

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

    public class AttendanceCalculation : CalculationTypeComponent
    {
        /// <inheritdoc/>
        public override string Title => "Attendance";

        /// <inheritdoc/>
        public override string IconCssClass => "fa fa-calendar-check";

        /// <inheritdoc/>
        public override Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            Calculation calculation,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            var groupTypeGuid = calculation.GetAttributeValue( AttributeKey.GroupType ).AsGuidOrNull();
            var minimumCount = calculation.GetAttributeValue( AttributeKey.MinimumCount ).AsIntegerOrNull() ?? 1;
            var withinDays = calculation.GetAttributeValue( AttributeKey.WithinDays ).AsIntegerOrNull() ?? 90;

            if ( !groupTypeGuid.HasValue )
            {
                return results;
            }

            var sinceDate = RockDateTime.Today.AddDays( -withinDays );

            var attendanceService = new AttendanceService( rockContext );
            var attendanceQuery = attendanceService.Queryable().AsNoTracking()
                .Where( a =>
                    a.DidAttend == true &&
                    a.StartDateTime >= sinceDate &&
                    a.Occurrence.Group != null &&
                    a.Occurrence.Group.GroupType.Guid == groupTypeGuid.Value &&
                    a.PersonAlias != null );

            // Scope to population
            if ( populationPersonIds != null && populationPersonIds.Count > 0 )
            {
                attendanceQuery = attendanceQuery.Where( a => populationPersonIds.Contains( a.PersonAlias.PersonId ) );
            }

            var attendanceSummary = attendanceQuery
                .GroupBy( a => a.PersonAlias.PersonId )
                .Select( g => new
                {
                    PersonId = g.Key,
                    AttendanceCount = g.Count(),
                    LastAttendanceDate = g.Max( a => a.StartDateTime )
                } )
                .Where( s => s.AttendanceCount >= minimumCount )
                .ToList();

            foreach ( var summary in attendanceSummary )
            {
                results[summary.PersonId] = new Dictionary<string, object>
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
