using Rock.Plugin;

namespace com.razayya.RSVPReminders.Migrations
{
    /// <summary>
    /// Adds the leader-facing "RSVP Group Exclusions" block (6919): registers the block
    /// type and places it on the external Group Toolbox page. The block self-gates
    /// (AutoRSVP-enabled group + person can manage it), so placement is safe even
    /// though the page serves every group.
    /// </summary>
    [MigrationNumber( 5, "1.16.0" )]
    public class AddRsvpGroupExclusionsBlock : Migration
    {
        private const string PAGE_GROUP_TOOLBOX = "A71619CF-D775-4FDE-9DB3-4B99489559A0";
        private const string BT_RSVP_GROUP_EXCLUSIONS = SystemGuid.BlockType.RSVP_GROUP_EXCLUSIONS;
        private const string B_RSVP_GROUP_EXCLUSIONS = SystemGuid.Block.RSVP_GROUP_EXCLUSIONS_GROUP_TOOLBOX;

        public override void Up()
        {
            RockMigrationHelper.UpdateBlockType(
                "RSVP Group Exclusions",
                "Lets group leaders skip upcoming meeting dates so RSVP emails aren't sent for those occurrences. Renders only for AutoRSVP-enabled groups the current person can manage.",
                "~/Plugins/com_razayya/RSVPReminders/RsvpGroupExclusions.ascx",
                "Razayya > RSVP Reminders",
                BT_RSVP_GROUP_EXCLUSIONS );

            RockMigrationHelper.AddBlock(
                PAGE_GROUP_TOOLBOX,
                null,
                BT_RSVP_GROUP_EXCLUSIONS,
                "RSVP Meeting Exclusions",
                "Main",
                string.Empty,
                string.Empty,
                2,
                B_RSVP_GROUP_EXCLUSIONS );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteBlock( B_RSVP_GROUP_EXCLUSIONS );
            RockMigrationHelper.DeleteBlockType( BT_RSVP_GROUP_EXCLUSIONS );
        }
    }
}
