using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Text;

using com.razayya.CustomPersonAttributeSyncEngine.CalculationTypes;
using com.razayya.CustomPersonAttributeSyncEngine.Constants;
using com.razayya.CustomPersonAttributeSyncEngine.Model;

using Microsoft.Extensions.Logging;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Jobs;
using Rock.Logging;
using Rock.Model;
using Rock.Web.Cache;

namespace com.razayya.CustomPersonAttributeSyncEngine.Jobs
{
    /// <summary>
    /// Nightly job that processes all active Calculation Groups in order,
    /// evaluates each Calculation against the scoped population, and writes
    /// computed values to target Person Attributes.
    /// </summary>
    [DisplayName( "Custom Person Attribute Sync Engine" )]
    [Description( "Processes Calculation Groups and writes computed values to Person Attributes based on configured calculation types." )]

    [BooleanField( "Show Debug Logs",
        Description = "Enable this to show detailed DEBUG logging messages for the job.",
        DefaultBooleanValue = false,
        Order = 0,
        Key = AttributeKey.ShowDebug )]

    public class RunAttributeSyncEngine : RockJob
    {
        private bool _showDebug;
        private int _attributesUpdated;
        private int _attributesSkipped;
        private int _errors;

        public override void Execute()
        {
            _showDebug = GetAttributeValue( AttributeKey.ShowDebug ).AsBoolean();
            _attributesUpdated = 0;
            _attributesSkipped = 0;
            _errors = 0;

            var rockContext = new RockContext();
            var groupService = new CalculationGroupService( rockContext );

            var groups = groupService.Queryable()
                .Where( g => g.IsActive )
                .OrderBy( g => g.Order )
                .ThenBy( g => g.Name )
                .ToList();

            if ( groups.Count == 0 )
            {
                Result = "No active Calculation Groups found.";
                return;
            }

            foreach ( var group in groups )
            {
                try
                {
                    ProcessGroup( group, rockContext );
                }
                catch ( Exception ex )
                {
                    _errors++;
                    Logger.LogError( ex, $"Error processing Calculation Group '{group.Name}' (Id: {group.Id})" );
                    Result += $"ERROR: Group '{group.Name}': {ex.Message}\n";
                }
            }

            Result += $"\nCompleted. {_attributesUpdated} attribute(s) updated. {_attributesSkipped} skipped. {_errors} error(s).";
        }

        private void ProcessGroup( CalculationGroup group, RockContext rockContext )
        {
            LogDebug( $"Processing Group: {group.Name} (Id: {group.Id})" );

            // Build base population
            var basePopulation = BuildBasePopulation( group, rockContext );

            LogDebug( $"  Base population: {basePopulation.Count} people" );

            // Process sub-groups in order
            var subGroups = new CalculationSubGroupService( rockContext ).Queryable()
                .Where( sg => sg.CalculationGroupId == group.Id && sg.IsActive )
                .OrderBy( sg => sg.Order )
                .ThenBy( sg => sg.Name )
                .ToList();

            HashSet<int> previousSubGroupPassers = null;

            foreach ( var subGroup in subGroups )
            {
                try
                {
                    previousSubGroupPassers = ProcessSubGroup( subGroup, basePopulation, previousSubGroupPassers, rockContext );
                }
                catch ( Exception ex )
                {
                    _errors++;
                    Logger.LogError( ex, $"Error processing SubGroup '{subGroup.Name}' (Id: {subGroup.Id})" );
                    Result += $"  ERROR: SubGroup '{subGroup.Name}': {ex.Message}\n";
                    // On error, pass the full base population forward so subsequent sub-groups aren't blocked
                    previousSubGroupPassers = basePopulation;
                }
            }

            // Update LastRunDateTime
            group.LastRunDateTime = RockDateTime.Now;
            rockContext.SaveChanges();

            Result += $"Group '{group.Name}': {basePopulation.Count} people, {subGroups.Count} sub-groups.\n";
        }

        private HashSet<int> BuildBasePopulation( CalculationGroup group, RockContext rockContext )
        {
            var personService = new PersonService( rockContext );
            var query = personService.Queryable().AsNoTracking();

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

            var personIds = query.Select( p => p.Id ).ToList();
            var result = new HashSet<int>( personIds );

            // Apply DataView filter if configured
            if ( group.DataViewId.HasValue )
            {
                var dataViewService = new DataViewService( rockContext );
                var dataView = dataViewService.Get( group.DataViewId.Value );
                if ( dataView != null )
                {
                    var dataViewPersonIds = dataView
                        .GetQuery( new DataViewGetQueryArgs { DbContext = rockContext } )
                        .Select( e => e.Id )
                        .ToHashSet();

                    result.IntersectWith( dataViewPersonIds );
                }
            }

            return result;
        }

        private HashSet<int> ProcessSubGroup(
            CalculationSubGroup subGroup,
            HashSet<int> basePopulation,
            HashSet<int> previousSubGroupPassers,
            RockContext rockContext )
        {
            LogDebug( $"  Processing SubGroup: {subGroup.Name} (Id: {subGroup.Id})" );

            // Determine the working population for this sub-group
            HashSet<int> workingPopulation;

            if ( subGroup.ScopeToPreviousSubGroup && previousSubGroupPassers != null )
            {
                workingPopulation = new HashSet<int>( previousSubGroupPassers );
            }
            else
            {
                workingPopulation = new HashSet<int>( basePopulation );
            }

            // Apply additional DataView filter
            if ( subGroup.AdditionalDataViewId.HasValue )
            {
                var dataViewService = new DataViewService( rockContext );
                var dataView = dataViewService.Get( subGroup.AdditionalDataViewId.Value );
                if ( dataView != null )
                {
                    var dataViewPersonIds = dataView
                        .GetQuery( new DataViewGetQueryArgs { DbContext = rockContext } )
                        .Select( e => e.Id )
                        .ToHashSet();

                    workingPopulation.IntersectWith( dataViewPersonIds );
                }
            }

            LogDebug( $"    Working population: {workingPopulation.Count} people" );

            // Process calculations in order
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
                    var passers = ProcessCalculation( calculation, workingPopulation, rockContext );

                    // If this is a Completion calculation, track its passers for the funnel
                    var componentType = calculation.CalculationTypeEntityType?.Name ?? string.Empty;
                    if ( componentType.Contains( "CompletionCalculation" ) )
                    {
                        completionPassers = passers;
                    }
                }
                catch ( Exception ex )
                {
                    _errors++;
                    Logger.LogError( ex, $"Error processing Calculation '{calculation.Name}' (Id: {calculation.Id})" );
                    Result += $"    ERROR: Calculation '{calculation.Name}': {ex.Message}\n";
                }
            }

            // Return the completion passers for funnel scoping, or the full working population if no completion calc
            return completionPassers ?? workingPopulation;
        }

        private HashSet<int> ProcessCalculation(
            Model.Calculation calculation,
            HashSet<int> workingPopulation,
            RockContext rockContext )
        {
            LogDebug( $"    Processing Calculation: {calculation.Name} (Id: {calculation.Id})" );

            // Resolve the component
            var entityType = EntityTypeCache.Get( calculation.CalculationTypeEntityTypeId );
            if ( entityType == null )
            {
                throw new Exception( $"CalculationType EntityType Id {calculation.CalculationTypeEntityTypeId} not found." );
            }

            var component = CalculationTypeContainer.GetComponent( entityType.Name );
            if ( component == null )
            {
                throw new Exception( $"CalculationType component '{entityType.Name}' not found or not active." );
            }

            // Load component attributes for this specific calculation
            calculation.LoadAttributes( rockContext );

            // Evaluate
            var matchedResults = component.Evaluate( rockContext, calculation, workingPopulation );

            LogDebug( $"      Matched: {matchedResults.Count} / {workingPopulation.Count}" );

            // Resolve the target attribute
            var targetAttribute = AttributeCache.Get( calculation.PersonAttributeId );
            if ( targetAttribute == null )
            {
                throw new Exception( $"Target Person Attribute Id {calculation.PersonAttributeId} not found." );
            }

            // Write results
            var personService = new PersonService( rockContext );
            int historyEntityTypeId = EntityTypeCache.Get( typeof( Person ) ).Id;
            int calcGroupEntityTypeId = EntityTypeCache.Get( typeof( CalculationGroup ) ).Id;

            foreach ( var personId in workingPopulation )
            {
                string newValue = null;

                if ( matchedResults.TryGetValue( personId, out var mergeFields ) )
                {
                    // Matched — resolve the result Lava
                    if ( !string.IsNullOrWhiteSpace( calculation.ResultLavaTemplate ) )
                    {
                        newValue = calculation.ResultLavaTemplate.ResolveMergeFields( mergeFields );
                    }
                    else
                    {
                        // Default: write "True" for boolean, current datetime for DateTime
                        newValue = mergeFields.ContainsKey( "Matched" ) ? mergeFields["Matched"]?.ToString() : "True";
                    }
                }
                else
                {
                    // Not matched
                    if ( calculation.NoMatchBehavior == NoMatchBehavior.LeaveUnchanged )
                    {
                        _attributesSkipped++;
                        continue;
                    }
                    else if ( !string.IsNullOrWhiteSpace( calculation.NoMatchLavaTemplate ) )
                    {
                        newValue = calculation.NoMatchLavaTemplate.ResolveMergeFields( new Dictionary<string, object> { { "Matched", false } } );
                    }
                }

                if ( newValue != null )
                {
                    // Use a fresh context to avoid large change tracking overhead
                    using ( var writeContext = new RockContext() )
                    {
                        var person = new PersonService( writeContext ).Get( personId );
                        if ( person != null )
                        {
                            person.LoadAttributes( writeContext );
                            var existingValue = person.GetAttributeValue( targetAttribute.Key );

                            if ( !string.Equals( existingValue, newValue, StringComparison.OrdinalIgnoreCase ) )
                            {
                                person.SetAttributeValue( targetAttribute.Key, newValue );
                                person.SaveAttributeValue( targetAttribute.Key, writeContext );

                                // Record history
                                var historyService = new HistoryService( writeContext );
                                historyService.Add( new History
                                {
                                    EntityTypeId = historyEntityTypeId,
                                    EntityId = personId,
                                    CategoryId = GetOrCreateHistoryCategoryId( writeContext ),
                                    Verb = "MODIFY",
                                    ChangeType = "Attribute Sync",
                                    ValueName = targetAttribute.Name,
                                    OldValue = existingValue,
                                    NewValue = newValue,
                                    Caption = $"Sync Engine: {calculation.Name}",
                                    RelatedEntityTypeId = calcGroupEntityTypeId,
                                    RelatedEntityId = calculation.CalculationSubGroup?.CalculationGroupId
                                } );

                                writeContext.SaveChanges();
                                _attributesUpdated++;
                            }
                            else
                            {
                                _attributesSkipped++;
                            }
                        }
                    }
                }
            }

            return new HashSet<int>( matchedResults.Keys );
        }

        private int? _historyCategoryId;
        private int GetOrCreateHistoryCategoryId( RockContext rockContext )
        {
            if ( _historyCategoryId.HasValue )
            {
                return _historyCategoryId.Value;
            }

            int historyEntityTypeId = EntityTypeCache.Get( "Rock.Model.History" ).Id;
            var categoryService = new CategoryService( rockContext );
            var category = categoryService.Queryable()
                .FirstOrDefault( c => c.EntityTypeId == historyEntityTypeId && c.Name == "Attribute Sync Engine" );

            if ( category == null )
            {
                category = new Category
                {
                    EntityTypeId = historyEntityTypeId,
                    Name = "Attribute Sync Engine",
                    IconCssClass = "fa fa-sync",
                    IsSystem = false
                };
                categoryService.Add( category );
                rockContext.SaveChanges();
            }

            _historyCategoryId = category.Id;
            return category.Id;
        }

        private void LogDebug( string message )
        {
            if ( _showDebug )
            {
                Result += message + "\n";
                Logger.LogDebug( message );
            }
        }
    }
}
