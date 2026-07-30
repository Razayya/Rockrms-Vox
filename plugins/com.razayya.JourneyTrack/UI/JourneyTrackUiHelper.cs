using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI.WebControls;

using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.UI.Controls;

namespace com.razayya.JourneyTrack.UI
{
    /// <summary>
    /// Shared UI helpers for the JourneyTrack WebForms blocks. Lives in the plugin
    /// assembly (not in any single .ascx code-behind) so every block can call it.
    ///
    /// <para>Why this exists: ASP.NET compiles each .ascx into its own assembly, so a
    /// <c>static</c> method declared on one control's code-behind (e.g.
    /// JourneyCalculationDetail) is NOT reliably resolvable from another control
    /// (JourneyProgramDetail / StageDetail) — that pattern fails to compile with
    /// "CS0103: The name '...' does not exist in the current context". Putting shared
    /// helpers in the DLL — which every block already references — resolves under any
    /// compilation mode.</para>
    /// </summary>
    public static class JourneyTrackUiHelper
    {
        /// <summary>
        /// Load all active SystemCommunication templates into a dropdown (alphabetical),
        /// preserve a "(none)" empty option, and select the current Id if configured.
        /// </summary>
        public static void PopulateSystemCommunicationPicker( RockDropDownList ddl, int? selectedId )
        {
            ddl.Items.Clear();
            ddl.Items.Add( new ListItem( "(none)", string.Empty ) );
            using ( var rockContext = new RockContext() )
            {
                var comms = new SystemCommunicationService( rockContext ).Queryable().AsNoTracking()
                    .Where( c => c.IsActive == true )
                    .OrderBy( c => c.Title )
                    .Select( c => new { c.Id, c.Title } )
                    .ToList();
                foreach ( var c in comms )
                {
                    ddl.Items.Add( new ListItem( c.Title, c.Id.ToString() ) );
                }
            }
            if ( selectedId.HasValue && selectedId.Value > 0 )
            {
                ddl.SetValue( selectedId.Value );
            }
        }

        /// <summary>
        /// Renders a friendly view-mode summary of a Stage's <c>MediaGroupsJson</c> (the
        /// Media Groups editor's persisted value), resolving calc Ids to names. Returns a
        /// "one sequence" note when no groups are configured. Lives here (DLL) rather than on
        /// the editor's .ascx code-behind so StageDetail can call it without the cross-control
        /// CS0103 trap described above.
        /// </summary>
        public static string FormatMediaGroupsSummaryHtml( string mediaGroupsJson, int stageId )
        {
            var config = StageMediaGroups.Parse( mediaGroupsJson );

            Dictionary<int, string> names;
            using ( var rockContext = new RockContext() )
            {
                names = new JourneyCalculationService( rockContext ).Queryable().AsNoTracking()
                    .Where( c => c.StageId == stageId )
                    .Select( c => new { c.Id, c.Name } )
                    .ToDictionary( c => c.Id, c => c.Name );
            }

            if ( config.Groups.Count == 0 )
            {
                return "<span class='text-muted'>No media groups &mdash; all videos play as one sequence.</span>";
            }

            var sb = new StringBuilder();
            int n = 0;
            foreach ( var group in config.Groups )
            {
                n++;
                var label = string.IsNullOrWhiteSpace( group.Name ) ? ( "Group " + n ) : group.Name;
                var vids = group.CalcIds.Select( id =>
                    HttpUtility.HtmlEncode( names.TryGetValue( id, out var nm ) ? nm : ( "calc #" + id ) ) );
                sb.Append( "<div><strong>" )
                  .Append( HttpUtility.HtmlEncode( label ) )
                  .Append( "</strong>: " )
                  .Append( string.Join( " &rarr; ", vids ) )
                  .Append( "</div>" );
            }
            return sb.ToString();
        }
    }
}
