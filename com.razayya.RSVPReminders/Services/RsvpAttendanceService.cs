using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

using Rock.Data;
using Rock.Model;

namespace com.razayya.RSVPReminders.Services
{
    /// <summary>
    /// Extends the <see cref="AttendanceService"/> with RSVP helper methods.
    /// </summary>
    public class RsvpAttendanceService : AttendanceService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RsvpAttendanceService"/> class.
        /// </summary>
        /// <param name="context">The Rock context.</param>
        public RsvpAttendanceService(RockContext context) : base(context)
        {
        }

        /// <summary>
        /// Registers the RSVP recipients for the occurrence and returns both existing
        /// and newly created attendance records.
        /// </summary>
        /// <param name="occurrenceId">The occurrence identifier.</param>
        /// <param name="personIdList">The list of person identifiers.</param>
        /// <param name="existingAttendanceRecords">Any attendance records already present for the occurrence.</param>
        /// <returns>All attendance records for the provided people and occurrence.</returns>
        public List<Attendance> RegisterRSVPRecipients(int occurrenceId, List<int> personIdList, out List<Attendance> attendanceRecords)
        {
            var rockContext = this.Context as RockContext;

            // Get Occurrence
            var occurrence = new AttendanceOccurrenceService(rockContext).Queryable().AsNoTracking()
                .FirstOrDefault(o => o.Id == occurrenceId);

            DateTime startDateTime = occurrence.Schedule != null && occurrence.Schedule.HasSchedule()
                ? occurrence.OccurrenceDate.Date.Add(occurrence.Schedule.StartTimeOfDay)
                : occurrence.OccurrenceDate;

            // Get PersonAliasIDs from PersonIDs
            var people = new PersonService(rockContext).Queryable().AsNoTracking()
                .Where(p => personIdList.Contains(p.Id))
                .ToList();

            var personAliasIds = people.Select(p => p.PrimaryAliasId).ToList();
            var personAliases = people.Select(x => x.PrimaryAlias).ToList();

            // Check for existing records
            attendanceRecords = this.Queryable().AsNoTracking()
                .Where(a => personAliasIds.Contains(a.PersonAliasId))
                .Where(a => a.OccurrenceId == occurrenceId)
                .ToList();

            var newAttendanceRecords = new List<Attendance>();
            foreach (var personAlias in personAliases)
            {
                if (!attendanceRecords.Any(a => a.PersonAliasId == personAlias.Id))
                {
                    newAttendanceRecords.Add(new Attendance
                    {
                        OccurrenceId = occurrenceId,
                        PersonAliasId = personAlias.Id,
                        StartDateTime = startDateTime,
                        RSVP = RSVP.Unknown,
                        DidAttend = false
                    });
                }
            }

            this.AddRange(newAttendanceRecords);
            rockContext.SaveChanges();

            // Hydrate PersonAlias for the newly inserted rows in a single round-trip
            var idsToHydrate = newAttendanceRecords.Select(a => a.PersonAliasId).ToList();
            var aliases = new PersonAliasService(rockContext).Queryable()
                .Where(pa => idsToHydrate.Contains(pa.Id))
                .ToDictionary(pa => pa.Id);

            foreach (var a in newAttendanceRecords)
            {
                a.PersonAlias = aliases[a.PersonAliasId.Value];
            }

            return attendanceRecords.Concat(newAttendanceRecords).ToList();
        }
    }
}