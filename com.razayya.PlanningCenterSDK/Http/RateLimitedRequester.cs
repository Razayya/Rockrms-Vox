using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.razayya.PlanningCenterSDK.Http
{
    public class RateLimitedRequester : RequesterBase
    {
        public readonly IDictionary<TimeSpan, int> RateLimits;
        private readonly bool _throwOnDelay;
        private readonly RateLimiter _rateLimiter;

        public RateLimitedRequester( string applicationId, string secret, IDictionary<TimeSpan, int> rateLimits, bool throwOnDelay = false ) : base( applicationId, secret )
        {
            RateLimits = rateLimits;
            _throwOnDelay = throwOnDelay;
            _rateLimiter = new RateLimiter( rateLimits, throwOnDelay );
        }

        public Task<string> CreateGetRequestAsync( string relativeUrl, List<string> queryParameters = null,
            bool useHttps = true )
        {
            var request = PrepareRequest( relativeUrl, queryParameters, useHttps, HttpMethod.Get );

            return GetRateLimitedResponseContentAsync( request );
        }

        private async Task<string> GetRateLimitedResponseContentAsync( HttpRequestMessage request )
        {
            await _rateLimiter.HandleRateLimitAsync().ConfigureAwait( false );

            using ( var response = await SendAsync( request ).ConfigureAwait( false ) )
            {
                return await GetResponseContentAsync( response ).ConfigureAwait( false );
            }
        }

    }
}
