using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JsonApiSerializer;
using Newtonsoft.Json;
using PlanningCenterSDK.Apps.Files.Model;
using PlanningCenterSDK.Http;
using PlanningCenterSDK.Shared;

namespace PlanningCenterSDK.Apps.Files.Endpoints
{
    public class FileUploadEndpoint : BaseEndpoint<FileData>
    {
        public FileUploadEndpoint( RateLimitedRequester requester ) : base( requester, "v2/files" )
        {
        }

        public virtual async Task<IEnumerable<FileData>> UploadAsync( Stream fileStream, string fileName )
        {
            var json = await _requester.CreateFileUploadRequestAsync( _url, fileStream, fileName ).ConfigureAwait( false );
            return JsonConvert.DeserializeObject<IEnumerable<FileData>>( json, new JsonApiSerializerSettings() );
        }

        public override Task<FileData> CreateAsync( FileData entity )
        {
            throw new NotImplementedException();
        }
        public override Task<bool> DeleteAsync( string Id )
        {
            throw new NotImplementedException();
        }
        public override Task<List<FileData>> GetAllAsync( Dictionary<string, string> parameters = null )
        {
            throw new NotImplementedException();
        }
        public override Task<List<FileData>> GetAsync( int offset = 0, int perPage = 25, Dictionary<string, string> parameters = null )
        {
            throw new NotImplementedException();
        }
        public override Task<FileData> GetByIdAsync( int id, Dictionary<string, string> parameters = null )
        {
            throw new NotImplementedException();
        }
        public override Task<FileData> UpdateAsync( FileData entity, string Id )
        {
            throw new NotImplementedException();
        }
    }
}
