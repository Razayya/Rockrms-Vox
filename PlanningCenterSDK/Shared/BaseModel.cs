using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PlanningCenterSDK.Misc
{
    public abstract class BaseModel
    {
        public int Id { get; set; }
        [JsonProperty(PropertyName = "created_at")]
        public DateTime? CreatedAt { get; set; }
        [JsonProperty( PropertyName = "updated_at" )]
        public DateTime? UpdatedAt { get; set; }
    }
}
