using System.Collections.Generic;

using Rock.Data;
using Rock.Extension;

namespace com.razayya.CustomPersonAttributeSyncEngine.CalculationTypes
{
    /// <summary>
    /// Base class for all calculation type components. Each implementation defines
    /// how to evaluate a population and produce per-person merge field results.
    /// </summary>
    public abstract class CalculationTypeComponent : Component
    {
        /// <summary>
        /// Gets the display title of this calculation type.
        /// </summary>
        public abstract string Title { get; }

        /// <summary>
        /// Gets the CSS class for the icon representing this calculation type.
        /// </summary>
        public abstract string IconCssClass { get; }

        /// <summary>
        /// Evaluates the calculation against the given population of person IDs.
        /// Returns a dictionary keyed by PersonId, where each value is a dictionary
        /// of merge field names to their resolved values for that person.
        ///
        /// Persons in the input population that do NOT appear in the returned dictionary
        /// are considered non-matches (NoMatchBehavior applies).
        /// </summary>
        /// <param name="rockContext">The Rock context.</param>
        /// <param name="calculation">The calculation entity with loaded attributes.</param>
        /// <param name="populationPersonIds">The set of person IDs to evaluate.</param>
        /// <returns>
        /// Dictionary of PersonId => merge fields. Only matched persons are included.
        /// Standard merge fields: "Matched" (bool). Component-specific fields vary by type.
        /// </returns>
        public abstract Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            Model.Calculation calculation,
            HashSet<int> populationPersonIds );

        /// <summary>
        /// Gets the merge field names that this calculation type provides.
        /// Used for documentation and Lava template help.
        /// </summary>
        public abstract List<MergeFieldInfo> GetMergeFields();
    }

    /// <summary>
    /// Describes a merge field provided by a calculation type.
    /// </summary>
    public class MergeFieldInfo
    {
        /// <summary>
        /// Gets or sets the name of the merge field (e.g., "AttendanceCount").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the data type (e.g., "Boolean", "Integer", "DateTime").
        /// </summary>
        public string DataType { get; set; }
    }
}
