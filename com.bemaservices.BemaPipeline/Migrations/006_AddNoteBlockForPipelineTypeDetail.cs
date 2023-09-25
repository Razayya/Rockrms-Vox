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
    [MigrationNumber( 6, "1.12.5" )]
    public class AddNoteBlockForPipelineTypeDetail : PipelineMigration
    {
        /// <summary>
        /// The commands to run to migrate plugin to the specific version
        /// </summary>
        public override void Up()
        {
            RockMigrationHelper.UpdateNoteType( "Pipeline Type Change Log", "com.bemaservices.BemaPipeline.Model.BemaPipelineType", true, "7A03E551-B5DB-4D3B-80A1-74F9F1395D5B" );

            // Add Block
            RockMigrationHelper.AddBlock( true, "3A512177-75CB-4A3C-87A4-680E417B7D86", "", Rock.SystemGuid.BlockType.NOTES, "Notes", "Main", "", "", 0, "86AE0567-71A2-4D06-B74A-4D81FC23DA68" );

            //Set Note Type
            RockMigrationHelper.AddBlockAttributeValue( "86AE0567-71A2-4D06-B74A-4D81FC23DA68", "CB89C2A5-49DB-4108-B924-6C610CEDFBF4", "7A03E551-B5DB-4D3B-80A1-74F9F1395D5B" );
            //Set Context Entity Type
            RockMigrationHelper.AddBlockAttributeValue( "86AE0567-71A2-4D06-B74A-4D81FC23DA68", "F1BCF615-FBCA-4BC2-A912-C35C0DC04174", SystemGuid.EntityType.BEMA_PIPELINE_TYPE );
            //Set Heading
            RockMigrationHelper.AddBlockAttributeValue( "86AE0567-71A2-4D06-B74A-4D81FC23DA68", "3CB0A7DF-996B-4D6C-B3B6-9BBCC40BDC69", "Change Log" );
            //Set Heading Icon
            RockMigrationHelper.AddBlockAttributeValue( "86AE0567-71A2-4D06-B74A-4D81FC23DA68", "B69937BE-000A-4B94-852F-16DE92344392", "fa fa-sticky-note-o" );
            //Set User Person Icon
            RockMigrationHelper.AddBlockAttributeValue( "86AE0567-71A2-4D06-B74A-4D81FC23DA68", "C05757C0-E83E-4170-8CBF-C4E1ABEC36E1", "True" );
            //Set Note View Lava Template
            RockMigrationHelper.AddBlockAttributeValue( "86AE0567-71A2-4D06-B74A-4D81FC23DA68", "328DDE3F-6FFF-4CA4-B6D0-C1BD4D643307", "{% include '~~/Assets/Lava/NoteViewList.lava' %}" );

            // Add Page Context Parameter
            RockMigrationHelper.UpdatePageContext( "3A512177-75CB-4A3C-87A4-680E417B7D86", "com.bemaservices.BemaPipeline.Model.BemaPipelineType", "PipelineTypeId", "D7141AF7-30AA-453B-838B-2E54DA69592C" );

        }

        /// <summary>
        /// The commands to undo a migration from a specific version
        /// </summary>
        public override void Down()
        {

        }
    }
}
