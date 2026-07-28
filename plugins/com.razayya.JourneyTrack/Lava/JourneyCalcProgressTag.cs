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
    /// Lava tag: {% journeycalcprogress calc:'&lt;guid&gt;' personid:'&lt;int&gt;' capture:'p' %}
    ///   or:    {% journeycalcprogress stage:'&lt;guid&gt;' personid:'&lt;int&gt;' capture:'items' %}
    ///   or:    {% journeycalcprogress calc:'&lt;guid&gt;' personaliasguid:'&lt;guid&gt;' capture:'p' %}
    ///
    /// Captures one person's progress toward a single JourneyCalculation's requirement
    /// (`calc:` mode → one dictionary) or toward every active calculation in a Stage
    /// (`stage:` mode → ordered list of dictionaries). The two are mutually exclusive;
    /// exactly one must be supplied.
    ///
    /// Unlike the engine's own evaluation — which only reports people who MATCH — this
    /// reports partial progress for people who haven't met the requirement yet, e.g. an
    /// attendance calc with MinimumCount 4 yields Current 3 / Target 4 for someone with
    /// three attendances. Uniform fields on every entry:
    ///   CalcId, CalcGuid, CalcName, CalcType, CalcIsActive, CalcOrder,
    ///   StageId, StageGuid, StageName, StageOrder,
    ///   Matched, Current, Target, ProgressPercent,
    ///   SinkAttributeKey, SinkValue, SinkTextValue
    /// plus the calc type's own merge fields (AttendanceCount, Remaining, WithinDays,
    /// LastAttendanceDate, WatchedPercentage, SessionCount, ...).
    ///
    /// Optional args:
    ///   sync:'true'         - run a real single-person engine sync on the calc (or the
    ///                         Stage cascade in stage: mode) FIRST, writing sink
    ///                         attributes per NoMatchBehavior, then report progress.
    ///                         Default false: rendering is read-only.
    ///   includestage:'true' - also evaluate the Stage's overall gate for the person and
    ///                         add StagePassed / StageIsCurrent to each entry. Costs a
    ///                         full read-only program evaluation.
    ///   bypasscache:'true'  - clear the MediaWatched 30s aggregate cache first (test
    ///                         pages / "I just watched it" flows only).
    ///
    /// Read-only by default: without sync:'true' nothing is written.
    ///
    /// Requires the host block's EnabledLavaCommands to include `journeycalcprogress`.
    /// </summary>
    public class JourneyCalcProgressTag : LavaTagBase, ILavaSecured
    {
        // SourceElementName auto-derives from the class name minus "Tag" suffix
        // → "journeycalcprogress". Don't override (the base property is not virtual).

        private class P
        {
            public const string Calc            = "calc";
            public const string Stage           = "stage";
            public const string PersonId        = "personid";
            public const string PersonAliasGuid = "personaliasguid";
            public const string Capture         = "capture";
            public const string Sync            = "sync";
            public const string IncludeStage    = "includestage";
            public const string BypassCache     = "bypasscache";
        }

        public override void OnRender( ILavaRenderContext context, TextWriter result )
        {
            // IMPORTANT: invoked in a mobile block's PRE-ROOT preamble (above the XAML root).
            // Writing anything to `result` here — even an XML comment — lands before the root
            // and trips the mobile shell's Xml_InvalidRootData(1,1). So we NEVER write to the
            // body; every outcome is surfaced through merge fields:
            //   <captureVar>       -> the progress dictionary (calc:) or list (stage:);
            //                         null dictionary / empty list on any failure
            //   <captureVar>Error  -> a human-readable message (render it INSIDE the root if wanted)
            var parms = new Dictionary<string, string>();
            LavaHelper.ParseCommandMarkup( this.ElementAttributesMarkup, context, parms );

            var captureVar = parms.GetValueOrNull( P.Capture );
            var errorVar = string.IsNullOrWhiteSpace( captureVar ) ? "journeyCalcProgressError" : captureVar + "Error";

            var stageGuid = parms.GetValueOrNull( P.Stage ).AsGuidOrNull();
            bool listMode = stageGuid.HasValue;

            void Fail( string message )
            {
                if ( !string.IsNullOrWhiteSpace( captureVar ) )
                {
                    context.SetMergeField( captureVar, listMode ? ( object ) new List<object>() : null );
                }
                context.SetMergeField( errorVar, "journeycalcprogress: " + message );
            }

            try
            {
                if ( !this.IsAuthorized( context ) )
                {
                    context.SetMergeField( errorVar, string.Format( LavaBlockBase.NotAuthorizedMessage, this.SourceElementName ) );
                    return;
                }

                if ( string.IsNullOrWhiteSpace( captureVar ) )
                {
                    context.SetMergeField( errorVar, "journeycalcprogress: missing required 'capture' argument" );
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
                    Fail( "missing or unresolved personid / personaliasguid" );
                    return;
                }

                // Resolve scope: `calc:` or `stage:` (mutually exclusive).
                var calcGuid = parms.GetValueOrNull( P.Calc ).AsGuidOrNull();
                if ( calcGuid.HasValue && stageGuid.HasValue )
                {
                    Fail( "calc and stage are mutually exclusive" );
                    return;
                }
                if ( !calcGuid.HasValue && !stageGuid.HasValue )
                {
                    Fail( "missing calc or stage guid" );
                    return;
                }

                // Bust the MediaWatched 30s cache when the caller has just written an Interaction
                // and needs immediate fresh state (test pages, "I just watched it" flows). Scoped
                // to this person — other concurrent users keep their warm cache entries.
                if ( parms.GetValueOrNull( P.BypassCache ).AsBoolean() )
                {
                    MediaWatchedCalculation.InvalidateWatchCacheForPerson( personId.Value );
                }

                bool sync = parms.GetValueOrNull( P.Sync ).AsBoolean();
                bool includeStage = parms.GetValueOrNull( P.IncludeStage ).AsBoolean();
                var service = new JourneyTrackService();

                if ( calcGuid.HasValue )
                {
                    var calc = new JourneyCalculationService( rockContext ).Get( calcGuid.Value );
                    if ( calc == null )
                    {
                        Fail( "calculation not found" );
                        return;
                    }

                    if ( sync )
                    {
                        service.ProcessCalculationForPerson( calc.Id, personId.Value );
                    }

                    var entry = service.GetCalcProgressForPerson( calc.Id, personId.Value, includeStage );
                    context.SetMergeField( captureVar, entry );
                }
                else
                {
                    var stage = new StageService( rockContext ).Get( stageGuid.Value );
                    if ( stage == null )
                    {
                        Fail( "stage not found" );
                        return;
                    }

                    if ( sync )
                    {
                        service.ProcessStageForPerson( stage.Id, personId.Value );
                    }

                    var entries = service.GetStageCalcProgressForPerson( stage.Id, personId.Value, includeStage );
                    context.SetMergeField( captureVar, entries );
                }
            }
            catch ( System.Exception ex )
            {
                // Surface the innermost message — EF wraps SQL errors (e.g. timeouts) in
                // an EntityCommandExecutionException whose own message says nothing.
                Fail( JourneyTrackService.GetInnermostMessage( ex ) );
            }
        }

        public string RequiredPermissionKey => "journeycalcprogress";
    }
}
