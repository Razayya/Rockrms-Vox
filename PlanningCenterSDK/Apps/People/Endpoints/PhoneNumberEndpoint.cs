using PlanningCenterSDK.Http;
using PlanningCenterSDK.Apps.People.Model;

namespace PlanningCenterSDK.Apps.People.Endpoints
{
    public class PhoneNumberEndpoint : PeopleEndpoint<PhoneNumber>
    {
        public PhoneNumberEndpoint( RateLimitedRequester requester, string relativePath = "phone_numbers" ) : base( requester, relativePath )
        {
        }
    }
}