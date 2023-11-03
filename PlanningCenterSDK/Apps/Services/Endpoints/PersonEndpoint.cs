using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlanningCenterSDK.Http;
using PlanningCenterSDK.Apps.Services.Model;
using Newtonsoft.Json;

namespace PlanningCenterSDK.Apps.Services.Endpoints
{
    public class PersonEndpoint : BaseEndpoint<Person>
    {
        public PersonEndpoint( RateLimitedRequester requester ) : base( requester, "people/" ) { }
    }
}
