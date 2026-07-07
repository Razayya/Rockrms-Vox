using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.JourneyTrack.Model;

using Newtonsoft.Json;

using Rock;
using Rock.Data;
using Rock.Media;
using Rock.Model;
using Rock.Web.Cache;

namespace com.razayya.JourneyTrack.CalculationTypes
{
    /// <summary>
    /// Evaluates whether a person has watched at least a configurable percentage
    /// of a specific MediaElement across ALL their sessions. Mirrors Triumph's
    /// `AppendWatches` Lava filter: union the WatchMap from every Interaction
    /// row (Rock.Media.MediaWatchedInteractionData) for that person + media,
    /// recompute coverage, compare against threshold.
    ///
    /// Config attributes (registered manually in 09-register-mediawatched.sql
    /// because there's no [MediaElementField] decorator in Rock):
    ///   MediaElement       MediaElementFieldType  required
    ///   MinWatchedPercent  IntegerFieldType       default 95
    /// </summary>
    [Description( "Evaluates whether a person has watched >=N% of a MediaElement, unioning WatchMaps across all interaction sessions." )]
    public class MediaWatchedCalculation : JourneyCalculationTypeComponent
    {
        /// <inheritdoc/>
        public override string Title => "Media Watched";

        /// <inheritdoc/>
        public override string IconCssClass => "fa fa-video";

        // Per-MediaElement decoded-union cache. Keyed by MediaElement.Id, valued by
        // (timestamp, dict of personId -> {union bits, maxSession%, sessionCount}).
        // Critical perf win: within a single sync, 5 calcs that share a MediaElement
        // only hit the Interaction firehose once. TTL is short (30s) so a fresh sync
        // a minute later re-queries.
        private struct PersonWatchAggregate
        {
            public int[] UnionBits;
            public double MaxSingleSession;
            public int SessionCount;
        }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, (System.DateTime CachedAt, Dictionary<int, PersonWatchAggregate> Map)> _watchCache
            = new System.Collections.Concurrent.ConcurrentDictionary<int, (System.DateTime, Dictionary<int, PersonWatchAggregate>)>();
        private static readonly System.TimeSpan _cacheTtl = System.TimeSpan.FromSeconds( 30 );

        /// <summary>
        /// Clears the per-MediaElement watch aggregate cache. Use from explicit
        /// "I just wrote an Interaction; show me fresh state now" surfaces such
        /// as the watch-flow test page; production paths should rely on the TTL.
        /// </summary>
        public static void InvalidateAllWatchCache()
        {
            _watchCache.Clear();
        }

        private static Dictionary<int, PersonWatchAggregate> GetWatchAggregates( int mediaElementId, RockContext rockContext )
        {
            if ( _watchCache.TryGetValue( mediaElementId, out var cached )
                && ( System.DateTime.UtcNow - cached.CachedAt ) < _cacheTtl )
            {
                return cached.Map;
            }

            var rows = new InteractionService( rockContext ).Queryable().AsNoTracking()
                .Where( i =>
                    i.InteractionComponent.EntityId == mediaElementId
                    && i.PersonAliasId.HasValue
                    && i.InteractionData != null )
                .Select( i => new { PersonId = i.PersonAlias.PersonId, i.InteractionData } )
                .ToList();

            var map = new Dictionary<int, PersonWatchAggregate>();
            foreach ( var personGroup in rows.GroupBy( r => r.PersonId ) )
            {
                int[] union = null;
                double maxSingleSession = 0;
                int sessionCount = 0;

                foreach ( var row in personGroup )
                {
                    MediaWatchedInteractionData data;
                    try { data = JsonConvert.DeserializeObject<MediaWatchedInteractionData>( row.InteractionData ); }
                    catch { continue; }
                    if ( data == null ) continue;

                    if ( data.WatchedPercentage > maxSingleSession ) maxSingleSession = data.WatchedPercentage;

                    var bits = RleToArray( data.WatchMap );
                    if ( bits == null || bits.Length == 0 ) continue;

                    sessionCount++;
                    union = ( union == null ) ? bits : MergeMaps( union, bits );
                }

                if ( union != null )
                {
                    map[personGroup.Key] = new PersonWatchAggregate
                    {
                        UnionBits = union,
                        MaxSingleSession = maxSingleSession,
                        SessionCount = sessionCount
                    };
                }
            }

            _watchCache[mediaElementId] = ( System.DateTime.UtcNow, map );
            return map;
        }

        /// <inheritdoc/>
        public override Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            JourneyCalculation calc,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            var mediaGuid = calc.GetAttributeValue( "MediaElement" ).AsGuidOrNull();
            var minPercent = calc.GetAttributeValue( "MinWatchedPercent" ).AsIntegerOrNull() ?? 95;

            if ( !mediaGuid.HasValue ) return results;
            if ( populationPersonIds == null || populationPersonIds.Count == 0 ) return results;

            var mediaElement = new MediaElementService( rockContext ).Get( mediaGuid.Value );
            if ( mediaElement == null ) return results;

            var aggregates = GetWatchAggregates( mediaElement.Id, rockContext );
            if ( aggregates.Count == 0 ) return results;

            foreach ( var personId in populationPersonIds )
            {
                if ( !aggregates.TryGetValue( personId, out var agg ) ) continue;

                int watchedSeconds = agg.UnionBits.Count( v => v > 0 );
                double percent = agg.UnionBits.Length > 0 ? ( watchedSeconds * 100.0 / agg.UnionBits.Length ) : 0;
                if ( percent < minPercent ) continue;

                results[personId] = new Dictionary<string, object>
                {
                    { "Matched", true },
                    { "WatchedPercentage", Math.Round( percent, 2 ) },
                    { "MaxSingleSessionPercentage", Math.Round( agg.MaxSingleSession, 2 ) },
                    { "WatchedSeconds", watchedSeconds },
                    { "MapLength", agg.UnionBits.Length },
                    { "SessionCount", agg.SessionCount }
                };
            }

            return results;
        }

        /// <inheritdoc/>
        // The per-MediaElement aggregate cache holds every person's unioned WatchMap
        // regardless of the threshold — the MinWatchedPercent filter is only applied
        // at lookup — so partial progress ("80% of 95%") is a straight map lookup.
        public override Dictionary<string, object> DescribeProgress(
            RockContext rockContext,
            JourneyCalculation calc,
            int personId )
        {
            var mediaGuid = calc.GetAttributeValue( "MediaElement" ).AsGuidOrNull();
            var minPercent = calc.GetAttributeValue( "MinWatchedPercent" ).AsIntegerOrNull() ?? 95;

            double percent = 0;
            double maxSingleSession = 0;
            int watchedSeconds = 0;
            int mapLength = 0;
            int sessionCount = 0;

            if ( mediaGuid.HasValue )
            {
                var mediaElement = new MediaElementService( rockContext ).Get( mediaGuid.Value );
                if ( mediaElement != null )
                {
                    var aggregates = GetWatchAggregates( mediaElement.Id, rockContext );
                    if ( aggregates.TryGetValue( personId, out var agg ) )
                    {
                        watchedSeconds = agg.UnionBits.Count( v => v > 0 );
                        mapLength = agg.UnionBits.Length;
                        percent = mapLength > 0 ? ( watchedSeconds * 100.0 / mapLength ) : 0;
                        maxSingleSession = agg.MaxSingleSession;
                        sessionCount = agg.SessionCount;
                    }
                }
            }

            return new Dictionary<string, object>
            {
                { "Matched", percent >= minPercent },
                { "Current", ( decimal ) Math.Round( percent, 2 ) },
                { "Target", ( decimal ) minPercent },
                { "WatchedPercentage", Math.Round( percent, 2 ) },
                { "MaxSingleSessionPercentage", Math.Round( maxSingleSession, 2 ) },
                { "WatchedSeconds", watchedSeconds },
                { "MapLength", mapLength },
                { "SessionCount", sessionCount }
            };
        }

        /// <inheritdoc/>
        public override List<MergeFieldInfo> GetMergeFields()
        {
            return new List<MergeFieldInfo>
            {
                new MergeFieldInfo { Name = "Matched", Description = "True if cumulative watched % meets the threshold.", DataType = "Boolean" },
                new MergeFieldInfo { Name = "WatchedPercentage", Description = "Cumulative % watched, unioned across sessions.", DataType = "Decimal" },
                new MergeFieldInfo { Name = "MaxSingleSessionPercentage", Description = "Highest single-session WatchedPercentage.", DataType = "Decimal" },
                new MergeFieldInfo { Name = "WatchedSeconds", Description = "Number of distinct seconds watched (union).", DataType = "Integer" },
                new MergeFieldInfo { Name = "MapLength", Description = "Total seconds in the media WatchMap.", DataType = "Integer" },
                new MergeFieldInfo { Name = "SessionCount", Description = "Number of Interaction rows considered.", DataType = "Integer" }
            };
        }

        #region WatchMap RLE helpers

        // Mirrors the JS encoder at RockWeb/Scripts/Rock/UI/mediaplayer/mediaplayer.ts:1016
        //   toRle: each run encoded as "<count><single-digit-value>", comma-joined
        //   value is 0..9 (max-clamp at 9, see mediaplayer.ts:633)
        // We only need the decoder.
        internal static int[] RleToArray( string rle )
        {
            if ( string.IsNullOrWhiteSpace( rle ) )
            {
                return null;
            }

            var segments = rle.Split( ',' );
            var result = new List<int>( segments.Length * 4 );
            foreach ( var seg in segments )
            {
                if ( seg.Length < 2 )
                {
                    continue;
                }

                var valueChar = seg[seg.Length - 1];
                if ( !char.IsDigit( valueChar ) )
                {
                    continue;
                }
                int value = valueChar - '0';

                var sizeText = seg.Substring( 0, seg.Length - 1 );
                if ( !int.TryParse( sizeText, out int size ) || size <= 0 )
                {
                    continue;
                }

                for ( int i = 0; i < size; i++ )
                {
                    result.Add( value );
                }
            }
            return result.ToArray();
        }

        // OR-union: positionwise max. Pads the shorter array (covers media-duration edits).
        internal static int[] MergeMaps( int[] a, int[] b )
        {
            int len = Math.Max( a.Length, b.Length );
            var result = new int[len];
            for ( int i = 0; i < len; i++ )
            {
                int av = i < a.Length ? a[i] : 0;
                int bv = i < b.Length ? b[i] : 0;
                result[i] = av > bv ? av : bv;
            }
            return result;
        }

        #endregion
    }
}
