using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    [MigrationNumber( 8, "1.15.0" )]
    public class AddEnrollment : Migration
    {
        private const string AssemblyName = "com.razayya.JourneyTrack";
        private const string ModelNs      = "com.razayya.JourneyTrack.Model";
        private const string ActionNs     = "com.razayya.JourneyTrack.Workflow.Action";

        public override void Up()
        {
            Sql( @"
-- ============================================================
-- JourneyProgram: add RequiresEnrollment flag (opt-in)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'RequiresEnrollment' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_JourneyProgram'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgram]
        ADD [RequiresEnrollment] BIT NOT NULL
            CONSTRAINT [DF__JTrack_JP_ReqEnroll] DEFAULT(0);
END

-- ============================================================
-- JourneyProgramEnrollment
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'_com_razayya_JourneyTrack_JourneyProgramEnrollment')
BEGIN
    CREATE TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgramEnrollment] (
        [Id]                       INT IDENTITY(1,1) NOT NULL,
        [JourneyProgramId]         INT NOT NULL,
        [PersonAliasId]            INT NOT NULL,
        [EnrolledDateTime]         DATETIME NOT NULL CONSTRAINT [DF__JTrack_JPE_EnrolledDT] DEFAULT(GETDATE()),
        [UnenrolledDateTime]       DATETIME NULL,
        [IsActive]                 BIT NOT NULL CONSTRAINT [DF__JTrack_JPE_IsActive] DEFAULT(1),
        [EnrolledByPersonAliasId]  INT NULL,
        [Source]                   NVARCHAR(100) NULL,
        [Note]                     NVARCHAR(MAX) NULL,
        [CreatedDateTime]          DATETIME NULL,
        [ModifiedDateTime]         DATETIME NULL,
        [CreatedByPersonAliasId]   INT NULL,
        [ModifiedByPersonAliasId]  INT NULL,
        [Guid]                     UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__JTrack_JPE_Guid] DEFAULT(NEWID()),
        [ForeignKey]               NVARCHAR(100) NULL,
        [ForeignGuid]              UNIQUEIDENTIFIER NULL,
        [ForeignId]                INT NULL,
        CONSTRAINT [PK__JTrack_JPE] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ__JTrack_JPE_Guid] UNIQUE NONCLUSTERED ([Guid])
    );

    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgramEnrollment]
        ADD CONSTRAINT [FK__JTrack_JPE_Program]    FOREIGN KEY ([JourneyProgramId]) REFERENCES [dbo].[_com_razayya_JourneyTrack_JourneyProgram]([Id]) ON DELETE CASCADE;
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgramEnrollment]
        ADD CONSTRAINT [FK__JTrack_JPE_PersonAlias] FOREIGN KEY ([PersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]) ON DELETE NO ACTION;
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgramEnrollment]
        ADD CONSTRAINT [FK__JTrack_JPE_EnrolledBy] FOREIGN KEY ([EnrolledByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]) ON DELETE NO ACTION;
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgramEnrollment]
        ADD CONSTRAINT [FK__JTrack_JPE_CreatedBy]  FOREIGN KEY ([CreatedByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]) ON DELETE NO ACTION;
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgramEnrollment]
        ADD CONSTRAINT [FK__JTrack_JPE_ModifiedBy] FOREIGN KEY ([ModifiedByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]) ON DELETE NO ACTION;

    -- Hot path: 'is this person enrolled and active in this program?' + 'list active enrollees for this program'
    CREATE INDEX [IX__JTrack_JPE_ProgramActive] ON [dbo].[_com_razayya_JourneyTrack_JourneyProgramEnrollment] ([JourneyProgramId], [IsActive]) INCLUDE ([PersonAliasId]);
    CREATE INDEX [IX__JTrack_JPE_PersonProgram] ON [dbo].[_com_razayya_JourneyTrack_JourneyProgramEnrollment] ([PersonAliasId], [JourneyProgramId]);
END
" );

            // Register the new Model EntityType + Workflow Action EntityType
            RockMigrationHelper.UpdateEntityType(
                $"{ModelNs}.JourneyProgramEnrollment",
                "Journey Program Enrollment",
                $"{ModelNs}.JourneyProgramEnrollment, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                true, true,
                SystemGuid.EntityType.JOURNEY_PROGRAM_ENROLLMENT );

            RockMigrationHelper.UpdateEntityType(
                $"{ActionNs}.EnrollPersonInJourneyProgram",
                "Enroll Person In Journey Program",
                $"{ActionNs}.EnrollPersonInJourneyProgram, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                false, false,
                SystemGuid.EntityType.ENROLL_PERSON_WORKFLOW_ACTION );
        }

        public override void Down()
        {
            Sql( @"
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'_com_razayya_JourneyTrack_JourneyProgramEnrollment')
    DROP TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgramEnrollment];

IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'RequiresEnrollment' AND Object_ID = Object_ID(N'_com_razayya_JourneyTrack_JourneyProgram'))
BEGIN
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgram] DROP CONSTRAINT [DF__JTrack_JP_ReqEnroll];
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgram] DROP COLUMN [RequiresEnrollment];
END
" );
        }
    }
}
