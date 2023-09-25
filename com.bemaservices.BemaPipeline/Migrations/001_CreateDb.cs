using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rock.Plugin;

//using com.bemaservices.BemaPipeline.SystemGuid;
using Rock.Web.Cache;
using Rock.Lava.Blocks;
using System.Security.AccessControl;
using Rock;

namespace com.bemaservices.BemaPipeline.Migrations
{
    [MigrationNumber( 1, "1.12.5" )]
    public class CreateDb : Migration
    {
        /// <summary>
        /// The commands to run to migrate plugin to the specific version
        /// </summary>
        public override void Up()
        {
            // Update Field Types
            RockMigrationHelper.UpdateFieldType( "Bema Pipeline Type", "", "com.bemaservices.BemaPipeline", "com.bemaservices.BemaPipeline.Field.Types.BemaPipelineTypeFieldType", SystemGuid.FieldType.BEMA_PIPELINE_TYPE );
            RockMigrationHelper.UpdateFieldType( "Bema Pipeline", "", "com.bemaservices.BemaPipeline", "com.bemaservices.BemaPipeline.Field.Types.BemaPipelineFieldType", SystemGuid.FieldType.BEMA_PIPELINE );
            RockMigrationHelper.UpdateFieldType( "Bema Pipeline Action", "", "com.bemaservices.BemaPipeline", "com.bemaservices.BemaPipeline.Field.Types.BemaPipelineActionFieldType", SystemGuid.FieldType.BEMA_PIPELINE_ACTION );

            // Update Entity Types
            RockMigrationHelper.UpdateEntityType( "com.bemaservices.BemaPipeline.Model.BemaPipelineType", "Bema Pipeline Type", "com.bemaservices.BemaPipeline.Model.BemaPipelineType, com.bemaservices.BemaPipeline, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", true, true, SystemGuid.EntityType.BEMA_PIPELINE_TYPE);
            RockMigrationHelper.UpdateEntityType( "com.bemaservices.BemaPipeline.Model.BemaPipelineActionType", "Bema Pipeline Action Type", "com.bemaservices.BemaPipeline.Model.BemaPipelineActionType, com.bemaservices.BemaPipeline, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", true, true, SystemGuid.EntityType.BEMA_PIPELINE_ACTION_TYPE );
            RockMigrationHelper.UpdateEntityType( "com.bemaservices.BemaPipeline.Model.BemaPipeline", "Bema Pipeline", "com.bemaservices.BemaPipeline.Model.BemaPipeline, com.bemaservices.BemaPipeline, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", true, true, SystemGuid.EntityType.BEMA_PIPELINE );
            RockMigrationHelper.UpdateEntityType( "com.bemaservices.BemaPipeline.Model.BemaPipelineAction", "Bema Pipeline Action", "com.bemaservices.BemaPipeline.Model.BemaPipelineAction, com.bemaservices.BemaPipeline, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", true, true, SystemGuid.EntityType.BEMA_PIPELINE_ACTION );

            // Create Tables
            Sql( @"
                CREATE TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineType](
                 [Id] [int] IDENTITY(1,1) NOT NULL,
                 [Name] [nvarchar](100) NULL,
                 [Description] [nvarchar](max) NULL,
                 [EntityTypeId] [int] NOT NULL,
                 [IsActive] [bit] NOT NULL,
                 [Guid] [uniqueidentifier] NOT NULL,
                 [CreatedDateTime] [datetime] NULL,
                 [ModifiedDateTime] [datetime] NULL,
                 [CreatedByPersonAliasId] [int] NULL,
                 [ModifiedByPersonAliasId] [int] NULL,
                 [ForeignKey] [nvarchar](50) NULL,
                 [ForeignGuid] [uniqueidentifier] NULL,
                 [ForeignId] [int] NULL,
                ) ON [PRIMARY]

                ALTER TABLE [_com_bemaservices_BemaPipeline_BemaPipelineType] ADD CONSTRAINT PK__com_bemaservices_BemaPipeline_BemaPipelineType_Id PRIMARY KEY CLUSTERED ( Id );

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineType]  WITH CHECK ADD  CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineType_CreatedByPersonAliasId] FOREIGN KEY([CreatedByPersonAliasId])
                REFERENCES [dbo].[PersonAlias] ([Id])
                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineType] CHECK CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineType_CreatedByPersonAliasId]

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineType]  WITH CHECK ADD  CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineType_ModifiedByPersonAliasId] FOREIGN KEY([ModifiedByPersonAliasId])
                REFERENCES [dbo].[PersonAlias] ([Id])
                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineType] CHECK CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineType_ModifiedByPersonAliasId]

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineType]  WITH CHECK ADD  CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineType_EntityType] FOREIGN KEY([EntityTypeId])
                REFERENCES [dbo].[EntityType] ([Id])
                ON DELETE CASCADE

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineType] CHECK CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineType_EntityType]
            " );

            Sql( @"
                CREATE TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineActionType](
                 [Id] [int] IDENTITY(1,1) NOT NULL,
                 [Name] [nvarchar](100) NULL,
                 [ComponentEntityTypeId] [int] NOT NULL,
                 [BemaPipelineTypeId] [int] NOT NULL,
                 [Order] [int] NOT NULL,
                 [IsActive] [bit] NOT NULL,
                 [Guid] [uniqueidentifier] NOT NULL,
                 [CreatedDateTime] [datetime] NULL,
                 [ModifiedDateTime] [datetime] NULL,
                 [CreatedByPersonAliasId] [int] NULL,
                 [ModifiedByPersonAliasId] [int] NULL,
                 [ForeignKey] [nvarchar](50) NULL,
                 [ForeignGuid] [uniqueidentifier] NULL,
                 [ForeignId] [int] NULL,
                ) ON [PRIMARY]

                ALTER TABLE [_com_bemaservices_BemaPipeline_BemaPipelineActionType] ADD CONSTRAINT PK__com_bemaservices_BemaPipeline_BemaPipelineActionType_Id PRIMARY KEY CLUSTERED ( Id );

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineActionType]  WITH CHECK ADD  CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineActionType_CreatedByPersonAliasId] FOREIGN KEY([CreatedByPersonAliasId])
                REFERENCES [dbo].[PersonAlias] ([Id])
                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineActionType] CHECK CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineActionType_CreatedByPersonAliasId]

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineActionType]  WITH CHECK ADD  CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineActionType_ModifiedByPersonAliasId] FOREIGN KEY([ModifiedByPersonAliasId])
                REFERENCES [dbo].[PersonAlias] ([Id])
                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineActionType] CHECK CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineActionType_ModifiedByPersonAliasId]

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineActionType]  WITH CHECK ADD  CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineActionType_BemaPipelineType] FOREIGN KEY([BemaPipelineTypeId])
                REFERENCES [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineType] ([Id])
                ON DELETE CASCADE

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineActionType] CHECK CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineActionType_BemaPipelineType]
            " );

            Sql( @"
                CREATE TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipeline](
                 [Id] [int] IDENTITY(1,1) NOT NULL,
                 [BemaPipelineTypeId] [int] NOT NULL,
                 [EntityId] [int] NOT NULL,
                 [BemaPipelineState] [int] NOT NULL,
                 [ActivatedDateTime] [datetime] NULL,
                 [LastProcessedDateTime] [datetime] NULL,
                 [CompletedDateTime] [datetime] NULL,
                 [AdditionalData] [nvarchar](max) NULL,
                 [IsProcessing] [bit] NOT NULL,
                 [Guid] [uniqueidentifier] NOT NULL,
                 [CreatedDateTime] [datetime] NULL,
                 [ModifiedDateTime] [datetime] NULL,
                 [CreatedByPersonAliasId] [int] NULL,
                 [ModifiedByPersonAliasId] [int] NULL,
                 [ForeignKey] [nvarchar](50) NULL,
                 [ForeignGuid] [uniqueidentifier] NULL,
                 [ForeignId] [int] NULL,
                ) ON [PRIMARY]

                ALTER TABLE [_com_bemaservices_BemaPipeline_BemaPipeline] ADD CONSTRAINT PK__com_bemaservices_BemaPipeline_BemaPipeline_Id PRIMARY KEY CLUSTERED ( Id );

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipeline]  WITH CHECK ADD  CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipeline_CreatedByPersonAliasId] FOREIGN KEY([CreatedByPersonAliasId])
                REFERENCES [dbo].[PersonAlias] ([Id])
                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipeline] CHECK CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipeline_CreatedByPersonAliasId]

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipeline]  WITH CHECK ADD  CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipeline_ModifiedByPersonAliasId] FOREIGN KEY([ModifiedByPersonAliasId])
                REFERENCES [dbo].[PersonAlias] ([Id])
                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipeline] CHECK CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipeline_ModifiedByPersonAliasId]

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipeline]  WITH CHECK ADD  CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipeline_BemaPipelineType] FOREIGN KEY([BemaPipelineTypeId])
                REFERENCES [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineType] ([Id])
                ON DELETE CASCADE

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipeline] CHECK CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipeline_BemaPipelineType]
            " );

            Sql( @"
                CREATE TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineAction](
                 [Id] [int] IDENTITY(1,1) NOT NULL,
                 [BemaPipelineId] [int] NOT NULL,
                 [BemaPipelineActionTypeId] [int] NOT NULL,
                 [BemaPipelineActionState] [int] NOT NULL,
                 [ActivatedDateTime] [datetime] NULL,
                 [LastProcessedDateTime] [datetime] NULL,
                 [CompletedDateTime] [datetime] NULL,
                 [Guid] [uniqueidentifier] NOT NULL,
                 [CreatedDateTime] [datetime] NULL,
                 [ModifiedDateTime] [datetime] NULL,
                 [CreatedByPersonAliasId] [int] NULL,
                 [ModifiedByPersonAliasId] [int] NULL,
                 [ForeignKey] [nvarchar](50) NULL,
                 [ForeignGuid] [uniqueidentifier] NULL,
                 [ForeignId] [int] NULL,
                ) ON [PRIMARY]

                ALTER TABLE [_com_bemaservices_BemaPipeline_BemaPipelineAction] ADD CONSTRAINT PK__com_bemaservices_BemaPipeline_BemaPipelineAction_Id PRIMARY KEY CLUSTERED ( Id );

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineAction]  WITH CHECK ADD  CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineAction_CreatedByPersonAliasId] FOREIGN KEY([CreatedByPersonAliasId])
                REFERENCES [dbo].[PersonAlias] ([Id])
                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineAction] CHECK CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineAction_CreatedByPersonAliasId]

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineAction]  WITH CHECK ADD  CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineAction_ModifiedByPersonAliasId] FOREIGN KEY([ModifiedByPersonAliasId])
                REFERENCES [dbo].[PersonAlias] ([Id])
                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineAction] CHECK CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineAction_ModifiedByPersonAliasId]

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineAction]  WITH CHECK ADD  CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineAction_BemaPipeline] FOREIGN KEY([BemaPipelineId])
                REFERENCES [dbo].[_com_bemaservices_BemaPipeline_BemaPipeline] ([Id])
                ON DELETE CASCADE

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineAction] CHECK CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineAction_BemaPipeline]

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineAction]  WITH CHECK ADD  CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineAction_BemaPipelineActionType] FOREIGN KEY([BemaPipelineActionTypeId])
                REFERENCES [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineActionType] ([Id])

                ALTER TABLE [dbo].[_com_bemaservices_BemaPipeline_BemaPipelineAction] CHECK CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineAction_BemaPipelineActionType]
            " );
        }

        /// <summary>
        /// The commands to undo a migration from a specific version
        /// </summary>
        public override void Down()
        {
        
        }
    }
}
