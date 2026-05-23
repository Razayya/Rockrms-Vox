using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    [MigrationNumber( 5, "1.15.0" )]
    public class RegisterFieldTypeAndWorkflowAction : Migration
    {
        private const string FT_JOURNEY_PROGRAM = "8d6e3b9c-4a52-43ed-a02f-7b1e0d4f5a01";

        public override void Up()
        {
            // Register the JourneyProgramFieldType so workflows and other systems can
            // pick a JourneyProgram as a typed attribute.
            RockMigrationHelper.UpdateFieldType(
                "Journey Program",
                "Selects an active Journey Program by name; stores the JourneyProgram Guid.",
                "com.razayya.JourneyTrack",
                "com.razayya.JourneyTrack.Field.Types.JourneyProgramFieldType",
                FT_JOURNEY_PROGRAM );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteFieldType( FT_JOURNEY_PROGRAM );
        }
    }
}
