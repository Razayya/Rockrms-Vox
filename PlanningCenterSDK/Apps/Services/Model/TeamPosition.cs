using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlanningCenterSDK.Misc;

namespace PlanningCenterSDK.Apps.Services.Model
{
    public class TeamPosition : BaseModel
    {
        public string Name { get; set; }
        public int? sequence { get; set; }
        public Team Team { get; set; }
    }
}
