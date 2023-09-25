// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

using Rock;
using Rock.Chart;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Rest.Filters;

using com.bemaservices.BemaPipeline.Model;
using System.Net.Http.Headers;
using com.bemaservices.BemaPipeline.Web.Cache;

namespace com.bemaservices.BemaPipeline.Rest.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    public partial class BemaPipelineController : Rock.Rest.ApiControllerBase
    {

        #region Pipeline Action Interactions



        /// <summary>
        /// Gets a list of scheduled attendances ( people that are scheduled ) for an attendance occurrence
        /// </summary>
        /// <param name="attendanceOccurrenceId">The attendance occurrence identifier.</param>
        /// <returns></returns>
        [Authenticate, Secured]
        [System.Web.Http.Route( "api/BemaPipeline/GetRenderPipelineHTML" )]
        [HttpGet]
        public HttpResponseMessage GetRenderPipelineHTML( int pipelineTypeId, int entityId, int? markCompleteActionId = null, string actionDivClass = null )
        {
            var rockContext = new RockContext();
            var pipelineService = new BemaPipelineService( rockContext );
            var person = GetPerson();
            List<string> errorMessages = new List<string>();

            //If PipelineType security isn't authorized, dont allow action
            BemaPipelineTypeCache bemaPipelineType = BemaPipelineTypeCache.Get( pipelineTypeId );
            if( bemaPipelineType == null )
            {
                var error = new HttpResponseMessage( HttpStatusCode.NotFound );
                error.Content = new StringContent( "Pipeline Type Not Found By Id. PipelineTypeId: " + pipelineTypeId.ToString() );
                return error;
            }

            if( entityId <= 0 )
            {
                var error = new HttpResponseMessage( HttpStatusCode.BadRequest );
                error.Content = new StringContent( "Entity Id given cannot be zero or negative. EntityId: " + entityId.ToString() );
                return error;
            }

            if ( !bemaPipelineType.IsAuthorized( Rock.Security.Authorization.VIEW, person )
                && !bemaPipelineType.IsAuthorized( Rock.Security.Authorization.EDIT, person ) )
            {
                return new HttpResponseMessage( HttpStatusCode.Unauthorized );
            }

            //Process Action as manually triggered, if action is provided
            if( markCompleteActionId.HasValue )
            {
                var action = new BemaPipelineActionService( rockContext ).Get( markCompleteActionId.Value );
                bool success = action.Process( rockContext, out errorMessages, true );

                //throw error if needed
                if ( errorMessages.Count > 0 )
                {
                    HttpResponseMessage error = new HttpResponseMessage( HttpStatusCode.InternalServerError );
                    error.Content = new StringContent( errorMessages.AsDelimited( "," ) );
                    throw new HttpResponseException( error );
                }

                if( success != true )
                {
                    HttpResponseMessage error = new HttpResponseMessage( HttpStatusCode.InternalServerError );
                    error.Content = new StringContent( "Action is not ready for processing. For manual completion, action must be in an 'Allow' state." );
                    throw new HttpResponseException( error );
                }
            }

            //Launches new pipeline if needed; else fetches current one
            var pipeline = pipelineService.LaunchPipeline( rockContext, pipelineTypeId, entityId );

            if( pipeline != null )
            {
                var response = new HttpResponseMessage();
                var renderedHtml = pipelineService.RenderPipelineHTML( rockContext, pipeline, person, actionDivClass );
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent( renderedHtml.ToString() );
                response.Content.Headers.ContentType = new MediaTypeHeaderValue( "text/html" );

                return response;
            }

            throw new HttpResponseException( new HttpResponseMessage( HttpStatusCode.InternalServerError ) );
        }


        #endregion


    }
}
