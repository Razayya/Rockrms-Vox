using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

using com.razayya.JourneyTrack.CalculationTypes;
using com.razayya.JourneyTrack.Logic;
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
        /// Optional progress sink. When set (e.g. by the nightly job, which wires this
        /// to RockJob.UpdateLastStatusMessage), the engine reports milestone-level
        /// progress so a long full-population run is observable live on the Jobs
        /// Administration page. Left null on the single-person / mobile sync paths, so
        /// those carry zero overhead.
        /// </summary>
        public Action<string> OnProgress { get; set; }

        /// <summary>
        /// Emits a progress message if a sink is wired. Never throws into the engine.
        /// </summary>
        private void Report( string message )
        {
            if ( OnProgress == null )
            {
                return;
            }
            try { OnProgress( message ); } catch { /* progress is best-effort */ }
        }

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

                int groupIndex = 0;
                foreach ( var group in groups )
                {
                    groupIndex++;
                    try
                    {
                        Report( $"Program {groupIndex}/{groups.Count} '{group.Name}': starting" );

                        // Reconcile enrollments from the population spec BEFORE processing,
                        // so the run iterates the freshly-materialized active set.
                        if ( group.RequiresEnrollment && group.AutoEnrollFromPopulation )
                        {
                            Report( $"Program '{group.Name}': reconciling enrollments" );
                            var rec = ReconcileEnrollments( group.Id );
                            if ( rec.Added > 0 || rec.Reactivated > 0 || rec.SoftUnenrolled > 0 )
                            {
                                result.Log.Add( $"Reconcile '{group.Name}': +{rec.Added} added, +{rec.Reactivated} reactivated, -{rec.SoftUnenrolled} soft-unenrolled (active now {rec.ActiveAfter})." );
                            }
                            Report( $"Program '{group.Name}': enrollment reconciled (+{rec.Added} new, +{rec.Reactivated} reactivated, active {rec.ActiveAfter})" );
                            result.Errors.AddRange( rec.Errors );
                        }

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
            return ProcessProgramForPersonInternal( calculationGroupId, personId, maxStageOrder: int.MaxValue );
        }

        /// <summary>
        /// Processes a single Stage for a single person. Runs the cascade from Stage Order
        /// 0 up through the target Stage (inclusive) so prerequisite gating remains correct,
        /// then stops — later Stages are not evaluated. The per-calc-type cache (30s TTL)
        /// keeps repeat upstream evaluation cheap when the mobile app opens nearby pages
        /// in quick succession. Skips the program rollup write (intersection over a partial
        /// stagePassers map would be wrong) — that's reserved for full-program syncs.
        /// </summary>
        public SyncResult ProcessStageForPerson( int stageId, int personId )
        {
            using ( var rockContext = new RockContext() )
            {
                var stage = new StageService( rockContext ).Get( stageId );
                if ( stage == null )
                {
                    var r = new SyncResult();
                    r.Errors.Add( $"Stage Id {stageId} not found." );
                    return r;
                }
                if ( !stage.IsActive )
                {
                    var r = new SyncResult();
                    r.Errors.Add( $"Stage Id {stageId} ('{stage.Name}') is inactive." );
                    return r;
                }
                return ProcessProgramForPersonInternal( stage.JourneyProgramId, personId, maxStageOrder: stage.Order );
            }
        }

        private SyncResult ProcessProgramForPersonInternal( int programId, int personId, int maxStageOrder )
        {
            var result = new SyncResult();

            using ( var rockContext = new RockContext() )
            {
                var group = new JourneyProgramService( rockContext ).Get( programId );
                if ( group == null )
                {
                    result.Errors.Add( $"Journey Program Id {programId} not found." );
                    return result;
                }

                var singlePersonPopulation = new HashSet<int> { personId };
                var scopeNote = maxStageOrder == int.MaxValue
                    ? $"full program"
                    : $"stage-scoped (Order <= {maxStageOrder})";
                result.Log.Add( $"Group '{group.Name}': single-person sync for PersonId {personId} — {scopeNote}" );

                var subGroups = new StageService( rockContext ).Queryable()
                    .Where( sg => sg.JourneyProgramId == group.Id && sg.IsActive && sg.Order <= maxStageOrder )
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

                // Top-level rollup (Optimization O10: in-memory from stagePassers, zero extra queries).
                // Only safe when every Stage has been evaluated — a partial stagePassers map would
                // make the AllStagesPass intersection report false completions.
                if ( maxStageOrder == int.MaxValue )
                {
                    WriteProgramRollup( group, singlePersonPopulation, subGroupPassers, result, rockContext );
                }
                else
                {
                    result.Log.Add( $"  (program rollup skipped — partial stage scope)" );
                }
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

                Report( $"Program '{group.Name}': building base population" );
                var basePopulation = BuildBasePopulation( group, rockContext );
                result.Log.Add( $"Group '{group.Name}': base population {basePopulation.Count}" );
                Report( $"Program '{group.Name}': base population {basePopulation.Count:N0}" );

                var subGroups = new StageService( rockContext ).Queryable()
                    .Where( sg => sg.JourneyProgramId == group.Id && sg.IsActive )
                    .OrderBy( sg => sg.Order )
                    .ThenBy( sg => sg.Name )
                    .ToList();

                var subGroupPassers = new Dictionary<int, HashSet<int>>();
                var progSummary = new ProgramRunSummary { ProgramName = group.Name, Enrollees = basePopulation.Count };

                int stageIndex = 0;
                foreach ( var subGroup in subGroups )
                {
                    stageIndex++;
                    try
                    {
                        var subResult = ProcessSubGroupInternal( subGroup, basePopulation, subGroupPassers, rockContext, stageIndex, subGroups.Count );
                        result.Merge( subResult.Result );
                        subGroupPassers[subGroup.Id] = subResult.Passers;
                        if ( subResult.Summary != null )
                        {
                            progSummary.Stages.Add( subResult.Summary );
                        }
                    }
                    catch ( Exception ex )
                    {
                        result.Errors.Add( $"SubGroup '{subGroup.Name}': {ex.Message}" );
                        subGroupPassers[subGroup.Id] = basePopulation;
                    }
                }

                // Top-level rollup (Optimization O10: in-memory from stagePassers, zero extra queries)
                WriteProgramRollup( group, basePopulation, subGroupPassers, result, rockContext, progSummary );
                result.ProgramSummaries.Add( progSummary );

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
            RockContext rockContext,
            int stageIndex = 0,
            int stageCount = 0 )
        {
            var result = new SyncResult();
            var stageLabel = stageCount > 0 ? $"Stage {stageIndex}/{stageCount} '{subGroup.Name}'" : $"Stage '{subGroup.Name}'";

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

            // Sequential early-exit: once the cascade drains the population, downstream calcs
            // produce zero matches anyway. Skip the per-calc evaluation overhead, keep Passers
            // empty, and let later Stages do the same.
            if ( workingPopulation.Count == 0 )
            {
                result.Log.Add( $"    (skipping {subGroup.Name} calc evaluation — empty working population)" );
                Report( $"{stageLabel}: skipped (no one reached this stage yet)" );
                int skippedCalcCount = new JourneyCalculationService( rockContext ).Queryable().AsNoTracking()
                    .Count( c => c.StageId == subGroup.Id && c.IsActive );
                return new SubGroupProcessResult
                {
                    Result = result,
                    Passers = new HashSet<int>(),
                    Summary = new StageRunSummary { Order = subGroup.Order, Name = subGroup.Name, CalcCount = skippedCalcCount, Skipped = true }
                };
            }

            var calculations = new JourneyCalculationService( rockContext ).Queryable()
                .Include( c => c.CalculationTypeEntityType )
                .Where( c => c.StageId == subGroup.Id && c.IsActive )
                .OrderBy( c => c.Order )
                .ThenBy( c => c.Name )
                .ToList();

            Report( $"{stageLabel}: evaluating {calculations.Count} calc(s) over {workingPopulation.Count:N0} people" );

            HashSet<int> completionPassers = null;
            var nonCompletionMatched = new List<HashSet<int>>();
            // Matched set per calc Id — consumed by the optional Stage logic tree,
            // which references calcs by Id and folds their sets with All/Any/…False.
            var matchedByCalcId = new Dictionary<int, HashSet<int>>();

            int calcIndex = 0;
            foreach ( var calc in calculations )
            {
                calcIndex++;
                try
                {
                    Report( $"{stageLabel}: calc {calcIndex}/{calculations.Count} '{calc.Name}'" );
                    var calcResult = ExecuteCalculation( calc, workingPopulation, rockContext );
                    result.Merge( calcResult );

                    var matched = calcResult.MatchedPersonIds ?? new HashSet<int>();
                    matchedByCalcId[calc.Id] = matched;

                    var componentName = calc.CalculationTypeEntityType?.Name ?? string.Empty;
                    if ( componentName.Contains( "CompletionCalculation" ) )
                    {
                        completionPassers = calcResult.MatchedPersonIds;
                    }
                    else
                    {
                        nonCompletionMatched.Add( matched );
                    }
                }
                catch ( Exception ex )
                {
                    result.Errors.Add( $"JourneyCalculation '{calc.Name}': {ex.Message}" );
                }
            }

            HashSet<int> finalPassers;

            // Stage logic tree (Option A): when configured, a nested ANY/ALL tree over
            // the Stage's calcs defines the passer set. Leaves reference a calc by Id;
            // All=Intersect, Any=Union, AllFalse/AnyFalse complement against the
            // working population. This is the set-space analog of a DataView filter tree.
            var stageTree = string.IsNullOrWhiteSpace( subGroup.LogicTreeJson )
                ? null
                : LogicTree.Parse<StageLogicLeaf>( subGroup.LogicTreeJson, LogicGroupType.All );

            if ( stageTree != null && !LogicTree.IsEmpty( stageTree ) )
            {
                finalPassers = EvaluateStageLogic( stageTree, matchedByCalcId, workingPopulation );
                result.Log.Add( $"    Stage logic tree → {finalPassers.Count} passers" );
            }
            else
            {
                // Legacy gate (unchanged): when there's no Completion meta-calc, the Stage
                // is "passed" by the intersection of every non-Completion calc's matched
                // persons. This makes transient-only Stages (Shape A — e.g. a single
                // StepCompletion calc with no Person Attr sink) gate prerequisites
                // correctly. Previous behavior fail-opened to the full workingPopulation,
                // which broke chained Stages.
                if ( completionPassers == null && nonCompletionMatched.Count > 0 )
                {
                    completionPassers = new HashSet<int>( nonCompletionMatched[0] );
                    for ( int i = 1; i < nonCompletionMatched.Count; i++ )
                    {
                        completionPassers.IntersectWith( nonCompletionMatched[i] );
                    }
                }

                finalPassers = completionPassers ?? workingPopulation;
            }

            // Stage-level transition detection. A person newly entering this Stage's
            // passer set (vs prior runs) triggers OnCompleteSystemCommunication if set.
            // Dedup is handled by JourneyCommunicationLog (one row per Stage per person).
            if ( subGroup.OnCompleteSystemCommunicationId.HasValue && finalPassers != null && finalPassers.Count > 0 )
            {
                QueueCommunicationLogs(
                    CommunicationContextType.Stage,
                    subGroup.Id,
                    subGroup.OnCompleteSystemCommunicationId.Value,
                    finalPassers,
                    rockContext, result );
            }

            return new SubGroupProcessResult
            {
                Result = result,
                Passers = finalPassers,
                Summary = new StageRunSummary
                {
                    Order = subGroup.Order,
                    Name = subGroup.Name,
                    CalcCount = calculations.Count,
                    Evaluated = workingPopulation.Count,
                    Passers = finalPassers?.Count ?? 0,
                    Written = result.Updated,
                    Unchanged = result.Skipped,
                    Skipped = false
                }
            };
        }

        /// <summary>
        /// Folds a Stage's logic tree into a passer set. Leaves resolve to a calc's
        /// matched-person set; groups combine in set-space: All=Intersect, Any=Union,
        /// AllFalse = population minus the union of children (in none), AnyFalse =
        /// population minus the intersection of children (not in all). An unconfigured
        /// or empty node matches nobody. Each call returns a fresh set, so the source
        /// matched sets are never mutated.
        /// </summary>
        private static HashSet<int> EvaluateStageLogic(
            LogicNode<StageLogicLeaf> node,
            Dictionary<int, HashSet<int>> matchedByCalcId,
            HashSet<int> population )
        {
            if ( node == null )
            {
                return new HashSet<int>();
            }

            if ( node.IsLeaf )
            {
                if ( node.Leaf != null && matchedByCalcId.TryGetValue( node.Leaf.CalcId, out var set ) )
                {
                    return new HashSet<int>( set );
                }
                return new HashSet<int>();
            }

            var children = node.Children ?? new List<LogicNode<StageLogicLeaf>>();
            if ( children.Count == 0 )
            {
                return new HashSet<int>();
            }

            switch ( node.Type.Value )
            {
                case LogicGroupType.All:
                {
                    HashSet<int> acc = null;
                    foreach ( var child in children )
                    {
                        var childSet = EvaluateStageLogic( child, matchedByCalcId, population );
                        if ( acc == null )
                        {
                            acc = childSet;
                        }
                        else
                        {
                            acc.IntersectWith( childSet );
                        }
                    }
                    return acc ?? new HashSet<int>();
                }

                case LogicGroupType.Any:
                {
                    var acc = new HashSet<int>();
                    foreach ( var child in children )
                    {
                        acc.UnionWith( EvaluateStageLogic( child, matchedByCalcId, population ) );
                    }
                    return acc;
                }

                case LogicGroupType.AllFalse:
                {
                    // People in NONE of the children: population minus the union.
                    var union = new HashSet<int>();
                    foreach ( var child in children )
                    {
                        union.UnionWith( EvaluateStageLogic( child, matchedByCalcId, population ) );
                    }
                    var res = new HashSet<int>( population );
                    res.ExceptWith( union );
                    return res;
                }

                case LogicGroupType.AnyFalse:
                {
                    // People NOT in all children: population minus the intersection.
                    HashSet<int> inter = null;
                    foreach ( var child in children )
                    {
                        var childSet = EvaluateStageLogic( child, matchedByCalcId, population );
                        if ( inter == null )
                        {
                            inter = childSet;
                        }
                        else
                        {
                            inter.IntersectWith( childSet );
                        }
                    }
                    var res = new HashSet<int>( population );
                    res.ExceptWith( inter ?? new HashSet<int>() );
                    return res;
                }

                default:
                    return new HashSet<int>();
            }
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

            // "Sticky" mode: if SkipIfTargetHasValue is set and the calc has a sink
            // attribute, exclude people who already have a non-blank value. Big win
            // for full-population syncs where most people are "done" with the step,
            // and for single-person syncs where the person has already completed it.
            var workingPopulationForEval = workingPopulation;
            int skippedCount = 0;
            if ( calc.SkipIfTargetHasValue && calc.PersonAttributeId.HasValue && workingPopulation.Count > 0 )
            {
                // Size-aware read (avoids a large-IN clause), then keep only non-blank values.
                var existingForSkip = ReadExistingAttributeValues( calc.PersonAttributeId.Value, workingPopulation, rockContext );
                var skipPersonIds = new HashSet<int>( existingForSkip
                    .Where( kv => !string.IsNullOrEmpty( kv.Value ) )
                    .Select( kv => kv.Key ) );
                if ( skipPersonIds.Count > 0 )
                {
                    workingPopulationForEval = new HashSet<int>( workingPopulation );
                    workingPopulationForEval.ExceptWith( skipPersonIds );
                    skippedCount = skipPersonIds.Count;
                }
            }

            // Short-circuit when sticky-skip drained the population entirely.
            if ( workingPopulationForEval.Count == 0 && skippedCount > 0 )
            {
                result.Log.Add( $"    JourneyCalculation '{calc.Name}': 0/{workingPopulation.Count} evaluated (sticky-skipped {skippedCount} already-set)" );
                RecordJourneyCalculationRun( calc.Id, runStart, workingPopulation.Count, 0, result );
                return result;
            }

            var matchedResults = component.Evaluate( rockContext, calc, workingPopulationForEval );

            result.MatchedPersonIds = new HashSet<int>( matchedResults.Keys );
            if ( skippedCount > 0 )
            {
                result.Log.Add( $"    JourneyCalculation '{calc.Name}': {matchedResults.Count}/{workingPopulationForEval.Count} matched (sticky-skipped {skippedCount} already-set)" );
            }
            else
            {
                result.Log.Add( $"    JourneyCalculation '{calc.Name}': {matchedResults.Count}/{workingPopulation.Count} matched" );
            }

            // Sink-optional: when no target attribute is configured the calc is transient.
            // We still report matches (so Stage / Program rollups consume them) but skip
            // the read/diff/write cycle entirely. Transition detection here uses
            // JourneyCommunicationLog as the dedup key since there's no AV to diff against.
            if ( !calc.PersonAttributeId.HasValue )
            {
                if ( calc.OnMatchSystemCommunicationId.HasValue && matchedResults.Count > 0 )
                {
                    QueueCommunicationLogs(
                        CommunicationContextType.Calculation,
                        calc.Id,
                        calc.OnMatchSystemCommunicationId.Value,
                        matchedResults.Keys,
                        rockContext, result );
                }
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

            // Batch-read existing attribute values for this attribute, restricted to the
            // population. Size-aware to avoid a large-IN clause on full-population syncs.
            Dictionary<int, string> existingValues;
            using ( var readContext = new RockContext() )
            {
                existingValues = ReadExistingAttributeValues( targetAttribute.Id, workingPopulation, readContext );
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

            // Track which persons newly transitioned blank/no-match → matched, so we can
            // queue comm log rows after the writes commit. The set is built from the same
            // pendingWrites list we're about to execute.
            HashSet<int> newlyMatchedPersonIds = null;
            if ( calc.OnMatchSystemCommunicationId.HasValue )
            {
                newlyMatchedPersonIds = new HashSet<int>();
                foreach ( var write in pendingWrites )
                {
                    if ( !matchedResults.ContainsKey( write.PersonId ) ) continue;
                    existingValues.TryGetValue( write.PersonId, out var prior );
                    if ( string.IsNullOrEmpty( prior ) )
                    {
                        // Blank prior → person is newly matched. Queue.
                        newlyMatchedPersonIds.Add( write.PersonId );
                    }
                }
            }

            // Execute writes set-based (chunked, grouped by value) rather than one round-trip
            // per person. A failed chunk is logged and the rest continue.
            int writtenCount = BulkWriteAttributeValues( targetAttribute.Id, pendingWrites, result );
            result.Updated += writtenCount;

            // Queue comm log rows for the newly-matched persons (transition: blank → match).
            // Dedup vs prior logs is handled inside QueueCommunicationLogs.
            if ( newlyMatchedPersonIds != null && newlyMatchedPersonIds.Count > 0 )
            {
                QueueCommunicationLogs(
                    CommunicationContextType.Calculation,
                    calc.Id,
                    calc.OnMatchSystemCommunicationId.Value,
                    newlyMatchedPersonIds,
                    rockContext, result );
            }

            RecordJourneyCalculationRun( calc.Id, runStart, workingPopulation.Count, matchedResults.Count, result );

            return result;
        }

        /// <summary>
        /// Insert JourneyCommunicationLog rows for the given (context, system communication, persons)
        /// — but only for personIds that don't already have an existing log row for the same
        /// (ContextType, ContextId, PersonAliasId). Dedupe is done in a single round-trip read
        /// before bulk-inserting. Map PersonId → primary PersonAliasId via one cached lookup.
        /// </summary>
        private void QueueCommunicationLogs(
            CommunicationContextType contextType,
            int contextId,
            int systemCommunicationId,
            IEnumerable<int> candidatePersonIds,
            RockContext rockContext,
            SyncResult result )
        {
            var candidateSet = candidatePersonIds as ICollection<int> ?? candidatePersonIds.ToList();
            if ( candidateSet.Count == 0 ) return;

            // Look up persons who ALREADY have a log row for this trigger key — these are
            // the "already-notified" set we'll exclude. JourneyCommunicationLog dedup keys
            // off (ContextType, ContextId, PersonAliasId) so a given trigger fires once
            // per person even across multiple sync passes.
            var ctxTypeInt = ( int ) contextType;
            var alreadyLoggedPersonIds = new JourneyCommunicationLogService( rockContext ).Queryable().AsNoTracking()
                .Where( l => l.ContextType == contextType
                    && l.ContextId == contextId
                    && candidateSet.Contains( l.PersonAlias.PersonId ) )
                .Select( l => l.PersonAlias.PersonId )
                .ToList();

            var toQueue = candidateSet.Except( alreadyLoggedPersonIds ).ToList();
            if ( toQueue.Count == 0 ) return;

            // Map PersonId → primary PersonAliasId (one query)
            var aliasMap = new PersonAliasService( rockContext ).Queryable().AsNoTracking()
                .Where( pa => toQueue.Contains( pa.PersonId )
                    && pa.AliasPersonId.HasValue
                    && pa.AliasPersonId == pa.PersonId )
                .Select( pa => new { pa.PersonId, AliasId = pa.Id } )
                .ToDictionary( x => x.PersonId, x => x.AliasId );

            var nowUtc = RockDateTime.Now;
            var rows = new List<JourneyCommunicationLog>( toQueue.Count );
            foreach ( var personId in toQueue )
            {
                if ( !aliasMap.TryGetValue( personId, out var aliasId ) ) continue;
                rows.Add( new JourneyCommunicationLog
                {
                    ContextType = contextType,
                    ContextId = contextId,
                    PersonAliasId = aliasId,
                    SystemCommunicationId = systemCommunicationId,
                    QueuedDateTime = nowUtc,
                    Guid = System.Guid.NewGuid(),
                    CreatedDateTime = nowUtc,
                    ModifiedDateTime = nowUtc
                } );
            }
            if ( rows.Count == 0 ) return;

            try
            {
                rockContext.BulkInsert( rows );
                result.Log.Add( $"      queued {rows.Count} {contextType} comm log row(s) (template Id {systemCommunicationId})" );
            }
            catch ( Exception ex )
            {
                result.Errors.Add( $"Failed to queue {contextType} comm logs (template {systemCommunicationId}): {ex.Message}" );
                ExceptionLogService.LogException( ex );
            }
        }

        private HashSet<int> BuildBasePopulation( JourneyProgram group, RockContext rockContext )
        {
            // RequiresEnrollment programs: the enrollment table IS the base population.
            // Population filters on the program are the auto-enroll SPEC (used by
            // ReconcileEnrollments), not a runtime filter — anyone who needed to be
            // included was reconciled into the enrollment table already.
            if ( group.RequiresEnrollment )
            {
                var enrolledPersonIds = new JourneyProgramEnrollmentService( rockContext ).Queryable().AsNoTracking()
                    .Where( e => e.JourneyProgramId == group.Id && e.IsActive )
                    .Select( e => e.PersonAlias.PersonId )
                    .Distinct()
                    .ToList();
                return new HashSet<int>( enrolledPersonIds );
            }

            // Legacy: program iterates everyone matching its demographic filters.
            return BuildCandidatePopulation( group, rockContext );
        }

        /// <summary>
        /// Compute the set of Person Ids matching this program's demographic filters
        /// (RecordStatus / Connection / Campus / DataView). Shared by legacy
        /// BuildBasePopulation and by ReconcileEnrollments (auto-enroll spec).
        /// </summary>
        private HashSet<int> BuildCandidatePopulation( JourneyProgram group, RockContext rockContext )
        {
            var query = new PersonService( rockContext ).Queryable().AsNoTracking();

            if ( group.RecordStatusValueId.HasValue )
                query = query.Where( p => p.RecordStatusValueId == group.RecordStatusValueId.Value );
            if ( group.ConnectionStatusValueId.HasValue )
                query = query.Where( p => p.ConnectionStatusValueId == group.ConnectionStatusValueId.Value );
            if ( group.CampusId.HasValue )
                query = query.Where( p => p.PrimaryCampusId == group.CampusId.Value );

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

        /// <summary>
        /// Reconcile the enrollment table for this program against its population
        /// spec. Uses Rock's BulkInsert + chunked BulkUpdate so an initial
        /// reconciliation of 30k+ candidates completes in seconds rather than
        /// hanging EF for minutes.
        /// </summary>
        public ReconcileResult ReconcileEnrollments( int programId )
        {
            var result = new ReconcileResult();
            using ( var rockContext = new RockContext() )
            {
                var group = new JourneyProgramService( rockContext ).Get( programId );
                if ( group == null )
                {
                    result.Errors.Add( $"Journey Program Id {programId} not found." );
                    return result;
                }
                if ( !group.RequiresEnrollment || !group.AutoEnrollFromPopulation )
                {
                    result.Log.Add( $"Program '{group.Name}' has AutoEnrollFromPopulation=false — nothing to reconcile." );
                    return result;
                }

                var candidates = BuildCandidatePopulation( group, rockContext );
                result.CandidatePopulation = candidates.Count;
                result.Log.Add( $"Program '{group.Name}': candidate population {candidates.Count}" );

                // Snapshot current enrollment state (active + inactive) in one round trip.
                var existing = new JourneyProgramEnrollmentService( rockContext ).Queryable().AsNoTracking()
                    .Where( e => e.JourneyProgramId == group.Id )
                    .Select( e => new { e.Id, PersonId = e.PersonAlias.PersonId, e.IsActive } )
                    .ToList();

                var activeEnrolledPersonIds = new HashSet<int>(
                    existing.Where( e => e.IsActive ).Select( e => e.PersonId ) );
                var inactiveEnrolledRowsByPersonId = existing
                    .Where( e => !e.IsActive )
                    .GroupBy( e => e.PersonId )
                    .ToDictionary( g => g.Key, g => g.OrderByDescending( x => x.Id ).First().Id );

                // Partition candidates → new vs needs-reactivation
                var personIdsToInsert = new List<int>();
                var enrollmentIdsToReactivate = new List<int>();
                foreach ( var personId in candidates )
                {
                    if ( activeEnrolledPersonIds.Contains( personId ) ) continue;

                    if ( inactiveEnrolledRowsByPersonId.TryGetValue( personId, out var inactiveId ) )
                    {
                        enrollmentIdsToReactivate.Add( inactiveId );
                    }
                    else
                    {
                        personIdsToInsert.Add( personId );
                    }
                }

                var nowUtc = RockDateTime.Now;

                // BULK INSERT new rows. Load all primary aliases in one query
                // (one-shot ~50k rows, far cheaper than 30k `Contains` calls).
                if ( personIdsToInsert.Count > 0 )
                {
                    var primaryAliasMap = new PersonAliasService( rockContext ).Queryable().AsNoTracking()
                        .Where( pa => pa.AliasPersonId.HasValue && pa.AliasPersonId == pa.PersonId )
                        .Select( pa => new { pa.PersonId, AliasId = pa.Id } )
                        .ToDictionary( x => x.PersonId, x => x.AliasId );

                    var newRows = new List<JourneyProgramEnrollment>( personIdsToInsert.Count );
                    foreach ( var personId in personIdsToInsert )
                    {
                        if ( !primaryAliasMap.TryGetValue( personId, out var aliasId ) ) continue;
                        newRows.Add( new JourneyProgramEnrollment
                        {
                            JourneyProgramId = group.Id,
                            PersonAliasId = aliasId,
                            EnrolledDateTime = nowUtc,
                            IsActive = true,
                            Source = "AutoEnroll",
                            Guid = System.Guid.NewGuid(),
                            CreatedDateTime = nowUtc,
                            ModifiedDateTime = nowUtc
                        } );
                    }
                    if ( newRows.Count > 0 )
                    {
                        rockContext.BulkInsert( newRows );
                        result.Added = newRows.Count;
                    }
                }

                // BULK REACTIVATE inactive rows. Chunk by ~2000 to keep the
                // generated IN-list tractable.
                if ( enrollmentIdsToReactivate.Count > 0 )
                {
                    int batchSize = 2000;
                    for ( int offset = 0; offset < enrollmentIdsToReactivate.Count; offset += batchSize )
                    {
                        var chunk = enrollmentIdsToReactivate.Skip( offset ).Take( batchSize ).ToList();
                        var q = new JourneyProgramEnrollmentService( rockContext ).Queryable()
                            .Where( e => chunk.Contains( e.Id ) );
                        rockContext.BulkUpdate( q, e => new JourneyProgramEnrollment
                        {
                            IsActive = true,
                            UnenrolledDateTime = null,
                            ModifiedDateTime = nowUtc
                        } );
                        result.Reactivated += chunk.Count;
                    }
                }

                // BULK SOFT-UNENROLL the no-longer-matching people, if configured.
                if ( group.AutoUnenrollOnPopulationLeave )
                {
                    var staleIds = existing
                        .Where( e => e.IsActive && !candidates.Contains( e.PersonId ) )
                        .Select( e => e.Id )
                        .ToList();
                    int batchSize = 2000;
                    for ( int offset = 0; offset < staleIds.Count; offset += batchSize )
                    {
                        var chunk = staleIds.Skip( offset ).Take( batchSize ).ToList();
                        var q = new JourneyProgramEnrollmentService( rockContext ).Queryable()
                            .Where( e => chunk.Contains( e.Id ) );
                        rockContext.BulkUpdate( q, e => new JourneyProgramEnrollment
                        {
                            IsActive = false,
                            UnenrolledDateTime = nowUtc,
                            ModifiedDateTime = nowUtc
                        } );
                        result.SoftUnenrolled += chunk.Count;
                    }
                }

                result.ActiveAfter = activeEnrolledPersonIds.Count + result.Added + result.Reactivated - result.SoftUnenrolled;
                result.Log.Add( $"  Added {result.Added}, Reactivated {result.Reactivated}, Soft-Unenrolled {result.SoftUnenrolled}, ActiveAfter {result.ActiveAfter}" );
            }
            return result;
        }

        /// <summary>
        /// "Reset Enrollment": removes enrollees from a program. Touches ONLY the enrollment
        /// table — no Person Attributes or calc data are changed. Hard delete (not
        /// soft-unenroll), so this is a clean reset:
        ///  - Auto-Enroll ON  (AutoEnrollFromPopulation): keep the auto-enrolled rows
        ///    (Source = "AutoEnroll") and remove the manually-added ones (any other / null
        ///    Source). Anyone still in the population is re-added fresh on the next reconcile.
        ///  - Auto-Enroll OFF: remove every enrollment row for the program.
        /// </summary>
        public ResetEnrollmentResult ResetEnrollment( int programId )
        {
            var result = new ResetEnrollmentResult();
            using ( var rockContext = new RockContext() )
            {
                var group = new JourneyProgramService( rockContext ).Get( programId );
                if ( group == null )
                {
                    result.Errors.Add( $"Journey Program Id {programId} not found." );
                    return result;
                }

                result.AutoEnrollMode = group.AutoEnrollFromPopulation;
                if ( group.AutoEnrollFromPopulation )
                {
                    result.KeptAutoEnrolled = new JourneyProgramEnrollmentService( rockContext ).Queryable().AsNoTracking()
                        .Count( e => e.JourneyProgramId == programId && e.Source == "AutoEnroll" );

                    // Keep auto-enrolled rows; remove manual (and null-Source) adds.
                    result.Removed = rockContext.Database.ExecuteSqlCommand(
                        "DELETE FROM [_com_razayya_JourneyTrack_JourneyProgramEnrollment] WHERE [JourneyProgramId] = @p0 AND ( [Source] IS NULL OR [Source] <> @p1 )",
                        programId, "AutoEnroll" );
                }
                else
                {
                    // No auto-enroll: clear everyone.
                    result.Removed = rockContext.Database.ExecuteSqlCommand(
                        "DELETE FROM [_com_razayya_JourneyTrack_JourneyProgramEnrollment] WHERE [JourneyProgramId] = @p0",
                        programId );
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

        /// <summary>
        /// Reads existing AttributeValue strings for an attribute, keyed by EntityId (PersonId),
        /// restricted to the given population. Avoids the large-IN-clause trap: for a big
        /// population a per-id IN() expands into tens of thousands of parameters that are slow
        /// to compile and transmit (this was the ~11s-per-calc cost on full-population syncs).
        /// For large populations we instead read every row for the attribute — an index seek
        /// bounded by the enrolled population for a dedicated sink attribute — and filter in
        /// memory. Small populations (single-person / collapsed downstream stages) keep the
        /// cheap IN() path so they never over-read.
        /// </summary>
        private Dictionary<int, string> ReadExistingAttributeValues( int attributeId, HashSet<int> population, RockContext rockContext )
        {
            const int inClauseThreshold = 2000;
            var query = new AttributeValueService( rockContext ).Queryable().AsNoTracking()
                .Where( av => av.AttributeId == attributeId && av.EntityId.HasValue );

            if ( population.Count <= inClauseThreshold )
            {
                var ids = population.ToList();
                return query.Where( av => ids.Contains( av.EntityId.Value ) )
                    .Select( av => new { av.EntityId, av.Value } )
                    .ToList()
                    .ToDictionary( av => av.EntityId.Value, av => av.Value ?? string.Empty );
            }

            return query
                .Select( av => new { av.EntityId, av.Value } )
                .ToList()
                .Where( av => population.Contains( av.EntityId.Value ) )
                .ToDictionary( av => av.EntityId.Value, av => av.Value ?? string.Empty );
        }

        /// <summary>
        /// Writes AttributeValue rows set-based instead of one ExecuteSqlCommand per person.
        /// Groups writes by target value (so each statement carries a single value parameter),
        /// then issues chunked multi-row INSERTs for new rows and chunked UPDATEs for changed
        /// rows — ~1000 entities per round-trip rather than one. This is the dominant win on
        /// full-population syncs (the program rollup alone was writing 30k+ rows one statement
        /// at a time). Mirrors the raw-SQL + IsPersistedValueDirty convention used elsewhere
        /// here: it bypasses EF change tracking and Rock save hooks; the dirty flag triggers a
        /// lazy recompute of the persisted Value* columns. A failed chunk is logged and the
        /// remaining chunks continue, matching the previous per-batch resilience.
        /// </summary>
        private int BulkWriteAttributeValues( int attributeId, List<AttributeWrite> writes, SyncResult result, Action<int> onProgress = null )
        {
            int written = 0;
            written += WriteAttributeValueChunks( attributeId, writes.Where( w => !w.IsUpdate ).GroupBy( w => w.NewValue ), false, result, written, onProgress );
            written += WriteAttributeValueChunks( attributeId, writes.Where( w => w.IsUpdate ).GroupBy( w => w.NewValue ), true, result, written, onProgress );
            return written;
        }

        private int WriteAttributeValueChunks( int attributeId, IEnumerable<IGrouping<string, AttributeWrite>> groups, bool isUpdate, SyncResult result, int alreadyWritten, Action<int> onProgress )
        {
            const int chunkSize = 1000;
            int writtenHere = 0;

            foreach ( var grp in groups )
            {
                var ids = grp.Select( w => w.PersonId ).ToList();
                for ( int i = 0; i < ids.Count; i += chunkSize )
                {
                    var chunk = ids.GetRange( i, Math.Min( chunkSize, ids.Count - i ) );

                    // args[0]=attributeId, args[1]=value, args[2..]=entityIds → @p0, @p1, @p2..
                    var args = new object[2 + chunk.Count];
                    args[0] = attributeId;
                    args[1] = (object)grp.Key ?? DBNull.Value;
                    for ( int k = 0; k < chunk.Count; k++ )
                    {
                        args[2 + k] = chunk[k];
                    }

                    string sql;
                    if ( isUpdate )
                    {
                        var inList = string.Join( ",", chunk.Select( ( _, k ) => $"@p{2 + k}" ) );
                        sql = $@"UPDATE [AttributeValue] SET [Value] = @p1, [ModifiedDateTime] = GETDATE(), [IsPersistedValueDirty] = 1
WHERE [AttributeId] = @p0 AND [EntityId] IN ({inList})";
                    }
                    else
                    {
                        var valuesList = string.Join( ",", chunk.Select( ( _, k ) => $"(@p{2 + k})" ) );
                        sql = $@"INSERT INTO [AttributeValue] ([IsSystem],[AttributeId],[EntityId],[Value],[Guid],[CreatedDateTime],[ModifiedDateTime],[IsPersistedValueDirty])
SELECT 0, @p0, v.EntityId, @p1, NEWID(), GETDATE(), GETDATE(), 1
FROM ( VALUES {valuesList} ) v(EntityId)
WHERE NOT EXISTS ( SELECT 1 FROM [AttributeValue] av WHERE av.[AttributeId] = @p0 AND av.[EntityId] = v.EntityId )";
                    }

                    try
                    {
                        using ( var writeContext = new RockContext() )
                        {
                            writeContext.Database.ExecuteSqlCommand( sql, args );
                        }
                        writtenHere += chunk.Count;
                        onProgress?.Invoke( alreadyWritten + writtenHere );
                    }
                    catch ( Exception ex )
                    {
                        result.Errors.Add( $"Bulk {( isUpdate ? "update" : "insert" )} of attribute {attributeId} failed for {chunk.Count} row(s): {ex.Message}" );
                        ExceptionLogService.LogException( ex );
                    }
                }
            }

            return writtenHere;
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
            public StageRunSummary Summary { get; set; }
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
            RockContext rockContext,
            ProgramRunSummary progSummary = null )
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

            // Read existing AVs (size-aware: avoids a large-IN clause on full-population syncs).
            Dictionary<int, string> existing;
            using ( var readContext = new RockContext() )
            {
                existing = ReadExistingAttributeValues( targetAttribute.Id, basePopulation, readContext );
            }

            // Track persons transitioning to complete this run (existing != True, new = True)
            // so we can queue rollup-level communications after the write.
            var newlyCompletedPersonIds = program.OnCompleteSystemCommunicationId.HasValue
                ? new HashSet<int>()
                : null;

            // Diff against existing → build the change list, then write it set-based.
            var pendingWrites = new List<AttributeWrite>();
            foreach ( var personId in basePopulation )
            {
                var newValue = completed.Contains( personId ) ? "True" : "False";
                existing.TryGetValue( personId, out var oldValue );
                oldValue = oldValue ?? string.Empty;
                if ( string.Equals( oldValue, newValue, StringComparison.OrdinalIgnoreCase ) )
                {
                    continue;
                }
                if ( newlyCompletedPersonIds != null
                    && newValue.Equals( "True", StringComparison.OrdinalIgnoreCase )
                    && !oldValue.Equals( "True", StringComparison.OrdinalIgnoreCase ) )
                {
                    newlyCompletedPersonIds.Add( personId );
                }

                pendingWrites.Add( new AttributeWrite
                {
                    PersonId = personId,
                    NewValue = newValue,
                    IsUpdate = existing.ContainsKey( personId )
                } );
            }

            int totalToWrite = pendingWrites.Count;
            Report( $"Program '{program.Name}': writing rollup ({totalToWrite:N0} change(s), {completed.Count:N0} complete)" );
            int written = BulkWriteAttributeValues( targetAttribute.Id, pendingWrites, result,
                done =>
                {
                    if ( done == totalToWrite || done % 5000 == 0 )
                    {
                        Report( $"Program '{program.Name}': rollup written {done:N0}/{totalToWrite:N0}" );
                    }
                } );

            result.Updated += written;
            result.Log.Add( $"  Program rollup '{program.Name}': {completed.Count}/{basePopulation.Count} complete, {written} attribute values written." );

            if ( progSummary != null )
            {
                progSummary.HasRollup = true;
                progSummary.RollupAttributeName = targetAttribute.Name;
                progSummary.RollupCompleted = completed.Count;
                progSummary.RollupWritten = written;
            }

            // Program-level transition detection: rollup went False/blank → True this run
            if ( newlyCompletedPersonIds != null && newlyCompletedPersonIds.Count > 0 )
            {
                QueueCommunicationLogs(
                    CommunicationContextType.JourneyProgram,
                    program.Id,
                    program.OnCompleteSystemCommunicationId.Value,
                    newlyCompletedPersonIds,
                    rockContext, result );
            }
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
    /// Result of a Reset Enrollment pass.
    /// </summary>
    public class ResetEnrollmentResult
    {
        /// <summary>Number of enrollment rows hard-deleted.</summary>
        public int Removed { get; set; }
        /// <summary>Auto-enrolled rows left in place (only meaningful when AutoEnrollMode).</summary>
        public int KeptAutoEnrolled { get; set; }
        /// <summary>True if the program auto-enrolls — i.e. only manual adds were removed.</summary>
        public bool AutoEnrollMode { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Result of an enrollment reconciliation pass.
    /// </summary>
    public class ReconcileResult
    {
        public int CandidatePopulation { get; set; }
        public int ActiveAfter { get; set; }
        public int Added { get; set; }
        public int Reactivated { get; set; }
        public int SoftUnenrolled { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Log { get; set; } = new List<string>();
    }

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
        /// <summary>Per-program, per-stage breakdown for the job summary (populated by ProcessGroup).</summary>
        public List<ProgramRunSummary> ProgramSummaries { get; set; } = new List<ProgramRunSummary>();

        public void Merge( SyncResult other )
        {
            Updated += other.Updated;
            Skipped += other.Skipped;
            Errors.AddRange( other.Errors );
            Log.AddRange( other.Log );
            ProgramSummaries.AddRange( other.ProgramSummaries );
        }

        public override string ToString()
        {
            return $"{Updated} updated, {Skipped} skipped, {Errors.Count} error(s)";
        }
    }

    /// <summary>
    /// Per-program breakdown of a full ProcessGroup run, for a human-readable job summary.
    /// </summary>
    public class ProgramRunSummary
    {
        public string ProgramName { get; set; }
        public int Enrollees { get; set; }
        public List<StageRunSummary> Stages { get; set; } = new List<StageRunSummary>();
        public bool HasRollup { get; set; }
        public string RollupAttributeName { get; set; }
        public int RollupCompleted { get; set; }   // people who passed every stage
        public int RollupWritten { get; set; }      // rollup attribute values written this run
    }

    /// <summary>
    /// Per-stage breakdown within a program run. Written/Unchanged count only this stage's
    /// own calc writes — the program rollup is reported separately on ProgramRunSummary.
    /// </summary>
    public class StageRunSummary
    {
        public int Order { get; set; }
        public string Name { get; set; }
        public int CalcCount { get; set; }
        public int Evaluated { get; set; }   // working population entering the stage
        public int Passers { get; set; }     // people who passed the stage gate
        public int Written { get; set; }     // attribute values written by this stage's calcs
        public int Unchanged { get; set; }   // people this stage's calcs left unchanged
        public bool Skipped { get; set; }    // stage skipped because nobody reached it
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
