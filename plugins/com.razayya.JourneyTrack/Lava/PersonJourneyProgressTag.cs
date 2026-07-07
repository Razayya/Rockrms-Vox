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
    /// Lava tag: {% personjourneyprogress program:'&lt;guid&gt;' personid:'&lt;int&gt;' capture:'stages' %}
    ///   or:    {% personjourneyprogress program:'&lt;guid&gt;' personaliasguid:'&lt;guid&gt;' capture:'stages' %}
    ///
    /// Captures the person's per-Stage progress for the given Journey Program — the
    /// engine-evaluated pass/current state used to drive "continue where you left off"
    /// navigation (e.g. a home-screen entry card forwarding to the current Stage page).
    ///
    /// Each captured item is a dictionary:
    ///   { StageId, StageName, Order, StageNumber (Order+1), Passed, IsCurrent }
    /// ordered by Stage Order. At most one Stage is flagged IsCurrent — the first the
    /// person has NOT passed (none are current when every Stage is passed).
    ///
    /// Read-only at render time: this evaluates each Stage's logic tree but does NOT
    /// write sink attributes. Pair with {% syncpersonjourney %} first if the caller
    /// needs freshly-recomputed completion before reading.
    ///
    /// Capture is required; the tag emits no body output.
    ///
    /// Requires the host block's EnabledLavaCommands to include `personjourneyprogress`.
    /// </summary>
    public class PersonJourneyProgressTag : LavaTagBase, ILavaSecured
    {
        // SourceElementName auto-derives from the class name minus "Tag" suffix
        // → "personjourneyprogress". Don't override (the base property is not virtual).

        private class P
        {
            public const string Program         = "program";
            public const string PersonId        = "personid";
            public const string PersonAliasGuid = "personaliasguid";
            public const string Capture         = "capture";
        }

        public override void OnRender( ILavaRenderContext context, TextWriter result )
        {
            // IMPORTANT: invoked in a mobile block's PRE-ROOT preamble (above the XAML root).
            // Writing anything to `result` here — even an XML comment — lands before the root
            // and trips the mobile shell's Xml_InvalidRootData(1,1). So we NEVER write to the
            // body; every outcome is surfaced through merge fields:
            //   <captureVar>       -> the stage list (empty list on any failure, safe for {% for %})
            //   <captureVar>Error  -> a human-readable message (render it INSIDE the root if wanted)
            var parms = new Dictionary<string, string>();
            LavaHelper.ParseCommandMarkup( this.ElementAttributesMarkup, context, parms );

            var captureVar = parms.GetValueOrNull( P.Capture );
            var errorVar = string.IsNullOrWhiteSpace( captureVar ) ? "personJourneyProgressError" : captureVar + "Error";

            try
            {
                if ( !this.IsAuthorized( context ) )
                {
                    context.SetMergeField( errorVar, string.Format( LavaBlockBase.NotAuthorizedMessage, this.SourceElementName ) );
                    return;
                }

                if ( string.IsNullOrWhiteSpace( captureVar ) )
                {
                    context.SetMergeField( errorVar, "personjourneyprogress: missing required 'capture' argument" );
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
                    context.SetMergeField( captureVar, new List<object>() );
                    context.SetMergeField( errorVar, "personjourneyprogress: missing or unresolved personid / personaliasguid" );
                    return;
                }

                var programGuid = parms.GetValueOrNull( P.Program ).AsGuidOrNull();
                if ( !programGuid.HasValue )
                {
                    context.SetMergeField( captureVar, new List<object>() );
                    context.SetMergeField( errorVar, "personjourneyprogress: missing or invalid program guid" );
                    return;
                }

                var program = new JourneyProgramService( rockContext ).Get( programGuid.Value );
                if ( program == null )
                {
                    context.SetMergeField( captureVar, new List<object>() );
                    context.SetMergeField( errorVar, "personjourneyprogress: program not found" );
                    return;
                }

                var service = new JourneyTrackService();
                var progress = service.GetProgramProgressForPerson( program.Id, personId.Value );

                // Bind an ordered list of plain dictionaries (Fluid renders these directly;
                // mirrors the capture shape used by {% syncpersonjourney %}).
                var items = new List<object>();
                foreach ( var stage in progress.Stages )
                {
                    items.Add( new Dictionary<string, object>
                    {
                        { "StageId",     stage.StageId },
                        { "StageName",   stage.StageName },
                        { "Order",       stage.Order },
                        { "StageNumber", stage.Order + 1 },
                        { "Passed",      stage.Passed },
                        { "IsCurrent",   stage.IsCurrent }
                    } );
                }

                context.SetMergeField( captureVar, items );
            }
            catch ( System.Exception ex )
            {
                if ( !string.IsNullOrWhiteSpace( captureVar ) )
                {
                    context.SetMergeField( captureVar, new List<object>() );
                }
                context.SetMergeField( errorVar, "personjourneyprogress: " + ex.Message );
            }
        }

        public string RequiredPermissionKey => "personjourneyprogress";
    }
}
