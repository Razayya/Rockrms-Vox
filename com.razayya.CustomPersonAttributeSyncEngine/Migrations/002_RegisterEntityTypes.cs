using Rock.Plugin;

namespace com.razayya.CustomPersonAttributeSyncEngine.Migrations
{
    [MigrationNumber( 2, "1.15.0" )]
    public class RegisterEntityTypes : Migration
    {
        private const string AssemblyName = "com.razayya.CustomPersonAttributeSyncEngine";
        private const string ModelNs = "com.razayya.CustomPersonAttributeSyncEngine.Model";
        private const string CalcTypeNs = "com.razayya.CustomPersonAttributeSyncEngine.CalculationTypes";

        public override void Up()
        {
            // Register entity types
            RockMigrationHelper.UpdateEntityType(
                $"{ModelNs}.CalculationGroup",
                "Calculation Group",
                $"{ModelNs}.CalculationGroup, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                true, true,
                SystemGuid.EntityType.CALCULATION_GROUP );

            RockMigrationHelper.UpdateEntityType(
                $"{ModelNs}.CalculationSubGroup",
                "Calculation Sub Group",
                $"{ModelNs}.CalculationSubGroup, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                true, true,
                SystemGuid.EntityType.CALCULATION_SUB_GROUP );

            RockMigrationHelper.UpdateEntityType(
                $"{ModelNs}.Calculation",
                "Calculation",
                $"{ModelNs}.Calculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                true, true,
                SystemGuid.EntityType.CALCULATION );

            // Register calculation type component entity types
            RockMigrationHelper.UpdateEntityType(
                $"{CalcTypeNs}.AttendanceCalculation",
                "Attendance Calculation",
                $"{CalcTypeNs}.AttendanceCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                false, true,
                SystemGuid.EntityType.CALCULATION_TYPE_ATTENDANCE );

            RockMigrationHelper.UpdateEntityType(
                $"{CalcTypeNs}.PersonFilterCalculation",
                "Person Filter Calculation",
                $"{CalcTypeNs}.PersonFilterCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                false, true,
                SystemGuid.EntityType.CALCULATION_TYPE_PERSON_FILTER );

            RockMigrationHelper.UpdateEntityType(
                $"{CalcTypeNs}.DataViewInclusionCalculation",
                "Data View Inclusion Calculation",
                $"{CalcTypeNs}.DataViewInclusionCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                false, true,
                SystemGuid.EntityType.CALCULATION_TYPE_DATAVIEW );

            RockMigrationHelper.UpdateEntityType(
                $"{CalcTypeNs}.GroupTypeMembershipCalculation",
                "Group Type Membership Calculation",
                $"{CalcTypeNs}.GroupTypeMembershipCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                false, true,
                SystemGuid.EntityType.CALCULATION_TYPE_GROUP_TYPE_MEMBERSHIP );

            RockMigrationHelper.UpdateEntityType(
                $"{CalcTypeNs}.CompletionCalculation",
                "Completion Calculation",
                $"{CalcTypeNs}.CompletionCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                false, true,
                SystemGuid.EntityType.CALCULATION_TYPE_COMPLETION );
        }

        public override void Down()
        {
        }
    }
}
