using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    [MigrationNumber( 2, "1.15.0" )]
    public class RegisterEntityTypes : Migration
    {
        private const string AssemblyName = "com.razayya.JourneyTrack";
        private const string ModelNs      = "com.razayya.JourneyTrack.Model";
        private const string ActionNs     = "com.razayya.JourneyTrack.Workflow.Action";

        public override void Up()
        {
            // Model EntityTypes (IsEntity = true, IsSecured = true)
            RockMigrationHelper.UpdateEntityType( $"{ModelNs}.JourneyProgram",              "Journey Program",                $"{ModelNs}.JourneyProgram, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",              true, true, SystemGuid.EntityType.CALCULATION_GROUP );
            RockMigrationHelper.UpdateEntityType( $"{ModelNs}.Stage",                       "Stage",                          $"{ModelNs}.Stage, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",                       true, true, SystemGuid.EntityType.CALCULATION_SUB_GROUP );
            RockMigrationHelper.UpdateEntityType( $"{ModelNs}.JourneyCalculation",          "Journey Calculation",            $"{ModelNs}.JourneyCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",          true, true, SystemGuid.EntityType.CALCULATION );
            RockMigrationHelper.UpdateEntityType( $"{ModelNs}.JourneyCalculationRun",       "Journey Calculation Run",        $"{ModelNs}.JourneyCalculationRun, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",       true, true, SystemGuid.EntityType.CALCULATION_RUN );
            RockMigrationHelper.UpdateEntityType( $"{ModelNs}.JourneyCommunicationLog",     "Journey Communication Log",      $"{ModelNs}.JourneyCommunicationLog, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",     true, true, SystemGuid.EntityType.JOURNEY_COMMUNICATION_LOG );

            // Workflow Action EntityType (IsEntity = false, IsSecured = false)
            RockMigrationHelper.UpdateEntityType( $"{ActionNs}.RunPersonJourneySync",       "Run Person Journey Sync",        $"{ActionNs}.RunPersonJourneySync, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",       false, false, "5fa6e8c4-71a1-4ae0-8a9a-a8b9b0e7c4d1" );
        }

        public override void Down()
        {
            // Best-effort cleanup; SystemGuid values are stable so we can delete by Guid.
            RockMigrationHelper.DeleteEntityType( "5fa6e8c4-71a1-4ae0-8a9a-a8b9b0e7c4d1" );
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.JOURNEY_COMMUNICATION_LOG );
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.CALCULATION_RUN );
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.CALCULATION );
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.CALCULATION_SUB_GROUP );
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.CALCULATION_GROUP );
        }
    }
}
