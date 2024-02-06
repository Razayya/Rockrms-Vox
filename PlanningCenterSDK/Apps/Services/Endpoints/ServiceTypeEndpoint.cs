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
    public class ServiceTypeEndpoint : ServicesEndpoint<ServiceType>
    {
        const string PATH = "service_types";

        public ServiceTypeEndpoint( RateLimitedRequester requester ) : base( requester, PATH ) { }

        public PersonTeamPositionAssignmentEndpoint PersonTeamPositionAssignment( int serviceTypeId, int teamPositionId )
        {
            string relativePath = $"{PATH}/{serviceTypeId}/team_positions/{teamPositionId}/person_team_position_assignments";
            return new PersonTeamPositionAssignmentEndpoint( _requester, relativePath );
        }
    }
}
