using Rock.Plugin;

namespace com.razayya.CustomPersonAttributeSyncEngine.Migrations
{
    [MigrationNumber( 5, "1.15.0" )]
    public class AddCalculationRun : Migration
    {
        private const string TableName = Constants.TableName.CalculationRun;
        private const string CalculationTable = Constants.TableName.Calculation;

        public override void Up()
        {
            Sql( $@"
CREATE TABLE [dbo].[{TableName}] (
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [CalculationId] [int] NOT NULL,
    [RunDateTime] [datetime] NOT NULL,
    [CompletedDateTime] [datetime] NULL,
    [RunByPersonAliasId] [int] NULL,
    [PopulationCount] [int] NOT NULL DEFAULT(0),
    [MatchedCount] [int] NOT NULL DEFAULT(0),
    [UpdatedCount] [int] NOT NULL DEFAULT(0),
    [SkippedCount] [int] NOT NULL DEFAULT(0),
    [ErrorCount] [int] NOT NULL DEFAULT(0),
    [WasSuccessful] [bit] NOT NULL DEFAULT(1),
    [StatusMessage] [nvarchar](max) NULL,
    [Guid] [uniqueidentifier] NOT NULL DEFAULT(NEWID()),
    [CreatedDateTime] [datetime] NULL,
    [ModifiedDateTime] [datetime] NULL,
    [CreatedByPersonAliasId] [int] NULL,
    [ModifiedByPersonAliasId] [int] NULL,
    [ForeignKey] [nvarchar](50) NULL,
    [ForeignGuid] [uniqueidentifier] NULL,
    [ForeignId] [int] NULL,
    CONSTRAINT [PK_{TableName}] PRIMARY KEY CLUSTERED ([Id] ASC)
);

ALTER TABLE [dbo].[{TableName}] ADD CONSTRAINT [FK_{TableName}_CalculationId]
    FOREIGN KEY ([CalculationId]) REFERENCES [dbo].[{CalculationTable}]([Id]) ON DELETE CASCADE;

ALTER TABLE [dbo].[{TableName}] ADD CONSTRAINT [FK_{TableName}_RunByPersonAliasId]
    FOREIGN KEY ([RunByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]);

ALTER TABLE [dbo].[{TableName}] ADD CONSTRAINT [FK_{TableName}_CreatedByPersonAliasId]
    FOREIGN KEY ([CreatedByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]);

ALTER TABLE [dbo].[{TableName}] ADD CONSTRAINT [FK_{TableName}_ModifiedByPersonAliasId]
    FOREIGN KEY ([ModifiedByPersonAliasId]) REFERENCES [dbo].[PersonAlias]([Id]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_{TableName}_Guid] ON [dbo].[{TableName}] ([Guid]);
CREATE NONCLUSTERED INDEX [IX_{TableName}_CalculationId] ON [dbo].[{TableName}] ([CalculationId]);
CREATE NONCLUSTERED INDEX [IX_{TableName}_RunDateTime] ON [dbo].[{TableName}] ([RunDateTime] DESC);
" );

            // Register entity type
            RockMigrationHelper.UpdateEntityType(
                "com.razayya.CustomPersonAttributeSyncEngine.Model.CalculationRun",
                "Calculation Run",
                "com.razayya.CustomPersonAttributeSyncEngine.Model.CalculationRun, com.razayya.CustomPersonAttributeSyncEngine, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                true, true,
                SystemGuid.EntityType.CALCULATION_RUN );
        }

        public override void Down()
        {
            Sql( $@"DROP TABLE IF EXISTS [dbo].[{TableName}];" );
        }
    }
}
