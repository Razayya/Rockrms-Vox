using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

using com.razayya.CustomPersonAttributeSyncEngine.CalculationTypes;
using com.razayya.CustomPersonAttributeSyncEngine.Model;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace com.razayya.CustomPersonAttributeSyncEngine.Data
{
    /// <summary>
    /// Shared service that encapsulates the sync engine's evaluation and write logic.
    /// Used by both the nightly job and on-demand UI execution.
    /// </summary>
    public class SyncEngineService
    {
        /// <summary>
        /// Gets or sets the PersonAliasId of the user who triggered this run.
        /// Null when triggered by the nightly job.
        /// </summary>
        public int? RunByPersonAliasId { get; set; }

        /// <summary>
        /// Processes all active Calculation Groups.
        /// </summary>
        public SyncResult ProcessAllGroups()
        {
            var result = new SyncResult();

            using ( var rockContext = new RockContext() )
            {
                var groups = new CalculationGroupService( rockContext ).Queryable()
                    .Where( g => g.IsActive )
                    .OrderBy( g => g.Order )
                    .ThenBy( g => g.Name )
                    .ToList();

                foreach ( var group in groups )
                {
                    try
                    {
                        var groupResult = ProcessGroup( group.Id );
                        result.Merge( groupResult );
                    }
                    catch ( Exception ex )
                    {
                        result.Errors.Add( $"Group '{group.Name}': {ex.Message}" );
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Processes a single person through a Calculation Group.
        /// </summary>
        public SyncResult ProcessGroupForPerson( int calculationGroupId, int personId )
        {
            var result = new SyncResult();

            using ( var rockContext = new RockContext() )
            {
                var group = new CalculationGroupService( rockContext ).Get( calculationGroupId );
                if ( group == null )
                {
                    result.Errors.Add( $"Calculation Group Id {calculationGroupId} not found." );
                    return result;
                }

                var singlePersonPopulation = new HashSet<int> { personId };
                result.Log.Add( $"Group '{group.Name}': single-person sync for PersonId {personId}" );

                var subGroups = new CalculationSubGroupService( rockContext ).Queryable()
                    .Where( sg => sg.CalculationGroupId == group.Id && sg.IsActive )
                    .OrderBy( sg => sg.Order )
                    .ThenBy( sg => sg.Name )
                    .ToList();

                var subGroupPassers = new Dictionary<int, HashSet<int>>();

                foreach ( var subGroup in subGroups )
                {
                    try
                    {
                        var subResult = ProcessSubGroupInternal( subGroup, singlePersonPopulation, subGroupPassers, rockContext );
                        result.Merge( subResult.Result );
                        subGroupPassers[subGroup.Id] = subResult.Passers;
                    }
                    catch ( Exception ex )
                    {
                        result.Errors.Add( $"SubGroup '{subGroup.Name}': {ex.Message}" );
                        subGroupPassers[subGroup.Id] = singlePersonPopulation;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Processes a single Calculation Group by Id.
        /// </summary>
        public SyncResult ProcessGroup( int calculationGroupId )
        {
            var result = new SyncResult();

            using ( var rockContext = new RockContext() )
            {
                var group = new CalculationGroupService( rockContext ).Get( calculationGroupId );
                if ( group == null )
                {
                    result.Errors.Add( $"Calculation Group Id {calculationGroupId} not found." );
                    return result;
                }

                var basePopulation = BuildBasePopulation( group, rockContext );
                result.Log.Add( $"Group '{group.Name}': base population {basePopulation.Count}" );

                var subGroups = new CalculationSubGroupService( rockContext ).Queryable()
                    .Where( sg => sg.CalculationGroupId == group.Id && sg.IsActive )
                    .OrderBy( sg => sg.Order )
                    .ThenBy( sg => sg.Name )
                    .ToList();

                var subGroupPassers = new Dictionary<int, HashSet<int>>();

                foreach ( var subGroup in subGroups )
                {
                    try
                    {
                        var subResult = ProcessSubGroupInternal( subGroup, basePopulation, subGroupPassers, rockContext );
                        result.Merge( subResult.Result );
                        subGroupPassers[subGroup.Id] = subResult.Passers;
                    }
                    catch ( Exception ex )
                    {
                        result.Errors.Add( $"SubGroup '{subGroup.Name}': {ex.Message}" );
                        subGroupPassers[subGroup.Id] = basePopulation;
                    }
                }

                group.LastRunDateTime = RockDateTime.Now;
                rockContext.SaveChanges();
            }

            return result;
        }

        /// <summary>
        /// Processes a single SubGroup by Id, building its parent group's population context.
        /// </summary>
        public SyncResult ProcessSubGroup( int calculationSubGroupId )
        {
            using ( var rockContext = new RockContext() )
            {
                var subGroup = new CalculationSubGroupService( rockContext ).Queryable()
                    .Include( sg => sg.CalculationGroup )
                    .FirstOrDefault( sg => sg.Id == calculationSubGroupId );

                if ( subGroup == null )
                {
                    return new SyncResult { Errors = { $"SubGroup Id {calculationSubGroupId} not found." } };
                }

                var basePopulation = BuildBasePopulation( subGroup.CalculationGroup, rockContext );
                var internalResult = ProcessSubGroupInternal( subGroup, basePopulation, new Dictionary<int, HashSet<int>>(), rockContext );
                return internalResult.Result;
            }
        }

        /// <summary>
        /// Processes a single Calculation by Id against the full parent population.
        /// </summary>
        public SyncResult ProcessCalculation( int calculationId, HashSet<int> personIdOverride = null )
        {
            using ( var rockContext = new RockContext() )
            {
                var calc = new CalculationService( rockContext ).Queryable()
                    .Include( c => c.CalculationTypeEntityType )
                    .Include( c => c.CalculationSubGroup.CalculationGroup )
                    .FirstOrDefault( c => c.Id == calculationId );

                if ( calc == null )
                {
                    return new SyncResult { Errors = { $"Calculation Id {calculationId} not found." } };
                }

                HashSet<int> population;
                if ( personIdOverride != null && personIdOverride.Count > 0 )
                {
                    population = personIdOverride;
                }
                else
                {
                    population = BuildBasePopulation( calc.CalculationSubGroup.CalculationGroup, rockContext );
                }

                return ExecuteCalculation( calc, population, rockContext );
            }
        }

        /// <summary>
        /// Processes a single Calculation for a single person.
        /// </summary>
        public SyncResult ProcessCalculationForPerson( int calculationId, int personId )
        {
            return ProcessCalculation( calculationId, new HashSet<int> { personId } );
        }

        /// <summary>
        /// Evaluates a calculation without writing any values. Returns preview data.
        /// </summary>
        public PreviewResult PreviewCalculation( int calculationId, HashSet<int> personIdOverride = null, int maxResults = 200 )
        {
            var preview = new PreviewResult();

            using ( var rockContext = new RockContext() )
            {
                var calc = new CalculationService( rockContext ).Queryable()
                    .Include( c => c.CalculationTypeEntityType )
                    .Include( c => c.CalculationSubGroup.CalculationGroup )
                    .Include( c => c.PersonAttribute )
                    .FirstOrDefault( c => c.Id == calculationId );

                if ( calc == null )
                {
                    preview.ErrorMessage = $"Calculation Id {calculationId} not found.";
                    return preview;
                }

                // Build population
                HashSet<int> population;
                if ( personIdOverride != null && personIdOverride.Count > 0 )
                {
                    population = personIdOverride;
                }
                else
                {
                    population = BuildBasePopulation( calc.CalculationSubGroup.CalculationGroup, rockContext );
                }

                preview.TotalPopulation = population.Count;

                if ( maxResults == 0 )
                {
                    return preview;
                }

                // Resolve component and evaluate
                var entityType = EntityTypeCache.Get( calc.CalculationTypeEntityTypeId );
                var component = CalculationTypeComponent.GetComponent( entityType?.Name );
                if ( component == null )
                {
                    preview.ErrorMessage = $"Component '{entityType?.Name}' not found.";
                    return preview;
                }

                calc.LoadAttributes( rockContext );
                var matchedResults = component.Evaluate( rockContext, calc, population );
                preview.MatchedCount = matchedResults.Count;

                var targetAttribute = AttributeCache.Get( calc.PersonAttributeId );
                if ( targetAttribute == null )
                {
                    preview.ErrorMessage = $"Target attribute Id {calc.PersonAttributeId} not found.";
                    return preview;
                }

                // Build preview rows — sample matched + some non-matched
                var matchedSample = matchedResults.Keys.Take( maxResults - 20 ).ToList();
                var nonMatchedSample = population.Where( id => !matchedResults.ContainsKey( id ) ).Take( 20 ).ToList();
                var sampleIds = matchedSample.Concat( nonMatchedSample ).ToList();

                var persons = new PersonService( rockContext ).Queryable().AsNoTracking()
                    .Where( p => sampleIds.Contains( p.Id ) )
                    .Select( p => new { p.Id, p.NickName, p.LastName } )
                    .ToList();

                // Batch-load the single target attribute value for all sample persons
                var currentValueLookup = new AttributeValueService( rockContext ).Queryable().AsNoTracking()
                    .Where( av => av.AttributeId == targetAttribute.Id && av.EntityId.HasValue && sampleIds.Contains( av.EntityId.Value ) )
                    .ToDictionary( av => av.EntityId.Value, av => av.Value ?? string.Empty );

                foreach ( var person in persons )
                {
                    currentValueLookup.TryGetValue( person.Id, out var currentValue );
                    currentValue = currentValue ?? string.Empty;

                    string newValue;
                    string action;

                    if ( matchedResults.TryGetValue( person.Id, out var mergeFields ) )
                    {
                        newValue = ResolveMatchValue( calc, mergeFields );
                        action = string.Equals( currentValue, newValue, StringComparison.OrdinalIgnoreCase ) ? "No Change" : "Update";
                    }
                    else
                    {
                        if ( calc.NoMatchBehavior == NoMatchBehavior.LeaveUnchanged )
                        {
                            newValue = currentValue;
                            action = "Skip";
                        }
                        else
                        {
                            newValue = ResolveNoMatchValue( calc );
                            action = string.Equals( currentValue, newValue, StringComparison.OrdinalIgnoreCase ) ? "No Change" : "Update";
                        }
                    }

                    preview.Rows.Add( new PreviewRow
                    {
                        PersonId = person.Id,
                        PersonName = $"{person.NickName} {person.LastName}",
                        CurrentValue = currentValue,
                        NewValue = newValue,
                        Action = action
                    } );
                }

                preview.Rows = preview.Rows.OrderBy( r => r.Action ).ThenBy( r => r.PersonName ).ToList();
            }

            return preview;
        }

        #region Internal Methods

        private SubGroupProcessResult ProcessSubGroupInternal(
            CalculationSubGroup subGroup,
            HashSet<int> basePopulation,
            Dictionary<int, HashSet<int>> subGroupPassers,
            RockContext rockContext )
        {
            var result = new SyncResult();

            HashSet<int> workingPopulation;
            var prerequisiteIds = ( subGroup.PrerequisiteSubGroupIds ?? string.Empty )
                .Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries )
                .Select( s => s.Trim().AsInteger() )
                .Where( id => id > 0 )
                .ToList();

            if ( prerequisiteIds.Any() )
            {
                // Start with a copy of base population, then intersect with each prerequisite's passers.
                workingPopulation = new HashSet<int>( basePopulation );

                foreach ( var prereqId in prerequisiteIds )
                {
                    if ( subGroupPassers.TryGetValue( prereqId, out var passers ) )
                    {
                        workingPopulation.IntersectWith( passers );
                    }
                    else
                    {
                        result.Log.Add( $"  SubGroup '{subGroup.Name}': prerequisite SubGroup Id {prereqId} not found in processed results — skipping that prerequisite." );
                    }
                }
            }
            else
            {
                workingPopulation = new HashSet<int>( basePopulation );
            }

            if ( subGroup.AdditionalDataViewId.HasValue )
            {
                var dataView = new DataViewService( rockContext ).Get( subGroup.AdditionalDataViewId.Value );
                if ( dataView != null )
                {
                    try
                    {
                        var dvIds = dataView
                            .GetQuery( new DataViewGetQueryArgs { DbContext = rockContext, DatabaseTimeoutSeconds = 180 } )
                            .Select( e => e.Id )
                            .ToHashSet();
                        workingPopulation.IntersectWith( dvIds );
                    }
                    catch ( Exception ex )
                    {
                        result.Errors.Add( $"SubGroup '{subGroup.Name}' DataView error: {ex.Message}" );
                        ExceptionLogService.LogException( ex );

                        result.Log.Add( $"  SubGroup '{subGroup.Name}': SKIPPED — DataView query failed." );
                        return new SubGroupProcessResult
                        {
                            Result = result,
                            Passers = basePopulation
                        };
                    }
                }
            }

            result.Log.Add( $"  SubGroup '{subGroup.Name}': working population {workingPopulation.Count}" );

            var calculations = new CalculationService( rockContext ).Queryable()
                .Include( c => c.CalculationTypeEntityType )
                .Where( c => c.CalculationSubGroupId == subGroup.Id && c.IsActive )
                .OrderBy( c => c.Order )
                .ThenBy( c => c.Name )
                .ToList();

            HashSet<int> completionPassers = null;

            foreach ( var calculation in calculations )
            {
                try
                {
                    var calcResult = ExecuteCalculation( calculation, workingPopulation, rockContext );
                    result.Merge( calcResult );

                    var componentName = calculation.CalculationTypeEntityType?.Name ?? string.Empty;
                    if ( componentName.Contains( "CompletionCalculation" ) )
                    {
                        completionPassers = calcResult.MatchedPersonIds;
                    }
                }
                catch ( Exception ex )
                {
                    result.Errors.Add( $"Calculation '{calculation.Name}': {ex.Message}" );
                }
            }

            return new SubGroupProcessResult
            {
                Result = result,
                Passers = completionPassers ?? workingPopulation
            };
        }

        private SyncResult ExecuteCalculation(
            Calculation calculation,
            HashSet<int> workingPopulation,
            RockContext rockContext )
        {
            var result = new SyncResult();
            var runStart = RockDateTime.Now;

            var entityType = EntityTypeCache.Get( calculation.CalculationTypeEntityTypeId );
            if ( entityType == null )
            {
                result.Errors.Add( $"EntityType Id {calculation.CalculationTypeEntityTypeId} not found." );
                RecordCalculationRun( calculation.Id, runStart, workingPopulation.Count, 0, result );
                return result;
            }

            var component = CalculationTypeComponent.GetComponent( entityType.Name );
            if ( component == null )
            {
                result.Errors.Add( $"Component '{entityType.Name}' not found." );
                RecordCalculationRun( calculation.Id, runStart, workingPopulation.Count, 0, result );
                return result;
            }

            calculation.LoadAttributes( rockContext );
            var matchedResults = component.Evaluate( rockContext, calculation, workingPopulation );

            result.MatchedPersonIds = new HashSet<int>( matchedResults.Keys );
            result.Log.Add( $"    Calculation '{calculation.Name}': {matchedResults.Count}/{workingPopulation.Count} matched" );

            var targetAttribute = AttributeCache.Get( calculation.PersonAttributeId );
            if ( targetAttribute == null )
            {
                result.Errors.Add( $"Target attribute Id {calculation.PersonAttributeId} not found." );
                RecordCalculationRun( calculation.Id, runStart, workingPopulation.Count, matchedResults.Count, result );
                return result;
            }

            foreach ( var personId in workingPopulation )
            {
                string newValue = null;

                if ( matchedResults.TryGetValue( personId, out var mergeFields ) )
                {
                    newValue = ResolveMatchValue( calculation, mergeFields );
                }
                else
                {
                    if ( calculation.NoMatchBehavior == NoMatchBehavior.LeaveUnchanged )
                    {
                        result.Skipped++;
                        continue;
                    }
                    else
                    {
                        newValue = ResolveNoMatchValue( calculation );
                    }
                }

                if ( newValue != null )
                {
                    using ( var writeContext = new RockContext() )
                    {
                        // Read existing value directly — avoids loading the full Person entity
                        var existingValue = new AttributeValueService( writeContext ).Queryable().AsNoTracking()
                            .Where( av => av.AttributeId == targetAttribute.Id && av.EntityId == personId )
                            .Select( av => av.Value )
                            .FirstOrDefault() ?? string.Empty;

                        if ( !string.Equals( existingValue, newValue, StringComparison.OrdinalIgnoreCase ) )
                        {
                            // Write attribute value via direct SQL to bypass Rock's automatic
                            // AttributeValue history hooks. Run history is tracked via CalculationRun.
                            int rowsUpdated = writeContext.Database.ExecuteSqlCommand(
                                "UPDATE [AttributeValue] SET [Value] = @p0, [ModifiedDateTime] = GETDATE(), [IsPersistedValueDirty] = 1 WHERE [AttributeId] = @p1 AND [EntityId] = @p2",
                                newValue, targetAttribute.Id, personId );

                            if ( rowsUpdated == 0 )
                            {
                                writeContext.Database.ExecuteSqlCommand(
                                    "INSERT INTO [AttributeValue] ([IsSystem], [AttributeId], [EntityId], [Value], [Guid], [CreatedDateTime], [ModifiedDateTime], [IsPersistedValueDirty]) VALUES (0, @p0, @p1, @p2, NEWID(), GETDATE(), GETDATE(), 1)",
                                    targetAttribute.Id, personId, newValue );
                            }

                            result.Updated++;
                        }
                        else
                        {
                            result.Skipped++;
                        }
                    }
                }
            }

            RecordCalculationRun( calculation.Id, runStart, workingPopulation.Count, matchedResults.Count, result );

            return result;
        }

        private HashSet<int> BuildBasePopulation( CalculationGroup group, RockContext rockContext )
        {
            var query = new PersonService( rockContext ).Queryable().AsNoTracking();

            if ( group.RecordStatusValueId.HasValue )
            {
                query = query.Where( p => p.RecordStatusValueId == group.RecordStatusValueId.Value );
            }

            if ( group.ConnectionStatusValueId.HasValue )
            {
                query = query.Where( p => p.ConnectionStatusValueId == group.ConnectionStatusValueId.Value );
            }

            if ( group.CampusId.HasValue )
            {
                query = query.Where( p => p.PrimaryCampusId == group.CampusId.Value );
            }

            var result = new HashSet<int>( query.Select( p => p.Id ).ToList() );

            if ( group.DataViewId.HasValue )
            {
                var dataView = new DataViewService( rockContext ).Get( group.DataViewId.Value );
                if ( dataView != null )
                {
                    try
                    {
                        var dvIds = dataView
                            .GetQuery( new DataViewGetQueryArgs { DbContext = rockContext, DatabaseTimeoutSeconds = 180 } )
                            .Select( e => e.Id )
                            .ToHashSet();
                        result.IntersectWith( dvIds );
                    }
                    catch ( Exception ex )
                    {
                        ExceptionLogService.LogException( ex );
                        throw new Exception( $"Group '{group.Name}' DataView failed: {ex.Message}", ex );
                    }
                }
            }

            return result;
        }

        private static string ResolveMatchValue( Calculation calculation, Dictionary<string, object> mergeFields )
        {
            if ( !string.IsNullOrWhiteSpace( calculation.ResultLavaTemplate ) )
            {
                return calculation.ResultLavaTemplate.ResolveMergeFields( mergeFields );
            }

            return mergeFields.ContainsKey( "Matched" ) ? mergeFields["Matched"]?.ToString() : "True";
        }

        private static string ResolveNoMatchValue( Calculation calculation )
        {
            if ( !string.IsNullOrWhiteSpace( calculation.NoMatchLavaTemplate ) )
            {
                return calculation.NoMatchLavaTemplate.ResolveMergeFields(
                    new Dictionary<string, object> { { "Matched", false } } );
            }

            return null;
        }

        private void RecordCalculationRun( int calculationId, DateTime runStart, int populationCount, int matchedCount, SyncResult result )
        {
            try
            {
                using ( var runContext = new RockContext() )
                {
                    var run = new CalculationRun
                    {
                        CalculationId = calculationId,
                        RunDateTime = runStart,
                        CompletedDateTime = RockDateTime.Now,
                        RunByPersonAliasId = RunByPersonAliasId,
                        PopulationCount = populationCount,
                        MatchedCount = matchedCount,
                        UpdatedCount = result.Updated,
                        SkippedCount = result.Skipped,
                        ErrorCount = result.Errors.Count,
                        WasSuccessful = !result.Errors.Any(),
                        StatusMessage = result.Errors.Any()
                            ? string.Join( "\n", result.Errors )
                            : null
                    };

                    new CalculationRunService( runContext ).Add( run );
                    runContext.SaveChanges();
                }
            }
            catch ( Exception ex )
            {
                result.Errors.Add( $"Failed to record CalculationRun: {ex.Message}" );
            }
        }

        #endregion

        #region Internal Classes

        private class SubGroupProcessResult
        {
            public SyncResult Result { get; set; }
            public HashSet<int> Passers { get; set; }
        }

        #endregion
    }

    #region Result Classes

    /// <summary>
    /// Result of a sync engine execution.
    /// </summary>
    public class SyncResult
    {
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Log { get; set; } = new List<string>();
        public HashSet<int> MatchedPersonIds { get; set; } = new HashSet<int>();

        public void Merge( SyncResult other )
        {
            Updated += other.Updated;
            Skipped += other.Skipped;
            Errors.AddRange( other.Errors );
            Log.AddRange( other.Log );
        }

        public override string ToString()
        {
            return $"{Updated} updated, {Skipped} skipped, {Errors.Count} error(s)";
        }
    }

    /// <summary>
    /// Result of a preview/dry-run evaluation.
    /// </summary>
    public class PreviewResult
    {
        public int TotalPopulation { get; set; }
        public int MatchedCount { get; set; }
        public string ErrorMessage { get; set; }
        public List<PreviewRow> Rows { get; set; } = new List<PreviewRow>();
    }

    /// <summary>
    /// A single row in a preview result.
    /// </summary>
    public class PreviewRow
    {
        public int PersonId { get; set; }
        public string PersonName { get; set; }
        public string CurrentValue { get; set; }
        public string NewValue { get; set; }
        public string Action { get; set; }
    }

    #endregion
}
