using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    [MigrationNumber( 4, "1.15.0" )]
    public class PagesAndBlocks : Migration
    {
        // Guids for pages + blocks (stable, owned by this plugin)
        private const string PAGE_JOURNEYTRACK              = "8B1A2C3D-4E5F-6789-ABCD-EF1234567890";
        private const string PAGE_CONFIGURATION             = "8B1A2C3D-4E5F-6789-ABCD-EF1234567891";
        private const string PAGE_RUN_HISTORY               = "8B1A2C3D-4E5F-6789-ABCD-EF1234567892";
        private const string PAGE_JOURNEY_PROGRAM_DETAIL    = "8B1A2C3D-4E5F-6789-ABCD-EF1234567893";
        private const string PAGE_STAGE_DETAIL              = "8B1A2C3D-4E5F-6789-ABCD-EF1234567894";
        private const string PAGE_JOURNEY_CALCULATION_DETAIL = "8B1A2C3D-4E5F-6789-ABCD-EF1234567895";

        private const string BT_JOURNEY_PROGRAM_LIST        = "9C2B3D4E-5F60-7891-ABCD-EF1234567A00";
        private const string BT_JOURNEY_PROGRAM_DETAIL      = "9C2B3D4E-5F60-7891-ABCD-EF1234567A01";
        private const string BT_JOURNEY_PROGRAM_TREE_VIEW   = "9C2B3D4E-5F60-7891-ABCD-EF1234567A02";
        private const string BT_STAGE_DETAIL                = "9C2B3D4E-5F60-7891-ABCD-EF1234567A03";
        private const string BT_JOURNEY_CALCULATION_DETAIL  = "9C2B3D4E-5F60-7891-ABCD-EF1234567A04";
        private const string BT_JOURNEY_CALCULATION_TREE_VIEW = "9C2B3D4E-5F60-7891-ABCD-EF1234567A05";
        private const string BT_JOURNEY_CALCULATION_RUN_LIST = "9C2B3D4E-5F60-7891-ABCD-EF1234567A06";

        // Installed Plugins page Guid (Rock built-in)
        private const string PARENT_INSTALLED_PLUGINS = "5b6dbc42-8b03-4d15-8d92-aafa28fd8616";
        private const string LAYOUT_FULL_WIDTH        = "d65f783d-87a9-4cc9-8110-e83466a0eadb";

        public override void Up()
        {
            // ============================================================
            // Pages
            // ============================================================
            RockMigrationHelper.AddPage( PARENT_INSTALLED_PLUGINS, LAYOUT_FULL_WIDTH, "JourneyTrack",          "Journey programs and calculations.", PAGE_JOURNEYTRACK, "fa fa-route" );
            RockMigrationHelper.AddPage( PAGE_JOURNEYTRACK,        LAYOUT_FULL_WIDTH, "Configuration",         "Configure journey programs, stages and calculations.", PAGE_CONFIGURATION );
            RockMigrationHelper.AddPage( PAGE_JOURNEYTRACK,        LAYOUT_FULL_WIDTH, "Run History",           "Historical runs of journey calculations.", PAGE_RUN_HISTORY );
            RockMigrationHelper.AddPage( PAGE_CONFIGURATION,       LAYOUT_FULL_WIDTH, "Journey Program",       "Detail page for a Journey Program.", PAGE_JOURNEY_PROGRAM_DETAIL );
            RockMigrationHelper.AddPage( PAGE_JOURNEY_PROGRAM_DETAIL, LAYOUT_FULL_WIDTH, "Stage",                "Detail page for a Stage.", PAGE_STAGE_DETAIL );
            RockMigrationHelper.AddPage( PAGE_STAGE_DETAIL,        LAYOUT_FULL_WIDTH, "Journey Calculation",   "Detail page for a Journey Calculation.", PAGE_JOURNEY_CALCULATION_DETAIL );

            // ============================================================
            // Block Types
            // ============================================================
            RockMigrationHelper.UpdateBlockType( "Journey Program List",          "Lists Journey Programs.",                            "~/Plugins/com_razayya/JourneyTrack/JourneyProgramList.ascx",          "Razayya > JourneyTrack", BT_JOURNEY_PROGRAM_LIST );
            RockMigrationHelper.UpdateBlockType( "Journey Program Detail",        "Detail editor for a Journey Program.",               "~/Plugins/com_razayya/JourneyTrack/JourneyProgramDetail.ascx",        "Razayya > JourneyTrack", BT_JOURNEY_PROGRAM_DETAIL );
            RockMigrationHelper.UpdateBlockType( "Journey Program Tree View",     "Sidebar tree of a Journey Program's stages.",        "~/Plugins/com_razayya/JourneyTrack/JourneyProgramTreeView.ascx",      "Razayya > JourneyTrack", BT_JOURNEY_PROGRAM_TREE_VIEW );
            RockMigrationHelper.UpdateBlockType( "Stage Detail",                  "Detail editor for a Stage.",                         "~/Plugins/com_razayya/JourneyTrack/StageDetail.ascx",                 "Razayya > JourneyTrack", BT_STAGE_DETAIL );
            RockMigrationHelper.UpdateBlockType( "Journey Calculation Detail",    "Detail editor for a Journey Calculation.",           "~/Plugins/com_razayya/JourneyTrack/JourneyCalculationDetail.ascx",    "Razayya > JourneyTrack", BT_JOURNEY_CALCULATION_DETAIL );
            RockMigrationHelper.UpdateBlockType( "Journey Calculation Tree View", "Sidebar tree of a Journey Calculation's children.",  "~/Plugins/com_razayya/JourneyTrack/JourneyCalculationTreeView.ascx",  "Razayya > JourneyTrack", BT_JOURNEY_CALCULATION_TREE_VIEW );
            RockMigrationHelper.UpdateBlockType( "Journey Calculation Run List",  "Lists historical Journey Calculation runs.",         "~/Plugins/com_razayya/JourneyTrack/JourneyCalculationRunList.ascx",   "Razayya > JourneyTrack", BT_JOURNEY_CALCULATION_RUN_LIST );

            // ============================================================
            // Block instances
            // ============================================================
            RockMigrationHelper.AddBlock( PAGE_CONFIGURATION,              null, BT_JOURNEY_PROGRAM_LIST,       "Journey Programs",        "Main",    string.Empty, string.Empty, 0, "B1B2B3B4-0001-4000-8000-000000000001" );
            RockMigrationHelper.AddBlock( PAGE_JOURNEY_PROGRAM_DETAIL,     null, BT_JOURNEY_PROGRAM_DETAIL,     "Journey Program Detail",  "Main",    string.Empty, string.Empty, 0, "B1B2B3B4-0001-4000-8000-000000000002" );
            RockMigrationHelper.AddBlock( PAGE_JOURNEY_PROGRAM_DETAIL,     null, BT_JOURNEY_PROGRAM_TREE_VIEW,  "Tree View",               "Sidebar1",string.Empty, string.Empty, 0, "B1B2B3B4-0001-4000-8000-000000000003" );
            RockMigrationHelper.AddBlock( PAGE_STAGE_DETAIL,               null, BT_STAGE_DETAIL,               "Stage Detail",            "Main",    string.Empty, string.Empty, 0, "B1B2B3B4-0001-4000-8000-000000000004" );
            RockMigrationHelper.AddBlock( PAGE_STAGE_DETAIL,               null, BT_JOURNEY_PROGRAM_TREE_VIEW,  "Tree View",               "Sidebar1",string.Empty, string.Empty, 0, "B1B2B3B4-0001-4000-8000-000000000005" );
            RockMigrationHelper.AddBlock( PAGE_JOURNEY_CALCULATION_DETAIL, null, BT_JOURNEY_CALCULATION_DETAIL, "Journey Calculation Detail", "Main", string.Empty, string.Empty, 0, "B1B2B3B4-0001-4000-8000-000000000006" );
            RockMigrationHelper.AddBlock( PAGE_JOURNEY_CALCULATION_DETAIL, null, BT_JOURNEY_PROGRAM_TREE_VIEW,  "Tree View",               "Sidebar1",string.Empty, string.Empty, 0, "B1B2B3B4-0001-4000-8000-000000000007" );
            RockMigrationHelper.AddBlock( PAGE_RUN_HISTORY,                null, BT_JOURNEY_CALCULATION_RUN_LIST, "Run History",          "Main",    string.Empty, string.Empty, 0, "B1B2B3B4-0001-4000-8000-000000000008" );
        }

        public override void Down()
        {
            // Delete blocks first, then pages, then block types.
            for ( int i = 1; i <= 8; i++ )
            {
                RockMigrationHelper.DeleteBlock( $"B1B2B3B4-0001-4000-8000-{i:D12}" );
            }
            RockMigrationHelper.DeleteBlockType( BT_JOURNEY_CALCULATION_RUN_LIST );
            RockMigrationHelper.DeleteBlockType( BT_JOURNEY_CALCULATION_TREE_VIEW );
            RockMigrationHelper.DeleteBlockType( BT_JOURNEY_CALCULATION_DETAIL );
            RockMigrationHelper.DeleteBlockType( BT_STAGE_DETAIL );
            RockMigrationHelper.DeleteBlockType( BT_JOURNEY_PROGRAM_TREE_VIEW );
            RockMigrationHelper.DeleteBlockType( BT_JOURNEY_PROGRAM_DETAIL );
            RockMigrationHelper.DeleteBlockType( BT_JOURNEY_PROGRAM_LIST );

            RockMigrationHelper.DeletePage( PAGE_JOURNEY_CALCULATION_DETAIL );
            RockMigrationHelper.DeletePage( PAGE_STAGE_DETAIL );
            RockMigrationHelper.DeletePage( PAGE_JOURNEY_PROGRAM_DETAIL );
            RockMigrationHelper.DeletePage( PAGE_RUN_HISTORY );
            RockMigrationHelper.DeletePage( PAGE_CONFIGURATION );
            RockMigrationHelper.DeletePage( PAGE_JOURNEYTRACK );
        }
    }
}
