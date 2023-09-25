using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.bemaservices.BemaPipeline.Web.Cache;
using Rock;
using Rock.Data;
using Rock.Model;

namespace com.bemaservices.BemaPipeline.Model
{
    public class BemaPipelineService : Service<BemaPipeline>
    {

        public BemaPipelineService( RockContext context ) : base( context ) { }

        public IQueryable<BemaPipeline> GetActive()
        {
            return this.Queryable()
                .Where( w =>
                    !w.CompletedDateTime.HasValue )
                .OrderBy( w => w.LastProcessedDateTime );
        }

        public void ProcessBemaPipelines()
        {
            var bemaPipelines = Queryable().Where( cp => cp.BemaPipelineType.IsActive && !cp.CompletedDateTime.HasValue ).ToList();
            foreach ( var bemaPipeline in bemaPipelines )
            {
                List<string> pipeLineErrors;

                ProcessBemaPipeline( bemaPipeline, out pipeLineErrors );
            }
        }

        public bool ProcessBemaPipeline( BemaPipeline bemaPipeline, out List<string> pipeLineErrors, RockContext rockContext = null)
        {


            pipeLineErrors = new List<string>();

            if ( bemaPipeline != null && bemaPipeline.CompletedDateTime.HasValue )
            {
                return false;
            }

            var pipelineType = BemaPipelineTypeCache.Get( bemaPipeline.BemaPipelineTypeId );
            if ( pipelineType != null && ( pipelineType.IsActive ?? true ) && bemaPipeline.IsProcessing != true )
            {
                if( rockContext == null )
                {
                    rockContext = ( RockContext )this.Context;
                }

                if ( bemaPipeline.Id > 0 )
                {
                    IEntity entity = bemaPipeline.GetPipelineEntity( rockContext );
                    if ( entity == null )
                    {
                        bemaPipeline.IsProcessing = false;
                        bemaPipeline.BemaPipelineState = 0;
                        bemaPipeline.CompletedDateTime = DateTime.Now;
                        pipeLineErrors = new List<string> { "Pipeline has been completed. Entity does not exist" };
                        rockContext.SaveChanges();
                        return false;
                    }
                    else
                    {
                        bemaPipeline.IsProcessing = true;
                        rockContext.SaveChanges();
                    }
                }

                bool result = false;
                try
                {
                    result = bemaPipeline.ProcessActions( rockContext, out pipeLineErrors );
                    if ( pipeLineErrors.Any() )
                    {
                        string errorMsg = "<ul><li>" + pipeLineErrors.AsDelimited( "</li><li>" ) + "</li></ul>";

                        ExceptionLogService.LogException( errorMsg );
                    }

                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                }

                bemaPipeline.IsProcessing = false;
                rockContext.SaveChanges();

                return result;
            }
            else
            {
                pipeLineErrors = new List<string> { "Workflow Type is invalid or not active!" };
                return false;
            }
        }

        /// <summary>
        /// Launches or creates a new pipeline for given pipeline type and entity
        /// </summary>
        /// <param name="rockContext"></param>
        /// <param name="bemaPipelineTypeId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public BemaPipeline LaunchPipeline( RockContext rockContext, int bemaPipelineTypeId, int entityId )
        {
            var bemaPipelineType = BemaPipelineTypeCache.Get( bemaPipelineTypeId );

            if ( bemaPipelineType != null  )
            {
                BemaPipeline bemaPipeline = null;
                bemaPipeline = this.Queryable ()
                    .Where ( p => p.BemaPipelineTypeId == bemaPipelineType.Id
                        && p.EntityId == entityId )
                    .OrderBy ( w => w.LastProcessedDateTime )
                    .FirstOrDefault ();

                //Create new pipeline, if type is still active
                if ( bemaPipeline == null && ( bemaPipelineType.IsActive ?? true ) )
                {
                    bemaPipeline = BemaPipeline.Activate( bemaPipelineType, entityId, rockContext );
                    Add( bemaPipeline );
                }

                rockContext.SaveChanges();

                List<string> pipeLineErrors;
                ProcessBemaPipeline(bemaPipeline, out pipeLineErrors, rockContext );

                return bemaPipeline;
            }

            return null;
        }

        /// <summary>
        /// Takes in a pipeline, along with a context, and renders the Pipleine into HTML. NOTE: does not process the pipeline; only renders the display. Optionally takes in the person calling the render and a divclass string for each action's div.
        /// </summary>
        /// <param name="rockContext"></param>
        /// <param name="bemaPipeline"></param>
        /// <param name="currentPerson"></param>
        /// <param name="divClass"></param>
        /// <returns></returns>
        public StringBuilder RenderPipelineHTML( RockContext rockContext, BemaPipeline bemaPipeline, Person currentPerson = null, string actionDivClass = "pipeline-action" )
        {
            StringBuilder str = new StringBuilder();
            IEntity entity = bemaPipeline.GetPipelineEntity( rockContext );
            List<string> errorMessages = new List<string>();


            //Loop through each action and render it's lava template
            foreach ( var action in bemaPipeline.BemaPipelineActions )
            {
                var displayLava = action.ActionTypeCache.GetAttributeValue( "DisplayedLava" );

                if( displayLava.IsNullOrWhiteSpace() )
                {
                    continue;
                }

                //Call Merge Fields from component, in case there are any special lava objects
                var mergeFields = action.ActionTypeCache.BemaPipelineActionTypeComponent.GetMergeFields( action, entity );

                //Add in links created by component, if any
                var actionLinks = action.ActionTypeCache.BemaPipelineActionTypeComponent.ActionLinks( rockContext, action, entity, currentPerson, out errorMessages );
                mergeFields.Add( "ActionLinks", actionLinks );

                // Fetch lava template from action type cache
                var actionHtml = displayLava.ResolveMergeFields( mergeFields );

                if ( actionHtml.IsNotNullOrWhiteSpace() )
                {
                    str.AppendFormat( @"<div class='{1} {1}-{2}'>{0}</div>", actionHtml, actionDivClass ?? "pipeline-action", action.BemaPipelineActionState.ToString().ToLower() );
                }
            }


            return str;
        }
    }
}