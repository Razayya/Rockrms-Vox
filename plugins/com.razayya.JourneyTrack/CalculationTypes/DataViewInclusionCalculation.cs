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

        // Per-DataView cache. DataViews can be expensive to evaluate (multi-second), so
        // a 30s TTL is a meaningful win when several calcs reuse the same DataView in
        // a sync or batch.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Guid, (System.DateTime CachedAt, HashSet<int> PersonIds)> _dvCache
            = new System.Collections.Concurrent.ConcurrentDictionary<System.Guid, (System.DateTime, HashSet<int>)>();
        private static readonly System.TimeSpan _cacheTtl = System.TimeSpan.FromSeconds( 30 );

        private static HashSet<int> GetDataViewPersonIds( System.Guid dvGuid, RockContext rockContext )
        {
            if ( _dvCache.TryGetValue( dvGuid, out var cached )
                && ( System.DateTime.UtcNow - cached.CachedAt ) < _cacheTtl )
            {
                return cached.PersonIds;
            }

            var dataView = new DataViewService( rockContext ).Get( dvGuid );
            if ( dataView == null )
            {
                var empty = new HashSet<int>();
                _dvCache[dvGuid] = ( System.DateTime.UtcNow, empty );
                return empty;
            }

            HashSet<int> ids;
            try
            {
                ids = new HashSet<int>(
                    dataView.GetQuery( new DataViewGetQueryArgs { DbContext = rockContext, DatabaseTimeoutSeconds = 180 } )
                        .Select( e => e.Id ).ToList() );
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
                ids = new HashSet<int>();
            }

            _dvCache[dvGuid] = ( System.DateTime.UtcNow, ids );
            return ids;
        }

        /// <inheritdoc/>
        public override Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            JourneyCalculation calc,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            var dataViewGuid = calc.GetAttributeValue( AttributeKey.DataView ).AsGuidOrNull();
            if ( !dataViewGuid.HasValue ) return results;
            if ( populationPersonIds == null || populationPersonIds.Count == 0 ) return results;

            var dvIds = GetDataViewPersonIds( dataViewGuid.Value, rockContext );
            if ( dvIds.Count == 0 ) return results;

            foreach ( var personId in populationPersonIds )
            {
                if ( !dvIds.Contains( personId ) ) continue;
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
