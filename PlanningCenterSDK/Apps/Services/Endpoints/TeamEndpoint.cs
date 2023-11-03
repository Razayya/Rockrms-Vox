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

namespace PlanningCenterSDK.Apps.Services.Endpoints
{
    public class TeamEndpoint : BaseEndpoint<Team>
    {
        public TeamEndpoint( RateLimitedRequester requester ) : base( requester, "teams/" ) { }

    }
}
