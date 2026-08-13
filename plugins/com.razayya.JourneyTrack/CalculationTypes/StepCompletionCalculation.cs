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

        // Per-(StepTypeId, RequireCompleted) global-Step cache. Within a sync, all 7 DP
        // StepCompletion calcs (1 per stage) hit this once each, but across a 100-person
        // batch the FIRST person warms the cache and the next 99 hit it. 30s TTL.
        private struct StepInfo
        {
            public System.DateTime? CompletedDateTime;
            public System.DateTime? LastCompletedDateTime;
            public int? StepStatusId;
            public string StepStatusName;
        }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<(int StepTypeId, bool RequireCompleted), (System.DateTime CachedAt, Dictionary<int, StepInfo> Map)> _stepCache
            = new System.Collections.Concurrent.ConcurrentDictionary<(int, bool), (System.DateTime, Dictionary<int, StepInfo>)>();
        private static readonly System.TimeSpan _cacheTtl = System.TimeSpan.FromSeconds( 30 );

        private static Dictionary<int, StepInfo> GetStepInfo( int stepTypeId, bool requireCompleted, RockContext rockContext )
        {
            var key = (stepTypeId, requireCompleted);
            if ( _stepCache.TryGetValue( key, out var cached )
                && ( System.DateTime.UtcNow - cached.CachedAt ) < _cacheTtl )
            {
                return cached.Map;
            }

            var query = new StepService( rockContext ).Queryable().AsNoTracking()
                .Where( s => s.StepTypeId == stepTypeId );
            if ( requireCompleted )
            {
                query = query.Where( s => s.StepStatus != null && s.StepStatus.IsCompleteStatus );
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

            var map = new Dictionary<int, StepInfo>();
            foreach ( var personGroup in rows.GroupBy( r => r.PersonId ) )
            {
                var first = personGroup
                    .OrderBy( r => r.CompletedDateTime ?? System.DateTime.MaxValue )
                    .ThenBy( r => r.StepStatusId )
                    .First();

                map[personGroup.Key] = new StepInfo
                {
                    CompletedDateTime = first.CompletedDateTime,
                    LastCompletedDateTime = personGroup.Max( r => r.CompletedDateTime ),
                    StepStatusId = first.StepStatusId,
                    StepStatusName = first.StepStatusName
                };
            }

            _stepCache[key] = ( System.DateTime.UtcNow, map );
            return map;
        }

        /// <inheritdoc/>
        public override Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            JourneyCalculation calc,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            // StepProgramStepTypeField stores value as "ProgramGuid|StepTypeGuid".
            var raw = calc.GetAttributeValue( AttributeKey.StepType );
            if ( string.IsNullOrWhiteSpace( raw ) ) return results;
            var parts = raw.Split( '|' );
            var stepTypeGuid = parts.Length >= 2 ? parts[1].AsGuidOrNull() : raw.AsGuidOrNull();
            if ( !stepTypeGuid.HasValue ) return results;

            var stepType = new StepTypeService( rockContext ).Get( stepTypeGuid.Value );
            if ( stepType == null ) return results;

            var requireCompleted = calc.GetAttributeValue( AttributeKey.RequireCompletedStatus ).AsBooleanOrNull() ?? true;

            var stepInfoMap = GetStepInfo( stepType.Id, requireCompleted, rockContext );
            if ( stepInfoMap.Count == 0 ) return results;

            var stepNumber = stepType.Order + 1;

            foreach ( var personId in populationPersonIds )
            {
                if ( !stepInfoMap.TryGetValue( personId, out var info ) ) continue;

                results[personId] = new Dictionary<string, object>
                {
                    { "Matched", true },
                    { "CompletedDateTime", info.CompletedDateTime },
                    { "LastCompletedDateTime", info.LastCompletedDateTime },
                    { "StepStatusName", info.StepStatusName },
                    { "StepStatusId", info.StepStatusId },
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
                new MergeFieldInfo { Name = "LastCompletedDateTime", Description = "Most recent CompletedDateTime on the person's matching Step rows.", DataType = "DateTime" },
                new MergeFieldInfo { Name = "StepStatusName", Description = "Name of the StepStatus on the matching Step.", DataType = "String" },
                new MergeFieldInfo { Name = "StepStatusId", Description = "Id of the StepStatus on the matching Step.", DataType = "Integer" },
                new MergeFieldInfo { Name = "StepTypeId", Description = "Id of the configured StepType.", DataType = "Integer" },
                new MergeFieldInfo { Name = "StepNumber", Description = "1-based step number = StepType.Order + 1.", DataType = "Integer" }
            };
        }
    }
}
