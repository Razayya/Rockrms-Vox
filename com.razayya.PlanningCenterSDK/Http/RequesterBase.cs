using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace com.razayya.PlanningCenterSDK.Http
{
    public abstract class RequesterBase
    {
        private HttpClient _httpClient;
        private string _secret;

        public string ApplicationId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestBase"/> class.
        /// </summary>
        /// <param name="applicationId">The Application Id</param>
        /// /// <param name="secret">The Secret</param>
        /// <exception cref="ArgumentNullException">applicationId</exception>
        /// <exception cref="ArgumentNullException">secret</exception>
        protected RequesterBase( string applicationId, string secret ) : this()
        {
            if ( string.IsNullOrWhiteSpace( applicationId ) )
            {
                throw new ArgumentNullException( nameof( applicationId ) );
            }

            if ( string.IsNullOrWhiteSpace( secret ) )
            {
                throw new ArgumentNullException( nameof( secret ) );
            }

            ApplicationId = applicationId;
            _secret = secret;
        }

        // <summary>
        /// Initializes a new instance of the <see cref="RequestBase"/> class.
        /// </summary>
        protected RequesterBase()
        {
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Send a <see cref="HttpRequestMessage"/> asynchronously.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="Exception">Thrown if an Http error occurs. Contains the Http error code and error message.</exception>
        protected async Task<HttpResponseMessage> SendAsync( HttpRequestMessage request )
        {
            var response = await _httpClient.SendAsync( request, HttpCompletionOption.ResponseHeadersRead ).ConfigureAwait( false );
            if ( !response.IsSuccessStatusCode )
            {
                HandleRequestFailure( response ); //Pass Response to get status code, Then Dispose Object.
            }
            return response;
        }

        protected HttpRequestMessage PrepareRequest( string relativeUrl, List<string> queryParameters,
            bool useHttps, HttpMethod httpMethod )
        {
            var scheme = useHttps ? "https" : "http";
            var url = queryParameters == null ?
                $"{scheme}://{relativeUrl}" :
                $"{scheme}://{relativeUrl}?{BuildArgumentsString( queryParameters )}";

            var requestMessage = new HttpRequestMessage( httpMethod, url );
            if ( !string.IsNullOrEmpty( ApplicationId ) && !string.IsNullOrEmpty(_secret) )
            {
                _httpClient.DefaultRequestHeaders.Authorization = GetAuthenticationHeader();
            }
            return requestMessage;
        }

        protected string BuildArgumentsString( List<string> arguments )
        {
            return arguments
                .Where( arg => !string.IsNullOrWhiteSpace( arg ) )
                .Aggregate( string.Empty, ( current, arg ) => current + ( "&" + arg ) );
        }

        protected void HandleRequestFailure( HttpResponseMessage response )
        {
            try
            {
                if ( response.StatusCode == ( HttpStatusCode ) 429 )
                {
                    var retryAfter = TimeSpan.Zero;
                    if ( response.Headers.TryGetValues( "Retry-After", out var retryAfterHeaderValues ) )
                    {
                        if ( int.TryParse( retryAfterHeaderValues.FirstOrDefault(), out var seconds ) )
                        {
                            retryAfter = TimeSpan.FromSeconds( seconds );
                        }
                    }

                    string rateLimitType = null;
                    if ( response.Headers.TryGetValues( "X-Rate-Limit-Type", out var rateLimitTypeHeaderValues ) )
                    {
                        rateLimitType = rateLimitTypeHeaderValues.FirstOrDefault();
                    }
                    throw new Exception( $"{response.StatusCode}: Rate Limit Exceeded" );
                }
                else
                    throw new Exception( $"{response.StatusCode}: Unexpected Error" );
            }
            finally
            {
                response.Dispose(); //Dispose Response On Error
            }
        }

        private AuthenticationHeaderValue GetAuthenticationHeader()
        {
            var byteArray = new UTF8Encoding().GetBytes( $"{ApplicationId}:{_secret}" );
            return new AuthenticationHeaderValue( "Basic", Convert.ToBase64String( byteArray ) );
        }

        protected async Task<string> GetResponseContentAsync( HttpResponseMessage response )
        {
            using ( response )
            using ( var content = response.Content )
            {
                return await content.ReadAsStringAsync().ConfigureAwait( false );
            }
        }
    }
}
