using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlanningCenterSDK.Misc;

namespace PlanningCenterSDK.Apps.Services.Model
{
    public class ServiceType : ArchivableModel
    {
        public string Name { get; set; }
        public int? Sequence { get; set; }
        public string Permissions { get; set; }
        public string Frequency { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool? AttachmentTypesEnabled { get; set; }
        public string BackgroundCheckPermissions { get; set; }
        public string CommentPermissions { get; set; }
        public string LastPlanFrom { get; set; }

        public override string ToString()
        {
            return Name;
        }

    }
}
