using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.JourneyTrack.Constants;
using com.razayya.JourneyTrack.Logic;
using com.razayya.JourneyTrack.Model;

using Newtonsoft.Json;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

using ComparisonType = com.razayya.JourneyTrack.Model.ComparisonType;

namespace com.razayya.JourneyTrack.CalculationTypes
{
    /// <summary>
    /// Evaluates persons against person property or attribute conditions using
    /// AND/OR logic. Supports comparisons like EqualTo, IsNotBlank, GreaterThan, etc.
    /// </summary>
    [Description( "Evaluates person properties and attributes against configurable filter conditions." )]


    [CodeEditorField( "Filter Conditions",
        Description = "A JSON array of conditions to evaluate against each person. "
            + "<br/><br/><strong>Source</strong>: <code>Property</code> (e.g. Email, NickName, ConnectionStatusValueId) or <code>Attribute</code> (person attribute key). "
            + "<br/><strong>ComparisonType</strong>: EqualTo, NotEqualTo, IsNotBlank, IsBlank, GreaterThan, LessThan, Contains, StartsWith, EndsWith."
            + "<br/><br/>Example:<br/><pre>[\n  { \"Source\": \"Property\", \"Key\": \"ConnectionStatusValueId\", \"ComparisonType\": \"EqualTo\", \"Value\": \"65\" },\n  { \"Source\": \"Attribute\", \"Key\": \"BaptismDate\", \"ComparisonType\": \"IsNotBlank\", \"Value\": \"\" }\n]</pre>",
        IsRequired = true,
        DefaultValue = "[]",
        Order = 0,
        Key = AttributeKey.FilterConditions,
        EditorMode = Rock.Web.UI.Controls.CodeEditorMode.JavaScript,
        EditorTheme = Rock.Web.UI.Controls.CodeEditorTheme.Rock,
        EditorHeight = 200 )]

    [BooleanField( "Match All",
        Description = "When enabled, all conditions must be met (AND). When disabled, any condition can be met (OR).",
        IsRequired = true,
        DefaultBooleanValue = true,
        Order = 1,
        Key = AttributeKey.MatchAll )]

    public class PersonFilterCalculation : JourneyCalculationTypeComponent
    {
        /// <inheritdoc/>
        public override string Title => "Person Filter";

        /// <inheritdoc/>
        public override string IconCssClass => "fa fa-user-check";

        /// <inheritdoc/>
        public override Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            JourneyCalculation calc,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            var conditionsJson = calc.GetAttributeValue( AttributeKey.FilterConditions );
            var matchAll = calc.GetAttributeValue( AttributeKey.MatchAll ).AsBoolean();

            // Parse into a nested ANY/ALL logic tree. A legacy flat array wraps in a
            // single root group whose type follows the existing "Match All" toggle
            // (All when on, Any when off) — so old configs evaluate exactly as before.
            var tree = LogicTree.Parse<FilterCondition>(
                conditionsJson,
                matchAll ? LogicGroupType.All : LogicGroupType.Any );

            if ( LogicTree.IsEmpty( tree ) )
            {
                return results;
            }

            var conditions = LogicTree.GetLeaves( tree ).ToList();
            bool needsProperties = conditions.Any( c => c.Source == FilterSource.Property );
            bool needsAttributes = conditions.Any( c => c.Source == FilterSource.Attribute );

            // Batch-load attribute values in one query (replaces per-person LoadAttributes)
            Dictionary<int, Dictionary<string, string>> attributeLookup = null;
            if ( needsAttributes )
            {
                var attributeKeys = conditions
                    .Where( c => c.Source == FilterSource.Attribute )
                    .Select( c => c.Key )
                    .Distinct()
                    .ToList();

                attributeLookup = new AttributeValueService( rockContext ).Queryable().AsNoTracking()
                    .Where( av =>
                        av.Attribute.Key != null &&
                        attributeKeys.Contains( av.Attribute.Key ) &&
                        av.EntityId.HasValue &&
                        populationPersonIds.Contains( av.EntityId.Value ) )
                    .Select( av => new { EntityId = av.EntityId.Value, av.Attribute.Key, av.Value } )
                    .ToList()
                    .GroupBy( av => av.EntityId )
                    .ToDictionary(
                        g => g.Key,
                        g => g.ToDictionary( av => av.Key, av => av.Value ?? string.Empty ) );
            }

            // Only load Person entities if we have Property conditions
            if ( needsProperties )
            {
                var personService = new PersonService( rockContext );
                var personsQuery = personService.Queryable().AsNoTracking();

                if ( populationPersonIds != null && populationPersonIds.Count > 0 )
                {
                    personsQuery = personsQuery.Where( p => populationPersonIds.Contains( p.Id ) );
                }

                var persons = personsQuery.ToList();

                foreach ( var person in persons )
                {
                    // EvaluateCondition handles both Property and Attribute leaves, so a
                    // mixed tree evaluates correctly in the property-bearing path.
                    bool matched = LogicTree.Evaluate( tree, c => EvaluateCondition( person, c, attributeLookup ) );

                    if ( matched )
                    {
                        results[person.Id] = new Dictionary<string, object>
                        {
                            { "Matched", true }
                        };
                    }
                }
            }
            else
            {
                // Attribute-only conditions — no need to load Person entities at all
                foreach ( var personId in populationPersonIds )
                {
                    bool matched = LogicTree.Evaluate( tree, c => EvaluateAttributeCondition( personId, c, attributeLookup ) );

                    if ( matched )
                    {
                        results[personId] = new Dictionary<string, object>
                        {
                            { "Matched", true }
                        };
                    }
                }
            }

            return results;
        }

        private bool EvaluateCondition( Person person, FilterCondition condition, Dictionary<int, Dictionary<string, string>> attributeLookup )
        {
            string actualValue;

            if ( condition.Source == FilterSource.Property )
            {
                var propInfo = typeof( Person ).GetProperty( condition.Key );
                if ( propInfo == null )
                {
                    return false;
                }

                var rawValue = propInfo.GetValue( person );
                actualValue = rawValue?.ToString() ?? string.Empty;
            }
            else
            {
                actualValue = GetAttributeValueFromLookup( person.Id, condition.Key, attributeLookup );
            }

            return CompareValues( actualValue, condition.Comparison, condition.Value );
        }

        private bool EvaluateAttributeCondition( int personId, FilterCondition condition, Dictionary<int, Dictionary<string, string>> attributeLookup )
        {
            var actualValue = GetAttributeValueFromLookup( personId, condition.Key, attributeLookup );
            return CompareValues( actualValue, condition.Comparison, condition.Value );
        }

        private static string GetAttributeValueFromLookup( int personId, string key, Dictionary<int, Dictionary<string, string>> attributeLookup )
        {
            if ( attributeLookup != null
                && attributeLookup.TryGetValue( personId, out var personAttrs )
                && personAttrs.TryGetValue( key, out var value ) )
            {
                return value;
            }

            return string.Empty;
        }

        /// <summary>
        /// Compares a value against a comparison type and expected value.
        /// </summary>
        internal static bool CompareValues( string actualValue, ComparisonType comparison, string expectedValue )
        {
            switch ( comparison )
            {
                case ComparisonType.EqualTo:
                    return string.Equals( actualValue, expectedValue, StringComparison.OrdinalIgnoreCase );

                case ComparisonType.NotEqualTo:
                    return !string.Equals( actualValue, expectedValue, StringComparison.OrdinalIgnoreCase );

                case ComparisonType.IsNotBlank:
                    return !string.IsNullOrWhiteSpace( actualValue );

                case ComparisonType.IsBlank:
                    return string.IsNullOrWhiteSpace( actualValue );

                case ComparisonType.GreaterThan:
                case ComparisonType.LessThan:
                case ComparisonType.GreaterThanOrEqualTo:
                case ComparisonType.LessThanOrEqualTo:
                    return CompareNumericOrDate( actualValue, expectedValue, comparison );

                case ComparisonType.Contains:
                    return ( actualValue ?? string.Empty ).IndexOf( expectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase ) >= 0;

                default:
                    return false;
            }
        }

        private static bool CompareNumericOrDate( string actualValue, string expectedValue, ComparisonType comparison )
        {
            // Try DateTime comparison first
            if ( DateTime.TryParse( actualValue, out var actualDate ) && DateTime.TryParse( expectedValue, out var expectedDate ) )
            {
                switch ( comparison )
                {
                    case ComparisonType.GreaterThan: return actualDate > expectedDate;
                    case ComparisonType.LessThan: return actualDate < expectedDate;
                    case ComparisonType.GreaterThanOrEqualTo: return actualDate >= expectedDate;
                    case ComparisonType.LessThanOrEqualTo: return actualDate <= expectedDate;
                }
            }

            // Try decimal comparison
            if ( decimal.TryParse( actualValue, out var actualNum ) && decimal.TryParse( expectedValue, out var expectedNum ) )
            {
                switch ( comparison )
                {
                    case ComparisonType.GreaterThan: return actualNum > expectedNum;
                    case ComparisonType.LessThan: return actualNum < expectedNum;
                    case ComparisonType.GreaterThanOrEqualTo: return actualNum >= expectedNum;
                    case ComparisonType.LessThanOrEqualTo: return actualNum <= expectedNum;
                }
            }

            // Fall back to string comparison
            int cmp = string.Compare( actualValue, expectedValue, StringComparison.OrdinalIgnoreCase );
            switch ( comparison )
            {
                case ComparisonType.GreaterThan: return cmp > 0;
                case ComparisonType.LessThan: return cmp < 0;
                case ComparisonType.GreaterThanOrEqualTo: return cmp >= 0;
                case ComparisonType.LessThanOrEqualTo: return cmp <= 0;
            }

            return false;
        }

        /// <inheritdoc/>
        public override List<MergeFieldInfo> GetMergeFields()
        {
            return new List<MergeFieldInfo>
            {
                new MergeFieldInfo { Name = "Matched", Description = "True if filter conditions were satisfied.", DataType = "Boolean" }
            };
        }
    }

    #region Filter Condition Models

    /// <summary>
    /// Represents a single filter condition in PersonFilter configuration.
    /// </summary>
    public class FilterCondition
    {
        /// <summary>
        /// Whether this condition checks a Person property or a Person attribute.
        /// </summary>
        public FilterSource Source { get; set; }

        /// <summary>
        /// The property name or attribute key to evaluate.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// The comparison operator.
        /// </summary>
        public ComparisonType Comparison { get; set; }

        /// <summary>
        /// The expected value to compare against. Not used for IsBlank/IsNotBlank.
        /// </summary>
        public string Value { get; set; }
    }

    /// <summary>
    /// The source type for a filter condition.
    /// </summary>
    public enum FilterSource
    {
        Property = 0,
        Attribute = 1
    }

    #endregion
}
