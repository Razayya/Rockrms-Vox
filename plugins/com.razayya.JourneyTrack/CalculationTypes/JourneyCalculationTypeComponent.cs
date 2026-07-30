using System;
using System.Collections.Generic;
using System.Linq;

using Rock.Data;
using Rock.Extension;
using Rock.Web.Cache;

namespace com.razayya.JourneyTrack.CalculationTypes
{
    /// <summary>
    /// Base class for all JourneyCalculation type components. Each implementation defines
    /// how to evaluate a population and produce per-person merge field results.
    /// </summary>
    public abstract class JourneyCalculationTypeComponent : Component
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
        /// Gets the display title of this JourneyCalculation type.
        /// </summary>
        public abstract string Title { get; }

        /// <summary>
        /// Gets the CSS class for the icon representing this JourneyCalculation type.
        /// </summary>
        public abstract string IconCssClass { get; }

        /// <summary>
        /// Evaluates the JourneyCalculation against the given population of person IDs.
        /// Returns a dictionary keyed by PersonId, where each value is a dictionary
        /// of merge field names to their resolved values for that person.
        ///
        /// Persons in the input population that do NOT appear in the returned dictionary
        /// are considered non-matches (NoMatchBehavior applies).
        /// </summary>
        /// <param name="rockContext">The Rock context.</param>
        /// <param name="JourneyCalculation">The JourneyCalculation entity with loaded attributes.</param>
        /// <param name="populationPersonIds">The set of person IDs to evaluate.</param>
        /// <returns>
        /// Dictionary of PersonId => merge fields. Only matched persons are included.
        /// Standard merge fields: "Matched" (bool). Component-specific fields vary by type.
        /// </returns>
        public abstract Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            Model.JourneyCalculation calc,
            HashSet<int> populationPersonIds );

        /// <summary>
        /// Gets the merge field names that this JourneyCalculation type provides.
        /// Used for documentation and Lava template help.
        /// </summary>
        public abstract List<MergeFieldInfo> GetMergeFields();

        /// <summary>
        /// Describes one person's progress toward this JourneyCalculation's requirement.
        /// Unlike <see cref="Evaluate"/> — which omits non-matching persons entirely — this
        /// always returns a result, including partial progress for someone who has not yet
        /// met the requirement (e.g. 3 of 4 attendances). Read-only; never writes sinks.
        ///
        /// The returned dictionary always contains:
        ///   Matched (bool)     - whether the requirement is currently met
        ///   Current (decimal)  - progress so far, in the requirement's own unit
        ///   Target  (decimal)  - the configured requirement threshold
        /// plus any type-specific merge fields (see <see cref="GetMergeFields"/>).
        ///
        /// The default implementation evaluates a 1-person population and reports a
        /// boolean-shaped requirement (Current = Matched ? 1 : 0, Target = 1). Override
        /// where partial progress is quantifiable (attendance counts, watch percentages).
        /// </summary>
        public virtual Dictionary<string, object> DescribeProgress(
            RockContext rockContext,
            Model.JourneyCalculation calc,
            int personId )
        {
            var evaluated = Evaluate( rockContext, calc, new HashSet<int> { personId } );

            var progress = evaluated != null && evaluated.TryGetValue( personId, out var mergeFields )
                ? new Dictionary<string, object>( mergeFields )
                : new Dictionary<string, object>();

            bool matched = progress.TryGetValue( "Matched", out var matchedValue )
                && matchedValue is bool matchedBool
                && matchedBool;

            progress["Matched"] = matched;
            progress["Current"] = matched ? 1m : 0m;
            progress["Target"] = 1m;

            return progress;
        }

        #region Static Factory

        private static readonly Dictionary<string, Lazy<JourneyCalculationTypeComponent>> _componentsByTypeName =
            new Dictionary<string, Lazy<JourneyCalculationTypeComponent>>( StringComparer.OrdinalIgnoreCase )
            {
                { typeof( AttendanceCalculation ).FullName, new Lazy<JourneyCalculationTypeComponent>( () => new AttendanceCalculation() ) },
                { typeof( GroupAttendanceCalculation ).FullName, new Lazy<JourneyCalculationTypeComponent>( () => new GroupAttendanceCalculation() ) },
                { typeof( CompletionCalculation ).FullName, new Lazy<JourneyCalculationTypeComponent>( () => new CompletionCalculation() ) },
                { typeof( DataViewInclusionCalculation ).FullName, new Lazy<JourneyCalculationTypeComponent>( () => new DataViewInclusionCalculation() ) },
                { typeof( GroupTypeMembershipCalculation ).FullName, new Lazy<JourneyCalculationTypeComponent>( () => new GroupTypeMembershipCalculation() ) },
                { typeof( GroupMembershipCalculation ).FullName, new Lazy<JourneyCalculationTypeComponent>( () => new GroupMembershipCalculation() ) },
                { typeof( PersonFilterCalculation ).FullName, new Lazy<JourneyCalculationTypeComponent>( () => new PersonFilterCalculation() ) },
                { typeof( StepCompletionCalculation ).FullName, new Lazy<JourneyCalculationTypeComponent>( () => new StepCompletionCalculation() ) },
                { typeof( MediaWatchedCalculation ).FullName, new Lazy<JourneyCalculationTypeComponent>( () => new MediaWatchedCalculation() ) },
            };

        /// <summary>
        /// Gets a component instance by its EntityType name.
        /// </summary>
        public static JourneyCalculationTypeComponent GetComponent( string entityTypeName )
        {
            // Side effect: touch the MEF container so its singleton lazy fires its first-time
            // Refresh() — that's what reflects each calc-type class's [GroupField]/[DataViewField]
            // /etc. decorators into Attribute rows. Without this, the JourneyCalculationDetail
            // editor shows the type's title with no editor controls below it.
            EnsureContainerInitialized();

            if ( string.IsNullOrWhiteSpace( entityTypeName ) )
            {
                return null;
            }

            return _componentsByTypeName.TryGetValue( entityTypeName, out var lazy ) ? lazy.Value : null;
        }

        private static int _containerInitialized;
        private static void EnsureContainerInitialized()
        {
            // Run exactly once per process. CompareExchange acts as a cheap lock-free guard.
            if ( System.Threading.Interlocked.CompareExchange( ref _containerInitialized, 1, 0 ) != 0 )
            {
                return;
            }

            // The MEF-based JourneyCalculationTypeContainer is empty in practice (the calc-type
            // classes don't carry the [Export] attribute), so we can't rely on Container.Refresh
            // to reflect each calc-type's [GroupField]/[IntegerField]/etc. decorators into the
            // Attribute table. Do that walk ourselves directly off the static dictionary above.
            try
            {
                int calculationEntityTypeId = Rock.Web.Cache.EntityTypeCache.Get( typeof( Model.JourneyCalculation ) ).Id;
                using ( var rockContext = new Rock.Data.RockContext() )
                {
                    foreach ( var lazyComponent in _componentsByTypeName.Values )
                    {
                        Type calcTypeType = lazyComponent.Value.GetType();

                        // CRITICAL: skip when the calc-type class has NO direct field-attribute
                        // decorators. Rock.Attribute.Helper.UpdateAttributes treats the empty
                        // discovered-set as the truth and **deletes** any pre-existing attributes
                        // that were registered through a different path (e.g. MediaWatched's
                        // MediaElement + MinWatchedPercent are SQL-registered because Rock has no
                        // [MediaElementField] sugar). Cascade-deletes wipe every per-calc
                        // AttributeValue too — see lesson 2026-06-10 (project 5002 spike).
                        var hasDirectFieldAttrs = calcTypeType
                            .GetCustomAttributes( typeof( Rock.Attribute.FieldAttribute ), false )
                            .Any();
                        if ( !hasDirectFieldAttrs )
                        {
                            continue;
                        }

                        int componentEntityTypeId = Rock.Web.Cache.EntityTypeCache.Get( calcTypeType ).Id;
                        Rock.Attribute.Helper.UpdateAttributes(
                            calcTypeType,
                            calculationEntityTypeId,
                            "CalculationTypeEntityTypeId",
                            componentEntityTypeId.ToString(),
                            rockContext );
                    }
                }
            }
            catch
            {
                // Swallow to avoid breaking GetComponent on cold-start races; subsequent UI
                // hits will re-attempt because they go through GetComponent again — but the
                // _containerInitialized guard means only the first call does the work. Reset
                // it on failure so a retry actually retries.
                System.Threading.Interlocked.Exchange( ref _containerInitialized, 0 );
            }
        }

        /// <summary>
        /// Gets all registered JourneyCalculation types as EntityTypeId/Title pairs for use in dropdowns.
        /// </summary>
        public static List<JourneyCalculationTypeInfo> GetAllTypes()
        {
            return _componentsByTypeName.Values
                .Select( lazy =>
                {
                    var component = lazy.Value;
                    var entityType = EntityTypeCache.Get( component.GetType() );
                    return new JourneyCalculationTypeInfo
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
    /// Info about a registered JourneyCalculation type for use in pickers.
    /// </summary>
    public class JourneyCalculationTypeInfo
    {
        public int EntityTypeId { get; set; }
        public Guid EntityTypeGuid { get; set; }
        public string Title { get; set; }
        public string IconCssClass { get; set; }
    }

    /// <summary>
    /// Describes a merge field provided by a JourneyCalculation type.
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
