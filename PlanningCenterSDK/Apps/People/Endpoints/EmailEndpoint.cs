using PlanningCenterSDK.Http;
using PlanningCenterSDK.Apps.People.Model;

namespace PlanningCenterSDK.Apps.People.Endpoints
{
    public class EmailEndpoint : PeopleEndpoint<Email>
    {
        public EmailEndpoint( RateLimitedRequester requester, string relativePath = "emails" ) : base( requester, relativePath )
        {
        }
    }
}