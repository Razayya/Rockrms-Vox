using System;
using System.Collections.Generic;
using PlanningCenterSDK.Apps.People.Endpoints;
using PlanningCenterSDK.Http;

namespace PlanningCenterSDK.Apps.People
{
    public class PeopleApp
    {
        private static PeopleApp _instance;

        public PersonEndpoint Person { get; }
        public NamePrefixEndpoint NamePrefix { get; }
        public NameSuffixEndpoint NameSuffix { get; }
        public MaritalStatusEndpoint MaritalStatus { get; }
        public AppEndpoint App { get; }
        public PhoneNumberEndpoint PhoneNumber { get; }
        public EmailEndpoint Email { get; }

        public static PeopleApp GetInstance( string applicationId, string secret, int rateLimitPer1s = 5,  int rateLimitPer2m = 600 )
        {
            return GetInstance( applicationId, secret, new Dictionary<TimeSpan, int>
            {
                [TimeSpan.FromSeconds( 1 )] = rateLimitPer1s,
                [TimeSpan.FromMinutes( 2 )] = rateLimitPer2m
            } );
        }

        public static PeopleApp GetInstance( string applicationId, string secret, IDictionary<TimeSpan, int> rateLimits )
        {
            if( _instance == null
                || Requesters.ServicesAppRequester == null
                || applicationId != Requesters.ServicesAppRequester.ApplicationId
                || !rateLimits.Equals(Requesters.ServicesAppRequester.RateLimits ) )
            {
                _instance = new PeopleApp( applicationId, secret, rateLimits );
            }
            return _instance;
        }

        private PeopleApp( string applicationId, string secret, IDictionary<TimeSpan, int> rateLimits )
        {
            Requesters.PeopleAppRequester = new RateLimitedRequester( applicationId, secret, rateLimits );
            Requesters.StaticApiRequester = new Requester( applicationId, secret );
            var requester = Requesters.PeopleAppRequester;

            Person = new PersonEndpoint( requester );
            NameSuffix = new NameSuffixEndpoint( requester );
            NamePrefix = new NamePrefixEndpoint( requester );
            MaritalStatus = new MaritalStatusEndpoint( requester );
            App = new AppEndpoint( requester );
            Email = new EmailEndpoint( requester );
            PhoneNumber = new PhoneNumberEndpoint( requester );
        }
    }
}