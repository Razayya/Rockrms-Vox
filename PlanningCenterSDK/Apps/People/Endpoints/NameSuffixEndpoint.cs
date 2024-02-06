using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlanningCenterSDK.Apps.People.Model;
using PlanningCenterSDK.Apps.Services.Endpoints;
using PlanningCenterSDK.Http;

namespace PlanningCenterSDK.Apps.People.Endpoints
{
    public class NameSuffixEndpoint : PeopleEndpoint<NameSuffix>
    {
        public NameSuffixEndpoint( RateLimitedRequester requester ) : base( requester, "name_suffixes" )
        {
        }
    }
}
