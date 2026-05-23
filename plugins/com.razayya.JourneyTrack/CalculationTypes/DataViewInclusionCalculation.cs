using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using com.razayya.JourneyTrack.Constants;
using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;

namespace com.razayya.JourneyTrack.CalculationTypes
{
    /// <summary>
    /// Evaluates whether persons are included in a specified DataView.
    /// </summary>
    [Description( "Checks whether a person is included in a specified Data View." )]

    [DataViewField( "Data View",
        Description = "The Data View to check for person inclusion.",
        IsRequired = true,
        EntityTypeName = "Rock.Model.Person",
        Order = 0,
        Key = AttributeKey.DataView )]

    public class DataViewInclusionCalculation : JourneyCalculationTypeComponent
    {
        /// <inheritdoc/>
        public override string Title => "Data View Inclusion";

        /// <inheritdoc/>
        public override string IconCssClass => "fa fa-filter";

        /// <inheritdoc/>
        public override Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            JourneyCalculation calc,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            var dataViewGuid = calc.GetAttributeValue( AttributeKey.DataView ).AsGuidOrNull();
            if ( !dataViewGuid.HasValue )
            {
                return results;
            }

            var dataViewService = new DataViewService( rockContext );
            var dataView = dataViewService.Get( dataViewGuid.Value );
            if ( dataView == null )
            {
                return results;
            }

            List<int> dataViewPersonIds;
            try
            {
                dataViewPersonIds = dataView.GetQuery( new DataViewGetQueryArgs { DbContext = rockContext, DatabaseTimeoutSeconds = 180 } )
                    .Select( e => e.Id )
                    .ToList();
            }
            catch ( Exception ex )
            {
                Rock.Model.ExceptionLogService.LogException( ex );
                return results;
            }

            // Intersect with population
            var matchedIds = populationPersonIds != null && populationPersonIds.Count > 0
                ? dataViewPersonIds.Where( id => populationPersonIds.Contains( id ) )
                : dataViewPersonIds;

            foreach ( var personId in matchedIds )
            {
                results[personId] = new Dictionary<string, object>
                {
                    { "Matched", true },
                    { "IsInDataView", true }
                };
            }

            return results;
        }

        /// <inheritdoc/>
        public override List<MergeFieldInfo> GetMergeFields()
        {
            return new List<MergeFieldInfo>
            {
                new MergeFieldInfo { Name = "Matched", Description = "True if person is in the Data View.", DataType = "Boolean" },
                new MergeFieldInfo { Name = "IsInDataView", Description = "True if person is in the Data View.", DataType = "Boolean" }
            };
        }
    }
}
