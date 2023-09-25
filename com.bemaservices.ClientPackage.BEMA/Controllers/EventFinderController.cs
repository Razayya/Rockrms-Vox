// <copyright>
// Copyright by BEMA Information Technologies
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Drawing;
using System.Linq;
using Ical.Net.DataTypes;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Rest.Filters;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace com.bemaservices.ClientPackage.BEMA.Controllers
{
    /// <summary>
    /// The controller class for the GroupTools
    /// </summary>
    public partial class EventFinderController : Rock.Rest.ApiController<Rock.Model.EventItem>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventFinderController"/> class.
        /// </summary>
        public EventFinderController() : base( new Rock.Model.EventItemService( new Rock.Data.RockContext() ) ) { }
    }

    public partial class EventFinderController
    {

        [Authenticate, Secured]
        [System.Web.Http.Route( "api/com_bemaservices/BEMA/GetEvents" )]
        public IQueryable<EventOccurrenceSummary> GetEvents(
            string calendarIds = "",
            string campusIds = "",
            string categoryIds = "",
            string topicIds = "",
            string keywords = "",
            DateTime? filterStartDate = null,
            DateTime? filterEndDate = null,
            int monthsAhead = 12,
            int? offset = null,
            int? limit = null,
            bool returnIndividualInstancePerRecurrence = true,
            string optionalFilterAttributeKey = null,
            string optionalFilterIds = null,
            string secondaryCategoryAttributeKey = null,
            string secondaryCategoryFilterIds = null,
            string meetingDays = "" )
        {
            var rockContext = new RockContext();
            IQueryable<EventOccurrenceSummary> qry = null;
            if ( returnIndividualInstancePerRecurrence )
            {
                qry = FilterEventSummaries( calendarIds,
                    campusIds,
                    categoryIds,
                    topicIds,
                    keywords,
                    rockContext,
                    filterStartDate,
                    filterEndDate,
                    monthsAhead,
                    optionalFilterAttributeKey,
                    optionalFilterIds,
                    secondaryCategoryAttributeKey,
                    secondaryCategoryFilterIds,
                    meetingDays );
            }
            else
            {
                qry = FilterEvents( calendarIds,
                    campusIds,
                    categoryIds,
                    topicIds,
                    keywords,
                    rockContext,
                    filterStartDate,
                    filterEndDate,
                    monthsAhead,
                    optionalFilterAttributeKey,
                    optionalFilterIds,
                    secondaryCategoryAttributeKey,
                    secondaryCategoryFilterIds,
                    meetingDays );
            }

            if ( offset.HasValue )
            {
                qry = qry.Skip( offset.Value );
            }

            if ( limit.HasValue )
            {
                qry = qry.Take( limit.Value );
            }

            return qry;
        }

        /// <summary>
        /// Gets the group count.
        /// </summary>
        /// <param name="groupTypeIds">The group type ids.</param>
        /// <param name="campusIds">The campus ids.</param>
        /// <param name="meetingDays">The meeting days.</param>
        /// <param name="categoryIds">The category ids.</param>
        /// <param name="lifeStageIds">The life stage ids.</param>
        /// <returns></returns>
        [Authenticate, Secured]
        [System.Web.Http.Route( "api/com_bemaservices/BEMA/GetEventCount" )]
        public int GetEventCount(
            string calendarIds = "",
            string campusIds = "",
            string categoryIds = "",
            string topicIds = "",
            string keywords = "",
            DateTime? filterStartDate = null,
            DateTime? filterEndDate = null,
            int monthsAhead = 12,
            string optionalFilterAttributeKey = null,
            string optionalFilterIds = null,
            string secondaryCategoryAttributeKey = null,
            string secondaryCategoryFilterIds = null,
            string meetingDays = "" )
        {
            var rockContext = new RockContext();
            IQueryable<EventOccurrenceSummary> qry = FilterEventSummaries( calendarIds,
                    campusIds,
                    categoryIds,
                    topicIds,
                    keywords,
                    rockContext,
                    filterStartDate,
                    filterEndDate,
                    monthsAhead,
                    optionalFilterAttributeKey,
                    optionalFilterIds,
                    secondaryCategoryAttributeKey,
                    secondaryCategoryFilterIds,
                    meetingDays );

            return qry.Count();
        }


        [Authenticate, Secured]
        [System.Web.Http.Route( "api/com_bemaservices/BEMA/GetEventDetails" )]
        public EventOccurrenceSummary GetEventDetails(
          int eventItemOccurrenceId,
            DateTime? filterStartDate = null,
            DateTime? filterEndDate = null,
            int monthsAhead = 12,
            string optionalFilterAttributeKey = null,
            string secondaryCategoryAttributeKey = null )
        {


            // Get the beginning and end dates
            var today = RockDateTime.Today.Date;
            var filterStart = filterStartDate.HasValue ? filterStartDate.Value : today;
            var monthStart = new DateTime( filterStart.Year, filterStart.Month, 1 );
            var rangeStart = monthStart.AddMonths( -1 );
            var rangeEnd = monthStart.AddMonths( monthsAhead );
            var beginDate = filterStartDate.HasValue ? filterStartDate.Value : filterStart;
            var endDate = filterEndDate.HasValue ? filterEndDate.Value : rangeEnd;

            endDate = endDate.AddDays( 1 ).AddMilliseconds( -1 );


            EventOccurrenceSummary eventOccurrenceSummary = null;
            var rockContext = new RockContext();
            var eventList = new List<EventOccurrenceSummary>();
            var eventItemOccurrence = new EventItemOccurrenceService( new RockContext() ).Get( eventItemOccurrenceId );
            if ( eventItemOccurrence != null )
            {
                // Get the occurrences                
                var eventDates = new List<EventDate>();
                foreach ( var scheduleOccurrence in eventItemOccurrence.Schedule.GetICalOccurrences( beginDate, endDate ).ToList() )
                {
                    var datetime = scheduleOccurrence.Period.StartTime.Value;
                    var occurrenceEndTime = scheduleOccurrence.Period.EndTime;

                    if ( datetime >= beginDate && datetime < endDate )
                    {
                        var eventDate = new EventDate();
                        eventDate.StartDateTime = datetime;
                        if ( occurrenceEndTime != null )
                        {
                            eventDate.EndDateTime = occurrenceEndTime.Value;
                        }
                        eventDates.Add( eventDate );
                    }
                }

                eventOccurrenceSummary = new EventOccurrenceSummary
                {
                    EventItemOccurrenceId = eventItemOccurrence.Id,
                    EventItemId = eventItemOccurrence.EventItem.Id,
                    Schedule = eventItemOccurrence.Schedule,
                    EventDates = eventDates,
                    Name = eventItemOccurrence.EventItem.Name,
                    Campus = eventItemOccurrence.Campus != null ? eventItemOccurrence.Campus.Name : "All Campuses",
                    CampusId = eventItemOccurrence.CampusId,
                    Location = eventItemOccurrence.Campus != null ? eventItemOccurrence.Campus.Name : "All Campuses",
                    LocationDescription = eventItemOccurrence.Location,
                    Description = eventItemOccurrence.EventItem.Description,
                    Summary = eventItemOccurrence.EventItem.Summary,
                    OccurrenceNote = eventItemOccurrence.Note.SanitizeHtml(),
                    DetailPage = string.IsNullOrWhiteSpace( eventItemOccurrence.EventItem.DetailsUrl ) ? null : eventItemOccurrence.EventItem.DetailsUrl,
                    FriendlyScheduleText = eventItemOccurrence.Schedule == null ? null : eventItemOccurrence.Schedule.FriendlyScheduleText,
                    Image = ( eventItemOccurrence.EventItem.Photo == null ) ? null : eventItemOccurrence.EventItem.Photo
                };

                SetLinkages( rockContext, eventItemOccurrence, eventOccurrenceSummary );

                GetTopics( eventItemOccurrence, eventOccurrenceSummary );

                GetOptionalAttributes( eventItemOccurrence, eventOccurrenceSummary, optionalFilterAttributeKey );

                GetSecondaryCategories( eventItemOccurrence, eventOccurrenceSummary, secondaryCategoryAttributeKey );

                GetAudiences( eventItemOccurrence, eventOccurrenceSummary );

            }

            return eventOccurrenceSummary;
        }

        private static IQueryable<EventOccurrenceSummary> FilterEventSummaries( string calendarIds,
            string campusIds,
            string categoryIds,
            string topicIds,
            string keywords,
            RockContext rockContext,
            DateTime? filterStartDate = null,
            DateTime? filterEndDate = null,
            int monthsAhead = 12,
            string optionalFilterAttributeKey = null,
            string optionalFilterIds = null,
            string secondaryCategoryAttributeKey = null,
            string secondaryCategoryFilterIds = null,
            string meetingDays = "" )
        {
            IQueryable<EventCalendarItem> qry = FilterNonDateItems( calendarIds,
                campusIds,
                categoryIds,
                topicIds,
                keywords,
                rockContext,
                optionalFilterAttributeKey,
                optionalFilterIds,
                secondaryCategoryAttributeKey,
                secondaryCategoryFilterIds,
                meetingDays );

            // Get the beginning and end dates
            var today = RockDateTime.Today.Date;
            var filterStart = filterStartDate.HasValue ? filterStartDate.Value : today;
            var monthStart = new DateTime( filterStart.Year, filterStart.Month, 1 );
            var rangeStart = monthStart.AddMonths( -1 );
            var rangeEnd = monthStart.AddMonths( monthsAhead );
            var beginDate = filterStartDate.HasValue ? filterStartDate.Value : filterStart;
            var endDate = filterEndDate.HasValue ? filterEndDate.Value : rangeEnd;

            endDate = endDate.AddDays( 1 ).AddMilliseconds( -1 );

            // Get the occurrences
            var occurrences = qry.SelectMany( m => m.EventItem.EventItemOccurrences ).ToList();
            var occurrencesWithDates = occurrences
                .Select( o =>
                {
                    var eventOccurrenceDate = new EventOccurrenceDate
                    {
                        EventItemOccurrence = o

                    };

                    if ( o.Schedule != null )
                    {
                        eventOccurrenceDate.ScheduleOccurrences = o.Schedule.GetICalOccurrences( beginDate, endDate ).ToList();
                    }
                    else
                    {
                        eventOccurrenceDate.ScheduleOccurrences = new List<Occurrence>();
                    }

                    return eventOccurrenceDate;
                } )
                .Where( d => d.ScheduleOccurrences.Any() )
                .ToList();

            var calendarEventDates = new List<DateTime>();

            var meetingDayIntegerList = meetingDays.SplitDelimitedValues().AsIntegerList();
            var meetingDayList = meetingDayIntegerList.Where( i => i >= 0 && i <= 6 ).Select( i => i.ToString().ConvertToEnum<DayOfWeek>() ).ToList();

            var eventOccurrenceSummaries = new List<EventOccurrenceSummary>();
            foreach ( var occurrenceDates in occurrencesWithDates )
            {
                var eventItemOccurrence = occurrenceDates.EventItemOccurrence;
                foreach ( var scheduleOccurrence in occurrenceDates.ScheduleOccurrences )
                {
                    var datetime = scheduleOccurrence.Period.StartTime.Value;
                    var occurrenceEndTime = scheduleOccurrence.Period.EndTime;

                    if ( datetime >= beginDate && datetime < endDate )
                    {
                        // Filter by Meeting Day
                        if ( !meetingDayList.Any() || meetingDayList.Contains( datetime.DayOfWeek ) )
                        {
                            var eventOccurrenceSummary = new EventOccurrenceSummary
                            {
                                EventItemOccurrenceId = eventItemOccurrence.Id,
                                EventItemId = eventItemOccurrence.EventItem.Id,
                                Schedule = eventItemOccurrence.Schedule,
                                Name = eventItemOccurrence.EventItem.Name,
                                DateTime = datetime,
                                Date = datetime.ToShortDateString(),
                                Time = datetime.ToShortTimeString(),
                                EndDate = occurrenceEndTime != null ? occurrenceEndTime.Value.ToShortDateString() : null,
                                EndTime = occurrenceEndTime != null ? occurrenceEndTime.Value.ToShortTimeString() : null,
                                Campus = eventItemOccurrence.Campus != null ? eventItemOccurrence.Campus.Name : "All Campuses",
                                CampusId = eventItemOccurrence.CampusId,
                                Location = eventItemOccurrence.Campus != null ? eventItemOccurrence.Campus.Name : "All Campuses",
                                LocationDescription = eventItemOccurrence.Location,
                                Description = eventItemOccurrence.EventItem.Description,
                                Summary = eventItemOccurrence.EventItem.Summary,
                                OccurrenceNote = eventItemOccurrence.Note.SanitizeHtml(),
                                DetailPage = string.IsNullOrWhiteSpace( eventItemOccurrence.EventItem.DetailsUrl ) ? null : eventItemOccurrence.EventItem.DetailsUrl,
                                FriendlyScheduleText = eventItemOccurrence.Schedule == null ? null : eventItemOccurrence.Schedule.FriendlyScheduleText,
                                Image = ( eventItemOccurrence.EventItem.Photo == null ) ? null : eventItemOccurrence.EventItem.Photo
                            };

                            SetLinkages( rockContext, eventItemOccurrence, eventOccurrenceSummary );

                            GetTopics( eventItemOccurrence, eventOccurrenceSummary );

                            GetOptionalAttributes( eventItemOccurrence, eventOccurrenceSummary, optionalFilterAttributeKey );

                            GetSecondaryCategories( eventItemOccurrence, eventOccurrenceSummary, secondaryCategoryAttributeKey );

                            GetAudiences( eventItemOccurrence, eventOccurrenceSummary );

                            eventOccurrenceSummaries.Add( eventOccurrenceSummary );

                        }

                    }
                }
            }

            var eventSummaries = eventOccurrenceSummaries
                .OrderBy( e => e.DateTime )
                .GroupBy( e => e.Name )
                .Select( e => e.ToList() )
                .ToList();

            var eventOccurrenceSummaryQry = eventOccurrenceSummaries
                 .OrderBy( e => e.DateTime )
                 .ThenBy( e => e.Name )
                 .AsQueryable();

            var campusIdList = campusIds.SplitDelimitedValues().AsIntegerList();
            if ( campusIdList.Any() )
            {
                eventOccurrenceSummaryQry = eventOccurrenceSummaryQry.Where( e => !e.CampusId.HasValue || campusIdList.Contains( e.CampusId.Value ) );
            }

            return eventOccurrenceSummaryQry;
        }

        private static IQueryable<EventOccurrenceSummary> FilterEvents( string calendarIds,
            string campusIds,
            string categoryIds,
            string topicIds,
            string keywords,
            RockContext rockContext,
            DateTime? filterStartDate = null,
            DateTime? filterEndDate = null,
            int monthsAhead = 12,
            string optionalFilterAttributeKey = null,
            string optionalFilterIds = null,
            string secondaryCategoryAttributeKey = null,
            string secondaryCategoryFilterIds = null,
            string meetingDays = "" )
        {
            IQueryable<EventCalendarItem> qry = FilterNonDateItems( calendarIds,
                campusIds,
                categoryIds,
                topicIds,
                keywords,
                rockContext,
                optionalFilterAttributeKey,
                optionalFilterIds,
                secondaryCategoryAttributeKey,
                secondaryCategoryFilterIds,
                meetingDays );

            // Get the beginning and end dates
            var today = RockDateTime.Today.Date;
            var filterStart = filterStartDate.HasValue ? filterStartDate.Value : today;
            var monthStart = new DateTime( filterStart.Year, filterStart.Month, 1 );
            var rangeStart = monthStart.AddMonths( -1 );
            var rangeEnd = monthStart.AddMonths( monthsAhead );
            var beginDate = filterStartDate.HasValue ? filterStartDate.Value : filterStart;
            var endDate = filterEndDate.HasValue ? filterEndDate.Value : rangeEnd;

            endDate = endDate.AddDays( 1 ).AddMilliseconds( -1 );

            // Get the occurrences
            var occurrences = qry.SelectMany( m => m.EventItem.EventItemOccurrences ).ToList();
            var occurrencesWithDates = occurrences
                .Select( o =>
                {
                    var eventOccurrenceDate = new EventOccurrenceDate
                    {
                        EventItemOccurrence = o

                    };

                    if ( o.Schedule != null )
                    {
                        eventOccurrenceDate.ScheduleOccurrences = o.Schedule.GetICalOccurrences( beginDate, endDate ).ToList();
                    }
                    else
                    {
                        eventOccurrenceDate.ScheduleOccurrences = new List<Occurrence>();
                    }

                    return eventOccurrenceDate;
                } )
                .Where( d => d.ScheduleOccurrences.Any() )
                .ToList();

            var calendarEventDates = new List<DateTime>();

            var meetingDayIntegerList = meetingDays.SplitDelimitedValues().AsIntegerList();
            var meetingDayList = meetingDayIntegerList.Where( i => i >= 0 && i <= 6 ).Select( i => i.ToString().ConvertToEnum<DayOfWeek>() ).ToList();

            var eventOccurrenceSummaries = new List<EventOccurrenceSummary>();
            foreach ( var occurrenceDates in occurrencesWithDates )
            {
                var eventItemOccurrence = occurrenceDates.EventItemOccurrence;
                var eventDates = new List<EventDate>();
                foreach ( var scheduleOccurrence in occurrenceDates.ScheduleOccurrences )
                {
                    var datetime = scheduleOccurrence.Period.StartTime.Value;
                    var occurrenceEndTime = scheduleOccurrence.Period.EndTime;

                    if ( datetime >= beginDate && datetime < endDate )
                    {
                        // Filter by Meeting Day
                        if ( !meetingDayList.Any() || meetingDayList.Contains( datetime.DayOfWeek ) )
                        {
                            var eventDate = new EventDate();
                            eventDate.StartDateTime = datetime;
                            if ( occurrenceEndTime != null )
                            {
                                eventDate.EndDateTime = occurrenceEndTime.Value;
                            }
                            eventDates.Add( eventDate );
                        }
                    }
                }

                if ( eventDates.Any() )
                {
                    var eventOccurrenceSummary = new EventOccurrenceSummary
                    {
                        EventItemOccurrenceId = eventItemOccurrence.Id,
                        EventItemId = eventItemOccurrence.EventItem.Id,
                        Schedule = eventItemOccurrence.Schedule,
                        Name = eventItemOccurrence.EventItem.Name,
                        EventDates = eventDates,
                        Campus = eventItemOccurrence.Campus != null ? eventItemOccurrence.Campus.Name : "All Campuses",
                        CampusId = eventItemOccurrence.CampusId,
                        Location = eventItemOccurrence.Campus != null ? eventItemOccurrence.Campus.Name : "All Campuses",
                        LocationDescription = eventItemOccurrence.Location,
                        Description = eventItemOccurrence.EventItem.Description,
                        Summary = eventItemOccurrence.EventItem.Summary,
                        OccurrenceNote = eventItemOccurrence.Note.SanitizeHtml(),
                        DetailPage = string.IsNullOrWhiteSpace( eventItemOccurrence.EventItem.DetailsUrl ) ? null : eventItemOccurrence.EventItem.DetailsUrl,
                        FriendlyScheduleText = eventItemOccurrence.Schedule == null ? null : eventItemOccurrence.Schedule.FriendlyScheduleText,
                        Image = ( eventItemOccurrence.EventItem.Photo == null ) ? null : eventItemOccurrence.EventItem.Photo
                    };

                    SetLinkages( rockContext, eventItemOccurrence, eventOccurrenceSummary );

                    GetTopics( eventItemOccurrence, eventOccurrenceSummary );

                    GetOptionalAttributes( eventItemOccurrence, eventOccurrenceSummary, optionalFilterAttributeKey );

                    GetSecondaryCategories( eventItemOccurrence, eventOccurrenceSummary, secondaryCategoryAttributeKey );

                    GetAudiences( eventItemOccurrence, eventOccurrenceSummary );

                    eventOccurrenceSummaries.Add( eventOccurrenceSummary );
                }
            }

            var eventSummaries = eventOccurrenceSummaries
                .OrderBy( e => e.EventDates.OrderBy( ed => ed.StartDateTime ).Select( ed => ed.StartDateTime ).First() )
                .GroupBy( e => e.Name )
                .Select( e => e.ToList() )
                .ToList();

            var eventOccurrenceSummaryQry = eventOccurrenceSummaries
                 .OrderBy( e => e.EventDates.OrderBy( ed => ed.StartDateTime ).Select( ed => ed.StartDateTime ).First() )
                 .ThenBy( e => e.Name )
                 .AsQueryable();

            var campusIdList = campusIds.SplitDelimitedValues().AsIntegerList();
            if ( campusIdList.Any() )
            {
                eventOccurrenceSummaryQry = eventOccurrenceSummaryQry.Where( e => !e.CampusId.HasValue || campusIdList.Contains( e.CampusId.Value ) );
            }

            return eventOccurrenceSummaryQry;
        }

        private static void GetAudiences( EventItemOccurrence eventItemOccurrence, EventOccurrenceSummary eventOccurrenceSummary )
        {
            eventOccurrenceSummary.Audiences = new List<DefinedValueCache>();
            var audienceGuids = eventItemOccurrence.EventItem.EventItemAudiences.Select( eia => eia.DefinedValue.Guid ).ToList();
            foreach ( var audienceGuid in audienceGuids )
            {
                eventOccurrenceSummary.Audiences.Add( DefinedValueCache.Get( audienceGuid ) );
            }
        }

        private static void GetTopics( EventItemOccurrence eventItemOccurrence, EventOccurrenceSummary eventOccurrenceSummary )
        {
            eventOccurrenceSummary.Topics = new List<DefinedValueCache>();
            eventItemOccurrence.EventItem.LoadAttributes();
            var topicGuids = eventItemOccurrence.EventItem.GetAttributeValue( "Topic" ).SplitDelimitedValues().AsGuidList();
            foreach ( var topicGuid in topicGuids )
            {
                eventOccurrenceSummary.Topics.Add( DefinedValueCache.Get( topicGuid ) );
            }
        }

        private static void GetSecondaryCategories( EventItemOccurrence eventItemOccurrence, EventOccurrenceSummary eventOccurrenceSummary, string attributeKey )
        {
            eventOccurrenceSummary.Topics = new List<DefinedValueCache>();
            if ( attributeKey.IsNotNullOrWhiteSpace() )
            {
                eventItemOccurrence.EventItem.LoadAttributes();
                var topicGuids = eventItemOccurrence.EventItem.GetAttributeValue( attributeKey ).SplitDelimitedValues().AsGuidList();
                foreach ( var topicGuid in topicGuids )
                {
                    eventOccurrenceSummary.SecondaryCategories.Add( DefinedValueCache.Get( topicGuid ) );
                }

            }
        }

        private static void GetOptionalAttributes( EventItemOccurrence eventItemOccurrence, EventOccurrenceSummary eventOccurrenceSummary, string attributeKey )
        {
            eventOccurrenceSummary.Topics = new List<DefinedValueCache>();
            if ( attributeKey.IsNotNullOrWhiteSpace() )
            {
                eventItemOccurrence.EventItem.LoadAttributes();
                var topicGuids = eventItemOccurrence.EventItem.GetAttributeValue( attributeKey ).SplitDelimitedValues().AsGuidList();
                foreach ( var topicGuid in topicGuids )
                {
                    eventOccurrenceSummary.OptionalAttributes.Add( DefinedValueCache.Get( topicGuid ) );
                }
            }
        }

        private static void SetLinkages( RockContext rockContext, EventItemOccurrence eventItemOccurrence, EventOccurrenceSummary eventOccurrenceSummary )
        {
            eventOccurrenceSummary.Linkages = new List<LinkageSummary>();
            var eventItemOccurrenceLinkagesCount = eventItemOccurrence.Linkages.Count;
            foreach ( var linkage in eventItemOccurrence.Linkages )
            {
                var linkageSummary = new LinkageSummary();
                var isActive = false;
                var registrationUrl = "";

                DateTime nowDate = RockDateTime.Now;
                double? daysTillStartDate = null;
                if ( linkage.RegistrationInstance != null && linkage.RegistrationInstance.StartDateTime != null )
                {
                    var startDate = linkage.RegistrationInstance.StartDateTime.Value;
                    daysTillStartDate = ( startDate - nowDate ).TotalDays;
                }

                double? daysTillEndDate = null;
                if ( linkage.RegistrationInstance != null && linkage.RegistrationInstance.EndDateTime != null )
                {
                    var linkageEndDate = linkage.RegistrationInstance.EndDateTime.Value;
                    daysTillEndDate = ( linkageEndDate - nowDate ).TotalDays;
                }

                var showRegistration = true;
                var registrationMessage = "";

                if ( daysTillStartDate.HasValue && daysTillStartDate > 0 )
                {
                    showRegistration = false;
                    if ( eventItemOccurrenceLinkagesCount == 1 )
                    {
                        registrationMessage = String.Format( "Registration opens on {0}",
                            linkage.RegistrationInstance.StartDateTime.Value.ToString( "dddd, MMMM d, yyyy" ) );
                    }
                    else
                    {
                        registrationMessage = String.Format( "Registration for {0} opens on {1}"
                            , linkage.PublicName
                            , linkage.RegistrationInstance.StartDateTime.Value.ToString( "dddd, MMMM d, yyyy" ) );
                    }
                }

                if ( daysTillEndDate.HasValue && daysTillEndDate < 0 )
                {
                    showRegistration = false;
                    if ( eventItemOccurrenceLinkagesCount == 1 )
                    {
                        registrationMessage = String.Format( "Registration closed on {0}",
                            linkage.RegistrationInstance.EndDateTime.Value.ToString( "dddd, MMMM d, yyyy" ) );
                    }
                    else
                    {
                        registrationMessage = String.Format( "Registration for {0} closed on {1}"
                            , linkage.PublicName
                            , linkage.RegistrationInstance.EndDateTime.Value.ToString( "dddd, MMMM d, yyyy" ) );
                    }
                }

                if ( showRegistration == true )
                {
                    var registrationInstance = linkage.RegistrationInstance;
                    int? maxRegistrantCount = null;
                    var currentRegistrationCount = 0;

                    if ( registrationInstance != null )
                    {
                        maxRegistrantCount = registrationInstance.MaxAttendees;
                    }

                    int? registrationSpotsAvailable = null;
                    if ( maxRegistrantCount.HasValue )
                    {
                        currentRegistrationCount = new RegistrationRegistrantService( rockContext ).Queryable().AsNoTracking()
                                                        .Where( r =>
                                                            r.Registration.RegistrationInstanceId == registrationInstance.Id
                                                            && r.OnWaitList == false )
                                                        .Count();
                        registrationSpotsAvailable = maxRegistrantCount - currentRegistrationCount;
                    }

                    string registrationStatusLabel = "Register";

                    if ( registrationSpotsAvailable.HasValue && registrationSpotsAvailable.Value < 1 )
                    {
                        if ( registrationInstance.RegistrationTemplate.WaitListEnabled )
                        {
                            registrationStatusLabel = "Join Wait List";
                        }
                        else
                        {
                            registrationStatusLabel = "Full";
                        }
                    }

                    if ( eventItemOccurrenceLinkagesCount == 1 )
                    {
                        registrationMessage = registrationStatusLabel;
                    }
                    else
                    {
                        registrationMessage = string.Format( "{0} for {1}", registrationStatusLabel, linkage.PublicName );
                    }

                    if ( registrationStatusLabel == "Full" )
                    {
                        if ( eventItemOccurrenceLinkagesCount == 1 )
                        {
                            registrationMessage = "Registration Full";
                        }
                        else
                        {
                            registrationMessage = string.Format( "{0}  (Registration Full)", linkage.PublicName );
                        }
                    }
                    else
                    {
                        isActive = true;
                        if ( linkage.UrlSlug != "" )
                        {
                            registrationUrl = String.Format( "?RegistrationInstanceId={0}&Slug={1}"
                                 , linkage.RegistrationInstanceId
                                 , linkage.UrlSlug );
                        }
                        else
                        {
                            registrationUrl = String.Format( "?RegistrationInstanceId={0}&EventOccurrenceID={1}"
                                , linkage.RegistrationInstanceId
                                , linkage.EventItemOccurrenceId
                            );
                        }
                    }
                }

                linkageSummary.RegistrationUrl = registrationUrl;
                linkageSummary.IsActive = isActive;
                linkageSummary.Message = registrationMessage;

                eventOccurrenceSummary.Linkages.Add( linkageSummary );
            }
        }

        private static IQueryable<EventCalendarItem> FilterNonDateItems( string calendarIds,
            string campusIds,
            string categoryIds,
            string topicIds,
            string keywords,
            RockContext rockContext,
            string optionalFilterAttributeKey = null,
            string optionalFilterIds = null,
            string secondaryCategoryAttributeKey = null,
            string secondaryCategoryFilterIds = null,
            string meetingDays = "" )
        {
            var definedValueService = new DefinedValueService( rockContext );

            var calendarIdList = calendarIds.SplitDelimitedValues().AsIntegerList();
            var campusIdList = campusIds.SplitDelimitedValues().AsIntegerList();
            var categoryIdList = categoryIds.SplitDelimitedValues().AsIntegerList();
            var topicIdList = topicIds.SplitDelimitedValues().AsIntegerList();

            var eventCalendarItemService = new EventCalendarItemService( rockContext );

            // Grab events
            var qry = eventCalendarItemService
                 .Queryable( "EventCalendar,EventItem.EventItemAudiences,EventItem.EventItemOccurrences.Schedule" )
                 .Where( m =>
                     m.EventItem != null &&
                     calendarIdList.Contains( m.EventCalendarId ) &&
                        m.EventItem.IsActive &&
                        m.EventItem.IsApproved );

            // Filter by campus
            if ( campusIdList.Any() )
            {
                qry = qry
                    .Where( i =>
                        i.EventItem.EventItemOccurrences
                            .Any( c =>
                                !c.CampusId.HasValue ||
                                campusIdList.Contains( c.CampusId.Value ) ) );
            }

            // Filter by Category
            if ( categoryIdList.Any() )
            {
                qry = qry.Where( i => i.EventItem.EventItemAudiences
                    .Any( c => categoryIdList.Contains( c.DefinedValueId ) ) );
            }


            // Filter by Topic
            if ( topicIdList.Any() )
            {
                var topicList = new DefinedValueService( rockContext ).GetByIds( topicIdList ).Select( c => c.Guid.ToString().ToUpper() ).ToList();
                qry = qry.WhereAttributeValue( rockContext, av => av.Attribute.Key == "Topic" && topicList.Any( c => av.Value.ToUpper().Contains( c ) ) );
            }

            // Filter by Keyword
            if ( keywords.IsNotNullOrWhiteSpace() )
            {
                qry = qry.Where( i => i.EventItem.Name.Contains( keywords ) || i.EventItem.Description.Contains( keywords ) );
            }

            // Filter by optional attribute
            if ( optionalFilterAttributeKey.IsNotNullOrWhiteSpace() && optionalFilterIds.IsNotNullOrWhiteSpace() )
            {
                var optionalFilterIdList = optionalFilterIds.SplitDelimitedValues().AsIntegerList();
                if ( optionalFilterIdList.Any() )
                {
                    var optionalFilterList = definedValueService.GetByIds( optionalFilterIdList ).Select( c => c.Guid.ToString().ToUpper() ).ToList();
                    qry = qry.WhereAttributeValue( rockContext, av => av.Attribute.Key == optionalFilterAttributeKey && optionalFilterList.Any( c => av.Value.ToUpper().Contains( c ) ) );
                }
            }

            // Filter by secondary category
            if ( secondaryCategoryAttributeKey.IsNotNullOrWhiteSpace() && secondaryCategoryFilterIds.IsNotNullOrWhiteSpace() )
            {
                var secondaryCategoryIdList = secondaryCategoryFilterIds.SplitDelimitedValues().AsIntegerList();
                if ( secondaryCategoryIdList.Any() )
                {
                    var secondaryCategoryList = definedValueService.GetByIds( secondaryCategoryIdList ).Select( c => c.Guid.ToString().ToUpper() ).ToList();
                    qry = qry.WhereAttributeValue( rockContext, av => av.Attribute.Key == secondaryCategoryAttributeKey && secondaryCategoryList.Any( c => av.Value.ToUpper().Contains( c ) ) );
                }
            }

            return qry;
        }

    }

    #region Helper Classes
    public class EventSummary
    {
        public EventItemOccurrence EventItemOccurrence { get; set; }

        public string Name { get; set; }

        public string ICalContent { get; set; }

        public string Campus { get; set; }

        public string Location { get; set; }

        public string LocationDescription { get; set; }

        public string Summary { get; set; }

        public string Description { get; set; }

        public string OccurrenceNote { get; set; }

        public string DetailPage { get; set; }

        public string FriendlyScheduleText { get; set; }
        public string ImageUrl { get; set; }

        public List<LinkageSummary> Linkages { get; set; }
    }

    /// <summary>
    /// A class to store event item occurrence data for liquid
    /// </summary>
    [DotLiquid.LiquidType( "EventItemOccurrence", "DateTime", "Name", "Date", "Time", "EndDate", "EndTime", "Campus", "Location", "LocationDescription", "Description", "Summary", "OccurrenceNote", "DetailPage" )]
    public class EventOccurrenceSummary
    {
        public int? EventItemOccurrenceId { get; set; }
        public int? EventItemId { get; set; }

        public string Name { get; set; }

        public string Campus { get; set; }

        public int? CampusId { get; set; }

        public string Location { get; set; }

        public string LocationDescription { get; set; }

        public string Summary { get; set; }

        public string Description { get; set; }

        public string OccurrenceNote { get; set; }

        public string DetailPage { get; set; }

        public string FriendlyScheduleText { get; set; }

        public BinaryFile Image { get; set; }

        public Schedule Schedule { get; set; }

        public List<EventDate> EventDates { get; set; }

        public DateTime DateTime { get; set; }

        public string Date { get; set; }

        public string Time { get; set; }

        public string EndDate { get; set; }

        public string EndTime { get; set; }

        public List<LinkageSummary> Linkages { get; set; }

        public List<DefinedValueCache> Audiences { get; set; }

        public List<DefinedValueCache> Topics { get; set; }

        public List<DefinedValueCache> OptionalAttributes { get; set; }

        public List<DefinedValueCache> SecondaryCategories { get; set; }
    }



    /// <summary>
    /// A class to store the event item occurrences dates
    /// </summary>
    public class EventOccurrenceDate
    {
        public EventItemOccurrence EventItemOccurrence { get; set; }

        public List<Occurrence> ScheduleOccurrences { get; set; }
    }

    public class EventDate
    {
        public DateTime StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }
    }

    public class LinkageSummary
    {
        public string Message { get; set; }

        public string RegistrationUrl { get; set; }

        public bool IsActive { get; set; }
    }

    #endregion
}