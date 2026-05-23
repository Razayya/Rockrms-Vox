using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

using com.razayya.JourneyTrack.Model;

using Newtonsoft.Json;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace com.razayya.JourneyTrack.Data
{
    /// <summary>
    /// Service for exporting, importing, and copying Journey Programs and their children.
    /// </summary>
    public class ImportExportService
    {
        #region Export

        /// <summary>
        /// Exports a JourneyProgram and all children to a portable JSON string.
        /// </summary>
        public string ExportGroup( int calculationGroupId )
        {
            var export = BuildGroupExport( calculationGroupId );
            if ( export == null )
            {
                return null;
            }

            return JsonConvert.SerializeObject( export, Formatting.Indented );
        }

        /// <summary>
        /// Exports a Stage and its Calculations to a portable JSON string.
        /// </summary>
        public string ExportSubGroup( int calculationSubGroupId )
        {
            using ( var rockContext = new RockContext() )
            {
                var subGroup = new StageService( rockContext ).Queryable().AsNoTracking()
                    .Include( sg => sg.AdditionalDataView )
                    .Include( sg => sg.Calculations.Select( c => c.CalculationTypeEntityType ) )
                    .Include( sg => sg.Calculations.Select( c => c.PersonAttribute ) )
                    .FirstOrDefault( sg => sg.Id == calculationSubGroupId );

                if ( subGroup == null )
                {
                    return null;
                }

                var export = BuildSubGroupExport( subGroup, rockContext );
                return JsonConvert.SerializeObject( export, Formatting.Indented );
            }
        }

        private JourneyProgramExport BuildGroupExport( int calculationGroupId )
        {
            using ( var rockContext = new RockContext() )
            {
                var group = new JourneyProgramService( rockContext ).Queryable().AsNoTracking()
                    .Include( g => g.RecordStatusValue )
                    .Include( g => g.ConnectionStatusValue )
                    .Include( g => g.Campus )
                    .Include( g => g.DataView )
                    .Include( g => g.Stages.Select( sg => sg.AdditionalDataView ) )
                    .Include( g => g.Stages.Select( sg => sg.Calculations.Select( c => c.CalculationTypeEntityType ) ) )
                    .Include( g => g.Stages.Select( sg => sg.Calculations.Select( c => c.PersonAttribute ) ) )
                    .FirstOrDefault( g => g.Id == calculationGroupId );

                if ( group == null )
                {
                    return null;
                }

                var export = new JourneyProgramExport
                {
                    Name = group.Name,
                    Description = group.Description,
                    IsActive = group.IsActive,
                    Order = group.Order,
                    RecordStatusValueGuid = group.RecordStatusValue?.Guid.ToString(),
                    ConnectionStatusValueGuid = group.ConnectionStatusValue?.Guid.ToString(),
                    CampusGuid = group.Campus?.Guid.ToString(),
                    DataViewGuid = group.DataView?.Guid.ToString()
                };

                foreach ( var subGroup in group.Stages.OrderBy( sg => sg.Order ).ThenBy( sg => sg.Name ) )
                {
                    export.SubGroups.Add( BuildSubGroupExport( subGroup, rockContext ) );
                }

                return export;
            }
        }

        private StageExport BuildSubGroupExport( Stage subGroup, RockContext rockContext )
        {
            var sgExport = new StageExport
            {
                Name = subGroup.Name,
                Description = subGroup.Description,
                IsActive = subGroup.IsActive,
                Order = subGroup.Order,
                PrerequisiteStageNames = ResolvePrerequisiteNames( subGroup, rockContext ),
                AdditionalDataViewGuid = subGroup.AdditionalDataView?.Guid.ToString()
            };

            foreach ( var calc in subGroup.Calculations.OrderBy( c => c.Order ).ThenBy( c => c.Name ) )
            {
                sgExport.Calculations.Add( BuildJourneyCalculationExport( calc, rockContext ) );
            }

            return sgExport;
        }

        private JourneyCalculationExport BuildJourneyCalculationExport( JourneyCalculation calc, RockContext rockContext )
        {
            var calcExport = new JourneyCalculationExport
            {
                Name = calc.Name,
                Description = calc.Description,
                IsActive = calc.IsActive,
                Order = calc.Order,
                PersonAttributeKey = calc.PersonAttribute?.Key,
                CalculationTypeEntityTypeName = calc.CalculationTypeEntityType?.Name,
                ResultLavaTemplate = calc.ResultLavaTemplate,
                NoMatchBehavior = calc.NoMatchBehavior,
                NoMatchLavaTemplate = calc.NoMatchLavaTemplate
            };

            // Export component-specific attributes as key/value pairs
            calc.LoadAttributes( rockContext );
            foreach ( var attr in calc.Attributes )
            {
                var value = calc.GetAttributeValue( attr.Key );
                if ( !string.IsNullOrWhiteSpace( value ) )
                {
                    calcExport.ComponentAttributes[attr.Key] = value;
                }
            }

            return calcExport;
        }

        #endregion

        #region Import

        /// <summary>
        /// Imports a JourneyProgram from a JSON string.
        /// </summary>
        public ImportResult ImportGroup( string json )
        {
            var result = new ImportResult();

            JourneyProgramExport export;
            try
            {
                export = JsonConvert.DeserializeObject<JourneyProgramExport>( json );
            }
            catch ( Exception ex )
            {
                result.Errors.Add( $"Invalid JSON: {ex.Message}" );
                return result;
            }

            if ( export == null )
            {
                result.Errors.Add( "Import data is empty." );
                return result;
            }

            using ( var rockContext = new RockContext() )
            {
                var group = new JourneyProgram
                {
                    Name = export.Name,
                    Description = export.Description,
                    IsActive = export.IsActive,
                    Order = export.Order
                };

                // Resolve population filter references
                group.RecordStatusValueId = ResolveDefinedValueId( export.RecordStatusValueGuid, "Record Status", result );
                group.ConnectionStatusValueId = ResolveDefinedValueId( export.ConnectionStatusValueGuid, "Connection Status", result );
                group.CampusId = ResolveCampusId( export.CampusGuid, result );
                group.DataViewId = ResolveDataViewId( export.DataViewGuid, "Group DataView", rockContext, result );

                new JourneyProgramService( rockContext ).Add( group );
                rockContext.SaveChanges();
                result.GroupsCreated++;

                // Import sub-groups
                foreach ( var sgExport in export.SubGroups.OrderBy( sg => sg.Order ) )
                {
                    ImportSubGroupInto( group.Id, sgExport, rockContext, result );
                }

                // Second pass: resolve prerequisite names to IDs now that all sub-groups exist
                ResolveImportedPrerequisites( group.Id, export.SubGroups, rockContext );
            }

            result.WasSuccessful = result.Errors.Count == 0;
            return result;
        }

        /// <summary>
        /// Imports a Stage from JSON into an existing group.
        /// </summary>
        public ImportResult ImportSubGroup( string json, int targetGroupId )
        {
            var result = new ImportResult();

            StageExport export;
            try
            {
                export = JsonConvert.DeserializeObject<StageExport>( json );
            }
            catch ( Exception ex )
            {
                result.Errors.Add( $"Invalid JSON: {ex.Message}" );
                return result;
            }

            if ( export == null )
            {
                result.Errors.Add( "Import data is empty." );
                return result;
            }

            using ( var rockContext = new RockContext() )
            {
                ImportSubGroupInto( targetGroupId, export, rockContext, result );
            }

            result.WasSuccessful = result.Errors.Count == 0;
            return result;
        }

        private void ImportSubGroupInto( int groupId, StageExport sgExport, RockContext rockContext, ImportResult result )
        {
            var subGroup = new Stage
            {
                JourneyProgramId = groupId,
                Name = sgExport.Name,
                Description = sgExport.Description,
                IsActive = sgExport.IsActive,
                Order = sgExport.Order,
                AdditionalDataViewId = ResolveDataViewId( sgExport.AdditionalDataViewGuid, $"SubGroup '{sgExport.Name}' DataView", rockContext, result )
            };

            new StageService( rockContext ).Add( subGroup );
            rockContext.SaveChanges();
            result.SubGroupsCreated++;

            foreach ( var calcExport in sgExport.Calculations.OrderBy( c => c.Order ) )
            {
                ImportCalculationInto( subGroup.Id, calcExport, rockContext, result );
            }
        }

        private void ImportCalculationInto( int subGroupId, JourneyCalculationExport calcExport, RockContext rockContext, ImportResult result )
        {
            var calc = new JourneyCalculation
            {
                StageId = subGroupId,
                Name = calcExport.Name,
                Description = calcExport.Description,
                IsActive = calcExport.IsActive,
                Order = calcExport.Order,
                ResultLavaTemplate = calcExport.ResultLavaTemplate,
                NoMatchBehavior = calcExport.NoMatchBehavior,
                NoMatchLavaTemplate = calcExport.NoMatchLavaTemplate
            };

            // Resolve PersonAttribute by key
            if ( !string.IsNullOrWhiteSpace( calcExport.PersonAttributeKey ) )
            {
                var attr = AttributeCache.All()
                    .FirstOrDefault( a =>
                        a.Key == calcExport.PersonAttributeKey &&
                        a.EntityTypeId == EntityTypeCache.GetId( typeof( Rock.Model.Person ) ) );

                if ( attr != null )
                {
                    calc.PersonAttributeId = attr.Id;
                }
                else
                {
                    result.Warnings.Add( $"JourneyCalculation '{calcExport.Name}': Person attribute '{calcExport.PersonAttributeKey}' not found." );
                    calc.IsActive = false;
                }
            }

            // Resolve CalculationType by entity type name
            if ( !string.IsNullOrWhiteSpace( calcExport.CalculationTypeEntityTypeName ) )
            {
                var entityType = EntityTypeCache.All()
                    .FirstOrDefault( et => et.Name == calcExport.CalculationTypeEntityTypeName );

                if ( entityType != null )
                {
                    calc.CalculationTypeEntityTypeId = entityType.Id;
                }
                else
                {
                    result.Warnings.Add( $"JourneyCalculation '{calcExport.Name}': JourneyCalculation type '{calcExport.CalculationTypeEntityTypeName}' not found." );
                    calc.IsActive = false;
                }
            }

            // Note: PersonAttributeId is nullable now (transient calcs are valid).
            // Only the CalculationType is strictly required.
            if ( calc.CalculationTypeEntityTypeId == 0 )
            {
                result.Warnings.Add( $"JourneyCalculation '{calcExport.Name}' imported as inactive due to missing references." );
                calc.IsActive = false;
            }

            new JourneyCalculationService( rockContext ).Add( calc );
            rockContext.SaveChanges();
            result.CalculationsCreated++;

            // Import component attributes
            if ( calcExport.ComponentAttributes != null && calcExport.ComponentAttributes.Count > 0 )
            {
                calc.LoadAttributes( rockContext );
                foreach ( var kvp in calcExport.ComponentAttributes )
                {
                    calc.SetAttributeValue( kvp.Key, kvp.Value );
                }
                calc.SaveAttributeValues( rockContext );
            }
        }

        #endregion

        #region Copy

        /// <summary>
        /// Creates a deep copy of a JourneyProgram with all SubGroups and Calculations.
        /// </summary>
        public int CopyGroup( int calculationGroupId )
        {
            using ( var rockContext = new RockContext() )
            {
                var source = new JourneyProgramService( rockContext ).Queryable().AsNoTracking()
                    .Include( g => g.Stages.Select( sg => sg.Calculations ) )
                    .FirstOrDefault( g => g.Id == calculationGroupId );

                if ( source == null )
                {
                    return 0;
                }

                var copy = new JourneyProgram
                {
                    Name = source.Name + " (Copy)",
                    Description = source.Description,
                    IsActive = false,
                    Order = source.Order,
                    RecordStatusValueId = source.RecordStatusValueId,
                    ConnectionStatusValueId = source.ConnectionStatusValueId,
                    CampusId = source.CampusId,
                    DataViewId = source.DataViewId
                };

                new JourneyProgramService( rockContext ).Add( copy );
                rockContext.SaveChanges();

                foreach ( var sg in source.Stages.OrderBy( sg => sg.Order ) )
                {
                    CopySubGroupInto( copy.Id, sg, rockContext );
                }

                return copy.Id;
            }
        }

        /// <summary>
        /// Creates a deep copy of a Stage with all Calculations.
        /// </summary>
        public int CopySubGroup( int calculationSubGroupId )
        {
            using ( var rockContext = new RockContext() )
            {
                var source = new StageService( rockContext ).Queryable().AsNoTracking()
                    .Include( sg => sg.Calculations )
                    .FirstOrDefault( sg => sg.Id == calculationSubGroupId );

                if ( source == null )
                {
                    return 0;
                }

                return CopySubGroupInto( source.JourneyProgramId, source, rockContext, appendCopySuffix: true );
            }
        }

        /// <summary>
        /// Creates a copy of a single calc.
        /// </summary>
        public int CopyCalculation( int calculationId )
        {
            using ( var rockContext = new RockContext() )
            {
                var source = new JourneyCalculationService( rockContext ).Queryable().AsNoTracking()
                    .FirstOrDefault( c => c.Id == calculationId );

                if ( source == null )
                {
                    return 0;
                }

                return CopyCalculationInto( source.StageId, source, rockContext, appendCopySuffix: true );
            }
        }

        private int CopySubGroupInto( int targetGroupId, Stage source, RockContext rockContext, bool appendCopySuffix = false )
        {
            var copy = new Stage
            {
                JourneyProgramId = targetGroupId,
                Name = appendCopySuffix ? source.Name + " (Copy)" : source.Name,
                Description = source.Description,
                IsActive = source.IsActive,
                Order = source.Order,
                PrerequisiteStageIds = source.PrerequisiteStageIds,
                AdditionalDataViewId = source.AdditionalDataViewId
            };

            new StageService( rockContext ).Add( copy );
            rockContext.SaveChanges();

            foreach ( var calc in source.Calculations.OrderBy( c => c.Order ) )
            {
                CopyCalculationInto( copy.Id, calc, rockContext );
            }

            return copy.Id;
        }

        private int CopyCalculationInto( int targetSubGroupId, JourneyCalculation source, RockContext rockContext, bool appendCopySuffix = false )
        {
            var copy = new JourneyCalculation
            {
                StageId = targetSubGroupId,
                Name = appendCopySuffix ? source.Name + " (Copy)" : source.Name,
                Description = source.Description,
                IsActive = source.IsActive,
                Order = source.Order,
                PersonAttributeId = source.PersonAttributeId,
                CalculationTypeEntityTypeId = source.CalculationTypeEntityTypeId,
                ResultLavaTemplate = source.ResultLavaTemplate,
                NoMatchBehavior = source.NoMatchBehavior,
                NoMatchLavaTemplate = source.NoMatchLavaTemplate
            };

            new JourneyCalculationService( rockContext ).Add( copy );
            rockContext.SaveChanges();

            // Copy component attributes
            source.LoadAttributes( rockContext );
            copy.LoadAttributes( rockContext );
            foreach ( var attr in source.Attributes )
            {
                var value = source.GetAttributeValue( attr.Key );
                if ( !string.IsNullOrWhiteSpace( value ) )
                {
                    copy.SetAttributeValue( attr.Key, value );
                }
            }
            copy.SaveAttributeValues( rockContext );

            return copy.Id;
        }

        #endregion

        #region Reference Resolution Helpers

        private static int? ResolveDefinedValueId( string guidString, string label, ImportResult result )
        {
            if ( string.IsNullOrWhiteSpace( guidString ) )
            {
                return null;
            }

            var guid = guidString.AsGuidOrNull();
            if ( !guid.HasValue )
            {
                return null;
            }

            var dv = DefinedValueCache.Get( guid.Value );
            if ( dv != null )
            {
                return dv.Id;
            }

            result.Warnings.Add( $"{label} '{guidString}' not found on this instance." );
            return null;
        }

        private static int? ResolveCampusId( string guidString, ImportResult result )
        {
            if ( string.IsNullOrWhiteSpace( guidString ) )
            {
                return null;
            }

            var guid = guidString.AsGuidOrNull();
            if ( !guid.HasValue )
            {
                return null;
            }

            var campus = CampusCache.Get( guid.Value );
            if ( campus != null )
            {
                return campus.Id;
            }

            result.Warnings.Add( $"Campus '{guidString}' not found on this instance." );
            return null;
        }

        private static int? ResolveDataViewId( string guidString, string label, RockContext rockContext, ImportResult result )
        {
            if ( string.IsNullOrWhiteSpace( guidString ) )
            {
                return null;
            }

            var guid = guidString.AsGuidOrNull();
            if ( !guid.HasValue )
            {
                return null;
            }

            var dv = new DataViewService( rockContext ).Get( guid.Value );
            if ( dv != null )
            {
                return dv.Id;
            }

            result.Warnings.Add( $"{label} '{guidString}' not found on this instance." );
            return null;
        }

        private string ResolvePrerequisiteNames( Stage subGroup, RockContext rockContext )
        {
            if ( string.IsNullOrWhiteSpace( subGroup.PrerequisiteStageIds ) )
            {
                return null;
            }

            var ids = subGroup.PrerequisiteStageIds
                .Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries )
                .Select( s => s.Trim().AsInteger() )
                .Where( id => id > 0 )
                .ToList();

            if ( !ids.Any() )
            {
                return null;
            }

            var names = new StageService( rockContext ).Queryable()
                .Where( sg => ids.Contains( sg.Id ) )
                .Select( sg => sg.Name )
                .ToList();

            return string.Join( ",", names );
        }

        private void ResolveImportedPrerequisites( int groupId, List<StageExport> exports, RockContext rockContext )
        {
            var siblings = new StageService( rockContext ).Queryable()
                .Where( sg => sg.JourneyProgramId == groupId )
                .Select( sg => new { sg.Id, sg.Name } )
                .ToList();

            foreach ( var sgExport in exports )
            {
                if ( string.IsNullOrWhiteSpace( sgExport.PrerequisiteStageNames ) )
                {
                    continue;
                }

                var prereqNames = sgExport.PrerequisiteStageNames
                    .Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries )
                    .Select( n => n.Trim() )
                    .ToList();

                var matchedIds = siblings
                    .Where( s => prereqNames.Contains( s.Name, StringComparer.OrdinalIgnoreCase ) )
                    .Select( s => s.Id )
                    .ToList();

                if ( matchedIds.Any() )
                {
                    var subGroup = new StageService( rockContext ).Queryable()
                        .FirstOrDefault( sg => sg.JourneyProgramId == groupId && sg.Name == sgExport.Name );

                    if ( subGroup != null )
                    {
                        subGroup.PrerequisiteStageIds = string.Join( ",", matchedIds );
                    }
                }
            }

            rockContext.SaveChanges();
        }

        #endregion
    }
}
