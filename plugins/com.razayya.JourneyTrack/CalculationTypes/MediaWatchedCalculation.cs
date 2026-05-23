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

        /// <inheritdoc/>
        public override Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            JourneyCalculation calc,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            var mediaGuid = calc.GetAttributeValue( "MediaElement" ).AsGuidOrNull();
            var minPercent = calc.GetAttributeValue( "MinWatchedPercent" ).AsIntegerOrNull() ?? 95;

            if ( !mediaGuid.HasValue )
            {
                return results;
            }
            if ( populationPersonIds == null || populationPersonIds.Count == 0 )
            {
                return results;
            }

            var mediaElement = new MediaElementService( rockContext ).Get( mediaGuid.Value );
            if ( mediaElement == null )
            {
                return results;
            }

            // Rock's MediaElement creates a dedicated InteractionComponent (EntityId = MediaElement.Id)
            // under the "Media Events" channel. Pull every matching Interaction row for our population.
            var rows = new InteractionService( rockContext ).Queryable().AsNoTracking()
                .Where( i =>
                    i.InteractionComponent.EntityId == mediaElement.Id
                    && i.PersonAliasId.HasValue
                    && populationPersonIds.Contains( i.PersonAlias.PersonId )
                    && i.InteractionData != null )
                .Select( i => new { PersonId = i.PersonAlias.PersonId, i.InteractionData } )
                .ToList();

            if ( rows.Count == 0 )
            {
                return results;
            }

            // Group by person, union WatchMaps within each group.
            foreach ( var personGroup in rows.GroupBy( r => r.PersonId ) )
            {
                int[] union = null;
                double maxSingleSession = 0;

                foreach ( var row in personGroup )
                {
                    MediaWatchedInteractionData data;
                    try
                    {
                        data = JsonConvert.DeserializeObject<MediaWatchedInteractionData>( row.InteractionData );
                    }
                    catch
                    {
                        continue;
                    }
                    if ( data == null )
                    {
                        continue;
                    }

                    if ( data.WatchedPercentage > maxSingleSession )
                    {
                        maxSingleSession = data.WatchedPercentage;
                    }

                    var bits = RleToArray( data.WatchMap );
                    if ( bits == null || bits.Length == 0 )
                    {
                        continue;
                    }

                    if ( union == null )
                    {
                        union = bits;
                    }
                    else
                    {
                        union = MergeMaps( union, bits );
                    }
                }

                if ( union == null )
                {
                    continue;
                }

                int watchedSeconds = union.Count( v => v > 0 );
                double percent = union.Length > 0 ? ( watchedSeconds * 100.0 / union.Length ) : 0;

                bool matched = percent >= minPercent;
                results[personGroup.Key] = new Dictionary<string, object>
                {
                    { "Matched", matched },
                    { "WatchedPercentage", Math.Round( percent, 2 ) },
                    { "MaxSingleSessionPercentage", Math.Round( maxSingleSession, 2 ) },
                    { "WatchedSeconds", watchedSeconds },
                    { "MapLength", union.Length },
                    { "SessionCount", personGroup.Count() }
                };
            }

            // Strip non-matches to keep the contract of "MatchedPersonIds = keys".
            return results.Where( kvp => kvp.Value.ContainsKey( "Matched" ) && ( bool ) kvp.Value["Matched"] )
                .ToDictionary( kvp => kvp.Key, kvp => kvp.Value );
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
