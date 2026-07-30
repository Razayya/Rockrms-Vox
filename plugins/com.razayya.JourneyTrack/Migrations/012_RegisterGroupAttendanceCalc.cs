using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    /// <summary>
    /// Registers the Group Attendance calculation type (attendance against specific,
    /// individually-chosen groups). Only the EntityType is created here — its config
    /// attributes (Groups, Minimum Count, Within Days) carry [FieldAttribute] decorators,
    /// so they are reflected into Attribute rows by the component container's
    /// UpdateAttributes walk on first use, exactly like the other decorator-based calc types.
    /// </summary>
    [MigrationNumber( 12, "1.15.0" )]
    public class RegisterGroupAttendanceCalc : Migration
    {
        private const string AssemblyName = "com.razayya.JourneyTrack";
        private const string CalcTypeNs   = "com.razayya.JourneyTrack.CalculationTypes";

        public override void Up()
        {
            RockMigrationHelper.UpdateEntityType(
                $"{CalcTypeNs}.GroupAttendanceCalculation",
                "Group Attendance Calculation",
                $"{CalcTypeNs}.GroupAttendanceCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                false, true,
                SystemGuid.EntityType.CALCULATION_TYPE_GROUP_ATTENDANCE );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.CALCULATION_TYPE_GROUP_ATTENDANCE );
        }
    }
}
