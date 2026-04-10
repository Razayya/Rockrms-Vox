using Rock.Plugin;

namespace com.razayya.CustomPersonAttributeSyncEngine.Migrations
{
    [MigrationNumber( 3, "1.15.0" )]
    public class AddServiceJob : Migration
    {
        public override void Up()
        {
            Sql( $@"
IF NOT EXISTS (SELECT 1 FROM [ServiceJob] WHERE [Guid] = '{SystemGuid.ServiceJob.ATTRIBUTE_SYNC_ENGINE}')
BEGIN
    INSERT INTO [ServiceJob] (
        [IsSystem], [IsActive], [Name], [Description], [Class], [CronExpression],
        [NotificationStatus], [Guid]
    )
    VALUES (
        0, 1,
        'Custom Person Attribute Sync Engine',
        'Nightly job that evaluates all active Calculation Groups and writes computed values to Person Attributes.',
        'com.razayya.CustomPersonAttributeSyncEngine.Jobs.RunAttributeSyncEngine',
        '{Constants.CRON.NightlySync}',
        3,
        '{SystemGuid.ServiceJob.ATTRIBUTE_SYNC_ENGINE}'
    );
END
" );
        }

        public override void Down()
        {
            Sql( $@"DELETE FROM [ServiceJob] WHERE [Guid] = '{SystemGuid.ServiceJob.ATTRIBUTE_SYNC_ENGINE}';" );
        }
    }
}
