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

using ComparisonType = com.razayya.JourneyTrack.Model.ComparisonType;

namespace com.razayya.JourneyTrack.CalculationTypes
{
    /// <summary>
    /// Evaluates whether a person has completed all required sibling calculations
    /// within the same Stage by checking their target attribute values
    /// against configured expectations. This is the "gate" that determines whether
    /// a person passes a SubGroup and flows into the next one.
    /// </summary>
    [Description( "Checks completion of sibling calculations by evaluating their target attribute values against configured criteria." )]


    [CodeEditorField( "Completion Criteria",
        Description = "A JSON array defining what sibling calculations must be satisfied for completion. "
            + "Each entry specifies a JourneyCalculation in this sub-group and how its target attribute value should be evaluated."
            + "<br/><br/><strong>JourneyCalculationId</strong>: The Id of a sibling JourneyCalculation in this sub-group. "
            + "<br/><strong>IsRequired</strong>: If <code>true</code>, this criterion must pass. If <code>false</code>, it is optional."
            + "<br/><strong>ComparisonType</strong>: EqualTo, NotEqualTo, IsNotBlank, IsBlank, GreaterThan, LessThan, Contains."
            + "<br/><strong>Value</strong>: The value to compare against (leave empty for IsNotBlank/IsBlank)."
            + "<br/><br/>Example:<br/><pre>[\n  { \"JourneyCalculationId\": 12, \"IsRequired\": true, \"ComparisonType\": \"EqualTo\", \"Value\": \"True\" },\n  { \"JourneyCalculationId\": 15, \"IsRequired\": true, \"ComparisonType\": \"IsNotBlank\", \"Value\": \"\" }\n]</pre>",
        IsRequired = true,
        DefaultValue = "[]",
        Order = 0,
        Key = AttributeKey.CompletionCriteria,
        EditorMode = Rock.Web.UI.Controls.CodeEditorMode.JavaScript,
        EditorTheme = Rock.Web.UI.Controls.CodeEditorTheme.Rock,
        EditorHeight = 300 )]

    public class CompletionCalculation : JourneyCalculationTypeComponent
    {
        /// <inheritdoc/>
        public override string Title => "Completion";

        /// <inheritdoc/>
        public override string IconCssClass => "fa fa-check-double";

        /// <inheritdoc/>
        public override Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            JourneyCalculation calc,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            var criteriaJson = calc.GetAttributeValue( AttributeKey.CompletionCriteria );

            // A nested ANY/ALL tree config (JSON object) routes to the tree evaluator.
            // A legacy flat array keeps the original required-criteria-AND behavior
            // below entirely untouched (including its "no required = everyone passes" rule).
            if ( ( criteriaJson ?? string.Empty ).TrimStart().StartsWith( "{" ) )
            {
                return EvaluateTree( rockContext, criteriaJson, populationPersonIds );
            }

            List<CompletionCriterion> criteria;
            try
            {
                criteria = JsonConvert.DeserializeObject<List<CompletionCriterion>>( criteriaJson );
            }
            catch
            {
                return results;
            }

            if ( criteria == null || criteria.Count == 0 )
            {
                return results;
            }

            // Get only the required criteria
            var requiredCriteria = criteria.Where( c => c.IsRequired ).ToList();
            if ( requiredCriteria.Count == 0 )
            {
                // No required criteria means everyone passes
                foreach ( var personId in populationPersonIds )
                {
                    results[personId] = new Dictionary<string, object>
                    {
                        { "Matched", true },
                        { "CompletedCount", 0 },
                        { "RequiredCount", 0 }
                    };
                }
                return results;
            }

            // Resolve the target attribute for each referenced JourneyCalculation
            var calculationService = new JourneyCalculationService( rockContext );
            var calculationIds = requiredCriteria.Select( c => c.JourneyCalculationId ).Distinct().ToList();
            var siblingCalculations = calculationService.Queryable().AsNoTracking()
                .Where( c => calculationIds.Contains( c.Id ) )
                .Select( c => new { c.Id, c.PersonAttributeId } )
                .ToList();

            // Map JourneyCalculationId => AttributeCache (skip siblings that are transient — no sink to read).
            var attributeMap = new Dictionary<int, Rock.Web.Cache.AttributeCache>();
            foreach ( var sibling in siblingCalculations )
            {
                if ( !sibling.PersonAttributeId.HasValue )
                {
                    continue;
                }
                var attrCache = Rock.Web.Cache.AttributeCache.Get( sibling.PersonAttributeId.Value );
                if ( attrCache != null )
                {
                    attributeMap[sibling.Id] = attrCache;
                }
            }

            // Batch-load only the specific attribute values we need — one query instead of N
            var neededAttributeIds = attributeMap.Values.Select( a => a.Id ).Distinct().ToList();

            var attributeLookup = new AttributeValueService( rockContext ).Queryable().AsNoTracking()
                .Where( av =>
                    neededAttributeIds.Contains( av.AttributeId ) &&
                    av.EntityId.HasValue &&
                    populationPersonIds.Contains( av.EntityId.Value ) )
                .Select( av => new { EntityId = av.EntityId.Value, av.AttributeId, av.Value } )
                .ToList()
                .GroupBy( av => av.EntityId )
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary( av => av.AttributeId, av => av.Value ?? string.Empty ) );

            // Evaluate each person against the criteria — no Person entity load needed
            foreach ( var personId in populationPersonIds )
            {
                int completedCount = 0;
                bool allRequiredMet = true;

                foreach ( var criterion in requiredCriteria )
                {
                    if ( !attributeMap.TryGetValue( criterion.JourneyCalculationId, out var targetAttribute ) )
                    {
                        allRequiredMet = false;
                        continue;
                    }

                    string attrValue = string.Empty;
                    if ( attributeLookup.TryGetValue( personId, out var personAttrs )
                        && personAttrs.TryGetValue( targetAttribute.Id, out var val ) )
                    {
                        attrValue = val;
                    }

                    bool met = PersonFilterCalculation.CompareValues( attrValue, criterion.Comparison, criterion.Value );

                    if ( met )
                    {
                        completedCount++;
                    }
                    else
                    {
                        allRequiredMet = false;
                    }
                }

                if ( allRequiredMet )
                {
                    results[personId] = new Dictionary<string, object>
                    {
                        { "Matched", true },
                        { "CompletedCount", completedCount },
                        { "RequiredCount", requiredCriteria.Count }
                    };
                }
            }

            return results;
        }

        /// <summary>
        /// Evaluates a nested ANY/ALL <see cref="LogicNode{CompletionCriterion}"/> tree.
        /// Each leaf compares a sibling JourneyCalculation's target attribute value;
        /// the tree combines those leaf results with All/Any/AllFalse/AnyFalse. The
        /// per-leaf <c>IsRequired</c> flag is unused here — requiredness is expressed
        /// by the tree shape (a leaf inside an All group is effectively required).
        /// </summary>
        private Dictionary<int, Dictionary<string, object>> EvaluateTree(
            RockContext rockContext,
            string criteriaJson,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            var tree = LogicTree.Parse<CompletionCriterion>( criteriaJson, LogicGroupType.All );
            if ( LogicTree.IsEmpty( tree ) )
            {
                return results;
            }

            var leaves = LogicTree.GetLeaves( tree ).ToList();

            // Resolve each referenced sibling calc's target attribute (skip transient siblings).
            var calculationService = new JourneyCalculationService( rockContext );
            var calculationIds = leaves.Select( c => c.JourneyCalculationId ).Distinct().ToList();
            var siblingCalculations = calculationService.Queryable().AsNoTracking()
                .Where( c => calculationIds.Contains( c.Id ) )
                .Select( c => new { c.Id, c.PersonAttributeId } )
                .ToList();

            var attributeMap = new Dictionary<int, Rock.Web.Cache.AttributeCache>();
            foreach ( var sibling in siblingCalculations )
            {
                if ( !sibling.PersonAttributeId.HasValue )
                {
                    continue;
                }
                var attrCache = Rock.Web.Cache.AttributeCache.Get( sibling.PersonAttributeId.Value );
                if ( attrCache != null )
                {
                    attributeMap[sibling.Id] = attrCache;
                }
            }

            // Batch-load only the specific attribute values needed — one query.
            var neededAttributeIds = attributeMap.Values.Select( a => a.Id ).Distinct().ToList();
            var attributeLookup = new AttributeValueService( rockContext ).Queryable().AsNoTracking()
                .Where( av =>
                    neededAttributeIds.Contains( av.AttributeId ) &&
                    av.EntityId.HasValue &&
                    populationPersonIds.Contains( av.EntityId.Value ) )
                .Select( av => new { EntityId = av.EntityId.Value, av.AttributeId, av.Value } )
                .ToList()
                .GroupBy( av => av.EntityId )
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary( av => av.AttributeId, av => av.Value ?? string.Empty ) );

            foreach ( var personId in populationPersonIds )
            {
                attributeLookup.TryGetValue( personId, out var personAttrs );

                Func<CompletionCriterion, bool> leafEval = criterion =>
                {
                    if ( !attributeMap.TryGetValue( criterion.JourneyCalculationId, out var targetAttribute ) )
                    {
                        return false;
                    }
                    string attrValue = string.Empty;
                    if ( personAttrs != null && personAttrs.TryGetValue( targetAttribute.Id, out var val ) )
                    {
                        attrValue = val;
                    }
                    return PersonFilterCalculation.CompareValues( attrValue, criterion.Comparison, criterion.Value );
                };

                if ( LogicTree.Evaluate( tree, leafEval ) )
                {
                    int completedCount = leaves.Count( leafEval );
                    results[personId] = new Dictionary<string, object>
                    {
                        { "Matched", true },
                        { "CompletedCount", completedCount },
                        { "RequiredCount", leaves.Count }
                    };
                }
            }

            return results;
        }

        /// <inheritdoc/>
        public override List<MergeFieldInfo> GetMergeFields()
        {
            return new List<MergeFieldInfo>
            {
                new MergeFieldInfo { Name = "Matched", Description = "True if all required criteria are met.", DataType = "Boolean" },
                new MergeFieldInfo { Name = "CompletedCount", Description = "Number of required criteria that were met.", DataType = "Integer" },
                new MergeFieldInfo { Name = "RequiredCount", Description = "Total number of required criteria.", DataType = "Integer" }
            };
        }
    }

    #region Completion Criterion Model

    /// <summary>
    /// Represents a single entry in the Completion JourneyCalculation's criteria configuration.
    /// </summary>
    public class CompletionCriterion
    {
        /// <summary>
        /// The Id of the sibling JourneyCalculation to check.
        /// </summary>
        public int JourneyCalculationId { get; set; }

        /// <summary>
        /// Whether this criterion is required for completion.
        /// Non-required criteria are tracked but don't gate progression.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// The comparison operator to use against the sibling's target attribute value.
        /// </summary>
        public ComparisonType Comparison { get; set; }

        /// <summary>
        /// The expected value to compare against. Not used for IsBlank/IsNotBlank.
        /// </summary>
        public string Value { get; set; }
    }

    #endregion
}
