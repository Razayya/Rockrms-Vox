using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    [MigrationNumber( 1, "1.15.0" )]
    public class CreateDb : Migration
    {
        public override void Up()
        {
            Sql( @"
-- ============================================================
-- JourneyProgram
-- ============================================================
CREATE TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgram] (
    [Id]                                  INT IDENTITY(1,1) NOT NULL,
    [Name]                                NVARCHAR(200) NOT NULL,
    [Description]                         NVARCHAR(MAX) NULL,
    [IsActive]                            BIT NOT NULL CONSTRAINT [DF__JTrack_JP_IsActive] DEFAULT(1),
    [Order]                               INT NOT NULL CONSTRAINT [DF__JTrack_JP_Order] DEFAULT(0),
    [RecordStatusValueId]                 INT NULL,
    [ConnectionStatusValueId]             INT NULL,
    [CampusId]                            INT NULL,
    [DataViewId]                          INT NULL,
    [LastRunDateTime]                     DATETIME NULL,
    [CompletionTargetPersonAttributeId]   INT NULL,
    [CompletionLogic]                     INT NOT NULL CONSTRAINT [DF__JTrack_JP_CompLogic] DEFAULT(0),
    [OnCompleteSystemCommunicationId]     INT NULL,
    [CreatedDateTime]                     DATETIME NULL,
    [ModifiedDateTime]                    DATETIME NULL,
    [CreatedByPersonAliasId]              INT NULL,
    [ModifiedByPersonAliasId]             INT NULL,
    [Guid]                                UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__JTrack_JP_Guid] DEFAULT(NEWID()),
    [ForeignKey]                          NVARCHAR(100) NULL,
    [ForeignGuid]                         UNIQUEIDENTIFIER NULL,
    [ForeignId]                           INT NULL,
    CONSTRAINT [PK__JTrack_JP] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ__JTrack_JP_Guid] UNIQUE NONCLUSTERED ([Guid])
);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgram] ADD CONSTRAINT [FK__JTrack_JP_RecordStatus]    FOREIGN KEY ([RecordStatusValueId])                ON DELETE NO ACTION REFERENCES [dbo].[DefinedValue]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgram] ADD CONSTRAINT [FK__JTrack_JP_ConnStatus]      FOREIGN KEY ([ConnectionStatusValueId])            ON DELETE NO ACTION REFERENCES [dbo].[DefinedValue]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgram] ADD CONSTRAINT [FK__JTrack_JP_Campus]          FOREIGN KEY ([CampusId])                           ON DELETE NO ACTION REFERENCES [dbo].[Campus]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgram] ADD CONSTRAINT [FK__JTrack_JP_DataView]        FOREIGN KEY ([DataViewId])                         ON DELETE NO ACTION REFERENCES [dbo].[DataView]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgram] ADD CONSTRAINT [FK__JTrack_JP_CompTargetAttr]  FOREIGN KEY ([CompletionTargetPersonAttributeId]) ON DELETE NO ACTION REFERENCES [dbo].[Attribute]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgram] ADD CONSTRAINT [FK__JTrack_JP_OnCompleteSC]    FOREIGN KEY ([OnCompleteSystemCommunicationId])   ON DELETE NO ACTION REFERENCES [dbo].[SystemCommunication]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgram] ADD CONSTRAINT [FK__JTrack_JP_CreatedBy]       FOREIGN KEY ([CreatedByPersonAliasId])             ON DELETE NO ACTION REFERENCES [dbo].[PersonAlias]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyProgram] ADD CONSTRAINT [FK__JTrack_JP_ModifiedBy]      FOREIGN KEY ([ModifiedByPersonAliasId])            ON DELETE NO ACTION REFERENCES [dbo].[PersonAlias]([Id]);

-- ============================================================
-- Stage
-- ============================================================
CREATE TABLE [dbo].[_com_razayya_JourneyTrack_Stage] (
    [Id]                              INT IDENTITY(1,1) NOT NULL,
    [JourneyProgramId]                INT NOT NULL,
    [Name]                            NVARCHAR(200) NOT NULL,
    [Description]                     NVARCHAR(MAX) NULL,
    [IsActive]                        BIT NOT NULL CONSTRAINT [DF__JTrack_St_IsActive] DEFAULT(1),
    [Order]                           INT NOT NULL CONSTRAINT [DF__JTrack_St_Order] DEFAULT(0),
    [PrerequisiteStageIds]            NVARCHAR(500) NULL,
    [AdditionalDataViewId]            INT NULL,
    [OnCompleteSystemCommunicationId] INT NULL,
    [CreatedDateTime]                 DATETIME NULL,
    [ModifiedDateTime]                DATETIME NULL,
    [CreatedByPersonAliasId]          INT NULL,
    [ModifiedByPersonAliasId]         INT NULL,
    [Guid]                            UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__JTrack_St_Guid] DEFAULT(NEWID()),
    [ForeignKey]                      NVARCHAR(100) NULL,
    [ForeignGuid]                     UNIQUEIDENTIFIER NULL,
    [ForeignId]                       INT NULL,
    CONSTRAINT [PK__JTrack_St] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ__JTrack_St_Guid] UNIQUE NONCLUSTERED ([Guid])
);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_Stage] ADD CONSTRAINT [FK__JTrack_St_Program]        FOREIGN KEY ([JourneyProgramId])                  ON DELETE CASCADE  REFERENCES [dbo].[_com_razayya_JourneyTrack_JourneyProgram]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_Stage] ADD CONSTRAINT [FK__JTrack_St_AdditionalDV]   FOREIGN KEY ([AdditionalDataViewId])              ON DELETE NO ACTION REFERENCES [dbo].[DataView]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_Stage] ADD CONSTRAINT [FK__JTrack_St_OnCompleteSC]   FOREIGN KEY ([OnCompleteSystemCommunicationId])   ON DELETE NO ACTION REFERENCES [dbo].[SystemCommunication]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_Stage] ADD CONSTRAINT [FK__JTrack_St_CreatedBy]      FOREIGN KEY ([CreatedByPersonAliasId])             ON DELETE NO ACTION REFERENCES [dbo].[PersonAlias]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_Stage] ADD CONSTRAINT [FK__JTrack_St_ModifiedBy]     FOREIGN KEY ([ModifiedByPersonAliasId])            ON DELETE NO ACTION REFERENCES [dbo].[PersonAlias]([Id]);

-- ============================================================
-- JourneyCalculation
-- ============================================================
CREATE TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation] (
    [Id]                                INT IDENTITY(1,1) NOT NULL,
    [StageId]                           INT NOT NULL,
    [Name]                              NVARCHAR(200) NOT NULL,
    [Description]                       NVARCHAR(MAX) NULL,
    [IsActive]                          BIT NOT NULL CONSTRAINT [DF__JTrack_JC_IsActive] DEFAULT(1),
    [Order]                             INT NOT NULL CONSTRAINT [DF__JTrack_JC_Order] DEFAULT(0),
    [PersonAttributeId]                 INT NULL,
    [CalculationTypeEntityTypeId]       INT NOT NULL,
    [ResultLavaTemplate]                NVARCHAR(MAX) NULL,
    [NoMatchBehavior]                   INT NOT NULL CONSTRAINT [DF__JTrack_JC_NMB] DEFAULT(0),
    [NoMatchLavaTemplate]               NVARCHAR(MAX) NULL,
    [OnMatchSystemCommunicationId]      INT NULL,
    [CreatedDateTime]                   DATETIME NULL,
    [ModifiedDateTime]                  DATETIME NULL,
    [CreatedByPersonAliasId]            INT NULL,
    [ModifiedByPersonAliasId]           INT NULL,
    [Guid]                              UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__JTrack_JC_Guid] DEFAULT(NEWID()),
    [ForeignKey]                        NVARCHAR(100) NULL,
    [ForeignGuid]                       UNIQUEIDENTIFIER NULL,
    [ForeignId]                         INT NULL,
    CONSTRAINT [PK__JTrack_JC] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ__JTrack_JC_Guid] UNIQUE NONCLUSTERED ([Guid])
);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation] ADD CONSTRAINT [FK__JTrack_JC_Stage]          FOREIGN KEY ([StageId])                       ON DELETE CASCADE  REFERENCES [dbo].[_com_razayya_JourneyTrack_Stage]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation] ADD CONSTRAINT [FK__JTrack_JC_PersonAttr]     FOREIGN KEY ([PersonAttributeId])             ON DELETE NO ACTION REFERENCES [dbo].[Attribute]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation] ADD CONSTRAINT [FK__JTrack_JC_CalcTypeET]     FOREIGN KEY ([CalculationTypeEntityTypeId])   ON DELETE NO ACTION REFERENCES [dbo].[EntityType]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation] ADD CONSTRAINT [FK__JTrack_JC_OnMatchSC]      FOREIGN KEY ([OnMatchSystemCommunicationId]) ON DELETE NO ACTION REFERENCES [dbo].[SystemCommunication]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation] ADD CONSTRAINT [FK__JTrack_JC_CreatedBy]      FOREIGN KEY ([CreatedByPersonAliasId])         ON DELETE NO ACTION REFERENCES [dbo].[PersonAlias]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculation] ADD CONSTRAINT [FK__JTrack_JC_ModifiedBy]     FOREIGN KEY ([ModifiedByPersonAliasId])        ON DELETE NO ACTION REFERENCES [dbo].[PersonAlias]([Id]);

-- ============================================================
-- JourneyCalculationRun
-- ============================================================
CREATE TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculationRun] (
    [Id]                       INT IDENTITY(1,1) NOT NULL,
    [JourneyCalculationId]     INT NOT NULL,
    [RunDateTime]              DATETIME NOT NULL,
    [CompletedDateTime]        DATETIME NULL,
    [RunByPersonAliasId]       INT NULL,
    [PopulationCount]          INT NOT NULL CONSTRAINT [DF__JTrack_JCR_PopCnt]   DEFAULT(0),
    [MatchedCount]             INT NOT NULL CONSTRAINT [DF__JTrack_JCR_MatCnt]   DEFAULT(0),
    [UpdatedCount]             INT NOT NULL CONSTRAINT [DF__JTrack_JCR_UpdCnt]   DEFAULT(0),
    [SkippedCount]             INT NOT NULL CONSTRAINT [DF__JTrack_JCR_SkpCnt]   DEFAULT(0),
    [ErrorCount]               INT NOT NULL CONSTRAINT [DF__JTrack_JCR_ErrCnt]   DEFAULT(0),
    [WasSuccessful]            BIT NOT NULL CONSTRAINT [DF__JTrack_JCR_Success]  DEFAULT(1),
    [StatusMessage]            NVARCHAR(MAX) NULL,
    [CreatedDateTime]          DATETIME NULL,
    [ModifiedDateTime]         DATETIME NULL,
    [CreatedByPersonAliasId]   INT NULL,
    [ModifiedByPersonAliasId]  INT NULL,
    [Guid]                     UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__JTrack_JCR_Guid] DEFAULT(NEWID()),
    [ForeignKey]               NVARCHAR(100) NULL,
    [ForeignGuid]              UNIQUEIDENTIFIER NULL,
    [ForeignId]                INT NULL,
    CONSTRAINT [PK__JTrack_JCR] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ__JTrack_JCR_Guid] UNIQUE NONCLUSTERED ([Guid])
);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculationRun] ADD CONSTRAINT [FK__JTrack_JCR_Calc]   FOREIGN KEY ([JourneyCalculationId]) ON DELETE CASCADE  REFERENCES [dbo].[_com_razayya_JourneyTrack_JourneyCalculation]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCalculationRun] ADD CONSTRAINT [FK__JTrack_JCR_RunBy]  FOREIGN KEY ([RunByPersonAliasId])    ON DELETE NO ACTION REFERENCES [dbo].[PersonAlias]([Id]);
CREATE NONCLUSTERED INDEX [IX__JTrack_JCR_RunDateTime] ON [dbo].[_com_razayya_JourneyTrack_JourneyCalculationRun] ([RunDateTime] DESC);

-- ============================================================
-- JourneyCommunicationLog
-- ============================================================
CREATE TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCommunicationLog] (
    [Id]                       INT IDENTITY(1,1) NOT NULL,
    [ContextType]              TINYINT NOT NULL,
    [ContextId]                INT NOT NULL,
    [PersonAliasId]            INT NOT NULL,
    [SystemCommunicationId]    INT NOT NULL,
    [QueuedDateTime]           DATETIME NOT NULL CONSTRAINT [DF__JTrack_JCL_Queued] DEFAULT(GETDATE()),
    [SentDateTime]             DATETIME NULL,
    [CreatedDateTime]          DATETIME NULL,
    [ModifiedDateTime]         DATETIME NULL,
    [CreatedByPersonAliasId]   INT NULL,
    [ModifiedByPersonAliasId]  INT NULL,
    [Guid]                     UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__JTrack_JCL_Guid] DEFAULT(NEWID()),
    [ForeignKey]               NVARCHAR(100) NULL,
    [ForeignGuid]              UNIQUEIDENTIFIER NULL,
    [ForeignId]                INT NULL,
    CONSTRAINT [PK__JTrack_JCL] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ__JTrack_JCL_Guid] UNIQUE NONCLUSTERED ([Guid])
);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCommunicationLog] ADD CONSTRAINT [FK__JTrack_JCL_PersonAlias] FOREIGN KEY ([PersonAliasId])         ON DELETE NO ACTION REFERENCES [dbo].[PersonAlias]([Id]);
ALTER TABLE [dbo].[_com_razayya_JourneyTrack_JourneyCommunicationLog] ADD CONSTRAINT [FK__JTrack_JCL_SystemComm]  FOREIGN KEY ([SystemCommunicationId]) ON DELETE NO ACTION REFERENCES [dbo].[SystemCommunication]([Id]);
-- Filtered index for fast pending-dispatch scan (Optimization O6)
CREATE NONCLUSTERED INDEX [IX__JTrack_JCL_Pending] ON [dbo].[_com_razayya_JourneyTrack_JourneyCommunicationLog] ([QueuedDateTime] ASC) WHERE [SentDateTime] IS NULL;
-- Dedupe-check composite
CREATE NONCLUSTERED INDEX [IX__JTrack_JCL_Context] ON [dbo].[_com_razayya_JourneyTrack_JourneyCommunicationLog] ([ContextType], [ContextId], [PersonAliasId]);
" );
        }

        public override void Down()
        {
            Sql( @"
IF OBJECT_ID('dbo._com_razayya_JourneyTrack_JourneyCommunicationLog','U') IS NOT NULL DROP TABLE dbo._com_razayya_JourneyTrack_JourneyCommunicationLog;
IF OBJECT_ID('dbo._com_razayya_JourneyTrack_JourneyCalculationRun','U')   IS NOT NULL DROP TABLE dbo._com_razayya_JourneyTrack_JourneyCalculationRun;
IF OBJECT_ID('dbo._com_razayya_JourneyTrack_JourneyCalculation','U')      IS NOT NULL DROP TABLE dbo._com_razayya_JourneyTrack_JourneyCalculation;
IF OBJECT_ID('dbo._com_razayya_JourneyTrack_Stage','U')                   IS NOT NULL DROP TABLE dbo._com_razayya_JourneyTrack_Stage;
IF OBJECT_ID('dbo._com_razayya_JourneyTrack_JourneyProgram','U')          IS NOT NULL DROP TABLE dbo._com_razayya_JourneyTrack_JourneyProgram;
" );
        }
    }
}
