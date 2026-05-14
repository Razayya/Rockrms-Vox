// <copyright>
// Copyright by BEMA Software Services
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
using System.Data.Entity;
using System.Linq;
using System.Text;
using com.bemaservices.BemaPipeline.Attribute;
using com.bemaservices.BemaPipeline.Model;

using Quartz;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace com.bemaservices.RoomManagement.Jobs
{

    [DisplayName( "Process BEMA Pipelines" )]
    [Description( "Runs continuously to process BEMA Pipelines in progress." )]

    [DisallowConcurrentExecution]
    public class ProcessBemaPipelines : IJob
    {

        public ProcessBemaPipelines()
        {
        }

        public virtual void Execute( IJobExecutionContext context )
        {
            int pipelinesProcessed = 0;
            int pipelineErrors = 0;
            int pipelineExceptions = 0;
            var processingErrors = new List<string>();
            var exceptionMsgs = new List<string>();

            foreach ( var pipelineId in new BemaPipelineService( new RockContext() )
                .GetActive()
                .Select( p => p.Id )
                .ToList() )
            {
                try
                {
                    // create a new rockContext and service for every pipeline to prevent a build-up of Context.ChangeTracker.Entries()
                    var rockContext = new RockContext();
                    var pipelineService = new BemaPipelineService( rockContext );
                    var pipeline = pipelineService.Queryable().FirstOrDefault( a => a.Id == pipelineId );
                    if ( pipeline != null )
                    {
                        var pipelineType = pipeline.BemaPipelineType;
                        if ( pipelineType != null )
                        {
                            try
                            {
                                var errorMessages = new List<string>();

                                var processed = pipelineService.ProcessBemaPipeline( pipeline, out errorMessages );
                                if ( processed )
                                {
                                    pipelinesProcessed++;
                                }
                                else
                                {
                                    pipelineErrors++;
                                    processingErrors.Add( string.Format( "{0} [{1}] - [{2}]: {3}", pipelineType.Name, pipelineType.Id, pipeline.Id, errorMessages.AsDelimited( ", " ) ) );
                                }
                            }
                            catch ( Exception ex )
                            {
                                string pipelineDetails = string.Format( "{0} [{1}] - [{2}]", pipelineType.Name, pipelineType.Id, pipeline.Id );
                                exceptionMsgs.Add( pipelineDetails + ": " + ex.Message );
                                throw new Exception( "Exception occurred processing pipeline: " + pipelineDetails, ex );
                            }
                        }
                    }
                }

                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex, null );
                    pipelineExceptions++;
                }
            }

            var resultMsg = new StringBuilder();
            resultMsg.AppendFormat( "{0} pipelines processed", pipelinesProcessed );
            if ( pipelineErrors > 0 )
            {
                resultMsg.AppendFormat( ", {0} pipelines reported an error", pipelineErrors );
            }
            if ( pipelineExceptions > 0 )
            {
                resultMsg.AppendFormat( ", {0} pipelines caused an exception", pipelineExceptions );
            }
            if ( processingErrors.Any() )
            {
                resultMsg.Append( Environment.NewLine + processingErrors.AsDelimited( Environment.NewLine ) );
            }

            if ( exceptionMsgs.Any() )
            {
                throw new Exception( "One or more exceptions occurred processing pipelines..." + Environment.NewLine + exceptionMsgs.AsDelimited( Environment.NewLine ) );
            }

            context.Result = resultMsg.ToString();
        }
    }
}