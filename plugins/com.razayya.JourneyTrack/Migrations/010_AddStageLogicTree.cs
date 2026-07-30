using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    [MigrationNumber( 10, "1.15.0" )]
    public class AddStageLogicTree : Migration
    {
        public override void Up()
        {
            Sql( @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'LogicTreeJson' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_Stage'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_Stage]
        ADD [LogicTreeJson] NVARCHAR(MAX) NULL;
END
" );
        }

        public override void Down()
        {
            Sql( @"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'LogicTreeJson' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_Stage'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_Stage] DROP COLUMN [LogicTreeJson];
END
" );
        }
    }
}
