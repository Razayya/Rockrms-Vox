using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    [MigrationNumber( 6, "1.15.0" )]
    public class RegisterCalculationTypes : Migration
    {
        private const string AssemblyName = "com.razayya.JourneyTrack";
        private const string CalcTypeNs   = "com.razayya.JourneyTrack.CalculationTypes";

        public override void Up()
        {
            RockMigrationHelper.UpdateEntityType( $"{CalcTypeNs}.AttendanceCalculation",              "Attendance Calculation",                 $"{CalcTypeNs}.AttendanceCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",              false, true, SystemGuid.EntityType.CALCULATION_TYPE_ATTENDANCE );
            RockMigrationHelper.UpdateEntityType( $"{CalcTypeNs}.PersonFilterCalculation",            "Person Filter Calculation",              $"{CalcTypeNs}.PersonFilterCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",            false, true, SystemGuid.EntityType.CALCULATION_TYPE_PERSON_FILTER );
            RockMigrationHelper.UpdateEntityType( $"{CalcTypeNs}.DataViewInclusionCalculation",       "DataView Inclusion Calculation",         $"{CalcTypeNs}.DataViewInclusionCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",       false, true, SystemGuid.EntityType.CALCULATION_TYPE_DATAVIEW );
            RockMigrationHelper.UpdateEntityType( $"{CalcTypeNs}.GroupTypeMembershipCalculation",     "Group Type Membership Calculation",      $"{CalcTypeNs}.GroupTypeMembershipCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",     false, true, SystemGuid.EntityType.CALCULATION_TYPE_GROUP_TYPE_MEMBERSHIP );
            RockMigrationHelper.UpdateEntityType( $"{CalcTypeNs}.GroupMembershipCalculation",         "Group Membership Calculation",           $"{CalcTypeNs}.GroupMembershipCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",         false, true, SystemGuid.EntityType.CALCULATION_TYPE_GROUP_MEMBERSHIP );
            RockMigrationHelper.UpdateEntityType( $"{CalcTypeNs}.CompletionCalculation",              "Completion Calculation",                 $"{CalcTypeNs}.CompletionCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",              false, true, SystemGuid.EntityType.CALCULATION_TYPE_COMPLETION );
            RockMigrationHelper.UpdateEntityType( $"{CalcTypeNs}.StepCompletionCalculation",          "Step Completion Calculation",            $"{CalcTypeNs}.StepCompletionCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",          false, true, SystemGuid.EntityType.CALCULATION_TYPE_STEP_COMPLETION );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.CALCULATION_TYPE_STEP_COMPLETION );
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.CALCULATION_TYPE_COMPLETION );
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.CALCULATION_TYPE_GROUP_MEMBERSHIP );
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.CALCULATION_TYPE_GROUP_TYPE_MEMBERSHIP );
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.CALCULATION_TYPE_DATAVIEW );
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.CALCULATION_TYPE_PERSON_FILTER );
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.CALCULATION_TYPE_ATTENDANCE );
        }
    }
}
