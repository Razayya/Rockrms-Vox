using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.razayya.PlanningCenterSDK.Http;
using com.razayya.PlanningCenterSDK.Apps.Services.Model;
using Newtonsoft.Json;

namespace com.razayya.PlanningCenterSDK.Apps.Services.Endpoints
{
    public class PersonEndpoint
    {
        public const string PersonRootUrl = "/services/v2/people";

        private readonly RateLimitedRequester _requester;

        public PersonEndpoint( RateLimitedRequester requester)
        {
            _requester = requester;
        }

        public async Task<Person> GetPersonByIdAsync(int id)
        {
            var json = await _requester.CreateGetRequestAsync(PersonRootUrl + string.Format("{0}", id)).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<Person>(json);
        }

        public async Task<Person> GetPersonByLegacyIdAsync(int legacyId)
        {
            var json = await _requester.CreateGetRequestAsync(
                PersonRootUrl,
                new List<string> { string.Format("where[legacy_id]={0}", legacyId) }).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<Person>(json);
        }

        public async Task<IEnumerable<Person>> GetPeopleAsync(int offset = 0, int perPage = 25)
        {
            var json = await _requester.CreateGetRequestAsync(
                PersonRootUrl,
                new List<string> { string.Format("offset={0}", offset), string.Format("per_page={0}", perPage) }).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<IEnumerable<Person>>(json);
        }
    }
}
