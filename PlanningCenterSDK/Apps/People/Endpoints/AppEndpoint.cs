using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlanningCenterSDK.Apps.People.Model;
using PlanningCenterSDK.Http;

namespace PlanningCenterSDK.Apps.People.Endpoints
{
    public class AppEndpoint : PeopleEndpoint<App>
    {
        public AppEndpoint( RateLimitedRequester requester ) : base( requester, "apps" )
        {
        }
    }
}
