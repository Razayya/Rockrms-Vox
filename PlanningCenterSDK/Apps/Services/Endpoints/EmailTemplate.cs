using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JsonApiSerializer;
using Newtonsoft.Json;
using PlanningCenterSDK.Apps.Services.Model;
using PlanningCenterSDK.Http;

namespace PlanningCenterSDK.Apps.Services.Endpoints
{
    public class EmailTemplate : ServicesEndpoint<EmailTemplate>
    {
        public EmailTemplate( RateLimitedRequester requester ) : base( requester, "email_templates" )
        {
        }

        public async Task<EmailTemplateRenderedResponse> Render( int templateId, int PersonId, EmailTemplateFormat format = EmailTemplateFormat.Html )
        {
            var request = new RenderEmailTemplateRequest()
            {
                Format = format.GetValue(),
                Person = new Person()
                {
                    Id = PersonId
                }
            };

            var json = await _requester.CreatePostRequestAsync( $"{_url}/{templateId}/render", JsonConvert.SerializeObject( request, new JsonApiSerializerSettings() ) ).ConfigureAwait( false );
            return JsonConvert.DeserializeObject<EmailTemplateRenderedResponse>( json, new JsonApiSerializerSettings() );
        }
    }
}
