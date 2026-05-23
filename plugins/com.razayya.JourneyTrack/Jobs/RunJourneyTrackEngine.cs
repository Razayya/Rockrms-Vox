using System.ComponentModel;
using System.Linq;

using com.razayya.JourneyTrack.Constants;
using com.razayya.JourneyTrack.Data;

using Microsoft.Extensions.Logging;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Jobs;
using Rock.Logging;

namespace com.razayya.JourneyTrack.Jobs
{
    /// <summary>
    /// Nightly job that processes all active Journey Programs in order,
    /// evaluates each JourneyCalculation against the scoped population, and writes
    /// computed values to target Person Attributes.
    /// </summary>
    [DisplayName( "JourneyTrack" )]
    [Description( "Processes Journey Programs and writes computed values to Person Attributes based on configured JourneyCalculation types." )]

    [BooleanField( "Show Debug Logs",
        Description = "Enable this to show detailed DEBUG logging messages for the job.",
        DefaultBooleanValue = false,
        Order = 0,
        Key = AttributeKey.ShowDebug )]

    public class RunJourneyTrackEngine : RockJob
    {
        public override void Execute()
        {
            var showDebug = GetAttributeValue( AttributeKey.ShowDebug ).AsBoolean();

            var service = new JourneyTrackService();
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

            // Optimization O9: Run-history retention. Drop JourneyCalculationRun rows older
            // than 90 days so the table doesn't grow unbounded.
            try
            {
                using ( var rockContext = new RockContext() )
                {
                    var retentionRowsDeleted = rockContext.Database.ExecuteSqlCommand(
                        "DELETE FROM [_com_razayya_JourneyTrack_JourneyCalculationRun] WHERE [RunDateTime] < DATEADD(day, -90, GETDATE())" );
                    if ( retentionRowsDeleted > 0 )
                    {
                        Result += $"\nRetention: removed {retentionRowsDeleted} run history row(s) older than 90 days.";
                    }
                }
            }
            catch ( System.Exception ex )
            {
                Logger.LogError( ex, "JourneyTrack retention sweep failed" );
            }
        }
    }
}
