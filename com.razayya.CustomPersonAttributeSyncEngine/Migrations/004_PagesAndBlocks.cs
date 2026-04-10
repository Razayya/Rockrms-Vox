using Rock.Plugin;

namespace com.razayya.CustomPersonAttributeSyncEngine.Migrations
{
    [MigrationNumber( 4, "1.15.0" )]
    public class PagesAndBlocks : Migration
    {
        // Rock's "Full Width" layout
        private const string LayoutGuid = "D65F783D-87A9-4CC9-8110-E83466A0EADB";

        // Rock's "Power Tools" parent page (under Admin Tools)
        private const string PowerToolsPageGuid = "7F1F4130-CB98-473B-9DE1-7A22D6F109BD";

        public override void Up()
        {
            // =====================================================
            // 1. Pages
            // =====================================================

            // Calculation Group List (top-level page under Power Tools)
            RockMigrationHelper.AddPage( true,
                PowerToolsPageGuid,
                LayoutGuid,
                "Attribute Sync Engine",
                "Configure and manage attribute sync calculation groups.",
                SystemGuid.Page.CALCULATION_GROUP_LIST,
                "fa fa-sync" );

            // Calculation Group Detail (child of List)
            RockMigrationHelper.AddPage( true,
                SystemGuid.Page.CALCULATION_GROUP_LIST,
                LayoutGuid,
                "Calculation Group",
                "",
                SystemGuid.Page.CALCULATION_GROUP_DETAIL,
                "fa fa-sync" );

            // Calculation Sub Group Detail (child of Group Detail)
            RockMigrationHelper.AddPage( true,
                SystemGuid.Page.CALCULATION_GROUP_DETAIL,
                LayoutGuid,
                "Calculation Sub Group",
                "",
                SystemGuid.Page.CALCULATION_SUB_GROUP_DETAIL,
                "fa fa-layer-group" );

            // Calculation Detail (child of Sub Group Detail)
            RockMigrationHelper.AddPage( true,
                SystemGuid.Page.CALCULATION_SUB_GROUP_DETAIL,
                LayoutGuid,
                "Calculation",
                "",
                SystemGuid.Page.CALCULATION_DETAIL,
                "fa fa-calculator" );

            // =====================================================
            // 2. Block Types
            // =====================================================

            RockMigrationHelper.UpdateBlockType(
                "Calculation Group List",
                "Lists Calculation Groups for the Attribute Sync Engine.",
                "~/Plugins/com_razayya/CustomPersonAttributeSyncEngine/CalculationGroupList.ascx",
                "Razayya > Attribute Sync Engine",
                SystemGuid.BlockType.CALCULATION_GROUP_LIST );

            RockMigrationHelper.UpdateBlockType(
                "Calculation Group Detail",
                "Displays details for a Calculation Group and its Sub Groups.",
                "~/Plugins/com_razayya/CustomPersonAttributeSyncEngine/CalculationGroupDetail.ascx",
                "Razayya > Attribute Sync Engine",
                SystemGuid.BlockType.CALCULATION_GROUP_DETAIL );

            RockMigrationHelper.UpdateBlockType(
                "Calculation Sub Group Detail",
                "Displays details for a Calculation Sub Group and its Calculations.",
                "~/Plugins/com_razayya/CustomPersonAttributeSyncEngine/CalculationSubGroupDetail.ascx",
                "Razayya > Attribute Sync Engine",
                SystemGuid.BlockType.CALCULATION_SUB_GROUP_DETAIL );

            RockMigrationHelper.UpdateBlockType(
                "Calculation Detail",
                "Displays details for a single Calculation with component-specific configuration.",
                "~/Plugins/com_razayya/CustomPersonAttributeSyncEngine/CalculationDetail.ascx",
                "Razayya > Attribute Sync Engine",
                SystemGuid.BlockType.CALCULATION_DETAIL );

            // =====================================================
            // 3. Block Instances on Pages
            // =====================================================

            // Group List block on the list page
            RockMigrationHelper.AddBlock( true,
                SystemGuid.Page.CALCULATION_GROUP_LIST,
                "",
                SystemGuid.BlockType.CALCULATION_GROUP_LIST,
                "Calculation Group List",
                "Main", "", "", 0,
                SystemGuid.Block.CALCULATION_GROUP_LIST );

            // Group Detail block on the detail page
            RockMigrationHelper.AddBlock( true,
                SystemGuid.Page.CALCULATION_GROUP_DETAIL,
                "",
                SystemGuid.BlockType.CALCULATION_GROUP_DETAIL,
                "Calculation Group Detail",
                "Main", "", "", 0,
                SystemGuid.Block.CALCULATION_GROUP_DETAIL );

            // Sub Group Detail block
            RockMigrationHelper.AddBlock( true,
                SystemGuid.Page.CALCULATION_SUB_GROUP_DETAIL,
                "",
                SystemGuid.BlockType.CALCULATION_SUB_GROUP_DETAIL,
                "Calculation Sub Group Detail",
                "Main", "", "", 0,
                SystemGuid.Block.CALCULATION_SUB_GROUP_DETAIL );

            // Calculation Detail block
            RockMigrationHelper.AddBlock( true,
                SystemGuid.Page.CALCULATION_DETAIL,
                "",
                SystemGuid.BlockType.CALCULATION_DETAIL,
                "Calculation Detail",
                "Main", "", "", 0,
                SystemGuid.Block.CALCULATION_DETAIL );

            // =====================================================
            // 4. LinkedPage Attributes & Values
            // =====================================================

            // Group List -> Detail Page
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute(
                SystemGuid.BlockType.CALCULATION_GROUP_LIST,
                "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108", // Page Reference field type
                "Detail Page", "DetailPage", "", @"", 0, @"",
                SystemGuid.BlockAttribute.GROUP_LIST_DETAIL_PAGE );

            RockMigrationHelper.AddBlockAttributeValue(
                SystemGuid.Block.CALCULATION_GROUP_LIST,
                SystemGuid.BlockAttribute.GROUP_LIST_DETAIL_PAGE,
                SystemGuid.Page.CALCULATION_GROUP_DETAIL );

            // Group Detail -> Sub Group Detail Page
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute(
                SystemGuid.BlockType.CALCULATION_GROUP_DETAIL,
                "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108",
                "Sub Group Detail Page", "SubGroupDetailPage", "", @"", 0, @"",
                SystemGuid.BlockAttribute.GROUP_DETAIL_SUBGROUP_PAGE );

            RockMigrationHelper.AddBlockAttributeValue(
                SystemGuid.Block.CALCULATION_GROUP_DETAIL,
                SystemGuid.BlockAttribute.GROUP_DETAIL_SUBGROUP_PAGE,
                SystemGuid.Page.CALCULATION_SUB_GROUP_DETAIL );

            // Sub Group Detail -> Calculation Detail Page
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute(
                SystemGuid.BlockType.CALCULATION_SUB_GROUP_DETAIL,
                "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108",
                "Calculation Detail Page", "CalculationDetailPage", "", @"", 0, @"",
                SystemGuid.BlockAttribute.SUBGROUP_DETAIL_CALC_PAGE );

            RockMigrationHelper.AddBlockAttributeValue(
                SystemGuid.Block.CALCULATION_SUB_GROUP_DETAIL,
                SystemGuid.BlockAttribute.SUBGROUP_DETAIL_CALC_PAGE,
                SystemGuid.Page.CALCULATION_DETAIL );

            // Sub Group Detail -> Parent (Group Detail) Page
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute(
                SystemGuid.BlockType.CALCULATION_SUB_GROUP_DETAIL,
                "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108",
                "Parent Page", "ParentPage", "", @"", 1, @"",
                SystemGuid.BlockAttribute.SUBGROUP_DETAIL_PARENT_PAGE );

            RockMigrationHelper.AddBlockAttributeValue(
                SystemGuid.Block.CALCULATION_SUB_GROUP_DETAIL,
                SystemGuid.BlockAttribute.SUBGROUP_DETAIL_PARENT_PAGE,
                SystemGuid.Page.CALCULATION_GROUP_DETAIL );

            // Calculation Detail -> Parent (Sub Group Detail) Page
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute(
                SystemGuid.BlockType.CALCULATION_DETAIL,
                "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108",
                "Parent Page", "ParentPage", "", @"", 0, @"",
                SystemGuid.BlockAttribute.CALC_DETAIL_PARENT_PAGE );

            RockMigrationHelper.AddBlockAttributeValue(
                SystemGuid.Block.CALCULATION_DETAIL,
                SystemGuid.BlockAttribute.CALC_DETAIL_PARENT_PAGE,
                SystemGuid.Page.CALCULATION_SUB_GROUP_DETAIL );
        }

        public override void Down()
        {
            // Remove blocks
            RockMigrationHelper.DeleteBlock( SystemGuid.Block.CALCULATION_DETAIL );
            RockMigrationHelper.DeleteBlock( SystemGuid.Block.CALCULATION_SUB_GROUP_DETAIL );
            RockMigrationHelper.DeleteBlock( SystemGuid.Block.CALCULATION_GROUP_DETAIL );
            RockMigrationHelper.DeleteBlock( SystemGuid.Block.CALCULATION_GROUP_LIST );

            // Remove block types
            RockMigrationHelper.DeleteBlockType( SystemGuid.BlockType.CALCULATION_DETAIL );
            RockMigrationHelper.DeleteBlockType( SystemGuid.BlockType.CALCULATION_SUB_GROUP_DETAIL );
            RockMigrationHelper.DeleteBlockType( SystemGuid.BlockType.CALCULATION_GROUP_DETAIL );
            RockMigrationHelper.DeleteBlockType( SystemGuid.BlockType.CALCULATION_GROUP_LIST );

            // Remove pages
            RockMigrationHelper.DeletePage( SystemGuid.Page.CALCULATION_DETAIL );
            RockMigrationHelper.DeletePage( SystemGuid.Page.CALCULATION_SUB_GROUP_DETAIL );
            RockMigrationHelper.DeletePage( SystemGuid.Page.CALCULATION_GROUP_DETAIL );
            RockMigrationHelper.DeletePage( SystemGuid.Page.CALCULATION_GROUP_LIST );
        }
    }
}
