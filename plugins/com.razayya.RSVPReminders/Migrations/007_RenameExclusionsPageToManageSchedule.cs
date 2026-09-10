using Rock.Plugin;

namespace com.razayya.RSVPReminders.Migrations
{
    /// <summary>
    /// The exclusions page now also carries the leader-facing meeting day and time
    /// editor (7478), so "Meeting Exclusions" undersells it. Renames the page and the
    /// two block instances to "Manage Group Schedule"; the block type keeps its
    /// original name so existing admin references still make sense.
    /// </summary>
    [MigrationNumber( 7, "1.16.0" )]
    public class RenameExclusionsPageToManageSchedule : Migration
    {
        public override void Up()
        {
            Sql( $@"
UPDATE [Page]
SET InternalName = 'Manage Group Schedule',
    PageTitle    = 'Manage Group Schedule',
    BrowserTitle = 'Manage Group Schedule',
    Description  = 'Leader-managed meeting day, time and skipped dates for a group.'
WHERE [Guid] = '{SystemGuid.Page.MEETING_EXCLUSIONS}'" );

            Sql( $"UPDATE [Block] SET Name = 'Group Schedule Manager' WHERE [Guid] = '{SystemGuid.Block.RSVP_GROUP_EXCLUSIONS_GROUP_TOOLBOX}'" );
            Sql( $"UPDATE [Block] SET Name = 'Group Schedule Link' WHERE [Guid] = '{SystemGuid.Block.RSVP_GROUP_EXCLUSIONS_LINK}'" );
        }

        public override void Down()
        {
            Sql( $@"
UPDATE [Page]
SET InternalName = 'Meeting Exclusions',
    PageTitle    = 'Meeting Exclusions',
    BrowserTitle = 'Meeting Exclusions',
    Description  = 'Leader-managed RSVP meeting exclusions for a group.'
WHERE [Guid] = '{SystemGuid.Page.MEETING_EXCLUSIONS}'" );

            Sql( $"UPDATE [Block] SET Name = 'RSVP Meeting Exclusions' WHERE [Guid] = '{SystemGuid.Block.RSVP_GROUP_EXCLUSIONS_GROUP_TOOLBOX}'" );
            Sql( $"UPDATE [Block] SET Name = 'RSVP Meeting Exclusions Link' WHERE [Guid] = '{SystemGuid.Block.RSVP_GROUP_EXCLUSIONS_LINK}'" );
        }
    }
}
