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
    [Description( "Completes a pipeline action." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Pipeline Action Complete" )]

    [WorkflowAttribute(
        "Pipeline Action Attribute",
        Description = "The attribute that contains the pipeline action.",
        IsRequired = true,
        DefaultValue = "",
        Order = 0,
        Key = AttributeKey.PipelineAction,
        FieldTypeClassNames = new string[] { "com.bemaservices.BemaPipeline.Field.Types.BemaPipelineActionFieldType" } )]


    public class CompletePipelineAction : ActionComponent
    {
        #region Attribute Keys

        private static class AttributeKey
        {
            public const string PipelineAction = "PipelineAction";
        }

        #endregion

        public override bool Execute( RockContext rockContext, WorkflowAction action, Object entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();
            BemaPipelineService bemaPipelineService = new BemaPipelineService( rockContext );
            BemaPipelineActionService bemaPipelineActionService = new BemaPipelineActionService( rockContext );

            // Get the pipeline action
            BemaPipelineAction bemaPipelineAction = null;
            Guid? bemaPipelineActionGuid = null;

            var bemaWorkflowAttributeValueGuid = GetAttributeValue( action, AttributeKey.PipelineAction ).AsGuidOrNull();
            if ( bemaWorkflowAttributeValueGuid.HasValue )
            {
                bemaPipelineActionGuid = action.GetWorkflowAttributeValue ( bemaWorkflowAttributeValueGuid.Value ).AsGuidOrNull ();
                if ( bemaPipelineActionGuid.HasValue )
                {
                    bemaPipelineAction = bemaPipelineActionService.Get ( bemaPipelineActionGuid.Value );
                }
            }

            if ( bemaPipelineAction == null )
            {
                errorMessages.Add( string.Format( "BEMA Pipeline Action could not be found for selected value ('{0}')!", bemaPipelineActionGuid.ToString() ) );
                return false;
            }

            try
            {
                bemaPipelineAction.BemaPipelineActionState = BemaPipelineActionState.Completed;
                bemaPipelineAction.LastProcessedDateTime = RockDateTime.Now;
                bemaPipelineAction.CompletedDateTime = RockDateTime.Now;
                rockContext.SaveChanges();

                bemaPipelineService.ProcessBemaPipeline( bemaPipelineAction.BemaPipeline, out errorMessages );
            }
            catch ( Exception ex )
            {
                errorMessages.Add( string.Format( "Could not complete pipeline action ('{0}')! {1}", bemaPipelineAction.Name, ex.Message ) );
                return false;
            }

            action.AddLogEntry( string.Format( "Completed BEMA Pipeline Action '{0}'.", bemaPipelineAction.Name ) );

            return true;
        }
    }
}
