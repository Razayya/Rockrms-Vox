using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rock.Plugin;

using Rock.Web.Cache;
using Rock.Lava.Blocks;
using System.Security.AccessControl;
using Rock;
using Rock.Data;

namespace com.bemaservices.BemaPipeline.Migrations
{
    public class PipelineMigrationHelper
    {
        private IMigration Migration = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="PipelineMigrationHelper"/> class.
        /// </summary>
        /// <param name="migration">The migration.</param>
        public PipelineMigrationHelper( IMigration migration )
        {
            Migration = migration;
        }

        public void UpdateActionTypeAttributeCategory( string name, string iconCssClass, string description, string guid, int order = 0 )
        {
            UpdateEntityAttributeCategory( "com.bemaservices.BemaPipeline.Model.BemaPipelineActionType", name, iconCssClass, description, guid, order );
        }
        public void UpdateActionAttributeCategory( string name, string iconCssClass, string description, string guid, int order = 0 )
        {
            UpdateEntityAttributeCategory( "com.bemaservices.BemaPipeline.Model.BemaPipelineActionT", name, iconCssClass, description, guid, order );
        }

        public void UpdateAttributeCategory( string attributeGuid, string categoryGuid )
        {
            Migration.Sql( $@"

                DECLARE @AttributeId int
                SET @AttributeId = (SELECT [Id] FROM [Attribute] WHERE [Guid] = '{attributeGuid}')

                DECLARE @CategoryId int
                SET @CategoryId = (SELECT [Id] FROM [Category] WHERE [Guid] = '{categoryGuid}')

                DELETE [AttributeCategory] WHERE [AttributeId] = @AttributeId AND [CategoryId] = @CategoryId
                INSERT INTO [AttributeCategory] ( [AttributeId], [CategoryId] ) VALUES ( @AttributeId, @CategoryId )
            " );
        }

        private void UpdateEntityAttributeCategory( string entityTypeName, string name, string iconCssClass, string description, string guid, int order = 0 )
        {
            Migration.Sql( $@"

                DECLARE @AttributeEntityTypeId int
                SET @AttributeEntityTypeId = (SELECT [Id] FROM [EntityType] WHERE [Guid] = '5997C8D3-8840-4591-99A5-552919F90CBD')

                DECLARE @EntityEntityTypeId int
                SET @EntityEntityTypeId = (SELECT [Id] FROM [EntityType] WHERE [Name] = '{entityTypeName}')

                IF EXISTS (
                    SELECT [Id]
                    FROM [Category]
                    WHERE [Guid] = '{guid}' )
                BEGIN
                    UPDATE [Category] SET
                        [Name] = '{name}',
                        [IconCssClass] = '{iconCssClass}',
                        [Description] = '{description.Replace( "'", "''" )}',
                        [Order] = {order}
                    WHERE [Guid] = '{guid}'
                END
                ELSE
                BEGIN
                    INSERT INTO [Category] ( [IsSystem],[EntityTypeId],[EntityTypeQualifierColumn],[EntityTypeQualifierValue],[Name],[IconCssClass],[Description],[Order],[Guid] )
                    VALUES( 1,@AttributeEntityTypeId,'EntityTypeId',CAST(@EntityEntityTypeId as varchar),'{name}','{iconCssClass}','{description.Replace( "'", "''" )}',{order},'{guid}' )
                END" );
        }

        public void AddOrUpdatePipelineActionAttribute(string componentEntityTypeName, string fieldTypeGuid, string name, string abbreviatedName, string description, int order, string defaultValue, string guid, string key )
        {
            key = key.IsNullOrWhiteSpace() ? name.Replace( " ", string.Empty ) : key;
            string formattedDescription = description.Replace( "'", "''" );

            var migrationSql = $@"
                DECLARE @ModelEntityTypeId INT = (SELECT [Id] FROM [EntityType] WHERE [Name] = 'com.bemaservices.BemaPipeline.Model.BemaPipelineAction')
                DECLARE @ComponentEntityTypeId INT= (SELECT [Id] FROM [EntityType] WHERE [Name] = '{componentEntityTypeName}')
                DECLARE @FieldTypeId INT = (SELECT [Id] FROM [FieldType] WHERE [Guid] = '{fieldTypeGuid}')

                IF EXISTS (
                    SELECT [Id]
                    FROM [Attribute]
                    WHERE [EntityTypeId] = @ModelEntityTypeId
                        AND [EntityTypeQualifierColumn] = 'ComponentEntityTypeId'
                        AND [EntityTypeQualifierValue] = CAST( @ComponentEntityTypeId AS varchar )
                        AND [Key] = '{key}' )
                BEGIN
                    UPDATE [Attribute] SET
                        [Name] = '{name}'
                        , [Description] = '{formattedDescription}'
                        , [Order] = {order}
                        , [DefaultValue] = '{defaultValue}'
                        , [Guid] = '{guid}'
                        , [AbbreviatedName] = '{abbreviatedName}'
                    WHERE [EntityTypeId] = @ModelEntityTypeId
                        AND [EntityTypeQualifierColumn] = 'ComponentEntityTypeId'
                        AND [EntityTypeQualifierValue] = CAST( @ComponentEntityTypeId AS varchar )
                        AND [Key] = '{key}'
                END
                ELSE
                BEGIN
                    INSERT INTO [Attribute] (
                          [IsSystem]
                        , [FieldTypeId]
                        , [EntityTypeId]
                        , [EntityTypeQualifierColumn]
                        , [EntityTypeQualifierValue]
                        , [Key]
                        , [Name]
                        , [Description]
                        , [Order]
                        , [IsGridColumn]
                        , [DefaultValue]
                        , [IsMultiValue]
                        , [IsRequired]
                        , [Guid]
                        , [AbbreviatedName])
                    VALUES(
                          1
                        , @FieldTypeId
                        , @ModelEntityTypeId
                        , 'ComponentEntityTypeId'
                        , CAST( @ComponentEntityTypeId AS varchar )
                        , '{key}'
                        , '{name}'
                        , '{formattedDescription}'
                        , {order}
                        , 0
                        , '{defaultValue}'
                        , 0
                        , 0
                        , '{guid}'
                        , '{abbreviatedName}')
                END";
            Migration.Sql( migrationSql );
        }



    }
}
