using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.JourneyTrack.Data;
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
        // POPULATION runs only: within a full sync, calcs that share a MediaElement
        // hit the Interaction firehose once. TTL is short (30s) so a fresh sync a
        // minute later re-queries. Single-person (render-path) evaluations never
        // rebuild this map — they take GetPersonWatchAggregate's per-person query,
        // using a fresh shared entry only as a free read.
        private struct PersonWatchAggregate
        {
            public int[] UnionBits;
            public double MaxSingleSession;
            public int SessionCount;
        }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, (System.DateTime CachedAt, Dictionary<int, PersonWatchAggregate> Map)> _watchCache
            = new System.Collections.Concurrent.ConcurrentDictionary<int, (System.DateTime, Dictionary<int, PersonWatchAggregate>)>();
        private static readonly System.TimeSpan _cacheTtl = System.TimeSpan.FromSeconds( 30 );

        // Per-person bust epochs: a person's single-person evaluation treats any entry
        // cached before their epoch as a miss (forcing one re-query that refreshes the
        // shared entry for everyone). Lets "I just watched it" flows get fresh state
        // without clearing the cache under every other concurrent user.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, System.DateTime> _personBustEpoch
            = new System.Collections.Concurrent.ConcurrentDictionary<int, System.DateTime>();

        /// <summary>
        /// Clears the per-MediaElement watch aggregate cache for every person. Prefer
        /// <see cref="InvalidateWatchCacheForPerson"/> on render paths — a global clear
        /// under concurrent app traffic makes every user repay the Interaction re-union.
        /// </summary>
        public static void InvalidateAllWatchCache()
        {
            _watchCache.Clear();
        }

        /// <summary>
        /// Person-scoped cache bust for "I just wrote an Interaction; show me fresh
        /// state now" surfaces. Only this person's next single-person evaluation
        /// re-queries; entries stay warm for everyone else.
        /// </summary>
        public static void InvalidateWatchCacheForPerson( int personId )
        {
            _personBustEpoch[personId] = System.DateTime.UtcNow;
        }

        // MediaElement Guid -> Id. A media element's Id never changes for a Guid, so
        // this never expires; it exists purely to skip the per-calc MediaElement.Get
        // round trip on render syncs.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, int> _mediaIdByGuid
            = new System.Collections.Concurrent.ConcurrentDictionary<Guid, int>();

        internal static int? ResolveMediaElementId( Guid mediaGuid, RockContext rockContext )
        {
            if ( _mediaIdByGuid.TryGetValue( mediaGuid, out var cachedId ) )
            {
                return cachedId;
            }

            var mediaElement = new MediaElementService( rockContext ).Get( mediaGuid );
            if ( mediaElement == null )
            {
                return null;
            }

            _mediaIdByGuid[mediaGuid] = mediaElement.Id;
            return mediaElement.Id;
        }

        /// <summary>
        /// Engine hook (stage prefetch): ONE Interaction query covering every
        /// MediaWatched calc in the stage for the person, decoded into per-media
        /// aggregates on the eval context. GetPersonWatchAggregate consumes them,
        /// so an 18-calc stage open pays one media round trip instead of 17.
        /// Calc attributes must already be loaded (the stage prefetch batch-loads
        /// them before calling this).
        /// </summary>
        internal static void PrefetchPersonAggregates( List<JourneyCalculation> calculations, int personId, RockContext rockContext, StageEvalContext ctx )
        {
            var mediaElementIds = new List<int>();
            foreach ( var calc in calculations )
            {
                if ( calc.CalculationTypeEntityType?.Name?.Contains( "MediaWatchedCalculation" ) != true )
                {
                    continue;
                }

                var mediaGuid = calc.GetAttributeValue( "MediaElement" ).AsGuidOrNull();
                if ( !mediaGuid.HasValue )
                {
                    continue;
                }

                var mediaId = ResolveMediaElementId( mediaGuid.Value, rockContext );
                if ( mediaId.HasValue )
                {
                    mediaElementIds.Add( mediaId.Value );
                }
            }

            ctx.MediaAggregateByElementId = new Dictionary<int, object>();
            if ( mediaElementIds.Count == 0 )
            {
                return;
            }

            var rows = new InteractionService( rockContext ).Queryable().AsNoTracking()
                .Where( i =>
                    i.InteractionComponent.EntityId.HasValue
                    && mediaElementIds.Contains( i.InteractionComponent.EntityId.Value )
                    && i.PersonAliasId.HasValue
                    && i.PersonAlias.PersonId == personId
                    && i.InteractionData != null )
                .Select( i => new { MediaId = i.InteractionComponent.EntityId.Value, i.InteractionData } )
                .ToList();

            foreach ( var mediaId in mediaElementIds.Distinct() )
            {
                var agg = BuildAggregate( rows.Where( r => r.MediaId == mediaId ).Select( r => r.InteractionData ) );
                ctx.MediaAggregateByElementId[mediaId] = agg.HasValue ? ( object ) agg.Value : null;
            }
        }

        private static bool IsBustedFor( int? personId, System.DateTime cachedAt )
        {
            if ( !personId.HasValue || !_personBustEpoch.TryGetValue( personId.Value, out var bustedAt ) )
            {
                return false;
            }

            // Self-clean: an epoch older than the TTL can't outlive any cache entry.
            if ( ( System.DateTime.UtcNow - bustedAt ) > _cacheTtl )
            {
                _personBustEpoch.TryRemove( personId.Value, out _ );
                return false;
            }

            return cachedAt < bustedAt;
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
                var agg = BuildAggregate( personGroup.Select( r => r.InteractionData ) );
                if ( agg.HasValue )
                {
                    map[personGroup.Key] = agg.Value;
                }
            }

            _watchCache[mediaElementId] = ( System.DateTime.UtcNow, map );
            return map;
        }

        /// <summary>
        /// Single-person aggregate for render-path evaluations. A fresh, un-busted
        /// shared entry is a free hit; otherwise this queries ONLY the person's own
        /// Interaction rows for the media element instead of rebuilding the
        /// all-persons union — that firehose decode (every watcher's JSON, per
        /// video, per open) was the bulk of the stage-page render cost. The shared
        /// cache is neither served stale nor written here: population runs keep
        /// their own rebuild cadence.
        /// </summary>
        private static PersonWatchAggregate? GetPersonWatchAggregate( int mediaElementId, int personId, RockContext rockContext )
        {
            // Stage prefetch hit: the engine already decoded this person's aggregate
            // for every media element in the stage in one query.
            var evalCtx = StageEvalContext.Current;
            if ( evalCtx?.SinglePersonId == personId && evalCtx.MediaAggregateByElementId != null
                && evalCtx.MediaAggregateByElementId.TryGetValue( mediaElementId, out var prefetched ) )
            {
                return prefetched == null ? ( PersonWatchAggregate? ) null : ( PersonWatchAggregate ) prefetched;
            }

            if ( _watchCache.TryGetValue( mediaElementId, out var cached )
                && ( System.DateTime.UtcNow - cached.CachedAt ) < _cacheTtl
                && !IsBustedFor( personId, cached.CachedAt ) )
            {
                return cached.Map.TryGetValue( personId, out var hit ) ? hit : ( PersonWatchAggregate? ) null;
            }

            var rows = new InteractionService( rockContext ).Queryable().AsNoTracking()
                .Where( i =>
                    i.InteractionComponent.EntityId == mediaElementId
                    && i.PersonAliasId.HasValue
                    && i.PersonAlias.PersonId == personId
                    && i.InteractionData != null )
                .Select( i => i.InteractionData )
                .ToList();

            return BuildAggregate( rows );
        }

        /// <summary>
        /// Builds the boxed watch aggregate for one person's InteractionData rows for a
        /// single media element — the exact value shape
        /// <see cref="Data.StageEvalContext.MediaAggregateByElementId"/> stores (null =
        /// no decodable watches). Lets callers that already hold the person's Interaction
        /// rows (StageVideoData's batched resume query) populate the prefetch slot
        /// without a second Interaction query.
        /// </summary>
        internal static object BuildBoxedAggregate( IEnumerable<string> interactionDataRows )
        {
            var agg = BuildAggregate( interactionDataRows );
            return agg.HasValue ? ( object ) agg.Value : null;
        }

        /// <summary>
        /// Unions one person's raw InteractionData rows into a watch aggregate.
        /// Returns null when no row carries a decodable WatchMap.
        /// </summary>
        private static PersonWatchAggregate? BuildAggregate( IEnumerable<string> interactionDataRows )
        {
            int[] union = null;
            double maxSingleSession = 0;
            int sessionCount = 0;

            foreach ( var raw in interactionDataRows )
            {
                MediaWatchedInteractionData data;
                try { data = JsonConvert.DeserializeObject<MediaWatchedInteractionData>( raw ); }
                catch { continue; }
                if ( data == null ) continue;

                if ( data.WatchedPercentage > maxSingleSession ) maxSingleSession = data.WatchedPercentage;

                var bits = RleToArray( data.WatchMap );
                if ( bits == null || bits.Length == 0 ) continue;

                sessionCount++;
                union = ( union == null ) ? bits : MergeMaps( union, bits );
            }

            if ( union == null )
            {
                return null;
            }

            return new PersonWatchAggregate
            {
                UnionBits = union,
                MaxSingleSession = maxSingleSession,
                SessionCount = sessionCount
            };
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

            var mediaElementId = ResolveMediaElementId( mediaGuid.Value, rockContext );
            if ( !mediaElementId.HasValue ) return results;

            // Render-path fast path: a single-person evaluation reads (at most) that
            // person's own Interaction rows — never the all-persons union.
            if ( populationPersonIds.Count == 1 )
            {
                var singlePersonId = populationPersonIds.First();
                var personAgg = GetPersonWatchAggregate( mediaElementId.Value, singlePersonId, rockContext );
                if ( personAgg.HasValue )
                {
                    AppendIfPassing( results, singlePersonId, personAgg.Value, minPercent );
                }
                return results;
            }

            var aggregates = GetWatchAggregates( mediaElementId.Value, rockContext );
            if ( aggregates.Count == 0 ) return results;

            foreach ( var personId in populationPersonIds )
            {
                if ( !aggregates.TryGetValue( personId, out var agg ) ) continue;
                AppendIfPassing( results, personId, agg, minPercent );
            }

            return results;
        }

        private static void AppendIfPassing(
            Dictionary<int, Dictionary<string, object>> results,
            int personId,
            PersonWatchAggregate agg,
            int minPercent )
        {
            int watchedSeconds = agg.UnionBits.Count( v => v > 0 );
            double percent = agg.UnionBits.Length > 0 ? ( watchedSeconds * 100.0 / agg.UnionBits.Length ) : 0;
            if ( percent < minPercent ) return;

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
                var mediaElementId = ResolveMediaElementId( mediaGuid.Value, rockContext );
                if ( mediaElementId.HasValue )
                {
                    var personAgg = GetPersonWatchAggregate( mediaElementId.Value, personId, rockContext );
                    if ( personAgg.HasValue )
                    {
                        var agg = personAgg.Value;
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
