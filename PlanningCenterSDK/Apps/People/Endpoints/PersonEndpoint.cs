using PlanningCenterSDK.Http;
using PlanningCenterSDK.Apps.People.Model;

namespace PlanningCenterSDK.Apps.People.Endpoints
{
    public class PersonEndpoint : PeopleEndpoint<Person>
    {
        const string PATH = "people";
        public PersonEndpoint( RateLimitedRequester requester ) : base( requester, PATH ) { }

        public EmailEndpoint Email( int personId )
        {
            string relativePath = $"{PATH}/{personId}/emails";
            return new EmailEndpoint( _requester, relativePath );
        }

        public PhoneNumberEndpoint PhoneNumber( int personId )
        {
            string relativePath = $"{PATH}/{personId}/phone_numbers";
            return new PhoneNumberEndpoint( _requester, relativePath );
        }
        public PersonAppEndpoint PersonApp( int personId )
        {
            string relativePath = $"{PATH}/{personId}/person_apps";
            return new PersonAppEndpoint( _requester, relativePath );
        }
    }
}
