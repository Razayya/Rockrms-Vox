using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PlanningCenterSDK.Misc
{
    public abstract class ArchivableModel : BaseModel
    {
        [JsonProperty( PropertyName = "archived_at" )]
        public DateTime? ArchivedAt { get; set; }
    }
}
