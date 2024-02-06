using System;
using System.Collections.Generic;
using PlanningCenterSDK.Apps.Files.Endpoints;
using PlanningCenterSDK.Apps.People.Endpoints;
using PlanningCenterSDK.Http;

namespace PlanningCenterSDK.Apps.Files
{
    public class FilesApp
    {
        private static FilesApp _instance;

        public readonly FileUploadEndpoint File;

        public static FilesApp GetInstance( string applicationId, string secret, int rateLimitPer1s = 5,  int rateLimitPer2m = 600 )
        {
            return GetInstance( applicationId, secret, new Dictionary<TimeSpan, int>
            {
                [TimeSpan.FromSeconds( 1 )] = rateLimitPer1s,
                [TimeSpan.FromMinutes( 2 )] = rateLimitPer2m
            } );
        }

        public static FilesApp GetInstance( string applicationId, string secret, IDictionary<TimeSpan, int> rateLimits )
        {
            if( _instance == null
                || Requesters.ServicesAppRequester == null
                || applicationId != Requesters.ServicesAppRequester.ApplicationId
                || !rateLimits.Equals(Requesters.ServicesAppRequester.RateLimits ) )
            {
                _instance = new FilesApp( applicationId, secret, rateLimits );
            }
            return _instance;
        }

        private FilesApp( string applicationId, string secret, IDictionary<TimeSpan, int> rateLimits )
        {
            Requesters.FilesAppRequester = new RateLimitedRequester( applicationId, secret, rateLimits );
            Requesters.StaticApiRequester = new Requester( applicationId, secret );
            var requester = Requesters.FilesAppRequester;

            File = new FileUploadEndpoint( requester );
            
        }
    }
}