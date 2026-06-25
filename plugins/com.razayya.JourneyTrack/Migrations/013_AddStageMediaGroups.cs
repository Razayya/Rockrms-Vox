using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    [MigrationNumber( 13, "1.15.0" )]
    public class AddStageMediaGroups : Migration
    {
        public override void Up()
        {
            Sql( @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'MediaGroupsJson' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_Stage'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_Stage]
        ADD [MediaGroupsJson] NVARCHAR(MAX) NULL;
END
" );
        }

        public override void Down()
        {
            Sql( @"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'MediaGroupsJson' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_Stage'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_Stage] DROP COLUMN [MediaGroupsJson];
END
" );
        }
    }
}
