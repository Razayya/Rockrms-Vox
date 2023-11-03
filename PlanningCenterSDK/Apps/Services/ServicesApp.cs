using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlanningCenterSDK.Apps.Services.Endpoints;
using PlanningCenterSDK.Http;

namespace PlanningCenterSDK.Apps.Services
{
    public class ServicesApp
    {
        private static ServicesApp _instance;

        public PersonEndpoint Person { get; }
        public ServiceTypeEndpoint ServiceType { get; }
        public TeamEndpoint Team { get; }

        public static ServicesApp GetInstance( string applicationId, string secret, int rateLimitPer1s = 5,  int rateLimitPer2m = 600 )
        {
            return GetInstance( applicationId, secret, new Dictionary<TimeSpan, int>
            {
                [TimeSpan.FromSeconds( 1 )] = rateLimitPer1s,
                [TimeSpan.FromMinutes( 2 )] = rateLimitPer2m
            } );
        }

        public static ServicesApp GetInstance( string applicationId, string secret, IDictionary<TimeSpan, int> rateLimits )
        {
            if( _instance == null
                || Requesters.ServicesAppRequester == null
                || applicationId != Requesters.ServicesAppRequester.ApplicationId
                || !rateLimits.Equals(Requesters.ServicesAppRequester.RateLimits ) )
            {
                _instance = new ServicesApp( applicationId, secret, rateLimits );
            }
            return _instance;
        }

        private ServicesApp( string applicationId, string secret, IDictionary<TimeSpan, int> rateLimits )
        {
            Requesters.ServicesAppRequester = new RateLimitedRequester( applicationId, secret, rateLimits );
            Requesters.StaticApiRequester = new Requester( applicationId, secret );
            var requester = Requesters.ServicesAppRequester;

            Person = new PersonEndpoint( requester );
            ServiceType = new ServiceTypeEndpoint( requester );
            Team = new TeamEndpoint( requester );
        }
    }
}