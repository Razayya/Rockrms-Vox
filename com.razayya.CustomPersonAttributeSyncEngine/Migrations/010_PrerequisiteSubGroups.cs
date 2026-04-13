using Rock.Plugin;

namespace com.razayya.CustomPersonAttributeSyncEngine.Migrations
{
    [MigrationNumber( 10, "1.15.0" )]
    public class PrerequisiteSubGroups : Migration
    {
        private const string TableName = Constants.TableName.CalculationSubGroup;

        public override void Up()
        {
            // 1. Add the new column
            Sql( $@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{TableName}' AND COLUMN_NAME = 'PrerequisiteSubGroupIds')
BEGIN
    ALTER TABLE [dbo].[{TableName}] ADD [PrerequisiteSubGroupIds] NVARCHAR(500) NULL
END
" );

            // 2. Migrate existing ScopeToPreviousSubGroup data:
            //    For each sub-group where ScopeToPreviousSubGroup = 1, find the immediately
            //    preceding sibling (by Order) in the same group and store its Id.
            Sql( $@"
;WITH Ordered AS (
    SELECT
        [Id],
        [CalculationGroupId],
        [ScopeToPreviousSubGroup],
        [Order],
        LAG([Id]) OVER (PARTITION BY [CalculationGroupId] ORDER BY [Order], [Name]) AS PreviousId
    FROM [dbo].[{TableName}]
)
UPDATE sg
SET sg.[PrerequisiteSubGroupIds] = CAST(o.PreviousId AS NVARCHAR(20))
FROM [dbo].[{TableName}] sg
INNER JOIN Ordered o ON sg.[Id] = o.[Id]
WHERE o.[ScopeToPreviousSubGroup] = 1
  AND o.PreviousId IS NOT NULL
" );

            // 3. Drop the old column
            Sql( $@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{TableName}' AND COLUMN_NAME = 'ScopeToPreviousSubGroup')
BEGIN
    ALTER TABLE [dbo].[{TableName}] DROP COLUMN [ScopeToPreviousSubGroup]
END
" );
        }

        public override void Down()
        {
            // Re-add the boolean column with default true
            Sql( $@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{TableName}' AND COLUMN_NAME = 'ScopeToPreviousSubGroup')
BEGIN
    ALTER TABLE [dbo].[{TableName}] ADD [ScopeToPreviousSubGroup] BIT NOT NULL CONSTRAINT [DF_{TableName}_ScopeToPrev] DEFAULT (1)
END
" );

            // Best-effort reverse: if PrerequisiteSubGroupIds is non-empty, set ScopeToPreviousSubGroup = 1
            Sql( $@"
UPDATE [dbo].[{TableName}]
SET [ScopeToPreviousSubGroup] = CASE WHEN [PrerequisiteSubGroupIds] IS NOT NULL AND [PrerequisiteSubGroupIds] != '' THEN 1 ELSE 0 END
" );

            // Drop the new column
            Sql( $@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{TableName}' AND COLUMN_NAME = 'PrerequisiteSubGroupIds')
BEGIN
    ALTER TABLE [dbo].[{TableName}] DROP COLUMN [PrerequisiteSubGroupIds]
END
" );
        }
    }
}
