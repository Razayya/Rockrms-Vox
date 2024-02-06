using Newtonsoft.Json;

namespace PlanningCenterSDK.Apps.Services.Model
{
    public class EmailTemplateRenderedResponse
    {
        public string Id { get; set; }
        public string Body { get; set; }
        public string Subject { get; set; }
        public Person Person { get; set; }
        [JsonProperty( propertyName: "email_template" )]
        public EmailTemplate EmailTemplate { get; set; }

    }
}
