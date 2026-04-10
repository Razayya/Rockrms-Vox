using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Data.Entity;
using System.Linq;

using com.razayya.CustomPersonAttributeSyncEngine.Constants;
using com.razayya.CustomPersonAttributeSyncEngine.Model;

using Newtonsoft.Json;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;

using ComparisonType = com.razayya.CustomPersonAttributeSyncEngine.Model.ComparisonType;

namespace com.razayya.CustomPersonAttributeSyncEngine.CalculationTypes
{
    /// <summary>
    /// Evaluates whether a person has completed all required sibling calculations
    /// within the same CalculationSubGroup by checking their target attribute values
    /// against configured expectations. This is the "gate" that determines whether
    /// a person passes a SubGroup and flows into the next one.
    /// </summary>
    [Description( "Checks completion of sibling calculations by evaluating their target attribute values against configured criteria." )]
    [Export( typeof( CalculationTypeComponent ) )]
    [ExportMetadata( "ComponentName", "Completion" )]

    [CodeEditorField( "Completion Criteria",
        Description = "JSON array defining completion criteria. Each entry has: CalculationId (int), IsRequired (bool), ComparisonType, and Value.",
        IsRequired = true,
        DefaultValue = "[]",
        Order = 0,
        Key = AttributeKey.CompletionCriteria,
        EditorMode = Rock.Web.UI.Controls.CodeEditorMode.JavaScript,
        EditorTheme = Rock.Web.UI.Controls.CodeEditorTheme.Rock,
        EditorHeight = 300 )]

    public class CompletionCalculation : CalculationTypeComponent
    {
        /// <inheritdoc/>
        public override string Title => "Completion";

        /// <inheritdoc/>
        public override string IconCssClass => "fa fa-check-double";

        /// <inheritdoc/>
        public override Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            Calculation calculation,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            var criteriaJson = calculation.GetAttributeValue( AttributeKey.CompletionCriteria );
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

            // Resolve the target attribute for each referenced calculation
            var calculationService = new CalculationService( rockContext );
            var calculationIds = requiredCriteria.Select( c => c.CalculationId ).Distinct().ToList();
            var siblingCalculations = calculationService.Queryable().AsNoTracking()
                .Where( c => calculationIds.Contains( c.Id ) )
                .Select( c => new { c.Id, c.PersonAttributeId } )
                .ToList();

            // Map CalculationId => AttributeCache
            var attributeMap = new Dictionary<int, Rock.Web.Cache.AttributeCache>();
            foreach ( var sibling in siblingCalculations )
            {
                var attrCache = Rock.Web.Cache.AttributeCache.Get( sibling.PersonAttributeId );
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
                    if ( !attributeMap.TryGetValue( criterion.CalculationId, out var targetAttribute ) )
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
    /// Represents a single entry in the Completion calculation's criteria configuration.
    /// </summary>
    public class CompletionCriterion
    {
        /// <summary>
        /// The Id of the sibling Calculation to check.
        /// </summary>
        public int CalculationId { get; set; }

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
