using System;
using System.Collections.Generic;
using System.Linq;

using Rock.Data;
using Rock.Extension;
using Rock.Web.Cache;

namespace com.razayya.CustomPersonAttributeSyncEngine.CalculationTypes
{
    /// <summary>
    /// Base class for all calculation type components. Each implementation defines
    /// how to evaluate a population and produce per-person merge field results.
    /// </summary>
    public abstract class CalculationTypeComponent : Component
    {
        /// <summary>
        /// Gets the attribute value defaults.
        /// </summary>
        public override Dictionary<string, string> AttributeValueDefaults
        {
            get => new Dictionary<string, string>
            {
                { "Active", "True" },
                { "Order", "0" }
            };
        }

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

        #region Static Factory

        private static readonly Dictionary<string, Lazy<CalculationTypeComponent>> _componentsByTypeName =
            new Dictionary<string, Lazy<CalculationTypeComponent>>( StringComparer.OrdinalIgnoreCase )
            {
                { typeof( AttendanceCalculation ).FullName, new Lazy<CalculationTypeComponent>( () => new AttendanceCalculation() ) },
                { typeof( CompletionCalculation ).FullName, new Lazy<CalculationTypeComponent>( () => new CompletionCalculation() ) },
                { typeof( DataViewInclusionCalculation ).FullName, new Lazy<CalculationTypeComponent>( () => new DataViewInclusionCalculation() ) },
                { typeof( GroupMembershipCalculation ).FullName, new Lazy<CalculationTypeComponent>( () => new GroupMembershipCalculation() ) },
                { typeof( PersonFilterCalculation ).FullName, new Lazy<CalculationTypeComponent>( () => new PersonFilterCalculation() ) },
            };

        /// <summary>
        /// Gets a component instance by its EntityType name.
        /// </summary>
        public static CalculationTypeComponent GetComponent( string entityTypeName )
        {
            if ( string.IsNullOrWhiteSpace( entityTypeName ) )
            {
                return null;
            }

            return _componentsByTypeName.TryGetValue( entityTypeName, out var lazy ) ? lazy.Value : null;
        }

        /// <summary>
        /// Gets all registered calculation types as EntityTypeId/Title pairs for use in dropdowns.
        /// </summary>
        public static List<CalculationTypeInfo> GetAllTypes()
        {
            return _componentsByTypeName.Values
                .Select( lazy =>
                {
                    var component = lazy.Value;
                    var entityType = EntityTypeCache.Get( component.GetType() );
                    return new CalculationTypeInfo
                    {
                        EntityTypeId = entityType?.Id ?? 0,
                        EntityTypeGuid = entityType?.Guid ?? Guid.Empty,
                        Title = component.Title,
                        IconCssClass = component.IconCssClass
                    };
                } )
                .Where( t => t.EntityTypeId > 0 )
                .OrderBy( t => t.Title )
                .ToList();
        }

        #endregion
    }

    /// <summary>
    /// Info about a registered calculation type for use in pickers.
    /// </summary>
    public class CalculationTypeInfo
    {
        public int EntityTypeId { get; set; }
        public Guid EntityTypeGuid { get; set; }
        public string Title { get; set; }
        public string IconCssClass { get; set; }
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
