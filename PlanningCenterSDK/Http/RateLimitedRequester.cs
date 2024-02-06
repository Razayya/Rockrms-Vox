using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using PlanningCenterSDK.Misc;

namespace PlanningCenterSDK.Http
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

        public Task<string> CreateGetRequestAsync( string relativeUrl, Dictionary<string,string> queryParameters = null,
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

        public Task<string> CreatePostRequestAsync( string relativeUrl, string body,
            Dictionary<string, string> queryParameters = null, bool useHttps = true )
        {
            var request = PrepareRequest( relativeUrl, queryParameters, useHttps, HttpMethod.Post );
            request.Content = new StringContent( body, Encoding.UTF8, "application/json" );

            return GetRateLimitedResponseContentAsync( request );
        }

        public Task<string> CreatePatchRequestAsync( string relativeUrl, string body,
            Dictionary<string, string> queryParameters = null, bool useHttps = true )
        {
            var request = PrepareRequest( relativeUrl, queryParameters, useHttps, new HttpMethod("PATCH") );
            request.Content = new StringContent( body, Encoding.UTF8, "application/json" );

            return GetRateLimitedResponseContentAsync( request );
        }

        public async Task<bool> CreateDeleteRequestAsync( string relativeUrl,
            Dictionary<string, string> queryParameters = null, bool useHttps = true )
        {
            var request = PrepareRequest( relativeUrl, queryParameters, useHttps, HttpMethod.Delete );

            await _rateLimiter.HandleRateLimitAsync().ConfigureAwait( false );

            using (var response = await SendAsync( request ).ConfigureAwait( false ))
            {
                // A 204 status code indicates success, so return true.
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public async Task<string> CreateFileUploadRequestAsync( string relativeUrl, Stream fileStream, string fileName, Dictionary<string, string> queryParameters = null, bool useHttps = true )
        {
            if (fileStream == null)
                throw new ArgumentNullException( nameof( fileStream ) );
            if (string.IsNullOrEmpty( fileName ))
                throw new ArgumentException( "File name must be provided.", nameof( fileName ) );

            var request = PrepareRequest( relativeUrl, queryParameters, useHttps, HttpMethod.Post, Constants.FILE_UPLOAD_URL );

            // Prepare multipart/form-data content
            using (var content = new MultipartFormDataContent())
            {
                // Add file content
                var fileContent = new StreamContent( fileStream );
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse( "multipart/form-data" );
                content.Add( fileContent, "file", fileName );

                request.Content = content;

                await _rateLimiter.HandleRateLimitAsync().ConfigureAwait( false );

                using (var response = await SendAsync( request ).ConfigureAwait( false ))
                {
                    return await GetResponseContentAsync( response ).ConfigureAwait( false );
                }
            }
        }
    }
}
