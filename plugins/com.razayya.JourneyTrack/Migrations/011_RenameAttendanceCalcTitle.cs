using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    /// <summary>
    /// Renames the original Attendance calculation to "Group Type Attendance" to
    /// distinguish it from the new Group Attendance calculation (which checks
    /// attendance against individually-chosen groups). Only the user-facing
    /// FriendlyName changes — the EntityType Name and Guid are preserved so every
    /// existing JourneyCalculation row keeps resolving to the same component.
    /// </summary>
    [MigrationNumber( 11, "1.15.0" )]
    public class RenameAttendanceCalcTitle : Migration
    {
        private const string AssemblyName = "com.razayya.JourneyTrack";
        private const string CalcTypeNs   = "com.razayya.JourneyTrack.CalculationTypes";

        public override void Up()
        {
            RockMigrationHelper.UpdateEntityType(
                $"{CalcTypeNs}.AttendanceCalculation",
                "Group Type Attendance Calculation",
                $"{CalcTypeNs}.AttendanceCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                false, true,
                SystemGuid.EntityType.CALCULATION_TYPE_ATTENDANCE );
        }

        public override void Down()
        {
            RockMigrationHelper.UpdateEntityType(
                $"{CalcTypeNs}.AttendanceCalculation",
                "Attendance Calculation",
                $"{CalcTypeNs}.AttendanceCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                false, true,
                SystemGuid.EntityType.CALCULATION_TYPE_ATTENDANCE );
        }
    }
}
