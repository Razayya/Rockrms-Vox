using System.Collections.Generic;
using System.IO;

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
        }

        public override void OnRender( ILavaRenderContext context, TextWriter result )
        {
            if ( !this.IsAuthorized( context ) )
            {
                result.Write( string.Format( LavaBlockBase.NotAuthorizedMessage, this.SourceElementName ) );
                return;
            }

            var parms = new Dictionary<string, string>();
            LavaHelper.ParseCommandMarkup( this.ElementAttributesMarkup, context, parms );

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
                result.Write( "<!-- syncpersonjourney: missing or unresolved personid / personaliasguid -->" );
                return;
            }

            // Resolve scope: `program:` or `stage:` (mutually exclusive).
            var programGuid = parms.GetValueOrNull( P.Program ).AsGuidOrNull();
            var stageGuid   = parms.GetValueOrNull( P.Stage ).AsGuidOrNull();

            if ( programGuid.HasValue && stageGuid.HasValue )
            {
                result.Write( "<!-- syncpersonjourney: program and stage are mutually exclusive -->" );
                return;
            }
            if ( !programGuid.HasValue && !stageGuid.HasValue )
            {
                result.Write( "<!-- syncpersonjourney: missing program or stage guid -->" );
                return;
            }

            var service = new JourneyTrackService();
            SyncResult syncResult;

            if ( stageGuid.HasValue )
            {
                var stage = new StageService( rockContext ).Get( stageGuid.Value );
                if ( stage == null )
                {
                    result.Write( "<!-- syncpersonjourney: stage not found -->" );
                    return;
                }
                syncResult = service.ProcessStageForPerson( stage.Id, personId.Value );
            }
            else
            {
                var program = new JourneyProgramService( rockContext ).Get( programGuid.Value );
                if ( program == null )
                {
                    result.Write( "<!-- syncpersonjourney: program not found -->" );
                    return;
                }
                syncResult = service.ProcessGroupForPerson( program.Id, personId.Value );
            }

            // Optionally capture into a Lava variable.
            var captureVar = parms.GetValueOrNull( P.Capture );
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

        public string RequiredPermissionKey => "syncpersonjourney";
    }
}
