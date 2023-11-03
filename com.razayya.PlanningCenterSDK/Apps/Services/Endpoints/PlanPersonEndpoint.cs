using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.razayya.PlanningCenterSDK.Apps.Services.Model;
using com.razayya.PlanningCenterSDK.Http;
using Newtonsoft.Json;

namespace com.razayya.PlanningCenterSDK.Apps.Services.Endpoints
{
    public class PlanPersonEndpoint
    {
        public const string RootUrl = "/people/{0}/plan_people";

        private readonly RateLimitedRequester _requester;

        public PlanPersonEndpoint(RateLimitedRequester requester)
        {
            _requester = requester;
        }

        public async Task<PlanPerson> GetPlanPersonByIdAsync(int personId, int id)
        {
            var json = await _requester.CreateGetRequestAsync(string.Format(RootUrl, personId) + string.Format("{0}", id)).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<PlanPerson>(json);
        }

        public async Task<IEnumerable<PlanPerson>> GetPlanPersonsAsync(int personId, int offset = 0, int perPage = 25)
        {
            var json = await _requester.CreateGetRequestAsync(
                string.Format(RootUrl, personId),
                new List<string> { string.Format("offset={0}", offset), string.Format("per_page={0}", perPage) }).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<IEnumerable<PlanPerson>>(json);
        }
    }
}
