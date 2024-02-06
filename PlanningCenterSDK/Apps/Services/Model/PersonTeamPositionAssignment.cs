
using Newtonsoft.Json;
using System.Collections.Generic;
using System;

namespace PlanningCenterSDK.Apps.Services.Model
{
    public class PersonTeamPositionAssignment
    {
        [JsonProperty( propertyName: "id" )]
        public string Id { get; set; }

        [JsonProperty( propertyName: "created_at" )]
        public DateTime? CreatedAt { get; set; }

        [JsonProperty( propertyName: "updated_at" )]
        public DateTime? UpdatedAt { get; set; }

        // Relationships
        [JsonProperty( propertyName: "person" )]
        public Person Person { get; set; }

        [JsonProperty( propertyName: "team_position" )]
        public TeamPosition TeamPosition { get; set; }

    }
}
