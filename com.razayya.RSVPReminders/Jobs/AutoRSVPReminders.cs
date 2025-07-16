using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Web;

using Microsoft.Extensions.Logging;

using Rock.Attribute;
using Rock.Communication;
using Rock.Data;
using Rock;
using Rock.Jobs;
using Rock.Logging;
using Rock.Model;
using Rock.Web.Cache;


namespace com.razayya.RSVPReminders.Jobs
{
    /// <summary>
    /// Sends RSVP emails to groups configured for RSVP reminders and registers all
    /// group members for response tracking so pending responses can be reviewed.
    /// </summary>
    [DisplayName("Send RSVP Email Notifications")]
    [Description("Sends RSVP communications to group members and creates attendance records so that responses can be tracked.")]
    [GroupTypeField("Auto RSVP Group Type", "The inherited group type over all RSVP activated Group Types", true, SystemGuid.GroupType.AUTO_RSVP_GROUP, "", 0, Constants.AttributeKey.AutoRSVPGroupType)]
    [TextField("Send Reminders",
        Description = "Comma delimited list of days before a group meets to send an RSVP reminder. For example, a value of '2,4' would result in an additional reminder getting sent two and four days before group meets if RSVP has not been entered.",
        Key = Constants.AttributeKey.SendReminders,
        IsRequired = true,
        Order = 1,
        DefaultValue = "2,7")]
    public class AutoRSVPReminders : RockJob
    {      
        public AutoRSVPReminders()
        {
        }

        public override void Execute()
        {

            var rockContext = new RockContext();
            var attrService = new AttributeService(rockContext);
            var entityService = new EntityTypeService(rockContext);
            var groupService = new GroupService(rockContext);
            var groupTypeService = new GroupTypeService(rockContext);
            var groupType = groupTypeService.GetByGuids(new List<Guid>() { GetAttributeValue(Constants.AttributeKey.AutoRSVPGroupType).AsGuid() }).Include(x => x.Attributes).FirstOrDefault();

            var results = new StringBuilder();

            if (groupType == null)
            {
                var warning = "No Auto RSVP Group Type Configured. Job cannot execute.";
                results.Append(FormatWarningMessage(warning));
                Logger.LogWarning(warning);
                this.Result = results.ToString();
                throw new RockJobWarningException(warning);
            }

            var groupEntityTypeId = entityService.GetByName("Rock.Model.Group", false).Id;
            var sendRsvpEmailsAttributeGuid = attrService.Get(groupEntityTypeId, "GroupTypeId", groupType.Id.ToString()).FirstOrDefault(x => x.Key == "SendsRsvpEmails")?.Guid;

            if (sendRsvpEmailsAttributeGuid == null)
            {
                var warning = "No SendRsvpEmails Attribute Configured on RSVP Group Type. Job cannot execute.";
                results.Append(FormatWarningMessage(warning));
                Logger.LogWarning(warning);
                this.Result = results.ToString();
                throw new RockJobWarningException(warning);
            }

            var groupTypeIds = groupType.GetAllDependentGroupTypeIds(rockContext);
            groupTypeIds.Add(groupType.Id);

            var groups = groupService
                .Queryable("Members.Person")
                .Where(g => groupTypeIds.Contains(g.GroupTypeId))
                .ToList();

            groups = groups
                .Where(g =>
                {
                    g.LoadAttributes(rockContext);
                    var value = g.GetAttributeValue(sendRsvpEmailsAttributeGuid.ToString());
                    return value.AsBoolean();
                })
                .ToList();

            int emailsSent = 0;
            int emailsFailed = 0;
            var occurrenceService = new AttendanceOccurrenceService(rockContext);
            var attendanceService = new AttendanceService(rockContext);
            var systemCommunicationService = new SystemCommunicationService(rockContext);

            var sendReminderOffsets = GetAttributeValue(Constants.AttributeKey.SendReminders)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().AsIntegerOrNull())
                .Where(d => d.HasValue)
                .Select(d => d.Value)
                .ToList();

            foreach (var group in groups)
            {
                if (!group.RSVPReminderSystemCommunicationId.HasValue || sendReminderOffsets.Count == 0)
                {
                    continue;
                }

                var communication = systemCommunicationService.Get(group.RSVPReminderSystemCommunicationId.Value);
                if (communication == null)
                {
                    continue;
                }

                foreach (var offset in sendReminderOffsets)
                {
                    var reminderDate = RockDateTime.Today.AddDays(offset * -1); // Offset is days BEFORE the event
                    var occurrence = occurrenceService
                        .Queryable()
                        .FirstOrDefault(o => o.GroupId == group.Id && DbFunctions.TruncateTime(o.OccurrenceDate) == reminderDate);

                    if (occurrence == null)
                    {
                        continue; // No occurrence on this reminder date
                    }

                    var attendanceRecords = attendanceService
                        .Queryable()
                        .Where(a => a.OccurrenceId == occurrence.Id)
                        .ToList();

                    var personIds = group.Members
                        .Where(m => m.InactiveDateTime == null)
                        .Select(m => m.PersonId)
                        .Distinct()
                        .ToList();

                    var recipientsToNotify = personIds
                        .Where(pid =>
                            !attendanceRecords.Any(a => a.PersonAlias != null && a.PersonAlias.PersonId == pid && a.RSVPDateTime.HasValue))
                        .ToList();

                    attendanceService.RegisterRSVPRecipients(occurrence.Id, recipientsToNotify);

                    foreach (var personId in recipientsToNotify)
                    {
                        var person = group.Members.FirstOrDefault(m => m.PersonId == personId)?.Person;
                        if (person == null || !person.IsEmailActive)
                        {
                            continue;
                        }

                        var mergeFields = Rock.Lava.LavaHelper.GetCommonMergeFields(null, person);
                        mergeFields.Add("Person", person);
                        mergeFields.Add("Group", group);
                        mergeFields.Add("Occurrence", occurrence);

                        var recipient = new RockEmailMessageRecipient(person, mergeFields);
                        var message = new RockEmailMessage(communication);
                        message.SetRecipients(new List<RockEmailMessageRecipient> { recipient });
                        message.Send(out List<string> errors);

                        if (!errors.Any())
                        {
                            emailsSent++;
                        }
                        else
                        {
                            emailsFailed++;
                        }
                    }
                }
            }

            rockContext.SaveChanges();
            Result = $"Sent {emailsSent} RSVP email{(emailsSent != 1 ? "s" : string.Empty)}. {emailsFailed} RSVPs failed to send.";
        }
        
        private StringBuilder FormatWarningMessage(string warning)
        {
            var errorMessages = new List<string> { warning };
            return FormatMessages(errorMessages, "Warning");
        }

        private StringBuilder FormatMessages(List<string> messages, string label)
        {
            StringBuilder sb = new StringBuilder();
            if (messages.Any())
            {
                var pluralizedLabel = label.PluralizeIf(messages.Count > 1);
                sb.AppendLine($"{messages.Count} {pluralizedLabel}:");
                messages.ForEach(w => { sb.AppendLine(w); });
            }
            return sb;
        }
    }
}
