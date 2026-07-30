using Rock.Plugin;

namespace com.razayya.RSVPReminders.Migrations
{
    /// <summary>
    /// Moves the leader exclusions manager to its own dedicated page (6919 feedback):
    /// a child page of the Group Toolbox hosts the full manager (with a back link the
    /// block renders itself), while the toolbox gets a second block instance at the top
    /// of Main that the block renders as a compact link. Both instances are the same
    /// block type — it picks link vs. manager mode from the page it's sitting on.
    /// </summary>
    [MigrationNumber( 6, "1.16.0" )]
    public class AddExclusionsPage : Migration
    {
        private const string PAGE_GROUP_TOOLBOX = "A71619CF-D775-4FDE-9DB3-4B99489559A0";
        private const string LAYOUT_FULL_WIDTH = "02677DA7-178C-4766-A20D-9C84699B66F1";

        public override void Up()
        {
            RockMigrationHelper.AddPage(
                PAGE_GROUP_TOOLBOX,
                LAYOUT_FULL_WIDTH,
                "Meeting Exclusions",
                "Leader-managed RSVP meeting exclusions for a group.",
                SystemGuid.Page.MEETING_EXCLUSIONS );

            // Hidden from navigation - reached only via the toolbox link (2 = Never).
            Sql( $"UPDATE [Page] SET DisplayInNavWhen = 2 WHERE [Guid] = '{SystemGuid.Page.MEETING_EXCLUSIONS}'" );

            // Move the existing manager block instance onto the dedicated page.
            Sql( $@"
UPDATE b SET b.PageId = p.Id, b.Zone = 'Main', b.[Order] = 0
FROM [Block] b
CROSS JOIN [Page] p
WHERE b.[Guid] = '{SystemGuid.Block.RSVP_GROUP_EXCLUSIONS_GROUP_TOOLBOX}'
  AND p.[Guid] = '{SystemGuid.Page.MEETING_EXCLUSIONS}'" );

            // Compact link instance in the toolbox sidebar, just under the group list
            // (Main-zone placement lost the visual tie-break against Group Detail Lava
            // and rendered at the bottom - 6919 feedback).
            RockMigrationHelper.AddBlock(
                PAGE_GROUP_TOOLBOX,
                null,
                SystemGuid.BlockType.RSVP_GROUP_EXCLUSIONS,
                "RSVP Meeting Exclusions Link",
                "Sidebar1",
                string.Empty,
                string.Empty,
                1,
                SystemGuid.Block.RSVP_GROUP_EXCLUSIONS_LINK );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteBlock( SystemGuid.Block.RSVP_GROUP_EXCLUSIONS_LINK );

            // Move the manager block back to the toolbox before deleting the page.
            Sql( $@"
UPDATE b SET b.PageId = p.Id, b.Zone = 'Main', b.[Order] = 2
FROM [Block] b
CROSS JOIN [Page] p
WHERE b.[Guid] = '{SystemGuid.Block.RSVP_GROUP_EXCLUSIONS_GROUP_TOOLBOX}'
  AND p.[Guid] = '{PAGE_GROUP_TOOLBOX}'" );

            RockMigrationHelper.DeletePage( SystemGuid.Page.MEETING_EXCLUSIONS );
        }
    }
}
