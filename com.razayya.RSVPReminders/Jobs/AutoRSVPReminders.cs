using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Web;

using com.razayya.RSVPReminders.Services;

using Microsoft.Extensions.Logging;

using Rock;
using Rock.Attribute;
using Rock.Communication;
using Rock.Data;
using Rock.Jobs;
using Rock.Lava.RockLiquid.Blocks;
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
    [SystemCommunicationField("RSVP Invitation Communication",
        Description = "The system communication used when inviting individuals to RSVP.",
        IsRequired = true,
        DefaultSystemCommunicationGuid = SystemGuid.SystemCommunication.RSVP_INVITATION,
        Order = 2,
        Key = Constants.AttributeKey.InvitationSystemCommunication)]
    [BooleanField("Show Debug Logs", "Enable this to show suppressed DEBUG logging messages for the job.", false, "", 3, Constants.AttributeKey.ShowDebug)]
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
            var groupType = groupTypeService.GetByGuids(new List<Guid>() { GetAttributeValue(Constants.AttributeKey.AutoRSVPGroupType).AsGuid() }).FirstOrDefault();
            var showDebug = GetAttributeValue(Constants.AttributeKey.ShowDebug).AsBoolean();

            var results = new StringBuilder();

            var sendReminderOffsetDates = GetAttributeValue(Constants.AttributeKey.SendReminders)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().AsIntegerOrNull())
                .Where(d => d.HasValue)
                .Select(d => d.Value)
                .Select(d => RockDateTime.Today.AddDays(d))
                .ToList();

            if (groupType == null)
            {
                var warning = "No Auto RSVP Group Type Configured. Job cannot execute.";
                results.Append(FormatWarningMessage(warning));
                Logger.LogWarning(warning);
                this.Result = results.ToString();
                throw new RockJobWarningException(warning);
            }

            var groupEntityTypeId = entityService.GetByName("Rock.Model.Group", false).Id;
            var sendRsvpEmailsAttributeGuid = attrService.Get(groupEntityTypeId, "GroupTypeId", groupType.Id.ToString()).FirstOrDefault(x => x.Key == Constants.AttributeKey.SendsRsvpEmails)?.Guid;

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

            Result += $@"Inherited GroupType Count: {groupTypeIds.Count}
";

            var groups = groupService
                .Queryable("Members.Person,Schedule,GroupType")
                .Where(g => groupTypeIds.Contains(g.GroupTypeId) && g.IsActive == true && g.InactiveDateTime == null && g.IsArchived == false)
                .ToList();

            Result += $@"Filtering groups. {groups.Count} prior to filter.
";

            groups = groups
                .Where(g =>
                {
                    if (g.Schedule != null)
                    {
                        g.LoadAttributes(rockContext);
                        var sendEmails = g.GetAttributeValue(SystemGuid.GroupAttribute.SEND_RSVP_EMAILS.AsGuid());

                        if (sendEmails.AsBoolean())
                        {
                            var lastRunDate = g.GetAttributeValue(SystemGuid.GroupAttribute.LAST_AUTO_RSVP_RUN.AsGuid()).AsDateTime();

                            if (lastRunDate.HasValue && lastRunDate.Value.Date == RockDateTime.Today)
                            {
                                return false;
                            }
                            else
                            {
                                var nextDates = g.Schedule.GetScheduledStartTimes(sendReminderOffsetDates.Min(), sendReminderOffsetDates.Max().AddSeconds(86399));

                                if (nextDates.Any())
                                {
                                    return nextDates.Any(d => sendReminderOffsetDates.Any(r => r.Date == d.Date));
                                }
                                else
                                {
                                    return false;
                                }

                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                })
                .ToList();

            Result += $@"Processing {groups.Count} Groups.
";

            int emailsSent = 0;
            int emailsFailed = 0;
            int remindersSent = 0;
            int invitationsSent = 0;
            var occurrenceService = new AttendanceOccurrenceService(rockContext);
            var attendanceService = new RsvpAttendanceService(rockContext);
            var systemCommunicationService = new SystemCommunicationService(rockContext);

            var inviteCommunicationGuid = GetAttributeValue(Constants.AttributeKey.InvitationSystemCommunication).AsGuid();
            var inviteCommunication = systemCommunicationService.Get(inviteCommunicationGuid);

            if (inviteCommunication == null)
            {
                var warning = "Configured RSVP Invitation Communication not found. Job cannot execute.";
                results.Append(FormatWarningMessage(warning));
                Logger.LogWarning(warning);
                this.Result = results.ToString();
                throw new RockJobWarningException(warning);
            }

            foreach (var group in groups)
            {
                if (sendReminderOffsetDates.Count == 0)
                {
                    Result += $@"{group.Id} - Validation Error - No Job Offsets Configured
";
                    continue;
                }

                var systemCommunicationId = group.RSVPReminderSystemCommunicationId ?? group.GroupType.RSVPReminderSystemCommunicationId;
                if (!systemCommunicationId.HasValue)
                {
                    Result += $@"{group.Id} - Validation Error - No RSVP Communication Found
";
                    continue;
                }

                var communication = systemCommunicationService.Get(systemCommunicationId.Value);
                if (communication == null)
                {
                    Result += $@"{group.Id} - Validation Error - Configured Communication Not Found.
";
                    continue;
                }

                var nextDates = group.Schedule.GetScheduledStartTimes(sendReminderOffsetDates.Min(), sendReminderOffsetDates.Max().AddSeconds(86399));
                var targetDate = nextDates.FirstOrDefault(d => sendReminderOffsetDates.Any(r => r.Date == d.Date));

                var occurrence = occurrenceService
                    .Queryable()
                    .FirstOrDefault(o => o.GroupId == group.Id &&
                                         DbFunctions.TruncateTime(o.OccurrenceDate) == targetDate);

                if (occurrence == null)
                {
                    if (showDebug)
                    {
                        Result += $@"{group.Id} - No Occurrence for NextStartDate. Creating for {targetDate.Date} 
";
                    }
                    occurrence = new AttendanceOccurrence
                    {
                        GroupId = group.Id,
                        OccurrenceDate = targetDate.Date,
                        ScheduleId = group.ScheduleId,
                    };
                    occurrenceService.Add(occurrence);
                    rockContext.SaveChanges();
                }

                Result += $@"{group.Id} - Occurrence (Id:{occurrence.Id})
";

                var personIds = group.Members
                    .Where(m => m.IsArchived == false && m.InactiveDateTime == null)
                    .Select(m => m.PersonId)
                    .Distinct()
                    .ToList();

                var attendanceRecords = attendanceService.RegisterRSVPRecipients(occurrence.Id, personIds, out var returnedAttendance);

                Result += $@"{group.Id} - ProcessRSVPRecipients: {attendanceRecords.Count} Recipients.
";

                foreach (var attendance in attendanceRecords)
                {
                    var person = attendance.PersonAlias?.Person;
                    if (person == null || !person.IsEmailActive)
                    {
                        continue;
                    }

                    RockEmailMessage message = null;

                    if (attendance.RSVP == RSVP.Yes)
                    {
                        message = new RockEmailMessage(communication);
                        remindersSent++;
                    }
                    else if (attendance.RSVP == RSVP.Unknown || attendance.RSVP == RSVP.Maybe)
                    {
                        message = new RockEmailMessage(inviteCommunication);
                        invitationsSent++;
                    }
                    else
                    {
                        continue;
                    }

                    var mergeFields = Rock.Lava.LavaHelper.GetCommonMergeFields(null, person);
                    mergeFields.Add("Person", person);
                    mergeFields.Add("Group", group);
                    mergeFields.Add("Occurrence", occurrence);

                    var recipient = new RockEmailMessageRecipient(person, mergeFields);
                    message.SetRecipients(new List<RockEmailMessageRecipient> { recipient });
                    message.Send(out List<string> errors);

                    if (!errors.Any())
                    {
                        emailsSent++;
                    }
                    else
                    {
                        emailsFailed++;
                        if (showDebug)
                        {
                            Result += $@"{group.Id}|{attendance.PersonAlias.PersonId} - Communication Failed:
";
                        }
                        foreach (var error in errors)
                        {
                            if (showDebug)
                            {
                                Result += $@"{error}
";
                            }
                        }
                    }
                }

                group.SetAttributeValue("LastAutoRSVPRun", RockDateTime.Today.Date);
            }

            rockContext.SaveChanges();
            Result += $@"Sent {emailsSent} RSVP email{(emailsSent != 1 ? "s" : string.Empty)}. {emailsFailed} RSVPs failed to send.
";
            Result += $@"{remindersSent} Reminders. {invitationsSent} Invitations.";

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