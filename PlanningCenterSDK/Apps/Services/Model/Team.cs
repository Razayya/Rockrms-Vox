using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PlanningCenterSDK.Misc;

namespace PlanningCenterSDK.Apps.Services.Model
{
    public class Team : ArchivableModel
    {
        public string Name { get; set; }
        [JsonProperty(propertyName:"rehearsal_team")]
        public bool? RehearsalTeam { get; set; }
        public int? Sequence { get; set; }
        [JsonProperty( propertyName: "schedule_to" )]
        public string ScheduleTo { get; set; }
        [JsonProperty( propertyName: "default_status" )]
        public string DefaultStatus { get; set; }
        [JsonProperty( propertyName: "default_prepare_notifications" )]
        public bool? DefaultPrepareNotifications { get; set; }
        [JsonProperty( propertyName: "viewers_see" )]
        public int? ViewersSee { get; set; }
        [JsonProperty( propertyName: "assigned_directly" )]
        public bool? AssignDirectly { get; set; }
        [JsonProperty( propertyName: "secure_team" )]
        public bool? SecureTeam { get; set; }
        [JsonProperty( propertyName: "last_plan_from" )]
        public string LastPlanFrom { get; set; }
        [JsonProperty( propertyName: "stage_color" )]
        public string StageColor { get; set; }
        [JsonProperty( propertyName: "stage_variant" )]
        public string StateVariant { get; set; }
        [JsonProperty( propertyName: "service_type" )]
        public ServiceType ServiceType { get; set; }
        public IEnumerable<Person> People { get; set; }
        [JsonProperty( propertyName: "team_positions")]
        public IEnumerable<TeamPosition> TeamPositions { get; set; }

        [JsonProperty( propertyName: "person_team_position_assignments" )]
        public IEnumerable<PersonTeamPositionAssignment> PersonTeamPositionsAssignments { get; set; }
    }
}
