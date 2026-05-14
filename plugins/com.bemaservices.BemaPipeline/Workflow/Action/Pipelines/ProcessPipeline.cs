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
    [Description( "Processes a pipeline." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Pipeline Process" )]

    [WorkflowAttribute(
        "Pipeline Attribute",
        Description = "The attribute that contains the pipeline.",
        IsRequired = true,
        DefaultValue = "",
        Order = 0,
        Key = AttributeKey.Pipeline,
        FieldTypeClassNames = new string[] { "com.bemaservices.BemaPipeline.Field.Types.BemaPipelineFieldType" } )]


    public class ProcessPipeline : ActionComponent
    {
        #region Attribute Keys

        private static class AttributeKey
        {
            public const string Pipeline = "Pipeline";
        }

        #endregion

        public override bool Execute( RockContext rockContext, WorkflowAction action, Object entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();
            BemaPipelineService bemaPipelineService = new BemaPipelineService( rockContext );

            // Get the pipeline
            Model.BemaPipeline bemaPipeline = null;
            Guid? bemaPipelineGuid = null;

            var workflow = action.Activity.Workflow;
            workflow.LoadAttributes ();
            bemaPipelineGuid = workflow.GetAttributeValue( AttributeKey.Pipeline ).AsGuidOrNull();
            if ( bemaPipelineGuid.HasValue )
            {
                bemaPipeline = bemaPipelineService.Get ( bemaPipelineGuid.Value );   
            }

            if ( bemaPipeline == null )
            {
                errorMessages.Add( string.Format( "BEMA Pipeline could not be found for selected value ('{0}')!", bemaPipelineGuid.Value.ToString() ) );
                return false;
            }

            try
            {
                bemaPipelineService.ProcessBemaPipeline( bemaPipeline, out errorMessages );
            }
            catch ( Exception ex )
            {
                errorMessages.Add( string.Format( "Could not process pipeline ('{0}')! {1}", bemaPipeline.Name, ex.Message ) );
                return false;
            }

            action.AddLogEntry( string.Format( "Processed BEMA Pipeline '{0}'.", bemaPipeline.Name ) );

            return true;
        }
    }
}
