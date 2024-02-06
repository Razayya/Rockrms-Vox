
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlanningCenterSDK.Http;
using Newtonsoft.Json;
using JsonApiSerializer;
using PlanningCenterSDK.Shared;

namespace PlanningCenterSDK.Apps.Services.Endpoints
{
    public class ServicesEndpoint<T> : BaseEndpoint<T>
    {
        private const string RootUrl = "services/v2";

        public ServicesEndpoint(RateLimitedRequester requester, string relativePath ) : base( requester, $"{RootUrl}/{relativePath}" )
        {
        }
    }
}
