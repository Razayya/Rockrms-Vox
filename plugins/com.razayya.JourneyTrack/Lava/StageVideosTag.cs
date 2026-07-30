using System.Collections.Generic;
using System.IO;

using com.razayya.JourneyTrack.Data;

using Rock;
using Rock.Data;
using Rock.Lava;
using Rock.Model;

namespace com.razayya.JourneyTrack.Lava
{
    /// <summary>
    /// Lava tag: {% stagevideos stage:'<guid>' personid:'<int>' capture:'videos' %}
    ///   or:   {% stagevideos stage:'<guid>' personaliasguid:'<guid>' capture:'videos' %}
    ///
    /// Optional group:'<name-or-key>' narrows the capture to a single Media Group
    /// sequence — matched on the group's Name or Key (or "default" for the ungrouped
    /// sequence). Omit it to capture every sequence in the Stage.
    ///
    /// Optional showcompleted:'true' narrows the capture to videos the person has already
    /// completed (for a "completed" carousel). Default returns the full active sequence —
    /// the "Up Next" surface just renders the first available item from it.
    ///
    /// Captures into the named Lava variable the ordered list of active MediaWatched
    /// JourneyCalculations under the supplied Stage, each with the engine-evaluated
    /// watch state for the resolved person and the most-recent Interaction Guid +
    /// WatchMap (for Rock:MediaPlayer resume). Render-time only — does NOT trigger
    /// engine sync. Callers that want fresh per-video sink writes should pair this
    /// with {% syncpersonjourney stage:'...' personid:'...' %} first.
    ///
    /// Each captured item also carries its Media Group / sequence membership
    /// (GroupName, IsGrouped, OrderInSequence, IsFirstInSequence) and lock state
    /// (IsLocked / IsAvailable, PreviousName) derived from Stage.MediaGroupsJson — so
    /// the app can render group headers and gate videos that aren't unlocked yet. With
    /// no groups configured every video is in one "default" sequence (fully sequential).
    ///
    /// Capture is required; the tag emits no body output.
    ///
    /// Requires the host block's EnabledLavaCommands to include `stagevideos`.
    /// </summary>
    public class StageVideosTag : LavaTagBase, ILavaSecured
    {
        // SourceElementName auto-derives from class name minus "Tag" suffix → "stagevideos".

        private class P
        {
            public const string Stage           = "stage";
            public const string PersonId        = "personid";
            public const string PersonAliasGuid = "personaliasguid";
            public const string Group           = "group";
            public const string ShowCompleted   = "showcompleted";
            public const string Capture         = "capture";
        }

        public override void OnRender( ILavaRenderContext context, TextWriter result )
        {
            // IMPORTANT: this tag is invoked in a mobile block's PRE-ROOT preamble (above the
            // XAML root element). Anything written to `result` there — even an XML comment —
            // lands before the root and trips the mobile shell's Xml_InvalidRootData(1,1).
            // So we NEVER write to the body. Every outcome is surfaced through merge fields:
            //   <captureVar>       -> the item list (empty list on any failure, safe for {% for %})
            //   <captureVar>Error  -> a human-readable message (render it INSIDE the root if wanted)
            var parms = new Dictionary<string, string>();
            LavaHelper.ParseCommandMarkup( this.ElementAttributesMarkup, context, parms );

            var captureVar = parms.GetValueOrNull( P.Capture );
            var errorVar = string.IsNullOrWhiteSpace( captureVar ) ? "stageVideosError" : captureVar + "Error";

            try
            {
                if ( !this.IsAuthorized( context ) )
                {
                    context.SetMergeField( errorVar, string.Format( LavaBlockBase.NotAuthorizedMessage, this.SourceElementName ) );
                    return;
                }

                if ( string.IsNullOrWhiteSpace( captureVar ) )
                {
                    context.SetMergeField( errorVar, "stagevideos: missing required 'capture' argument" );
                    return;
                }

                var stageGuid = parms.GetValueOrNull( P.Stage ).AsGuidOrNull();
                if ( !stageGuid.HasValue )
                {
                    context.SetMergeField( captureVar, new List<object>() );
                    context.SetMergeField( errorVar, "stagevideos: missing or invalid stage guid" );
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
                    context.SetMergeField( errorVar, "stagevideos: missing or unresolved personid / personaliasguid" );
                    return;
                }

                // Optional: render only one sequence, addressed by Media Group Name or Key.
                var groupRef = parms.GetValueOrNull( P.Group );

                // Optional: showcompleted:'true' captures only videos the person has completed
                // (for a "completed" carousel). Default/false returns the full active sequence.
                var showCompleted = parms.GetValueOrNull( P.ShowCompleted ).AsBooleanOrNull() ?? false;

                var items = StageVideoData.GetForStageAndPerson( stageGuid.Value, personId.Value, rockContext, groupRef, showCompleted );
                context.SetMergeField( captureVar, items );
            }
            catch ( System.Exception ex )
            {
                if ( !string.IsNullOrWhiteSpace( captureVar ) )
                {
                    context.SetMergeField( captureVar, new List<object>() );
                }
                context.SetMergeField( errorVar, "stagevideos: " + ex.Message );
            }
        }

        public string RequiredPermissionKey => "stagevideos";
    }
}
