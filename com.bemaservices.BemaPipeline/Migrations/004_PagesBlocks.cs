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
    [MigrationNumber( 4, "1.12.5" )]
    public class PagesBlocks : PipelineMigration
    {
        /// <summary>
        /// The commands to run to migrate plugin to the specific version
        /// </summary>
        public override void Up()
        {
            // Page: BEMA Pipeline Type List
            RockMigrationHelper.AddPage( "5B6DBC42-8B03-4D15-8D92-AAFA28FD8616", "D65F783D-87A9-4CC9-8110-E83466A0EADB", "BEMA Pipeline", "", "A000C64B-76AF-4043-BC28-2A88253F8562", "fa fa-list-alt" ); // Site:Rock RMS
            RockMigrationHelper.UpdateBlockType( "Pipeline Type List", "Lists the Pipeline Types currently in the system.", "~/Plugins/com_bemaservices/BemaPipeline/BemaPipelineTypeList.ascx", "BEMA Services > Bema Pipeline", "6B36E831-F18C-4993-A8EE-4FC356927CE0" );
            // Add Block to Page: BEMA Pipeline, Site: Rock RMS
            RockMigrationHelper.AddBlock( true, "A000C64B-76AF-4043-BC28-2A88253F8562", "", "6B36E831-F18C-4993-A8EE-4FC356927CE0", "Pipeline Type List", "Main", "", "", 0, "F364AE56-6E9C-43CC-8627-D59CDC82E96E" );
            // Attrib for BlockType: Pipeline Type List:Pipeline Type Detail
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute( "6B36E831-F18C-4993-A8EE-4FC356927CE0", "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108", "Pipeline Type Detail", "PipelineTypeDetail", "Pipeline Type Detail", @"", 0, @"", "CC7C9449-ECC3-411A-9557-E0E74E0264A9" );
            // Attrib for BlockType: Pipeline Type List:core.CustomGridColumnsConfig
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute( "6B36E831-F18C-4993-A8EE-4FC356927CE0", "9C204CD0-1233-41C5-818A-C5DA439445AA", "core.CustomGridColumnsConfig", "core.CustomGridColumnsConfig", "core.CustomGridColumnsConfig", @"", 0, @"", "983707C6-5F98-46BE-A073-4CB64960AA50" );
            // Attrib for BlockType: Pipeline Type List:core.CustomGridEnableStickyHeaders
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute( "6B36E831-F18C-4993-A8EE-4FC356927CE0", "1EDAFDED-DFE6-4334-B019-6EECBA89E05A", "core.CustomGridEnableStickyHeaders", "core.CustomGridEnableStickyHeaders", "core.CustomGridEnableStickyHeaders", @"", 0, @"False", "1001F370-50CC-458B-9860-01AF767DD6DD" );
            // Attrib for BlockType: Pipeline Type List:core.CustomActionsConfigs
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute( "6B36E831-F18C-4993-A8EE-4FC356927CE0", "9C204CD0-1233-41C5-818A-C5DA439445AA", "core.CustomActionsConfigs", "core.CustomActionsConfigs", "core.CustomActionsConfigs", @"", 0, @"", "E878EA2C-E51A-4BC6-A960-0FDFE13DFBEA" );
            // Attrib for BlockType: Pipeline Type List:core.EnableDefaultWorkflowLauncher
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute( "6B36E831-F18C-4993-A8EE-4FC356927CE0", "1EDAFDED-DFE6-4334-B019-6EECBA89E05A", "core.EnableDefaultWorkflowLauncher", "core.EnableDefaultWorkflowLauncher", "core.EnableDefaultWorkflowLauncher", @"", 0, @"True", "78633777-ED03-4BF1-AB0B-8FE1247CDA8F" );
            
            // Attrib Value for Block:Pipeline Type List, Attribute:core.CustomGridEnableStickyHeaders Page: BEMA Pipeline, Site: Rock RMS
            RockMigrationHelper.AddBlockAttributeValue( "F364AE56-6E9C-43CC-8627-D59CDC82E96E", "1001F370-50CC-458B-9860-01AF767DD6DD", @"False" );
            // Attrib Value for Block:Pipeline Type List, Attribute:core.EnableDefaultWorkflowLauncher Page: BEMA Pipeline, Site: Rock RMS
            RockMigrationHelper.AddBlockAttributeValue( "F364AE56-6E9C-43CC-8627-D59CDC82E96E", "78633777-ED03-4BF1-AB0B-8FE1247CDA8F", @"False" );


            // Page: Pipeline Type Detail
            RockMigrationHelper.AddPage( "A000C64B-76AF-4043-BC28-2A88253F8562", "D65F783D-87A9-4CC9-8110-E83466A0EADB", "Pipeline Type", "", "3A512177-75CB-4A3C-87A4-680E417B7D86", "" ); // Site:Rock RMS
            RockMigrationHelper.UpdateBlockType( "Pipeline Type Detail", "Configures the pipeline type that processes pipeline actions", "~/Plugins/com_bemaservices/BemaPipeline/BemaPipelineTypeDetail.ascx", "BEMA Services > Bema Pipeline", "E69668E2-452C-4439-8A8D-9D9D2E60D902" );
            // Add Block to Page: Pipeline Type, Site: Rock RMS
            RockMigrationHelper.AddBlock( true, "3A512177-75CB-4A3C-87A4-680E417B7D86", "", "E69668E2-452C-4439-8A8D-9D9D2E60D902", "Pipeline Type Detail", "Main", "", "", 0, "309F82F0-84E4-4CA0-9FFE-A0FDF0CF4567" );

            // Attrib Value for Block:Pipeline Type List, Attribute:Pipeline Type Detail Page: BEMA Pipeline, Site: Rock RMS
            RockMigrationHelper.AddBlockAttributeValue( "F364AE56-6E9C-43CC-8627-D59CDC82E96E", "CC7C9449-ECC3-411A-9557-E0E74E0264A9", @"3a512177-75cb-4a3c-87a4-680e417b7d86" );
        }

        /// <summary>
        /// The commands to undo a migration from a specific version
        /// </summary>
        public override void Down()
        {

        }
    }
}
