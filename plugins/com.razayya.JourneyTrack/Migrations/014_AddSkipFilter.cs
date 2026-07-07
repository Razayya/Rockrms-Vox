using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    /// <summary>
    /// Adds per-calculation "Skip If" support: a PersonFilter-style condition set
    /// (SkipFilterJson, same shape as PersonFilter's FilterConditions) plus an AND/OR
    /// toggle (SkipFilterMatchAll). When a person matches, the engine folds them into
    /// the calc's matched set (passes for stage completion + downstream gating), excludes
    /// them from component evaluation, and leaves the sink blank. Null/blank = no skip logic.
    /// </summary>
    [MigrationNumber( 14, "1.15.0" )]
    public class AddSkipFilter : Migration
    {
        public override void Up()
        {
            Sql( @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'SkipFilterJson' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_JourneyCalculation'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation]
        ADD [SkipFilterJson] NVARCHAR(MAX) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'SkipFilterMatchAll' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_JourneyCalculation'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation]
        ADD [SkipFilterMatchAll] BIT NOT NULL
            CONSTRAINT [DF__JTrack_JC_SkipFilterMatchAll] DEFAULT(1);
END
" );
        }

        public override void Down()
        {
            Sql( @"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'SkipFilterMatchAll' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_JourneyCalculation'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation] DROP CONSTRAINT [DF__JTrack_JC_SkipFilterMatchAll];
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation] DROP COLUMN [SkipFilterMatchAll];
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'SkipFilterJson' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_JourneyCalculation'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation] DROP COLUMN [SkipFilterJson];
END
" );
        }
    }
}
