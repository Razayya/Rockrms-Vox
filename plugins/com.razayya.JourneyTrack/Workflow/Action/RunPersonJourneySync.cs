using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;

using com.razayya.JourneyTrack.Data;
using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Workflow;

namespace com.razayya.JourneyTrack.Workflow.Action
{
    /// <summary>
    /// Runs the JourneyTrack for a single person against a specified Journey Program.
    /// </summary>
    [ActionCategory( "Razayya > JourneyTrack" )]
    [Description( "Runs the JourneyTrack for a single person against a specified Journey Program." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Run Person Journey Sync" )]

    [WorkflowAttribute( "Person",
        Description = "The person to sync attributes for.",
        IsRequired = true,
        Order = 0,
        Key = AttributeKey.Person,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.PersonFieldType" } )]

    [WorkflowAttribute( "Journey Program",
        Description = "The Journey Program to process.",
        IsRequired = true,
        Order = 1,
        Key = AttributeKey.JourneyProgram,
        FieldTypeClassNames = new string[] { "com.razayya.JourneyTrack.Field.Types.JourneyProgramFieldType" } )]

    public class RunPersonJourneySync : ActionComponent
    {
        private static class AttributeKey
        {
            public const string Person = "Person";
            public const string JourneyProgram = "JourneyProgram";
        }

        /// <summary>
        /// Executes the action.
        /// </summary>
        public override bool Execute( RockContext rockContext, WorkflowAction action, object entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            // Resolve the Person
            int? personId = null;
            string personAttributeValue = GetAttributeValue( action, AttributeKey.Person );
            Guid? personAttrGuid = personAttributeValue.AsGuidOrNull();

            if ( personAttrGuid.HasValue )
            {
                string personAliasGuidStr = action.GetWorkflowAttributeValue( personAttrGuid.Value );
                Guid? personAliasGuid = personAliasGuidStr.AsGuidOrNull();

                if ( personAliasGuid.HasValue )
                {
                    var personAlias = new PersonAliasService( rockContext ).Get( personAliasGuid.Value );
                    if ( personAlias != null )
                    {
                        personId = personAlias.PersonId;
                    }
                }
            }

            if ( !personId.HasValue )
            {
                errorMessages.Add( "Could not resolve a valid Person from the workflow attribute." );
                return false;
            }

            // Resolve the Journey Program
            int? calculationGroupId = null;
            string groupAttributeValue = GetAttributeValue( action, AttributeKey.JourneyProgram );
            Guid? groupAttrGuid = groupAttributeValue.AsGuidOrNull();

            if ( groupAttrGuid.HasValue )
            {
                string groupGuidStr = action.GetWorkflowAttributeValue( groupAttrGuid.Value );
                Guid? groupGuid = groupGuidStr.AsGuidOrNull();

                if ( groupGuid.HasValue )
                {
                    var group = new JourneyProgramService( rockContext ).Get( groupGuid.Value );
                    if ( group != null )
                    {
                        calculationGroupId = group.Id;
                    }
                }
            }

            if ( !calculationGroupId.HasValue )
            {
                errorMessages.Add( "Could not resolve a valid Journey Program from the workflow attribute." );
                return false;
            }

            // Run the sync
            var syncService = new JourneyTrackService();
            var result = syncService.ProcessGroupForPerson( calculationGroupId.Value, personId.Value );

            // Log results
            foreach ( var logEntry in result.Log )
            {
                action.AddLogEntry( logEntry );
            }

            if ( result.Errors.Any() )
            {
                foreach ( var error in result.Errors )
                {
                    action.AddLogEntry( $"ERROR: {error}" );
                    errorMessages.Add( error );
                }

                return false;
            }

            action.AddLogEntry( $"Sync completed: {result.Updated} attribute(s) updated for PersonId {personId.Value}." );
            return true;
        }
    }
}
