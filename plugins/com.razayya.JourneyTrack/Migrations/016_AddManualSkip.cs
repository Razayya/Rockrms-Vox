using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    /// <summary>
    /// Adds per-person manual skips (7038): a JourneyCalculationSkip row marks one
    /// person as manually skipped on one calc. The engine folds active skips into the
    /// calc's matched set exactly like a "Skip If" filter match — passes for stage
    /// completion and downstream gating, sink left blank — and CompletionCalculation
    /// treats a skipped sibling criterion as satisfied. Restore = IsActive 0 +
    /// RemovedDateTime/RemovedBy (audit preserved).
    /// </summary>
    [MigrationNumber( 16, "1.15.0" )]
    public class AddManualSkip : Migration
    {
        private const string AssemblyName = "com.razayya.JourneyTrack";
        private const string ModelNs      = "com.razayya.JourneyTrack.Model";

        public override void Up()
        {
            Sql( @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'_com_razayya_JourneyTrack_JourneyCalculationSkip')
BEGIN
    CREATE TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculationSkip] (
        [Id]                       INT IDENTITY(1,1) NOT NULL,
        [JourneyCalculationId]     INT NOT NULL,
        [PersonAliasId]            INT NOT NULL,
        [IsActive]                 BIT NOT NULL CONSTRAINT [DF__JTrack_JCS_IsActive] DEFAULT(1),
        [Note]                     NVARCHAR(MAX) NULL,
        [RemovedDateTime]          DATETIME NULL,
        [RemovedByPersonAliasId]   INT NULL,
        [CreatedDateTime]          DATETIME NULL,
        [ModifiedDateTime]         DATETIME NULL,
        [CreatedByPersonAliasId]   INT NULL,
        [ModifiedByPersonAliasId]  INT NULL,
        [Guid]                     UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__JTrack_JCS_Guid] DEFAULT(NEWID()),
        [ForeignKey]               NVARCHAR(100) NULL,
        [ForeignGuid]              UNIQUEIDENTIFIER NULL,
        [ForeignId]                INT NULL,
        CONSTRAINT [PK__JTrack_JCS] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ__JTrack_JCS_Guid] UNIQUE NONCLUSTERED ([Guid])
    );

    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculationSkip]
        ADD CONSTRAINT [FK__JTrack_JCS_Calculation] FOREIGN KEY ([JourneyCalculationId]) REFERENCES [dbo].[_com_razayya_JourneyTrack_JourneyCalculation]([Id]) ON DELETE CASCADE;
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculationSkip]
        ADD CONSTRAINT [FK__JTrack_JCS_PersonAlias] FOREIGN KEY ([PersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]) ON DELETE NO ACTION;
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculationSkip]
        ADD CONSTRAINT [FK__JTrack_JCS_RemovedBy]  FOREIGN KEY ([RemovedByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]) ON DELETE NO ACTION;
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculationSkip]
        ADD CONSTRAINT [FK__JTrack_JCS_CreatedBy]  FOREIGN KEY ([CreatedByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]) ON DELETE NO ACTION;
    ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculationSkip]
        ADD CONSTRAINT [FK__JTrack_JCS_ModifiedBy] FOREIGN KEY ([ModifiedByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]) ON DELETE NO ACTION;

    -- Hot path: engine per-calc lookup of active skips during ExecuteCalculation
    CREATE INDEX [IX__JTrack_JCS_CalcActive] ON [dbo].[_com_razayya_JourneyTrack_JourneyCalculationSkip] ([JourneyCalculationId], [IsActive]) INCLUDE ([PersonAliasId]);
    -- Profile block: all skips for one person across a program's calcs
    CREATE INDEX [IX__JTrack_JCS_PersonCalc] ON [dbo].[_com_razayya_JourneyTrack_JourneyCalculationSkip] ([PersonAliasId], [JourneyCalculationId]);
END
" );

            RockMigrationHelper.UpdateEntityType(
                $"{ModelNs}.JourneyCalculationSkip",
                "Journey Calculation Skip",
                $"{ModelNs}.JourneyCalculationSkip, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                true, true,
                SystemGuid.EntityType.JOURNEY_CALCULATION_SKIP );
        }

        public override void Down()
        {
            Sql( @"
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'_com_razayya_JourneyTrack_JourneyCalculationSkip')
    DROP TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculationSkip];
" );
        }
    }
}
