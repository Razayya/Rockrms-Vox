using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.razayya.PlanningCenterSDK.Misc;

namespace com.razayya.PlanningCenterSDK.Apps.Services.Model
{
    public class Team : ArchivableModel
    {
        public string Name { get; set; }
        public bool RehearsalTeam { get; set; }
        public int Sequence { get; set; }
        public string ScheduleTo { get; set; }
        public string DefaultStatus { get; set; }
        public bool DefaultPrepareNotifications { get; set; }
        public int ViewersSee { get; set; }
        public bool AssignDirectly { get; set; }
        public bool SecureTeam { get; set; }
        public string LastPlanFrom { get; set; }
        public string StageColor { get; set; }
        public string StateVariant { get; set; }
        public ServiceType? ServiceType { get; set; }
        public ICollection<Person>? People { get; set; }
    }
}
