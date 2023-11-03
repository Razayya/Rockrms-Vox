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
    public class TeamEndpoint
    {
        public const string RootUrl = "/services/v2/teams";

        private readonly RateLimitedRequester _requester;

        public TeamEndpoint(RateLimitedRequester requester)
        {
            _requester = requester;
        }

        public async Task<Team> GetTeamByIdAsync(int id)
        {
            var json = await _requester.CreateGetRequestAsync(RootUrl + string.Format("{0}", id)).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<Team>(json);
        }

        public async Task<IEnumerable<Team>> GetTeamsAsync(int offset = 0, int perPage = 25)
        {
            var json = await _requester.CreateGetRequestAsync(
                RootUrl,
                new List<string> { string.Format("offset={0}", offset), string.Format("per_page={0}", perPage) }).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<IEnumerable<Team>>(json);
        }
    }
}
