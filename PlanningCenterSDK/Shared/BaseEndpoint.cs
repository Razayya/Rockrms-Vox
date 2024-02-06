using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JsonApiSerializer;
using Newtonsoft.Json;
using PlanningCenterSDK.Http;

namespace PlanningCenterSDK.Shared
{
    public class BaseEndpoint<T>
    {
        internal readonly RateLimitedRequester _requester;
        internal string _url;

        public BaseEndpoint( RateLimitedRequester requester, string url )
        {
            _requester = requester;
            _url = url;
        }

        public virtual async Task<T> GetByIdAsync( int id, Dictionary<string, string> parameters = null )
        {
            var json = await _requester.CreateGetRequestAsync( _url + string.Format( "{0}", id ), parameters ).ConfigureAwait( false );
            return JsonConvert.DeserializeObject<T>( json, new JsonApiSerializerSettings() );
        }

        public virtual async Task<List<T>> GetAsync( int offset = 0, int perPage = 25, Dictionary<string, string> parameters = null )
        {

            if (parameters == null)
                parameters = new Dictionary<string, string>();

            if (parameters.ContainsKey( "offset" ))
            {
                parameters["offset"] = offset.ToString();
            }
            else
            {
                parameters.Add( "offset", offset.ToString() );
            }

            if (parameters.ContainsKey( "per_page" ))
            {
                parameters["per_page"] = perPage.ToString();
            }
            else
            {
                parameters.Add( "per_page", perPage.ToString() );
            }

            var json = await _requester.CreateGetRequestAsync(
                _url,
                parameters ).ConfigureAwait( false );
            return JsonConvert.DeserializeObject<List<T>>( json, new JsonApiSerializerSettings() );
        }

        public virtual async Task<List<T>> GetAllAsync( Dictionary<string, string> parameters = null )
        {
            int offset = 0;
            int perPage = 100;
            var allItems = new List<T>();

            while (true)
            {
                var items = await GetAsync( offset, perPage, parameters ).ConfigureAwait( false );

                if (items == null || !items.Any())
                {
                    return allItems;
                }
                allItems.AddRange( items );

                offset += perPage;
            }

        }

        public virtual async Task<T> UpdateAsync( T entity, string Id )
        {
            var json = await _requester.CreatePatchRequestAsync( $"{_url}/{Id}", JsonConvert.SerializeObject( entity, new JsonApiSerializerSettings() ) ).ConfigureAwait( false );
            return JsonConvert.DeserializeObject<T>( json, new JsonApiSerializerSettings() );
        }

        public virtual async Task<T> CreateAsync( T entity )
        {
            var json = await _requester.CreatePostRequestAsync( _url, JsonConvert.SerializeObject( entity, new JsonApiSerializerSettings() ) ).ConfigureAwait( false );
            return JsonConvert.DeserializeObject<T>( json, new JsonApiSerializerSettings() );
        }

        public virtual Task<bool> DeleteAsync( string Id )
        {
            return _requester.CreateDeleteRequestAsync( $"{_url}/{Id}" );
        }
    }
}
