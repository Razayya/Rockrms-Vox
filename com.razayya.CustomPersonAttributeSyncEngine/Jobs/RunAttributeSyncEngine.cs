using System.ComponentModel;
using System.Linq;

using com.razayya.CustomPersonAttributeSyncEngine.Constants;
using com.razayya.CustomPersonAttributeSyncEngine.Data;

using Microsoft.Extensions.Logging;

using Rock;
using Rock.Attribute;
using Rock.Jobs;
using Rock.Logging;

namespace com.razayya.CustomPersonAttributeSyncEngine.Jobs
{
    /// <summary>
    /// Nightly job that processes all active Calculation Groups in order,
    /// evaluates each Calculation against the scoped population, and writes
    /// computed values to target Person Attributes.
    /// </summary>
    [DisplayName( "Custom Person Attribute Sync Engine" )]
    [Description( "Processes Calculation Groups and writes computed values to Person Attributes based on configured calculation types." )]

    [BooleanField( "Show Debug Logs",
        Description = "Enable this to show detailed DEBUG logging messages for the job.",
        DefaultBooleanValue = false,
        Order = 0,
        Key = AttributeKey.ShowDebug )]

    public class RunAttributeSyncEngine : RockJob
    {
        public override void Execute()
        {
            var showDebug = GetAttributeValue( AttributeKey.ShowDebug ).AsBoolean();

            var service = new SyncEngineService();
            var result = service.ProcessAllGroups();

            if ( showDebug && result.Log.Any() )
            {
                Result += string.Join( "\n", result.Log ) + "\n\n";
            }

            if ( result.Errors.Any() )
            {
                foreach ( var error in result.Errors )
                {
                    Logger.LogError( error );
                    Result += $"ERROR: {error}\n";
                }
            }

            Result += $"Completed. {result.Updated} attribute(s) updated. {result.Skipped} skipped. {result.Errors.Count} error(s).";
        }
    }
}
