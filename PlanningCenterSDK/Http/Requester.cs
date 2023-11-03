using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PlanningCenterSDK.Http
{
    public class Requester : RequesterBase
    {
        public Requester( string applicationId, string secret ) : base( applicationId, secret ) { }

        public async Task<string> CreateGetRequestAsync( string relativeUrl, Dictionary<string,string>queryParameters = null, bool useHttps = true )
        {
            var request = PrepareRequest( relativeUrl, queryParameters, useHttps, HttpMethod.Get );
            var response = await SendAsync( request ).ConfigureAwait( false );
            return await GetResponseContentAsync( response ).ConfigureAwait( false );
        }
    }
}
