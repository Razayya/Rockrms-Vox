using Rock.Plugin;

namespace com.razayya.CustomPersonAttributeSyncEngine.Migrations
{
    [MigrationNumber( 8, "1.15.0" )]
    public class RegisterFieldTypeAndWorkflowAction : Migration
    {
        private const string FieldTypeGuid = "F1A2B3C4-D5E6-4F7A-8B9C-0D1E2F3A4B5C";
        private const string WorkflowActionEntityTypeGuid = "A2B3C4D5-E6F7-4A8B-9C0D-1E2F3A4B5C6D";

        public override void Up()
        {
            // 1. Register the CalculationGroupFieldType in the FieldType table
            Sql( $@"
IF NOT EXISTS (SELECT 1 FROM [FieldType] WHERE [Guid] = '{FieldTypeGuid}')
BEGIN
    INSERT INTO [FieldType] ([IsSystem], [Name], [Description], [Assembly], [Class], [Guid])
    VALUES (
        0,
        'Calculation Group',
        'A field type that stores a reference to a Calculation Group.',
        'com.razayya.CustomPersonAttributeSyncEngine',
        'com.razayya.CustomPersonAttributeSyncEngine.Field.Types.CalculationGroupFieldType',
        '{FieldTypeGuid}'
    )
END
" );

            // 2. Register the Workflow Action EntityType
            Sql( $@"
IF NOT EXISTS (SELECT 1 FROM [EntityType] WHERE [Guid] = '{WorkflowActionEntityTypeGuid}')
BEGIN
    INSERT INTO [EntityType] ([Name], [AssemblyName], [FriendlyName], [IsEntity], [IsSecured], [IsCommon], [Guid])
    VALUES (
        'com.razayya.CustomPersonAttributeSyncEngine.Workflow.Action.RunPersonAttributeSync',
        'com.razayya.CustomPersonAttributeSyncEngine',
        'Run Person Attribute Sync',
        0, 0, 0,
        '{WorkflowActionEntityTypeGuid}'
    )
END
" );
        }

        public override void Down()
        {
            Sql( $"DELETE FROM [EntityType] WHERE [Guid] = '{WorkflowActionEntityTypeGuid}'" );
            Sql( $"DELETE FROM [FieldType] WHERE [Guid] = '{FieldTypeGuid}'" );
        }
    }
}
