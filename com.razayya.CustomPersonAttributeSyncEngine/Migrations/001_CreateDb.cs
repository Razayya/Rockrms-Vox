using Rock.Plugin;

namespace com.razayya.CustomPersonAttributeSyncEngine.Migrations
{
    [MigrationNumber( 1, "1.15.0" )]
    public class CreateDb : Migration
    {
        private const string CalculationGroupTable = Constants.TableName.CalculationGroup;
        private const string CalculationSubGroupTable = Constants.TableName.CalculationSubGroup;
        private const string CalculationTable = Constants.TableName.Calculation;

        public override void Up()
        {
            // CalculationGroup
            Sql( $@"
CREATE TABLE [dbo].[{CalculationGroupTable}] (
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [Name] [nvarchar](200) NOT NULL,
    [Description] [nvarchar](max) NULL,
    [IsActive] [bit] NOT NULL DEFAULT(1),
    [Order] [int] NOT NULL DEFAULT(0),
    [RecordStatusValueId] [int] NULL,
    [ConnectionStatusValueId] [int] NULL,
    [CampusId] [int] NULL,
    [DataViewId] [int] NULL,
    [LastRunDateTime] [datetime] NULL,
    [Guid] [uniqueidentifier] NOT NULL DEFAULT(NEWID()),
    [CreatedDateTime] [datetime] NULL,
    [ModifiedDateTime] [datetime] NULL,
    [CreatedByPersonAliasId] [int] NULL,
    [ModifiedByPersonAliasId] [int] NULL,
    [ForeignKey] [nvarchar](50) NULL,
    [ForeignGuid] [uniqueidentifier] NULL,
    [ForeignId] [int] NULL,
    CONSTRAINT [PK_{CalculationGroupTable}] PRIMARY KEY CLUSTERED ([Id] ASC)
);

ALTER TABLE [dbo].[{CalculationGroupTable}] ADD CONSTRAINT [FK_{CalculationGroupTable}_RecordStatusValueId]
    FOREIGN KEY ([RecordStatusValueId]) REFERENCES [dbo].[DefinedValue]([Id]);

ALTER TABLE [dbo].[{CalculationGroupTable}] ADD CONSTRAINT [FK_{CalculationGroupTable}_ConnectionStatusValueId]
    FOREIGN KEY ([ConnectionStatusValueId]) REFERENCES [dbo].[DefinedValue]([Id]);

ALTER TABLE [dbo].[{CalculationGroupTable}] ADD CONSTRAINT [FK_{CalculationGroupTable}_CampusId]
    FOREIGN KEY ([CampusId]) REFERENCES [dbo].[Campus]([Id]);

ALTER TABLE [dbo].[{CalculationGroupTable}] ADD CONSTRAINT [FK_{CalculationGroupTable}_DataViewId]
    FOREIGN KEY ([DataViewId]) REFERENCES [dbo].[DataView]([Id]);

ALTER TABLE [dbo].[{CalculationGroupTable}] ADD CONSTRAINT [FK_{CalculationGroupTable}_CreatedByPersonAliasId]
    FOREIGN KEY ([CreatedByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]);

ALTER TABLE [dbo].[{CalculationGroupTable}] ADD CONSTRAINT [FK_{CalculationGroupTable}_ModifiedByPersonAliasId]
    FOREIGN KEY ([ModifiedByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_{CalculationGroupTable}_Guid] ON [dbo].[{CalculationGroupTable}] ([Guid]);
" );

            // CalculationSubGroup
            Sql( $@"
CREATE TABLE [dbo].[{CalculationSubGroupTable}] (
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [CalculationGroupId] [int] NOT NULL,
    [Name] [nvarchar](200) NOT NULL,
    [Description] [nvarchar](max) NULL,
    [IsActive] [bit] NOT NULL DEFAULT(1),
    [Order] [int] NOT NULL DEFAULT(0),
    [ScopeToPreviousSubGroup] [bit] NOT NULL DEFAULT(1),
    [AdditionalDataViewId] [int] NULL,
    [Guid] [uniqueidentifier] NOT NULL DEFAULT(NEWID()),
    [CreatedDateTime] [datetime] NULL,
    [ModifiedDateTime] [datetime] NULL,
    [CreatedByPersonAliasId] [int] NULL,
    [ModifiedByPersonAliasId] [int] NULL,
    [ForeignKey] [nvarchar](50) NULL,
    [ForeignGuid] [uniqueidentifier] NULL,
    [ForeignId] [int] NULL,
    CONSTRAINT [PK_{CalculationSubGroupTable}] PRIMARY KEY CLUSTERED ([Id] ASC)
);

ALTER TABLE [dbo].[{CalculationSubGroupTable}] ADD CONSTRAINT [FK_{CalculationSubGroupTable}_CalculationGroupId]
    FOREIGN KEY ([CalculationGroupId]) REFERENCES [dbo].[{CalculationGroupTable}]([Id]) ON DELETE CASCADE;

ALTER TABLE [dbo].[{CalculationSubGroupTable}] ADD CONSTRAINT [FK_{CalculationSubGroupTable}_AdditionalDataViewId]
    FOREIGN KEY ([AdditionalDataViewId]) REFERENCES [dbo].[DataView]([Id]);

ALTER TABLE [dbo].[{CalculationSubGroupTable}] ADD CONSTRAINT [FK_{CalculationSubGroupTable}_CreatedByPersonAliasId]
    FOREIGN KEY ([CreatedByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]);

ALTER TABLE [dbo].[{CalculationSubGroupTable}] ADD CONSTRAINT [FK_{CalculationSubGroupTable}_ModifiedByPersonAliasId]
    FOREIGN KEY ([ModifiedByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_{CalculationSubGroupTable}_Guid] ON [dbo].[{CalculationSubGroupTable}] ([Guid]);
CREATE NONCLUSTERED INDEX [IX_{CalculationSubGroupTable}_CalculationGroupId] ON [dbo].[{CalculationSubGroupTable}] ([CalculationGroupId]);
" );

            // Calculation
            Sql( $@"
CREATE TABLE [dbo].[{CalculationTable}] (
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [CalculationSubGroupId] [int] NOT NULL,
    [Name] [nvarchar](200) NOT NULL,
    [Description] [nvarchar](max) NULL,
    [IsActive] [bit] NOT NULL DEFAULT(1),
    [Order] [int] NOT NULL DEFAULT(0),
    [PersonAttributeId] [int] NOT NULL,
    [CalculationTypeEntityTypeId] [int] NOT NULL,
    [ResultLavaTemplate] [nvarchar](max) NULL,
    [NoMatchBehavior] [int] NOT NULL DEFAULT(0),
    [NoMatchLavaTemplate] [nvarchar](max) NULL,
    [Guid] [uniqueidentifier] NOT NULL DEFAULT(NEWID()),
    [CreatedDateTime] [datetime] NULL,
    [ModifiedDateTime] [datetime] NULL,
    [CreatedByPersonAliasId] [int] NULL,
    [ModifiedByPersonAliasId] [int] NULL,
    [ForeignKey] [nvarchar](50) NULL,
    [ForeignGuid] [uniqueidentifier] NULL,
    [ForeignId] [int] NULL,
    CONSTRAINT [PK_{CalculationTable}] PRIMARY KEY CLUSTERED ([Id] ASC)
);

ALTER TABLE [dbo].[{CalculationTable}] ADD CONSTRAINT [FK_{CalculationTable}_CalculationSubGroupId]
    FOREIGN KEY ([CalculationSubGroupId]) REFERENCES [dbo].[{CalculationSubGroupTable}]([Id]) ON DELETE CASCADE;

ALTER TABLE [dbo].[{CalculationTable}] ADD CONSTRAINT [FK_{CalculationTable}_PersonAttributeId]
    FOREIGN KEY ([PersonAttributeId]) REFERENCES [dbo].[Attribute]([Id]);

ALTER TABLE [dbo].[{CalculationTable}] ADD CONSTRAINT [FK_{CalculationTable}_CalculationTypeEntityTypeId]
    FOREIGN KEY ([CalculationTypeEntityTypeId]) REFERENCES [dbo].[EntityType]([Id]);

ALTER TABLE [dbo].[{CalculationTable}] ADD CONSTRAINT [FK_{CalculationTable}_CreatedByPersonAliasId]
    FOREIGN KEY ([CreatedByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]);

ALTER TABLE [dbo].[{CalculationTable}] ADD CONSTRAINT [FK_{CalculationTable}_ModifiedByPersonAliasId]
    FOREIGN KEY ([ModifiedByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_{CalculationTable}_Guid] ON [dbo].[{CalculationTable}] ([Guid]);
CREATE NONCLUSTERED INDEX [IX_{CalculationTable}_CalculationSubGroupId] ON [dbo].[{CalculationTable}] ([CalculationSubGroupId]);
CREATE NONCLUSTERED INDEX [IX_{CalculationTable}_PersonAttributeId] ON [dbo].[{CalculationTable}] ([PersonAttributeId]);
" );
        }

        public override void Down()
        {
            Sql( $@"
DROP TABLE IF EXISTS [dbo].[{CalculationTable}];
DROP TABLE IF EXISTS [dbo].[{CalculationSubGroupTable}];
DROP TABLE IF EXISTS [dbo].[{CalculationGroupTable}];
" );
        }
    }
}
