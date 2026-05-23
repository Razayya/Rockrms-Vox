using System;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Jobs;
using Rock.Model;

namespace com.razayya.JourneyTrack.Jobs
{
    /// <summary>
    /// Scans JourneyCommunicationLog for pending rows (SentDateTime IS NULL) and
    /// queues a Rock Communication per row so Rock's standard delivery + opt-out
    /// handling apply.
    /// </summary>
    [DisplayName( "Send Journey Communications" )]
    [Description( "Dispatches queued JourneyTrack communications via Rock SystemCommunication." )]

    [IntegerField( "Batch Size",
        Description = "Maximum number of pending rows to claim per run.",
        IsRequired = true,
        DefaultIntegerValue = 1000,
        Order = 0,
        Key = "BatchSize" )]

    public class SendJourneyCommunications : RockJob
    {
        public override void Execute()
        {
            var batchSize = GetAttributeValue( "BatchSize" ).AsIntegerOrNull() ?? 1000;
            if ( batchSize <= 0 )
            {
                batchSize = 1000;
            }

            int dispatched = 0;
            int errors = 0;

            using ( var rockContext = new RockContext() )
            {
                var pending = new JourneyCommunicationLogService( rockContext ).Queryable()
                    .Where( l => l.SentDateTime == null )
                    .OrderBy( l => l.QueuedDateTime )
                    .Take( batchSize )
                    .Include( l => l.PersonAlias.Person )
                    .Include( l => l.SystemCommunication )
                    .ToList();

                foreach ( var entry in pending )
                {
                    try
                    {
                        var person = entry.PersonAlias?.Person;
                        var template = entry.SystemCommunication;
                        if ( person == null || template == null )
                        {
                            entry.SentDateTime = RockDateTime.Now;
                            continue;
                        }

                        var mergeFields = Rock.Lava.LavaHelper.GetCommonMergeFields( null, person );

                        var communication = new Communication
                        {
                            Status = CommunicationStatus.Approved,
                            SystemCommunicationId = template.Id,
                            Name = template.Title,
                            Subject = template.Subject?.ResolveMergeFields( mergeFields ),
                            Message = !string.IsNullOrWhiteSpace( template.Body )
                                ? template.Body.ResolveMergeFields( mergeFields )
                                : null,
                            SMSMessage = !string.IsNullOrWhiteSpace( template.SMSMessage )
                                ? template.SMSMessage.ResolveMergeFields( mergeFields )
                                : null,
                            SmsFromSystemPhoneNumberId = template.SmsFromSystemPhoneNumberId,
                            FromEmail = template.From,
                            FromName = template.FromName,
                            CommunicationType = !string.IsNullOrWhiteSpace( template.SMSMessage )
                                ? CommunicationType.SMS
                                : CommunicationType.Email
                        };

                        communication.Recipients.Add( new CommunicationRecipient
                        {
                            PersonAliasId = entry.PersonAliasId,
                            Status = CommunicationRecipientStatus.Pending
                        } );

                        new CommunicationService( rockContext ).Add( communication );

                        entry.SentDateTime = RockDateTime.Now;
                        dispatched++;
                    }
                    catch ( Exception ex )
                    {
                        ExceptionLogService.LogException( ex );
                        errors++;
                    }
                }

                rockContext.SaveChanges();
            }

            Result = string.Format( "Queued {0} communication(s); {1} error(s).", dispatched, errors );
        }
    }
}
