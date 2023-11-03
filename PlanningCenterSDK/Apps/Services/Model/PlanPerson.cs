using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlanningCenterSDK.Misc;

namespace PlanningCenterSDK.Apps.Services.Model
{
    public class PlanPerson : BaseModel
    {
        public string Type { get; set; } = "plan_person";
        public string Status { get; set; }
        public string Notes { get; set; }
        public string DeclineReason { get; set; }
        public string Name { get; set; }
        public string NotificationChangedByName { get; set; }
        public string NotificationSenderName { get; set; }
        public string TeamPositionName { get; set; }
        public string PhotoThumbnail { get; set; }
        public DateTime? StatusUpdatedAt { get; set; }
        public DateTime? NotificationChangedAt { get; set; }
        public DateTime? NotificationPreparedAt { get; set; }
        public DateTime? NotificationReadAt { get; set; }
        public DateTime? NotificationSentAt { get; set; }
        public bool? PreparedNotification { get; set; }
        public bool? CanAcceptPartial { get; set; }
        public Person Person { get; set; }
        public Team Team { get; set; }
        public ServiceType ServiceType { get; set; }


    }
}
