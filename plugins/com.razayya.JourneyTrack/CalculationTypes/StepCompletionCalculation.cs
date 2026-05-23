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
    /// Evaluates whether a person has completed a specific StepType in a StepProgram.
    /// Reads the Step table directly so a Step record is the single source of truth
    /// for step completion. Most useful as a transient calc (no target Person Attribute)
    /// consumed by a Stage's Completion gate and/or the JourneyProgram-level rollup.
    /// </summary>
    [Description( "Checks whether a person has a Step record for the configured StepType." )]

    [StepProgramStepTypeField( "Step Type",
        Description = "The StepProgram + StepType whose completion to check. Stored as 'StepProgramGuid|StepTypeGuid'.",
        IsRequired = true,
        Order = 0,
        Key = AttributeKey.StepType )]

    [BooleanField( "Require Completed Status",
        Description = "When enabled (default), only Step records whose StepStatus has IsCompleteStatus = true count as a match. Disable to also count in-progress/started Steps.",
        IsRequired = true,
        DefaultBooleanValue = true,
        Order = 1,
        Key = AttributeKey.RequireCompletedStatus )]

    public class StepCompletionCalculation : JourneyCalculationTypeComponent
    {
        /// <inheritdoc/>
        public override string Title => "Step Completion";

        /// <inheritdoc/>
        public override string IconCssClass => "fa fa-flag-checkered";

        /// <inheritdoc/>
        public override Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            JourneyCalculation calc,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            // StepProgramStepTypeField stores value as "ProgramGuid|StepTypeGuid".
            var raw = calc.GetAttributeValue( AttributeKey.StepType );
            if ( string.IsNullOrWhiteSpace( raw ) )
            {
                return results;
            }
            var parts = raw.Split( '|' );
            var stepTypeGuid = parts.Length >= 2 ? parts[1].AsGuidOrNull() : raw.AsGuidOrNull();
            if ( !stepTypeGuid.HasValue )
            {
                return results;
            }

            var stepType = new StepTypeService( rockContext ).Get( stepTypeGuid.Value );
            if ( stepType == null )
            {
                return results;
            }

            var requireCompleted = calc.GetAttributeValue( AttributeKey.RequireCompletedStatus ).AsBooleanOrNull() ?? true;

            // Single batched query: Step JOIN PersonAlias JOIN StepStatus
            // filtered by StepTypeId + populationPersonIds, grouped by PersonId.
            var query = new StepService( rockContext ).Queryable().AsNoTracking()
                .Where( s => s.StepTypeId == stepType.Id );

            if ( requireCompleted )
            {
                query = query.Where( s => s.StepStatus != null && s.StepStatus.IsCompleteStatus );
            }

            if ( populationPersonIds != null && populationPersonIds.Count > 0 )
            {
                query = query.Where( s => populationPersonIds.Contains( s.PersonAlias.PersonId ) );
            }

            var rows = query
                .Select( s => new
                {
                    PersonId = s.PersonAlias.PersonId,
                    s.CompletedDateTime,
                    StepStatusId = s.StepStatusId,
                    StepStatusName = s.StepStatus != null ? s.StepStatus.Name : null
                } )
                .ToList();

            var stepNumber = stepType.Order + 1;

            foreach ( var personGroup in rows.GroupBy( r => r.PersonId ) )
            {
                // Pick the earliest completed Step row for stable merge fields.
                var first = personGroup
                    .OrderBy( r => r.CompletedDateTime ?? System.DateTime.MaxValue )
                    .ThenBy( r => r.StepStatusId )
                    .First();

                results[personGroup.Key] = new Dictionary<string, object>
                {
                    { "Matched", true },
                    { "CompletedDateTime", first.CompletedDateTime },
                    { "StepStatusName", first.StepStatusName },
                    { "StepStatusId", first.StepStatusId },
                    { "StepTypeId", stepType.Id },
                    { "StepNumber", stepNumber }
                };
            }

            return results;
        }

        /// <inheritdoc/>
        public override List<MergeFieldInfo> GetMergeFields()
        {
            return new List<MergeFieldInfo>
            {
                new MergeFieldInfo { Name = "Matched", Description = "True if person has a matching Step record.", DataType = "Boolean" },
                new MergeFieldInfo { Name = "CompletedDateTime", Description = "Earliest CompletedDateTime on the person's matching Step rows.", DataType = "DateTime" },
                new MergeFieldInfo { Name = "StepStatusName", Description = "Name of the StepStatus on the matching Step.", DataType = "String" },
                new MergeFieldInfo { Name = "StepStatusId", Description = "Id of the StepStatus on the matching Step.", DataType = "Integer" },
                new MergeFieldInfo { Name = "StepTypeId", Description = "Id of the configured StepType.", DataType = "Integer" },
                new MergeFieldInfo { Name = "StepNumber", Description = "1-based step number = StepType.Order + 1.", DataType = "Integer" }
            };
        }
    }
}
