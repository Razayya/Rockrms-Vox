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
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using com.bemaservices.BemaPipeline.Model;
using com.bemaservices.BemaPipeline.Web.Cache;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Workflow;

namespace com.bemaservices.BemaPipeline.Workflow.Action
{

    [ActionCategory( "BEMA Pipelines" )]
    [Description( "Launches a pipeline." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Pipeline Launch" )]

    [WorkflowAttribute(
        "Pipeline Type Attribute",
        Description = "The attribute that contains the pipeline type.",
        IsRequired = true,
        DefaultValue = "",
        Order = 0,
        Key = AttributeKey.PipelineType,
        FieldTypeClassNames = new string[] { "com.bemaservices.BemaPipeline.Field.Types.BemaPipelineTypeFieldType" } )]

    //[EntityTypeField(
    //    "Entity Type",
    //    Description = "The type of Entity.",
    //    IsRequired = true,
    //    Order = 1,
    //    IncludeGlobalAttributeOption = false,
    //    Key = AttributeKey.EntityType )]

    [WorkflowTextOrAttribute(
        "Entity Id or Guid",
        "Entity Attribute",
        Description = "The id or guid of the entity. <span class='tip tip-lava'></span>",
        IsRequired = true,
        Order = 2,
        Key = AttributeKey.EntityIdGuid )]


    public class LaunchPipeline : ActionComponent
    {
        #region Attribute Keys

        private static class AttributeKey
        {
            public const string PipelineType = "PipelineType";
            //public const string EntityType = "EntityType";
            public const string EntityIdGuid = "EntityIdGuid";
        }

        #endregion

        public override bool Execute( RockContext rockContext, WorkflowAction action, Object entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            // Get the pipeline type
            BemaPipelineTypeCache bemaPipelineType = null;
            Guid bemaPipelineTypeGuid = action.GetWorkflowAttributeValue(GetAttributeValue(action, AttributeKey.PipelineType).AsGuid()).AsGuid();
            bemaPipelineType = BemaPipelineTypeCache.Get( bemaPipelineTypeGuid );
            if ( bemaPipelineType == null )
            {
                errorMessages.Add( string.Format( "BEMA Pipeline Type could not be found for selected value ('{0}')!", bemaPipelineTypeGuid.ToString() ) );
                return false;
            }

            // Get the entity type
            EntityTypeCache entityType = null;
            var entityTypeGuid = bemaPipelineType.EntityType.Guid; //GetAttributeValue( action, AttributeKey.EntityType ).AsGuidOrNull();

            entityType = EntityTypeCache.Get( entityTypeGuid );

            if ( entityType == null )
            {
                errorMessages.Add( string.Format( "Entity Type could not be found for selected value ('{0}')!", entityTypeGuid.ToString() ) );
                return false;
            }

            var mergeFields = GetMergeFields( action );
            RockContext _rockContext = new RockContext();

            // Get the entity
            EntityTypeService entityTypeService = new EntityTypeService( _rockContext );
            IEntity entityObject = null;
            string entityIdGuidString = GetAttributeValue( action, AttributeKey.EntityIdGuid, true ).ResolveMergeFields( mergeFields ).Trim();
            var entityId = entityIdGuidString.AsIntegerOrNull();
            if ( entityId.HasValue )
            {
                entityObject = entityTypeService.GetEntity( entityType.Id, entityId.Value );
            }
            else
            {
                var entityGuid = entityIdGuidString.AsGuidOrNull();
                if ( entityGuid.HasValue )
                {
                    entityObject = entityTypeService.GetEntity( entityType.Id, entityGuid.Value );
                }
            }

            if ( entityObject == null )
            {
                var value = GetActionAttributeValue( action, AttributeKey.EntityIdGuid );
                entityObject = action.GetEntityFromAttributeValue( value, rockContext );
            }

            if ( entityObject == null )
            {
                errorMessages.Add( string.Format( "Entity could not be found for selected value ('{0}')!", entityIdGuidString ) );
                return false;
            }

            try
            {
                BemaPipelineService bemaPipelineService = new BemaPipelineService( _rockContext );
                bemaPipelineService.LaunchPipeline( _rockContext, bemaPipelineType.Id, entityObject.Id );
                _rockContext.SaveChanges();
            }
            catch ( Exception ex )
            {
                errorMessages.Add( string.Format( "Could not launch pipeline type ('{0}')! {1}", bemaPipelineType.Name, ex.Message ) );
                return false;
            }

            action.AddLogEntry( string.Format( "Launched BEMA Pipeline type '{0}'.", bemaPipelineType.Name ) );

            return true;
        }
    }
}
