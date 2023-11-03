using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlanningCenterSDK.Apps.Services.Model;
using PlanningCenterSDK.Http;
using Newtonsoft.Json;

namespace PlanningCenterSDK.Apps.Services.Endpoints
{
    public class ServiceTypeEndpoint : BaseEndpoint<ServiceType>
    {
        public ServiceTypeEndpoint( RateLimitedRequester requester ) : base( requester, "service_types/" ) { }
    }
}
