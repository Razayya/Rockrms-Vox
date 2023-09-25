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
using Rock.Data;
using Rock.Model;

namespace com.bemaservices.BemaPipeline.Migrations
{
    [MigrationNumber( 2, "1.12.5" )]
    public class CreateLaunchWorkflowActionType : PipelineMigration
    {
        /// <summary>
        /// The commands to run to migrate plugin to the specific version
        /// </summary>
        public override void Up()
        {

            // Update Entity Types
            RockMigrationHelper.UpdateEntityType( "com.bemaservices.BemaPipeline.BemaPipelineActionTypes.BemaPipelineActionLaunchWorkflow", "Bema Pipeline Launch Workflow Action Type", "com.bemaservices.BemaPipeline.BemaPipelineActionTypes.BemaPipelineActionLaunchWorkflow, com.bemaservices.BemaPipeline, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", true, true, SystemGuid.EntityType.BEMA_PIPELINE_ACTION_LAUNCH_WORKFLOW);

            PipelineMigrationHelper.AddOrUpdatePipelineActionAttribute(
               componentEntityTypeName: "com.bemaservices.BemaPipeline.BemaPipelineActionTypes.BemaPipelineActionLaunchWorkflow"
              , fieldTypeGuid: Rock.SystemGuid.FieldType.WORKFLOW
              , name: "Workflow"
              , abbreviatedName: "Workflow"
              , description: ""
              , order: 0
              , defaultValue: ""
              , guid: SystemGuid.Attribute.LAUNCH_WORKFLOW_COMPONENT_WORKFLOW
              , key: "Workflow"
              );      
        }

        /// <summary>
        /// The commands to undo a migration from a specific version
        /// </summary>
        public override void Down()
        {
        
        }
    }
}
