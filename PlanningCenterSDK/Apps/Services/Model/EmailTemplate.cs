using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PlanningCenterSDK.Apps.Services.Model
{
    public class EmailTemplate
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        [JsonProperty( propertyName: "created_at" )]
        public DateTime? CreatedAt { get; set; }
        [JsonProperty( propertyName: "updated_at" )]
        public DateTime? UpdatedAt { get; set; }
        [JsonProperty( propertyName: "html_body" )]
        public string HtmlBody { get; set; }
        public string Subject { get; set; }

    }
}
