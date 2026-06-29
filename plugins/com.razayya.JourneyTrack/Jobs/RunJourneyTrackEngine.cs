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

            // Show the last few milestones, each on its own line, so the Jobs Administration
            // status reads as a short rolling log instead of one squished line.
            // UpdateLastStatusMessage persists immediately (one small read+write per call), so
            // the engine only reports at program / stage / calc boundaries and throttled inside
            // the bulk write loop — never per person.
            var recent = new System.Collections.Generic.Queue<string>();
            var service = new JourneyTrackService
            {
                OnProgress = message =>
                {
                    recent.Enqueue( message );
                    while ( recent.Count > 4 ) recent.Dequeue();
                    UpdateLastStatusMessage( string.Join( "\n", recent ) );
                }
            };
            var result = service.ProcessAllGroups();

            // Build the final summary fresh. Rock writes Result to LastStatusMessage when the
            // job ends; without this reset it would append onto the last in-flight progress
            // line (the overwrite-per-message progress leaves that line in Result).
            Result = string.Empty;

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

            // Per-program, per-stage breakdown so "written" vs "unchanged" is unambiguous:
            // each stage's own calc writes are reported separately from the program completion
            // rollup (the rollup is what writes the "Completed" flag for everyone).
            var sb = new System.Text.StringBuilder();
            foreach ( var prog in result.ProgramSummaries )
            {
                sb.AppendLine( $"{prog.ProgramName} — {prog.Enrollees:N0} enrollees" );
                foreach ( var st in prog.Stages )
                {
                    var calcs = $"{st.CalcCount} calc{( st.CalcCount == 1 ? "" : "s" )}";
                    if ( st.Skipped )
                    {
                        sb.AppendLine( $"  {st.Name} ({calcs}): skipped — nobody reached this stage" );
                    }
                    else
                    {
                        sb.AppendLine( $"  {st.Name} ({calcs}): {st.Passers:N0} of {st.Evaluated:N0} passed · {st.Written:N0} written, {st.Unchanged:N0} unchanged" );
                    }
                }
                if ( prog.HasRollup )
                {
                    sb.AppendLine( $"  Completion rollup ({prog.RollupAttributeName}): {prog.RollupCompleted:N0} of {prog.Enrollees:N0} finished all stages — wrote the flag for {prog.RollupWritten:N0}" );
                }
            }
            sb.Append( $"Totals: {result.Updated:N0} value(s) written, {result.Skipped:N0} left unchanged, {result.Errors.Count} error(s)." );
            Result += sb.ToString();

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
