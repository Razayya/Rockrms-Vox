using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlanningCenterSDK.Apps.Services.Model;
using PlanningCenterSDK.Http;
using Newtonsoft.Json;
using JsonApiSerializer;
using JsonApiSerializer.JsonApi;

namespace PlanningCenterSDK.Apps.Services.Endpoints
{
    public class BaseEndpoint<T>
    {
        private const string RootUrl = "/services/v2/";

        internal readonly RateLimitedRequester _requester;
        internal string _url;

        public BaseEndpoint(RateLimitedRequester requester, string relativePath )
        {
            _requester = requester;
            _url = RootUrl + relativePath;
        }

        public virtual async Task<T> GetByIdAsync(int id, Dictionary<string, string> parameters = null )
        {
            var json = await _requester.CreateGetRequestAsync( _url + string.Format("{0}", id), parameters).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<T>(json, new JsonApiSerializerSettings() );
        }

        public virtual async Task<List<T>> GetAsync(int offset = 0, int perPage = 25, Dictionary<string,string> parameters = null)
        {

            if ( parameters == null )
                parameters = new Dictionary<string, string>();

            if( parameters.ContainsKey("offset") )
            {
                parameters["offset"] = offset.ToString();
            }
            else
            {
                parameters.Add( "offset", offset.ToString() );
            }

            if ( parameters.ContainsKey( "per_page" ) )
            {
                parameters["per_page"] = perPage.ToString();
            }
            else
            {
                parameters.Add( "per_page", perPage.ToString() );
            }

            var json = await _requester.CreateGetRequestAsync(
                _url,
                parameters ).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<List<T>>(json, new JsonApiSerializerSettings() );
        }

        public virtual async Task<List<T>> GetAllAsync( Dictionary<string, string> parameters = null )
        {
            int offset = 0;
            int perPage = 100;
            var allItems = new List<T>();

            while ( true )
            {
                var items = await GetAsync( offset, perPage, parameters ).ConfigureAwait( false );

                if( items == null || !items.Any() )
                {
                    return allItems;
                }
                allItems.AddRange( items );

                offset += offset + perPage;
            }

        }
    }
}
