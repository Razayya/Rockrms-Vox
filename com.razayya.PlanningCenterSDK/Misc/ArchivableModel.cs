using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.razayya.PlanningCenterSDK.Misc
{
    public abstract class ArchivableModel : BaseModel
    {
        public DateTime? ArchivedAt { get; set; }
    }
}
