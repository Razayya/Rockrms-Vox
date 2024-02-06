using PlanningCenterSDK.Http;
using PlanningCenterSDK.Apps.People.Model;

namespace PlanningCenterSDK.Apps.People.Endpoints
{
    public class PersonAppEndpoint : PeopleEndpoint<PersonApp>
    {
        public PersonAppEndpoint( RateLimitedRequester requester, string relativePath ) : base( requester, relativePath )
        {
        }
    }
}