using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlanningCenterSDK.Apps.Services.Model;
using PlanningCenterSDK.Http;

namespace PlanningCenterSDK.Apps.Services.Endpoints
{
    public class PersonTeamPositionAssignmentEndpoint : ServicesEndpoint<PersonTeamPositionAssignment>
    {
        public PersonTeamPositionAssignmentEndpoint( RateLimitedRequester requester, string relativePath ) : base( requester, relativePath )
        {
        }
    }
}