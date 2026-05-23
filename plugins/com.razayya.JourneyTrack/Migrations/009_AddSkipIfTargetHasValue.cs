using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    [MigrationNumber( 9, "1.15.0" )]
    public class AddSkipIfTargetHasValue : Migration
    {
        public override void Up()
        {
            Sql( @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'SkipIfTargetHasValue' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_JourneyCalculation'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation]
        ADD [SkipIfTargetHasValue] BIT NOT NULL
            CONSTRAINT [DF__JTrack_JC_SkipIfHasVal] DEFAULT(0);
END
" );
        }

        public override void Down()
        {
            Sql( @"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'SkipIfTargetHasValue' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_JourneyCalculation'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation] DROP CONSTRAINT [DF__JTrack_JC_SkipIfHasVal];
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation] DROP COLUMN [SkipIfTargetHasValue];
END
" );
        }
    }
}
