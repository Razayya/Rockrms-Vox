using Rock.Plugin;

namespace com.razayya.CustomPersonAttributeSyncEngine.Migrations
{
    [MigrationNumber( 6, "1.15.0" )]
    public class RestructurePages : Migration
    {
        // Rock well-known GUIDs
        private const string InstalledPluginsPageGuid = "5B6DBC42-8B03-4D15-8D92-AAFA28FD8616";
        private const string FullWidthLayoutGuid = "D65F783D-87A9-4CC9-8110-E83466A0EADB";
        private const string PageMenuBlockTypeGuid = "CACB9D1A-A820-4587-986A-D66A69EE9948";

        // PageMenu attribute GUIDs (Rock core)
        private const string PageMenuTemplateAttr = "1322186A-862A-4CF1-B349-28ECB67229BA";
        private const string PageMenuLevelsAttr = "6C952052-BC79-41BA-8B88-AB8EA3E99648";
        private const string PageMenuIncludeParamsAttr = "EEE71DDE-C6BC-489B-BAA5-1753E322F183";

        public override void Up()
        {
            // =====================================================
            // 1. Create new parent page under Installed Plugins
            // =====================================================
            RockMigrationHelper.AddPage( true,
                InstalledPluginsPageGuid,
                FullWidthLayoutGuid,
                "Person Attribute Sync Engine",
                "Configure and manage the attribute sync engine.",
                SystemGuid.Page.PLUGIN_ROOT,
                "fa fa-sync" );

            // =====================================================
            // 2. Add PageMenu block for tab navigation
            // =====================================================
            RockMigrationHelper.AddBlock( true,
                SystemGuid.Page.PLUGIN_ROOT,
                "",
                PageMenuBlockTypeGuid,
                "Page Menu",
                "Main", "", "", 0,
                SystemGuid.Block.PAGE_MENU );

            RockMigrationHelper.AddBlockAttributeValue(
                SystemGuid.Block.PAGE_MENU,
                PageMenuTemplateAttr,
                @"{% include '~~/Assets/Lava/PageListAsTabs.lava' %}" );

            RockMigrationHelper.AddBlockAttributeValue(
                SystemGuid.Block.PAGE_MENU,
                PageMenuLevelsAttr,
                @"1" );

            RockMigrationHelper.AddBlockAttributeValue(
                SystemGuid.Block.PAGE_MENU,
                PageMenuIncludeParamsAttr,
                @"True" );

            // =====================================================
            // 3. Create "Configuration" child page (tab)
            // =====================================================
            RockMigrationHelper.AddPage( true,
                SystemGuid.Page.PLUGIN_ROOT,
                FullWidthLayoutGuid,
                "Configuration",
                "Manage calculation groups, sub-groups, and calculations.",
                SystemGuid.Page.CONFIGURATION,
                "fa fa-cog" );

            // Move existing Calculation Group List block to the Configuration page
            Sql( $@"
UPDATE [Block]
SET [PageId] = (SELECT [Id] FROM [Page] WHERE [Guid] = '{SystemGuid.Page.CONFIGURATION}')
WHERE [Guid] = '{SystemGuid.Block.CALCULATION_GROUP_LIST}'
" );

            // Re-parent existing detail pages under Configuration
            Sql( $@"
UPDATE [Page]
SET [ParentPageId] = (SELECT [Id] FROM [Page] WHERE [Guid] = '{SystemGuid.Page.CONFIGURATION}')
WHERE [Guid] = '{SystemGuid.Page.CALCULATION_GROUP_DETAIL}'
" );

            // Update the Group List block's Detail Page attribute to still point correctly
            RockMigrationHelper.AddBlockAttributeValue(
                SystemGuid.Block.CALCULATION_GROUP_LIST,
                SystemGuid.BlockAttribute.GROUP_LIST_DETAIL_PAGE,
                SystemGuid.Page.CALCULATION_GROUP_DETAIL );

            // =====================================================
            // 4. Create "Run History" child page (tab)
            // =====================================================
            RockMigrationHelper.AddPage( true,
                SystemGuid.Page.PLUGIN_ROOT,
                FullWidthLayoutGuid,
                "Run History",
                "View execution history for sync calculations.",
                SystemGuid.Page.RUN_HISTORY,
                "fa fa-history" );

            // Register Run List block type
            RockMigrationHelper.UpdateBlockType(
                "Calculation Run List",
                "Displays the run history for attribute sync calculations.",
                "~/Plugins/com_razayya/CustomPersonAttributeSyncEngine/CalculationRunList.ascx",
                "Razayya > Attribute Sync Engine",
                SystemGuid.BlockType.CALCULATION_RUN_LIST );

            // Add Run List block to Run History page
            RockMigrationHelper.AddBlock( true,
                SystemGuid.Page.RUN_HISTORY,
                "",
                SystemGuid.BlockType.CALCULATION_RUN_LIST,
                "Calculation Run List",
                "Main", "", "", 0,
                SystemGuid.Block.CALCULATION_RUN_LIST );

            // =====================================================
            // 5. Remove the old Power Tools page (now empty)
            // =====================================================
            RockMigrationHelper.DeletePage( SystemGuid.Page.CALCULATION_GROUP_LIST );
        }

        public override void Down()
        {
            // Recreate old Power Tools page
            RockMigrationHelper.AddPage( true,
                "7F1F4130-CB98-473B-9DE1-7A22D6F109BD",
                FullWidthLayoutGuid,
                "Attribute Sync Engine",
                "Configure and manage attribute sync calculation groups.",
                SystemGuid.Page.CALCULATION_GROUP_LIST,
                "fa fa-sync" );

            // Move group list block back
            Sql( $@"
UPDATE [Block]
SET [PageId] = (SELECT [Id] FROM [Page] WHERE [Guid] = '{SystemGuid.Page.CALCULATION_GROUP_LIST}')
WHERE [Guid] = '{SystemGuid.Block.CALCULATION_GROUP_LIST}'
" );

            // Re-parent detail pages back under old list page
            Sql( $@"
UPDATE [Page]
SET [ParentPageId] = (SELECT [Id] FROM [Page] WHERE [Guid] = '{SystemGuid.Page.CALCULATION_GROUP_LIST}')
WHERE [Guid] = '{SystemGuid.Page.CALCULATION_GROUP_DETAIL}'
" );

            // Remove new pages and blocks
            RockMigrationHelper.DeleteBlock( SystemGuid.Block.CALCULATION_RUN_LIST );
            RockMigrationHelper.DeleteBlock( SystemGuid.Block.PAGE_MENU );
            RockMigrationHelper.DeleteBlockType( SystemGuid.BlockType.CALCULATION_RUN_LIST );
            RockMigrationHelper.DeletePage( SystemGuid.Page.RUN_HISTORY );
            RockMigrationHelper.DeletePage( SystemGuid.Page.CONFIGURATION );
            RockMigrationHelper.DeletePage( SystemGuid.Page.PLUGIN_ROOT );
        }
    }
}
