using Rock.Plugin;

namespace com.razayya.CustomPersonAttributeSyncEngine.Migrations
{
    [MigrationNumber( 9, "1.15.0" )]
    public class PageListAsBlocks : Migration
    {
        // PageMenu block attribute for Template (from Rock core)
        private const string PageMenuTemplateAttr = "1322186A-862A-4CF1-B349-28ECB67229BA";

        public override void Up()
        {
            RockMigrationHelper.AddBlockAttributeValue(
                SystemGuid.Block.PAGE_MENU,
                PageMenuTemplateAttr,
                @"{% include '~~/Assets/Lava/PageListAsBlocks.lava' %}" );
        }

        public override void Down()
        {
            RockMigrationHelper.AddBlockAttributeValue(
                SystemGuid.Block.PAGE_MENU,
                PageMenuTemplateAttr,
                @"{% include '~~/Assets/Lava/PageListAsTabs.lava' %}" );
        }
    }
}
