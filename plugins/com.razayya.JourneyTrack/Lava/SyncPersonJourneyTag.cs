using System.Collections.Generic;
using System.IO;

using com.razayya.JourneyTrack.CalculationTypes;
using com.razayya.JourneyTrack.Data;
using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Data;
using Rock.Lava;
using Rock.Model;

namespace com.razayya.JourneyTrack.Lava
{
    /// <summary>
    /// Lava tag: {% syncpersonjourney personid:'...' program:'...' %}
    ///   or:    {% syncpersonjourney personaliasguid:'...' program:'...' %}
    ///   or:    {% syncpersonjourney personid:'...' stage:'...' %}
    ///   or:    {% syncpersonjourney personid:'...' program:'...' capture:'result' %}
    ///
    /// Synchronously runs a single-person JourneyTrack sync against either the named
    /// Journey Program (`program:` arg) OR a specific Stage (`stage:` arg). The two are
    /// mutually exclusive; exactly one must be supplied. Stage-scoped syncs process
    /// the cascade through the target Stage (inclusive) and skip later Stages —
    /// useful for mobile UX where we only need the open Stage's progress refreshed.
    ///
    /// Returns nothing by default; when `capture:'varName'` is supplied, a small
    /// dictionary with { matched, updated, skipped, errors } is bound into the Lava
    /// context under that variable name.
    ///
    /// Requires the host block's EnabledLavaCommands to include `syncpersonjourney`.
    /// </summary>
    public class SyncPersonJourneyTag : LavaTagBase, ILavaSecured
    {
        // SourceElementName auto-derives from the class name minus "Tag" suffix
        // → "syncpersonjourney". Don't override (the base property is not virtual).

        private class P
        {
            public const string PersonId         = "personid";
            public const string PersonAliasGuid  = "personaliasguid";
            public const string Program          = "program";
            public const string Stage            = "stage";
            public const string Capture          = "capture";
            public const string BypassCache      = "bypasscache";
        }

        public override void OnRender( ILavaRenderContext context, TextWriter result )
        {
            // IMPORTANT: invoked in a mobile block's PRE-ROOT preamble (above the XAML root).
            // Writing anything to `result` here — even an XML comment — lands before the root
            // and trips the mobile shell's Xml_InvalidRootData(1,1). So we NEVER write to the
            // body; every outcome is surfaced through merge fields:
            //   <captureVar>            -> the { matched, updated, skipped, errors } summary
            //   syncPersonJourneyError  -> a human-readable message (or <captureVar>Error when captured)
            var parms = new Dictionary<string, string>();
            LavaHelper.ParseCommandMarkup( this.ElementAttributesMarkup, context, parms );

            var captureVar = parms.GetValueOrNull( P.Capture );
            var errorVar = string.IsNullOrWhiteSpace( captureVar ) ? "syncPersonJourneyError" : captureVar + "Error";

            try
            {
                if ( !this.IsAuthorized( context ) )
                {
                    context.SetMergeField( errorVar, string.Format( LavaBlockBase.NotAuthorizedMessage, this.SourceElementName ) );
                    return;
                }

                var rockContext = LavaHelper.GetRockContextFromLavaContext( context );

                // Resolve PersonId either from personid:'<int>' or personaliasguid:'<guid>'.
                int? personId = parms.GetValueOrNull( P.PersonId ).AsIntegerOrNull();
                if ( !personId.HasValue )
                {
                    var paGuid = parms.GetValueOrNull( P.PersonAliasGuid ).AsGuidOrNull();
                    if ( paGuid.HasValue )
                    {
                        var pa = new PersonAliasService( rockContext ).Get( paGuid.Value );
                        if ( pa != null )
                        {
                            personId = pa.PersonId;
                        }
                    }
                }

                if ( !personId.HasValue )
                {
                    context.SetMergeField( errorVar, "syncpersonjourney: missing or unresolved personid / personaliasguid" );
                    return;
                }

                // Resolve scope: `program:` or `stage:` (mutually exclusive).
                var programGuid = parms.GetValueOrNull( P.Program ).AsGuidOrNull();
                var stageGuid   = parms.GetValueOrNull( P.Stage ).AsGuidOrNull();

                if ( programGuid.HasValue && stageGuid.HasValue )
                {
                    context.SetMergeField( errorVar, "syncpersonjourney: program and stage are mutually exclusive" );
                    return;
                }
                if ( !programGuid.HasValue && !stageGuid.HasValue )
                {
                    context.SetMergeField( errorVar, "syncpersonjourney: missing program or stage guid" );
                    return;
                }

                // Bust the MediaWatched 30s cache when the caller has just written an Interaction
                // and needs immediate fresh state (test pages, "I just watched it" flows). Scoped
                // to this person — other concurrent users keep their warm cache entries.
                if ( parms.GetValueOrNull( P.BypassCache ).AsBoolean() )
                {
                    MediaWatchedCalculation.InvalidateWatchCacheForPerson( personId.Value );
                }

                var service = new JourneyTrackService();
                SyncResult syncResult;

                if ( stageGuid.HasValue )
                {
                    var stage = new StageService( rockContext ).Get( stageGuid.Value );
                    if ( stage == null )
                    {
                        context.SetMergeField( errorVar, "syncpersonjourney: stage not found" );
                        return;
                    }
                    syncResult = service.ProcessStageForPerson( stage.Id, personId.Value );
                }
                else
                {
                    var program = new JourneyProgramService( rockContext ).Get( programGuid.Value );
                    if ( program == null )
                    {
                        context.SetMergeField( errorVar, "syncpersonjourney: program not found" );
                        return;
                    }
                    syncResult = service.ProcessGroupForPerson( program.Id, personId.Value );
                }

                // Optionally capture into a Lava variable.
                if ( !string.IsNullOrWhiteSpace( captureVar ) )
                {
                    var summary = new Dictionary<string, object>
                    {
                        { "matched", syncResult.MatchedPersonIds?.Count ?? 0 },
                        { "updated", syncResult.Updated },
                        { "skipped", syncResult.Skipped },
                        { "errors",  syncResult.Errors }
                    };
                    context.SetMergeField( captureVar, summary );
                }
            }
            catch ( System.Exception ex )
            {
                context.SetMergeField( errorVar, "syncpersonjourney: " + ex.Message );
            }
        }

        public string RequiredPermissionKey => "syncpersonjourney";
    }
}
