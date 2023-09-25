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
    [MigrationNumber( 3, "1.12.5" )]
    public class BaseComponentAttributes : PipelineMigration
    {
        /// <summary>
        /// The commands to run to migrate plugin to the specific version
        /// </summary>
        public override void Up()
        {
            RockMigrationHelper.UpdateEntityType( "com.bemaservices.BemaPipeline.BemaPipelineActionTypes.BemaPipelineActionTypeComponent", "Bema Pipeline Action Type Component", "com.bemaservices.BemaPipeline.BemaPipelineActionTypes.BemaPipelineActionTypeComponent, com.bemaservices.BemaPipeline, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", true, true, SystemGuid.EntityType.BEMA_PIPELINE_ACTION_TYPE_COMPONENT );

            PipelineMigrationHelper.UpdateActionTypeAttributeCategory(
                name: BemaPipelineActionTypes.BemaPipelineActionTypeComponent.BaseAttributeCategories.LogicSettings
                , iconCssClass: ""
                , description: ""
                , guid: SystemGuid.Category.BASE_COMPONENT_LOGIC_SETTINGS
                );

            RockMigrationHelper.AddOrUpdateEntityAttribute(
                entityTypeName: "com.bemaservices.BemaPipeline.Model.BemaPipelineActionType"
              , fieldTypeGuid: Rock.SystemGuid.FieldType.LAVA
              , entityTypeQualifierColumn: ""
              , entityTypeQualifierValue: ""
              , name: "Process Logic"
              , abbreviatedName: "Process Logic"
              , description: "Lava that determines whether and how the action is processed. Returns one of four values: Wait, Process, Complete, or Timeout. The default is Wait."
              , order: 0
              , defaultValue: ""
              , guid: SystemGuid.Attribute.BASE_COMPONENT_PROCESS_LOGIC
              , key: BemaPipelineActionTypes.BemaPipelineActionTypeComponent.BaseAttributeKeys.ProcessLogic
              );
            PipelineMigrationHelper.UpdateAttributeCategory(
                attributeGuid: SystemGuid.Attribute.BASE_COMPONENT_PROCESS_LOGIC
                , categoryGuid: SystemGuid.Category.BASE_COMPONENT_LOGIC_SETTINGS );

            RockMigrationHelper.AddOrUpdateEntityAttribute(
                entityTypeName: "com.bemaservices.BemaPipeline.Model.BemaPipelineActionType"
              , fieldTypeGuid: Rock.SystemGuid.FieldType.WORKFLOW_TYPES
              , entityTypeQualifierColumn: ""
              , entityTypeQualifierValue: ""
              , name: "Timeout Workflows"
              , abbreviatedName: "Timeout Workflows"
              , description: "What workflows should be launched if this action times out?"
              , order: 1
              , defaultValue: ""
              , guid: SystemGuid.Attribute.BASE_COMPONENT_TIMEOUT_WORKFLOWS
              , key: BemaPipelineActionTypes.BemaPipelineActionTypeComponent.BaseAttributeKeys.TimeoutWorkflows
              );
            RockMigrationHelper.AddAttributeQualifier(
                attributeGuid: SystemGuid.Attribute.BASE_COMPONENT_TIMEOUT_WORKFLOWS
                ,key: "allowMultiple"
                ,value: "False"
                ,guid: SystemGuid.AttributeQualifier.BASE_COMPONENT_TIMEOUT_WORKFLOWS_ALLOW_MULTIPLE );
            PipelineMigrationHelper.UpdateAttributeCategory(
                attributeGuid: SystemGuid.Attribute.BASE_COMPONENT_TIMEOUT_WORKFLOWS
                , categoryGuid: SystemGuid.Category.BASE_COMPONENT_LOGIC_SETTINGS );


            PipelineMigrationHelper.UpdateActionTypeAttributeCategory(
                name: BemaPipelineActionTypes.BemaPipelineActionTypeComponent.BaseAttributeCategories.VisualSettings
                , iconCssClass: ""
                , description: ""
                , guid: SystemGuid.Category.BASE_COMPONENT_VISUAL_SETTINGS
                );

            RockMigrationHelper.AddOrUpdateEntityAttribute(
                entityTypeName: "com.bemaservices.BemaPipeline.Model.BemaPipelineActionType"
              , fieldTypeGuid: Rock.SystemGuid.FieldType.LAVA
              , entityTypeQualifierColumn: ""
              , entityTypeQualifierValue: ""
              , name: "Display Logic"
              , abbreviatedName: "Display Logic"
              , description: "Lava that determines whether the action is displayed by shortcodes. Returns either True or False. The Default is True."
              , order: 2
              , defaultValue: ""
              , guid: SystemGuid.Attribute.BASE_COMPONENT_DISPLAY_LOGIC
              , key: BemaPipelineActionTypes.BemaPipelineActionTypeComponent.BaseAttributeKeys.DisplayLogic
              );
            PipelineMigrationHelper.UpdateAttributeCategory(
                attributeGuid: SystemGuid.Attribute.BASE_COMPONENT_DISPLAY_LOGIC
                , categoryGuid: SystemGuid.Category.BASE_COMPONENT_VISUAL_SETTINGS );

            RockMigrationHelper.AddOrUpdateEntityAttribute(
                entityTypeName: "com.bemaservices.BemaPipeline.Model.BemaPipelineActionType"
              , fieldTypeGuid: Rock.SystemGuid.FieldType.BOOLEAN
              , entityTypeQualifierColumn: ""
              , entityTypeQualifierValue: ""
              , name: "Allow Manual Override"
              , abbreviatedName: "Allow Manual Override"
              , description: "Are users allowed to bypass this step and mark it complete without processing it?"
              , order: 3
              , defaultValue: "false"
              , guid: SystemGuid.Attribute.BASE_COMPONENT_ALLOW_MANUAL_OVERRIDE
              , key: BemaPipelineActionTypes.BemaPipelineActionTypeComponent.BaseAttributeKeys.AllowManualOverride
              );
            PipelineMigrationHelper.UpdateAttributeCategory(
                attributeGuid: SystemGuid.Attribute.BASE_COMPONENT_ALLOW_MANUAL_OVERRIDE
                , categoryGuid: SystemGuid.Category.BASE_COMPONENT_VISUAL_SETTINGS );

            RockMigrationHelper.AddOrUpdateEntityAttribute(
                entityTypeName: "com.bemaservices.BemaPipeline.Model.BemaPipelineActionType"
              , fieldTypeGuid: Rock.SystemGuid.FieldType.TEXT
              , entityTypeQualifierColumn: ""
              , entityTypeQualifierValue: ""
              , name: "Button Text"
              , abbreviatedName: "Button Text"
              , description: "The text used for the button that opens the Pipeline Entry Block."
              , order: 4
              , defaultValue: "Launch"
              , guid: SystemGuid.Attribute.BASE_COMPONENT_BUTTON_TEXT
              , key: BemaPipelineActionTypes.BemaPipelineActionTypeComponent.BaseAttributeKeys.ButtonText
              );
            PipelineMigrationHelper.UpdateAttributeCategory(
                attributeGuid: SystemGuid.Attribute.BASE_COMPONENT_BUTTON_TEXT
                , categoryGuid: SystemGuid.Category.BASE_COMPONENT_VISUAL_SETTINGS );

            RockMigrationHelper.AddOrUpdateEntityAttribute(
                entityTypeName: "com.bemaservices.BemaPipeline.Model.BemaPipelineActionType"
              , fieldTypeGuid: Rock.SystemGuid.FieldType.TEXT
              , entityTypeQualifierColumn: ""
              , entityTypeQualifierValue: ""
              , name: "Icon Css Class"
              , abbreviatedName: "Icon Css Class"
              , description: "The icon used to represent this action."
              , order: 5
              , defaultValue: "fa fa-gear"
              , guid: SystemGuid.Attribute.BASE_COMPONENT_ICON_CSS_CLASS
              , key: BemaPipelineActionTypes.BemaPipelineActionTypeComponent.BaseAttributeKeys.IconCssClass
              );
            PipelineMigrationHelper.UpdateAttributeCategory(
                attributeGuid: SystemGuid.Attribute.BASE_COMPONENT_ICON_CSS_CLASS
                , categoryGuid: SystemGuid.Category.BASE_COMPONENT_VISUAL_SETTINGS );

            RockMigrationHelper.AddOrUpdateEntityAttribute(
                entityTypeName: "com.bemaservices.BemaPipeline.Model.BemaPipelineActionType"
              , fieldTypeGuid: Rock.SystemGuid.FieldType.LAVA
              , entityTypeQualifierColumn: ""
              , entityTypeQualifierValue: ""
              , name: "Instructions"
              , abbreviatedName: "Instructions"
              , description: ""
              , order: 6
              , defaultValue: ""
              , guid: SystemGuid.Attribute.BASE_COMPONENT_INSTRUCTIONS
              , key: BemaPipelineActionTypes.BemaPipelineActionTypeComponent.BaseAttributeKeys.Instructions
              );
            PipelineMigrationHelper.UpdateAttributeCategory(
                attributeGuid: SystemGuid.Attribute.BASE_COMPONENT_INSTRUCTIONS
                , categoryGuid: SystemGuid.Category.BASE_COMPONENT_VISUAL_SETTINGS );

            RockMigrationHelper.AddOrUpdateEntityAttribute(
                entityTypeName: "com.bemaservices.BemaPipeline.Model.BemaPipelineActionType"
              , fieldTypeGuid: Rock.SystemGuid.FieldType.LAVA
              , entityTypeQualifierColumn: ""
              , entityTypeQualifierValue: ""
              , name: "Display Lava"
              , abbreviatedName: "Display Lava"
              , description: ""
              , order: 7
              , defaultValue: ""
              , guid: SystemGuid.Attribute.BASE_COMPONENT_DISPLAY_LAVA
              , key: BemaPipelineActionTypes.BemaPipelineActionTypeComponent.BaseAttributeKeys.DisplayedLava
              );
            PipelineMigrationHelper.UpdateAttributeCategory(
                attributeGuid: SystemGuid.Attribute.BASE_COMPONENT_DISPLAY_LAVA
                , categoryGuid: SystemGuid.Category.BASE_COMPONENT_VISUAL_SETTINGS );
        }

        /// <summary>
        /// The commands to undo a migration from a specific version
        /// </summary>
        public override void Down()
        {

        }
    }
}
