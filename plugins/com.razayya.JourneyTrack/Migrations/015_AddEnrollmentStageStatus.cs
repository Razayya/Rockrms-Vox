using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    /// <summary>
    /// Adds persisted per-stage progress to enrollments: StageStatusJson (a
    /// {stageId: passed} map maintained by the engine on every sync) plus its
    /// modified timestamp. Read surfaces (the personjourneyprogress Lava tag,
    /// Person Profile progress block) serve this stored state instead of
    /// re-running the whole calculation engine on every page render.
    /// </summary>
    [MigrationNumber( 15, "1.15.0" )]
    public class AddEnrollmentStageStatus : Migration
    {
        public override void Up()
        {
            Sql( @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'StageStatusJson' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_JourneyProgramEnrollment'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgramEnrollment]
        ADD [StageStatusJson] NVARCHAR(MAX) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'StageStatusModifiedDateTime' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_JourneyProgramEnrollment'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgramEnrollment]
        ADD [StageStatusModifiedDateTime] DATETIME NULL;
END
" );
        }

        public override void Down()
        {
            Sql( @"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'StageStatusModifiedDateTime' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_JourneyProgramEnrollment'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgramEnrollment] DROP COLUMN [StageStatusModifiedDateTime];
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'StageStatusJson' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_JourneyProgramEnrollment'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgramEnrollment] DROP COLUMN [StageStatusJson];
END
" );
        }
    }
}
