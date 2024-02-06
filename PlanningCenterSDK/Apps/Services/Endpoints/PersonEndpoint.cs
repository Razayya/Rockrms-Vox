using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlanningCenterSDK.Http;
using PlanningCenterSDK.Apps.Services.Model;
using Newtonsoft.Json;
using System.IO;

namespace PlanningCenterSDK.Apps.Services.Endpoints
{
    public class PersonEndpoint : ServicesEndpoint<Person>
    {
        const string PATH = "people";

        public PersonEndpoint( RateLimitedRequester requester ) : base( requester, PATH ) { }

        public PersonTeamPositionAssignmentEndpoint PersonTeamPositionAssignment( int personId )
        {
            string relativePath = $"{PATH}/{personId}/person_team_position_assignments";
            return new PersonTeamPositionAssignmentEndpoint( _requester, relativePath );
        }
    }
}
