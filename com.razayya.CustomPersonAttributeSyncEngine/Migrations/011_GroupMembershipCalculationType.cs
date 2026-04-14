using Rock.Plugin;

namespace com.razayya.CustomPersonAttributeSyncEngine.Migrations
{
    [MigrationNumber( 11, "1.15.0" )]
    public class GroupMembershipCalculationType : Migration
    {
        private const string AssemblyName = "com.razayya.CustomPersonAttributeSyncEngine";
        private const string CalcTypeNs = "com.razayya.CustomPersonAttributeSyncEngine.CalculationTypes";

        public override void Up()
        {
            // Rename existing GroupMembershipCalculation entity type to GroupTypeMembershipCalculation
            // (keeps the same GUID so existing calculations continue to work).
            RockMigrationHelper.UpdateEntityType(
                $"{CalcTypeNs}.GroupTypeMembershipCalculation",
                "Group Type Membership Calculation",
                $"{CalcTypeNs}.GroupTypeMembershipCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                false, true,
                SystemGuid.EntityType.CALCULATION_TYPE_GROUP_TYPE_MEMBERSHIP );

            // Register the new GroupMembershipCalculation (specific group + include children).
            RockMigrationHelper.UpdateEntityType(
                $"{CalcTypeNs}.GroupMembershipCalculation",
                "Group Membership Calculation",
                $"{CalcTypeNs}.GroupMembershipCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                false, true,
                SystemGuid.EntityType.CALCULATION_TYPE_GROUP_MEMBERSHIP );
        }

        public override void Down()
        {
        }
    }
}
