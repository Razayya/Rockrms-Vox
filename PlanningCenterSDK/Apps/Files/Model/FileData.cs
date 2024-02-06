using System;
using Newtonsoft.Json;

namespace PlanningCenterSDK.Apps.Files.Model
{
    public class FileData
    {
        public string Id { get; set; }
        [JsonProperty( propertyName: "source_ip" )]
        public string SourceIp { get; set; }
        public string Md5 { get; set; }
        [JsonProperty( propertyName: "content_type" )]
        public string ContentType { get; set; }
        [JsonProperty( propertyName: "file_size" )]
        public int FileSize { get; set; }
        public string Name { get; set; }
        [JsonProperty( propertyName: "expires_at" )]
        public DateTime ExpiresAt { get; set; }
    }
}
