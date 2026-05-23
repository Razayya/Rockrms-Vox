using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

using com.razayya.JourneyTrack.CalculationTypes;
using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace com.razayya.JourneyTrack.Data
{
    /// <summary>
    /// Shared service that encapsulates the sync engine's evaluation and write logic.
    /// Used by both the nightly job and on-demand UI execution.
    /// </summary>
    public class JourneyTrackService
    {
        /// <summary>
        /// Gets or sets the PersonAliasId of the user who triggered this run.
        /// Null when triggered by the nightly job.
        /// </summary>
        public int? RunByPersonAliasId { get; set; }

        /// <summary>
        /// Processes all active Journey Programs.
        /// </summary>
        public SyncResult ProcessAllGroups()
        {
            var result = new SyncResult();

            using ( var rockContext = new RockContext() )
            {
                var groups = new JourneyProgramService( rockContext ).Queryable()
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
        /// Processes a single person through a Journey Program.
        /// </summary>
        public SyncResult ProcessGroupForPerson( int calculationGroupId, int personId )
        {
            var result = new SyncResult();

            using ( var rockContext = new RockContext() )
            {
                var group = new JourneyProgramService( rockContext ).Get( calculationGroupId );
                if ( group == null )
                {
                    result.Errors.Add( $"Journey Program Id {calculationGroupId} not found." );
                    return result;
                }

                var singlePersonPopulation = new HashSet<int> { personId };
                result.Log.Add( $"Group '{group.Name}': single-person sync for PersonId {personId}" );

                var subGroups = new StageService( rockContext ).Queryable()
                    .Where( sg => sg.JourneyProgramId == group.Id && sg.IsActive )
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

                // Top-level rollup (Optimization O10: in-memory from stagePassers, zero extra queries)
                WriteProgramRollup( group, singlePersonPopulation, subGroupPassers, result, rockContext );
            }

            FlushAttributeCache();
            return result;
        }

        /// <summary>
        /// Processes a single Journey Program by Id.
        /// </summary>
        public SyncResult ProcessGroup( int calculationGroupId )
        {
            var result = new SyncResult();

            using ( var rockContext = new RockContext() )
            {
                var group = new JourneyProgramService( rockContext ).Get( calculationGroupId );
                if ( group == null )
                {
                    result.Errors.Add( $"Journey Program Id {calculationGroupId} not found." );
                    return result;
                }

                var basePopulation = BuildBasePopulation( group, rockContext );
                result.Log.Add( $"Group '{group.Name}': base population {basePopulation.Count}" );

                var subGroups = new StageService( rockContext ).Queryable()
                    .Where( sg => sg.JourneyProgramId == group.Id && sg.IsActive )
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

                // Top-level rollup (Optimization O10: in-memory from stagePassers, zero extra queries)
                WriteProgramRollup( group, basePopulation, subGroupPassers, result, rockContext );

                group.LastRunDateTime = RockDateTime.Now;
                rockContext.SaveChanges();
            }

            FlushAttributeCache();
            return result;
        }

        /// <summary>
        /// Finds all failed JourneyCalculationRuns (since a given date) and re-runs those calculations.
        /// Safe to call repeatedly — the batch-read/diff logic means already-written values are skipped.
        /// </summary>
        public SyncResult ReprocessFailedRuns( DateTime? since = null )
        {
            var result = new SyncResult();
            var cutoff = since ?? RockDateTime.Now.AddDays( -7 );

            List<int> failedJourneyCalculationIds;
            using ( var rockContext = new RockContext() )
            {
                failedJourneyCalculationIds = new JourneyCalculationRunService( rockContext ).Queryable().AsNoTracking()
                    .Where( r => !r.WasSuccessful && r.RunDateTime >= cutoff )
                    .Select( r => r.JourneyCalculationId )
                    .Distinct()
                    .ToList();
            }

            if ( !failedJourneyCalculationIds.Any() )
            {
                result.Log.Add( "No failed runs found to reprocess." );
                return result;
            }

            result.Log.Add( $"Reprocessing {failedJourneyCalculationIds.Count} JourneyCalculation(s) with failed runs since {cutoff:yyyy-MM-dd}." );

            foreach ( var calcId in failedJourneyCalculationIds )
            {
                try
                {
                    var calcResult = ProcessCalculation( calcId );
                    result.Merge( calcResult );
                }
                catch ( Exception ex )
                {
                    result.Errors.Add( $"Reprocess JourneyCalculationId {calcId}: {ex.Message}" );
                }
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
                var subGroup = new StageService( rockContext ).Queryable()
                    .Include( sg => sg.JourneyProgram )
                    .FirstOrDefault( sg => sg.Id == calculationSubGroupId );

                if ( subGroup == null )
                {
                    return new SyncResult { Errors = { $"SubGroup Id {calculationSubGroupId} not found." } };
                }

                var basePopulation = BuildBasePopulation( subGroup.JourneyProgram, rockContext );
                var internalResult = ProcessSubGroupInternal( subGroup, basePopulation, new Dictionary<int, HashSet<int>>(), rockContext );
                return internalResult.Result;
            }
        }

        /// <summary>
        /// Processes a single JourneyCalculation by Id against the full parent population.
        /// </summary>
        public SyncResult ProcessCalculation( int calculationId, HashSet<int> personIdOverride = null )
        {
            using ( var rockContext = new RockContext() )
            {
                var calc = new JourneyCalculationService( rockContext ).Queryable()
                    .Include( c => c.CalculationTypeEntityType )
                    .Include( c => c.Stage.JourneyProgram )
                    .FirstOrDefault( c => c.Id == calculationId );

                if ( calc == null )
                {
                    return new SyncResult { Errors = { $"JourneyCalculation Id {calculationId} not found." } };
                }

                HashSet<int> population;
                if ( personIdOverride != null && personIdOverride.Count > 0 )
                {
                    population = personIdOverride;
                }
                else
                {
                    population = BuildBasePopulation( calc.Stage.JourneyProgram, rockContext );
                }

                return ExecuteCalculation( calc, population, rockContext );
            }
        }

        /// <summary>
        /// Processes a single JourneyCalculation for a single person.
        /// </summary>
        public SyncResult ProcessCalculationForPerson( int calculationId, int personId )
        {
            return ProcessCalculation( calculationId, new HashSet<int> { personId } );
        }

        /// <summary>
        /// Evaluates a JourneyCalculation without writing any values. Returns preview data.
        /// </summary>
        public PreviewResult PreviewCalculation( int calculationId, HashSet<int> personIdOverride = null, int maxResults = 200 )
        {
            var preview = new PreviewResult();

            using ( var rockContext = new RockContext() )
            {
                var calc = new JourneyCalculationService( rockContext ).Queryable()
                    .Include( c => c.CalculationTypeEntityType )
                    .Include( c => c.Stage.JourneyProgram )
                    .Include( c => c.PersonAttribute )
                    .FirstOrDefault( c => c.Id == calculationId );

                if ( calc == null )
                {
                    preview.ErrorMessage = $"JourneyCalculation Id {calculationId} not found.";
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
                    population = BuildBasePopulation( calc.Stage.JourneyProgram, rockContext );
                }

                preview.TotalPopulation = population.Count;

                if ( maxResults == 0 )
                {
                    return preview;
                }

                // Resolve component and evaluate
                var entityType = EntityTypeCache.Get( calc.CalculationTypeEntityTypeId );
                var component = JourneyCalculationTypeComponent.GetComponent( entityType?.Name );
                if ( component == null )
                {
                    preview.ErrorMessage = $"Component '{entityType?.Name}' not found.";
                    return preview;
                }

                calc.LoadAttributes( rockContext );
                var matchedResults = component.Evaluate( rockContext, calc, population );
                preview.MatchedCount = matchedResults.Count;

                if ( !calc.PersonAttributeId.HasValue )
                {
                    // Transient calculation - no target attribute to preview.
                    preview.ErrorMessage = "This is a transient calculation (no Person Attribute target). Preview shows matched count only.";
                    return preview;
                }
                var targetAttribute = AttributeCache.Get( calc.PersonAttributeId.Value );
                if ( targetAttribute == null )
                {
                    preview.ErrorMessage = $"Target attribute Id {calc.PersonAttributeId.Value} not found.";
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
            Stage subGroup,
            HashSet<int> basePopulation,
            Dictionary<int, HashSet<int>> subGroupPassers,
            RockContext rockContext )
        {
            var result = new SyncResult();

            HashSet<int> workingPopulation;
            var prerequisiteIds = ( subGroup.PrerequisiteStageIds ?? string.Empty )
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

            var calculations = new JourneyCalculationService( rockContext ).Queryable()
                .Include( c => c.CalculationTypeEntityType )
                .Where( c => c.StageId == subGroup.Id && c.IsActive )
                .OrderBy( c => c.Order )
                .ThenBy( c => c.Name )
                .ToList();

            HashSet<int> completionPassers = null;

            foreach ( var calc in calculations )
            {
                try
                {
                    var calcResult = ExecuteCalculation( calc, workingPopulation, rockContext );
                    result.Merge( calcResult );

                    var componentName = calc.CalculationTypeEntityType?.Name ?? string.Empty;
                    if ( componentName.Contains( "CompletionCalculation" ) )
                    {
                        completionPassers = calcResult.MatchedPersonIds;
                    }
                }
                catch ( Exception ex )
                {
                    result.Errors.Add( $"JourneyCalculation '{calc.Name}': {ex.Message}" );
                }
            }

            return new SubGroupProcessResult
            {
                Result = result,
                Passers = completionPassers ?? workingPopulation
            };
        }

        private SyncResult ExecuteCalculation(
            JourneyCalculation calc,
            HashSet<int> workingPopulation,
            RockContext rockContext )
        {
            var result = new SyncResult();
            var runStart = RockDateTime.Now;

            var entityType = EntityTypeCache.Get( calc.CalculationTypeEntityTypeId );
            if ( entityType == null )
            {
                result.Errors.Add( $"EntityType Id {calc.CalculationTypeEntityTypeId} not found." );
                RecordJourneyCalculationRun( calc.Id, runStart, workingPopulation.Count, 0, result );
                return result;
            }

            var component = JourneyCalculationTypeComponent.GetComponent( entityType.Name );
            if ( component == null )
            {
                result.Errors.Add( $"Component '{entityType.Name}' not found." );
                RecordJourneyCalculationRun( calc.Id, runStart, workingPopulation.Count, 0, result );
                return result;
            }

            calc.LoadAttributes( rockContext );
            var matchedResults = component.Evaluate( rockContext, calc, workingPopulation );

            result.MatchedPersonIds = new HashSet<int>( matchedResults.Keys );
            result.Log.Add( $"    JourneyCalculation '{calc.Name}': {matchedResults.Count}/{workingPopulation.Count} matched" );

            // Sink-optional: when no target attribute is configured the calc is transient.
            // We still report matches (so Stage / Program rollups consume them) but skip
            // the read/diff/write cycle entirely.
            if ( !calc.PersonAttributeId.HasValue )
            {
                RecordJourneyCalculationRun( calc.Id, runStart, workingPopulation.Count, matchedResults.Count, result );
                return result;
            }

            var targetAttribute = AttributeCache.Get( calc.PersonAttributeId.Value );
            if ( targetAttribute == null )
            {
                result.Errors.Add( $"Target attribute Id {calc.PersonAttributeId.Value} not found." );
                RecordJourneyCalculationRun( calc.Id, runStart, workingPopulation.Count, matchedResults.Count, result );
                return result;
            }

            // Batch-read all existing attribute values for this attribute + population in one query.
            Dictionary<int, string> existingValues;
            using ( var readContext = new RockContext() )
            {
                var personIdList = workingPopulation.ToList();
                existingValues = new AttributeValueService( readContext ).Queryable().AsNoTracking()
                    .Where( av => av.AttributeId == targetAttribute.Id && personIdList.Contains( av.EntityId.Value ) )
                    .Select( av => new { av.EntityId, av.Value } )
                    .ToDictionary( av => av.EntityId.Value, av => av.Value ?? string.Empty );
            }

            // Build the list of writes needed (resolve Lava templates first, then diff against existing).
            var pendingWrites = new List<AttributeWrite>();

            foreach ( var personId in workingPopulation )
            {
                string newValue = null;

                if ( matchedResults.TryGetValue( personId, out var mergeFields ) )
                {
                    newValue = ResolveMatchValue( calc, mergeFields );
                }
                else
                {
                    if ( calc.NoMatchBehavior == NoMatchBehavior.LeaveUnchanged )
                    {
                        result.Skipped++;
                        continue;
                    }
                    else
                    {
                        newValue = ResolveNoMatchValue( calc );
                    }
                }

                if ( newValue != null )
                {
                    existingValues.TryGetValue( personId, out var existingValue );
                    existingValue = existingValue ?? string.Empty;

                    if ( !string.Equals( existingValue, newValue, StringComparison.OrdinalIgnoreCase ) )
                    {
                        pendingWrites.Add( new AttributeWrite
                        {
                            PersonId = personId,
                            NewValue = newValue,
                            IsUpdate = existingValues.ContainsKey( personId )
                        } );
                    }
                    else
                    {
                        result.Skipped++;
                    }
                }
            }

            // Execute writes in batches. On partial failure, record progress and continue
            // with remaining batches so a single bad row doesn't block the entire population.
            const int batchSize = 200;
            int writtenCount = 0;

            for ( int i = 0; i < pendingWrites.Count; i += batchSize )
            {
                var batch = pendingWrites.GetRange( i, Math.Min( batchSize, pendingWrites.Count - i ) );

                try
                {
                    using ( var writeContext = new RockContext() )
                    {
                        foreach ( var write in batch )
                        {
                            if ( write.IsUpdate )
                            {
                                writeContext.Database.ExecuteSqlCommand(
                                    "UPDATE [AttributeValue] SET [Value] = @p0, [ModifiedDateTime] = GETDATE(), [IsPersistedValueDirty] = 1 WHERE [AttributeId] = @p1 AND [EntityId] = @p2",
                                    write.NewValue, targetAttribute.Id, write.PersonId );
                            }
                            else
                            {
                                writeContext.Database.ExecuteSqlCommand(
                                    "INSERT INTO [AttributeValue] ([IsSystem], [AttributeId], [EntityId], [Value], [Guid], [CreatedDateTime], [ModifiedDateTime], [IsPersistedValueDirty]) VALUES (0, @p0, @p1, @p2, NEWID(), GETDATE(), GETDATE(), 1)",
                                    targetAttribute.Id, write.PersonId, write.NewValue );
                            }

                            writtenCount++;
                        }
                    }

                    result.Updated += batch.Count;
                }
                catch ( Exception ex )
                {
                    result.Errors.Add( $"Write batch failed after {writtenCount} of {pendingWrites.Count} writes for attribute '{targetAttribute.Name}': {ex.Message}" );
                    ExceptionLogService.LogException( ex );
                }
            }

            RecordJourneyCalculationRun( calc.Id, runStart, workingPopulation.Count, matchedResults.Count, result );

            return result;
        }

        private HashSet<int> BuildBasePopulation( JourneyProgram group, RockContext rockContext )
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

        private static string ResolveMatchValue( JourneyCalculation calc, Dictionary<string, object> mergeFields )
        {
            if ( !string.IsNullOrWhiteSpace( calc.ResultLavaTemplate ) )
            {
                return calc.ResultLavaTemplate.ResolveMergeFields( mergeFields );
            }

            return mergeFields.ContainsKey( "Matched" ) ? mergeFields["Matched"]?.ToString() : "True";
        }

        private static string ResolveNoMatchValue( JourneyCalculation calc )
        {
            if ( !string.IsNullOrWhiteSpace( calc.NoMatchLavaTemplate ) )
            {
                return calc.NoMatchLavaTemplate.ResolveMergeFields(
                    new Dictionary<string, object> { { "Matched", false } } );
            }

            return null;
        }

        private void RecordJourneyCalculationRun( int calculationId, DateTime runStart, int populationCount, int matchedCount, SyncResult result )
        {
            try
            {
                using ( var runContext = new RockContext() )
                {
                    var run = new JourneyCalculationRun
                    {
                        JourneyCalculationId = calculationId,
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

                    new JourneyCalculationRunService( runContext ).Add( run );
                    runContext.SaveChanges();
                }
            }
            catch ( Exception ex )
            {
                result.Errors.Add( $"Failed to record JourneyCalculationRun: {ex.Message}" );
            }
        }

        #endregion

        #region Internal Classes

        private class SubGroupProcessResult
        {
            public SyncResult Result { get; set; }
            public HashSet<int> Passers { get; set; }
        }

        private class AttributeWrite
        {
            public int PersonId { get; set; }
            public string NewValue { get; set; }
            public bool IsUpdate { get; set; }
        }

        #endregion

        #region Read-only progress for UI surfaces

        /// <summary>
        /// Read-only progress for one person across one Journey Program. Evaluates every
        /// Stage's calculations against a 1-person population but does NOT write any
        /// AttributeValues. Designed for the Person Profile progress block (WP9) - a
        /// single page-load should clock well under 50ms even with 10 Stages.
        /// </summary>
        public ProgramProgressResult GetProgramProgressForPerson( int programId, int personId )
        {
            var result = new ProgramProgressResult { PersonId = personId, ProgramId = programId };

            using ( var rockContext = new RockContext() )
            {
                var program = new JourneyProgramService( rockContext ).Get( programId );
                if ( program == null )
                {
                    result.NotFound = true;
                    return result;
                }
                result.ProgramName = program.Name;

                var population = new HashSet<int> { personId };
                var stages = new StageService( rockContext ).Queryable()
                    .Where( s => s.JourneyProgramId == program.Id && s.IsActive )
                    .OrderBy( s => s.Order )
                    .ThenBy( s => s.Name )
                    .ToList();

                var stagePassers = new Dictionary<int, HashSet<int>>();
                bool sawIncomplete = false;

                foreach ( var stage in stages )
                {
                    var stageEntry = new StageProgressEntry { StageId = stage.Id, StageName = stage.Name, Order = stage.Order };

                    try
                    {
                        var subResult = ProcessSubGroupInternal( stage, population, stagePassers, rockContext );
                        stagePassers[stage.Id] = subResult.Passers;
                        stageEntry.Passed = subResult.Passers.Contains( personId );
                    }
                    catch ( Exception ex )
                    {
                        stageEntry.Passed = false;
                        stageEntry.ErrorMessage = ex.Message;
                        stagePassers[stage.Id] = population;
                    }

                    if ( !stageEntry.Passed && !sawIncomplete )
                    {
                        stageEntry.IsCurrent = true;
                        sawIncomplete = true;
                    }

                    result.Stages.Add( stageEntry );
                }

                result.AllPassed = result.Stages.Count > 0 && result.Stages.All( s => s.Passed );
            }

            return result;
        }

        #endregion

        #region Program-level rollup + housekeeping

        /// <summary>
        /// Writes the JourneyProgram's CompletionTargetPersonAttribute for every person
        /// in the base population. A person is "complete" when they appear in every
        /// Stage's passer set (AllStagesPass logic).
        /// </summary>
        private void WriteProgramRollup(
            JourneyProgram program,
            HashSet<int> basePopulation,
            Dictionary<int, HashSet<int>> stagePassers,
            SyncResult result,
            RockContext rockContext )
        {
            if ( !program.CompletionTargetPersonAttributeId.HasValue || basePopulation.Count == 0 )
            {
                return;
            }
            if ( stagePassers.Count == 0 )
            {
                return;
            }

            var targetAttribute = AttributeCache.Get( program.CompletionTargetPersonAttributeId.Value );
            if ( targetAttribute == null )
            {
                result.Errors.Add( $"Program rollup target attribute Id {program.CompletionTargetPersonAttributeId.Value} not found." );
                return;
            }

            // AllStagesPass: intersection of every stagePassers set
            var completed = new HashSet<int>( basePopulation );
            foreach ( var passers in stagePassers.Values )
            {
                completed.IntersectWith( passers );
            }

            // Read existing AVs in one batch
            var personIdList = basePopulation.ToList();
            Dictionary<int, string> existing;
            using ( var readContext = new RockContext() )
            {
                existing = new AttributeValueService( readContext ).Queryable().AsNoTracking()
                    .Where( av => av.AttributeId == targetAttribute.Id && av.EntityId.HasValue && personIdList.Contains( av.EntityId.Value ) )
                    .Select( av => new { av.EntityId, av.Value } )
                    .ToDictionary( av => av.EntityId.Value, av => av.Value ?? string.Empty );
            }

            int written = 0;
            using ( var writeContext = new RockContext() )
            {
                foreach ( var personId in basePopulation )
                {
                    var newValue = completed.Contains( personId ) ? "True" : "False";
                    existing.TryGetValue( personId, out var oldValue );
                    oldValue = oldValue ?? string.Empty;
                    if ( string.Equals( oldValue, newValue, StringComparison.OrdinalIgnoreCase ) )
                    {
                        continue;
                    }

                    var isUpdate = existing.ContainsKey( personId );
                    try
                    {
                        if ( isUpdate )
                        {
                            writeContext.Database.ExecuteSqlCommand(
                                "UPDATE [AttributeValue] SET [Value] = @p0, [ModifiedDateTime] = GETDATE(), [IsPersistedValueDirty] = 1 WHERE [AttributeId] = @p1 AND [EntityId] = @p2",
                                newValue, targetAttribute.Id, personId );
                        }
                        else
                        {
                            writeContext.Database.ExecuteSqlCommand(
                                "INSERT INTO [AttributeValue] ([IsSystem], [AttributeId], [EntityId], [Value], [Guid], [CreatedDateTime], [ModifiedDateTime], [IsPersistedValueDirty]) VALUES (0, @p0, @p1, @p2, NEWID(), GETDATE(), GETDATE(), 1)",
                                targetAttribute.Id, personId, newValue );
                        }
                        written++;
                    }
                    catch ( Exception ex )
                    {
                        result.Errors.Add( $"Program rollup write failed for PersonId {personId}: {ex.Message}" );
                        ExceptionLogService.LogException( ex );
                    }
                }
            }

            result.Updated += written;
            result.Log.Add( $"  Program rollup '{program.Name}': {completed.Count}/{basePopulation.Count} complete, {written} attribute values written." );
        }

        /// <summary>
        /// Invalidates the AttributeValue side of Rock's cache so the freshly written values
        /// surface immediately. Optimization O8 - one call at end of run.
        ///
        /// Rock 18 doesn't expose a granular AttributeValue flush, so we fall back to a
        /// broad RockCache clear. If this proves too aggressive in practice, replace with
        /// targeted FlushAttributesForBlockType / FlushItem(attributeId) calls.
        /// </summary>
        private void FlushAttributeCache()
        {
            try
            {
                Rock.Web.Cache.RockCache.ClearAllCachedItems();
            }
            catch ( Exception ex )
            {
                // Cache flush failures are non-fatal; new values will surface on natural cache expiry.
                ExceptionLogService.LogException( ex );
            }
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

    /// <summary>
    /// Read-only progress of one person through one Journey Program.
    /// Consumed by the Person Profile progress block (WP9).
    /// </summary>
    public class ProgramProgressResult
    {
        public int ProgramId { get; set; }
        public string ProgramName { get; set; }
        public int PersonId { get; set; }
        public bool NotFound { get; set; }
        public bool AllPassed { get; set; }
        public List<StageProgressEntry> Stages { get; set; } = new List<StageProgressEntry>();
    }

    public class StageProgressEntry
    {
        public int StageId { get; set; }
        public string StageName { get; set; }
        public int Order { get; set; }
        public bool Passed { get; set; }
        public bool IsCurrent { get; set; }
        public string ErrorMessage { get; set; }
    }

    #endregion
}
