using System.Collections.Generic;

namespace com.razayya.JourneyTrack.Model
{
    /// <summary>
    /// Portable export format for a JourneyProgram and all its children.
    /// Uses GUIDs and keys (not IDs) so the export is portable between Rock instances.
    /// </summary>
    public class JourneyProgramExport
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public int Order { get; set; }

        // Population filters — referenced by Guid so they resolve on any instance
        public string RecordStatusValueGuid { get; set; }
        public string ConnectionStatusValueGuid { get; set; }
        public string CampusGuid { get; set; }
        public string DataViewGuid { get; set; }

        public List<StageExport> SubGroups { get; set; } = new List<StageExport>();
    }

    /// <summary>
    /// Portable export format for a Stage and its Calculations.
    /// </summary>
    public class StageExport
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public int Order { get; set; }
        public string PrerequisiteStageNames { get; set; }

        public string AdditionalDataViewGuid { get; set; }

        public List<JourneyCalculationExport> Calculations { get; set; } = new List<JourneyCalculationExport>();
    }

    /// <summary>
    /// Portable export format for a JourneyCalculation and its component attributes.
    /// </summary>
    public class JourneyCalculationExport
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public int Order { get; set; }

        // Target attribute identified by key (stable across instances)
        public string PersonAttributeKey { get; set; }

        // Component type identified by entity type name (stable across instances)
        public string CalculationTypeEntityTypeName { get; set; }

        public string ResultLavaTemplate { get; set; }
        public NoMatchBehavior NoMatchBehavior { get; set; }
        public string NoMatchLavaTemplate { get; set; }

        // Component-specific attribute key/value pairs
        public Dictionary<string, string> ComponentAttributes { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Result of an import operation with summary and any warnings.
    /// </summary>
    public class ImportResult
    {
        public bool WasSuccessful { get; set; }
        public int GroupsCreated { get; set; }
        public int SubGroupsCreated { get; set; }
        public int CalculationsCreated { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }
}
