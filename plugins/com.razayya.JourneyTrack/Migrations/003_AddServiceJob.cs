using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    [MigrationNumber( 3, "1.15.0" )]
    public class AddServiceJob : Migration
    {
        public override void Up()
        {
            // Nightly journey-track engine job
            Sql( $@"
IF NOT EXISTS (SELECT 1 FROM [ServiceJob] WHERE [Guid] = '{SystemGuid.ServiceJob.JOURNEY_TRACK_ENGINE}')
BEGIN
    INSERT INTO [ServiceJob] (
        [IsSystem], [IsActive], [Name], [Description], [Class], [CronExpression],
        [NotificationStatus], [Guid], [CreatedDateTime], [ModifiedDateTime], [HistoryCount]
    ) VALUES (
        0, 1,
        'JourneyTrack Engine',
        'Nightly processing of all active Journey Programs. Writes computed values to Person Attributes and queues communications on transitions.',
        'com.razayya.JourneyTrack.Jobs.RunJourneyTrackEngine',
        '{Constants.CRON.NightlySync}',
        1,
        '{SystemGuid.ServiceJob.JOURNEY_TRACK_ENGINE}',
        GETDATE(), GETDATE(), 500
    );
END;
" );

            // Communication dispatch job (every 15 min)
            Sql( $@"
IF NOT EXISTS (SELECT 1 FROM [ServiceJob] WHERE [Guid] = '{SystemGuid.ServiceJob.SEND_JOURNEY_COMMUNICATIONS}')
BEGIN
    INSERT INTO [ServiceJob] (
        [IsSystem], [IsActive], [Name], [Description], [Class], [CronExpression],
        [NotificationStatus], [Guid], [CreatedDateTime], [ModifiedDateTime], [HistoryCount]
    ) VALUES (
        0, 1,
        'Send Journey Communications',
        'Picks up pending JourneyCommunicationLog rows and dispatches them as Rock Communications.',
        'com.razayya.JourneyTrack.Jobs.SendJourneyCommunications',
        '0 0/15 * 1/1 * ? *',
        1,
        '{SystemGuid.ServiceJob.SEND_JOURNEY_COMMUNICATIONS}',
        GETDATE(), GETDATE(), 500
    );
END;
" );
        }

        public override void Down()
        {
            Sql( $@"DELETE FROM [ServiceJob] WHERE [Guid] IN ('{SystemGuid.ServiceJob.JOURNEY_TRACK_ENGINE}','{SystemGuid.ServiceJob.SEND_JOURNEY_COMMUNICATIONS}');" );
        }
    }
}
