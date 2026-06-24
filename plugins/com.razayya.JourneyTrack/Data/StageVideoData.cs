using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

using com.razayya.JourneyTrack.CalculationTypes;
using com.razayya.JourneyTrack.Model;

using Newtonsoft.Json;

using Rock;
using Rock.Data;
using Rock.Media;
using Rock.Model;
using Rock.Utility;
using Rock.Web.Cache;

namespace com.razayya.JourneyTrack.Data
{
    /// <summary>
    /// Shared data layer for the StageVideos Lava surfaces (shortcode + command).
    /// Loads active MediaWatched JourneyCalculations under a Stage, evaluates each
    /// against a single person, and returns a flattened DTO list suitable for
    /// direct iteration in mobile XAML — including the most-recent Interaction
    /// Guid + WatchMap so Rock:MediaPlayer can resume the in-progress session.
    /// </summary>
    public static class StageVideoData
    {
        /// <summary>
        /// One entry per active MediaWatched calc on the Stage. Fields chosen to
        /// match what block 6187's XAML currently consumes from the AppendWatches
        /// filter — so the XAML migration is mostly s/video.X/dtoItem.X/.
        ///
        /// Inherits from <see cref="Rock.Utility.RockDynamic"/> so Lava can access
        /// the properties — Rock 18's Fluid Lava engine doesn't expose plain POCO
        /// properties to `{{ item.PropertyName }}` syntax without this base.
        /// </summary>
        public class StageVideoItem : RockDynamic
        {
            // MediaElement fields (what XAML's MediaPlayer needs)
            public Guid   Guid                 { get; set; }
            public int    Id                   { get; set; }
            public string Name                 { get; set; }
            public string DefaultFileUrl       { get; set; }
            public string DefaultThumbnailUrl  { get; set; }
            public int    DurationSeconds      { get; set; }

            // Calc identity / ordering
            public int    CalcId               { get; set; }
            public Guid   CalcGuid             { get; set; }
            public string CalcName             { get; set; }
            public int    CalcOrder            { get; set; }

            // Engine evaluation for the requested person
            public bool   Matched              { get; set; }
            public double WatchedPercentage    { get; set; }   // alias: WatchLength (the existing XAML uses .WatchLength)
            public double WatchLength          { get; set; }   // == WatchedPercentage; kept as a XAML-compat alias
            public double MaxSingleSessionPercentage { get; set; }
            public int    WatchedSeconds       { get; set; }
            public int    MapLength            { get; set; }
            public int    SessionCount         { get; set; }

            // Most-recent interaction (so MediaPlayer can resume the in-progress session)
            public Guid?  WatchInteractionGuid { get; set; }
            public string WatchMap             { get; set; }
        }

        /// <summary>
        /// Loads active MediaWatched JourneyCalculations under <paramref name="stageGuid"/>,
        /// ordered by [Order]/Name, and returns one DTO per calc with engine-evaluated
        /// watch state for <paramref name="personId"/>. Returns an empty list when the
        /// Stage isn't found or has no MediaWatched calcs.
        ///
        /// Does NOT call the engine sync — callers that want fresh writes to the per-video
        /// sink attribute should fire {% syncpersonjourney stage:'...' %} first.
        /// </summary>
        public static List<StageVideoItem> GetForStageAndPerson( Guid stageGuid, int personId, RockContext rockContext )
        {
            var output = new List<StageVideoItem>();
            if ( rockContext == null )
            {
                return output;
            }

            var stage = new StageService( rockContext ).Queryable().AsNoTracking()
                .FirstOrDefault( s => s.Guid == stageGuid && s.IsActive );
            if ( stage == null )
            {
                return output;
            }

            var mediaWatchedEntityTypeName = typeof( MediaWatchedCalculation ).FullName;
            var calcs = new JourneyCalculationService( rockContext ).Queryable()
                .Include( c => c.CalculationTypeEntityType )
                .Where( c => c.StageId == stage.Id
                    && c.IsActive
                    && c.CalculationTypeEntityType.Name == mediaWatchedEntityTypeName )
                .OrderBy( c => c.Order )
                .ThenBy( c => c.Name )
                .ToList();

            if ( calcs.Count == 0 )
            {
                return output;
            }

            var component = JourneyCalculationTypeComponent.GetComponent( mediaWatchedEntityTypeName );
            if ( component == null )
            {
                return output;
            }

            var singlePersonPopulation = new HashSet<int> { personId };

            // Pre-resolve PersonAlias.Ids for this person so the Interaction lookup can join.
            var personAliasIds = new PersonAliasService( rockContext ).Queryable().AsNoTracking()
                .Where( pa => pa.PersonId == personId )
                .Select( pa => pa.Id )
                .ToList();

            // Batch-resolve calc → MediaElement Guid in one pass over loaded attribute values,
            // then batch-load all MediaElement records in a single query. Avoids the per-calc
            // round trip when a Stage's typical 5-10 calcs each point at a distinct media.
            var calcToMediaGuid = new Dictionary<int, Guid>();
            foreach ( var calc in calcs )
            {
                calc.LoadAttributes( rockContext );
                var mediaGuid = calc.GetAttributeValue( "MediaElement" ).AsGuidOrNull();
                if ( mediaGuid.HasValue )
                {
                    calcToMediaGuid[calc.Id] = mediaGuid.Value;
                }
            }

            var distinctMediaGuids = calcToMediaGuid.Values.Distinct().ToList();
            var mediaElementsByGuid = new MediaElementService( rockContext ).Queryable().AsNoTracking()
                .Where( me => distinctMediaGuids.Contains( me.Guid ) )
                .ToDictionary( me => me.Guid );

            // Batch-load matching Interactions in one round trip, then pick the latest per
            // MediaElement in memory. EF6's LINQ-to-SQL translation can choke on a server-side
            // `GroupBy(...).Select(g => g.OrderByDescending(...).First())` shape, so we
            // materialize first. Cardinality is bounded (one person × ~10 medias × N sessions
            // each, typically < 50 rows), so the over-the-wire cost is negligible.
            var latestByMediaId = new Dictionary<int, (Guid InteractionGuid, string InteractionData)>();
            if ( personAliasIds.Count > 0 && mediaElementsByGuid.Count > 0 )
            {
                var mediaIds = mediaElementsByGuid.Values.Select( me => me.Id ).ToList();
                var rows = new InteractionService( rockContext ).Queryable().AsNoTracking()
                    .Where( i => i.InteractionComponent.EntityId.HasValue
                        && mediaIds.Contains( i.InteractionComponent.EntityId.Value )
                        && i.PersonAliasId.HasValue
                        && personAliasIds.Contains( i.PersonAliasId.Value )
                        && i.InteractionData != null )
                    .Select( i => new
                    {
                        MediaId = i.InteractionComponent.EntityId.Value,
                        i.InteractionDateTime,
                        i.Guid,
                        i.InteractionData
                    } )
                    .ToList();

                foreach ( var grp in rows.GroupBy( r => r.MediaId ) )
                {
                    var latest = grp.OrderByDescending( r => r.InteractionDateTime ).First();
                    latestByMediaId[grp.Key] = (latest.Guid, latest.InteractionData);
                }
            }

            foreach ( var calc in calcs )
            {
                if ( !calcToMediaGuid.TryGetValue( calc.Id, out var mediaGuid ) )
                {
                    continue;
                }
                if ( !mediaElementsByGuid.TryGetValue( mediaGuid, out var mediaElement ) )
                {
                    continue;
                }

                // Engine evaluation against the single person.
                var evalResult = component.Evaluate( rockContext, calc, singlePersonPopulation );

                bool matched = false;
                double watchedPct = 0;
                double maxSingle = 0;
                int watchedSec = 0;
                int mapLen = 0;
                int sessionCount = 0;

                if ( evalResult != null && evalResult.TryGetValue( personId, out var fields ) && fields != null )
                {
                    matched = true;
                    watchedPct  = SafeToDouble( fields, "WatchedPercentage" );
                    maxSingle   = SafeToDouble( fields, "MaxSingleSessionPercentage" );
                    watchedSec  = SafeToInt( fields, "WatchedSeconds" );
                    mapLen      = SafeToInt( fields, "MapLength" );
                    sessionCount = SafeToInt( fields, "SessionCount" );
                }

                // Most-recent Interaction for resume — looked up from the batched dictionary.
                // Mirrors what Rock's AppendWatches filter exposes (single latest InteractionGuid + WatchMap).
                Guid? watchInteractionGuid = null;
                string watchMap = null;

                if ( latestByMediaId.TryGetValue( mediaElement.Id, out var latest ) )
                {
                    watchInteractionGuid = latest.InteractionGuid;
                    try
                    {
                        var data = JsonConvert.DeserializeObject<MediaWatchedInteractionData>( latest.InteractionData );
                        watchMap = data?.WatchMap;
                        // If single-session percentage from the latest row is higher than the
                        // engine's union (rare, but possible mid-sync), surface the higher value
                        // so the progress bar doesn't appear to regress between page renders.
                        if ( data != null && data.WatchedPercentage > maxSingle )
                        {
                            maxSingle = data.WatchedPercentage;
                        }
                    }
                    catch
                    {
                        // Malformed InteractionData — leave watchMap null.
                    }
                }

                output.Add( new StageVideoItem
                {
                    Guid                 = mediaElement.Guid,
                    Id                   = mediaElement.Id,
                    Name                 = mediaElement.Name,
                    DefaultFileUrl       = mediaElement.DefaultFileUrl,
                    DefaultThumbnailUrl  = mediaElement.DefaultThumbnailUrl,
                    DurationSeconds      = mediaElement.DurationSeconds ?? 0,

                    CalcId    = calc.Id,
                    CalcGuid  = calc.Guid,
                    CalcName  = calc.Name,
                    CalcOrder = calc.Order,

                    Matched                   = matched,
                    WatchedPercentage         = watchedPct,
                    WatchLength               = watchedPct,
                    MaxSingleSessionPercentage = maxSingle,
                    WatchedSeconds            = watchedSec,
                    MapLength                 = mapLen,
                    SessionCount              = sessionCount,

                    WatchInteractionGuid = watchInteractionGuid,
                    WatchMap             = watchMap
                } );
            }

            return output;
        }

        private static double SafeToDouble( Dictionary<string, object> fields, string key )
        {
            if ( fields == null || !fields.TryGetValue( key, out var raw ) || raw == null )
            {
                return 0;
            }
            return Convert.ToDouble( raw );
        }

        private static int SafeToInt( Dictionary<string, object> fields, string key )
        {
            if ( fields == null || !fields.TryGetValue( key, out var raw ) || raw == null )
            {
                return 0;
            }
            return Convert.ToInt32( raw );
        }
    }
}
