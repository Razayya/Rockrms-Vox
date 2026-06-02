using System.Data.Entity;
using System.Linq;
using System.Web.UI.WebControls;

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
    }
}
