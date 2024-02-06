using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlanningCenterSDK.Apps.Services.Model;
using PlanningCenterSDK.Http;
using Newtonsoft.Json;
using JsonApiSerializer;
using JsonApiSerializer.JsonApi;
using PlanningCenterSDK.Shared;

namespace PlanningCenterSDK.Apps.People.Endpoints
{
    public class PeopleEndpoint<T> : BaseEndpoint<T>
    {
        private const string RootUrl = "people/v2";

        public PeopleEndpoint( RateLimitedRequester requester, string relativePath ) : base( requester, $"{RootUrl}/{relativePath}" ) { }
    }
}
