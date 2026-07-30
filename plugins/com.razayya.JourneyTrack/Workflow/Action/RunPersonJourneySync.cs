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
    /// Runs the JourneyTrack for a single person. Scope is either:
    ///  - the full Journey Program (program-scoped, classic behavior); or
    ///  - a single Stage within a Program (stage-scoped, mobile-friendly — skips later Stages, no program rollup).
    /// Exactly one of Journey Program / Stage Guid must be supplied at runtime.
    /// </summary>
    [ActionCategory( "Razayya > JourneyTrack" )]
    [Description( "Runs the JourneyTrack for a single person against a specified Journey Program or Stage." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Run Person Journey Sync" )]

    [WorkflowAttribute( "Person",
        Description = "The person to sync attributes for.",
        IsRequired = true,
        Order = 0,
        Key = AttributeKey.Person,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.PersonFieldType" } )]

    [WorkflowAttribute( "Journey Program",
        Description = "The Journey Program to process. Leave blank if Stage Guid is set.",
        IsRequired = false,
        Order = 1,
        Key = AttributeKey.JourneyProgram,
        FieldTypeClassNames = new string[] { "com.razayya.JourneyTrack.Field.Types.JourneyProgramFieldType" } )]

    [WorkflowTextOrAttribute( "Stage Guid",
        "Stage Guid Attribute",
        Description = "Optional. When set, runs a stage-scoped sync (skips later Stages, no program rollup). Mutually exclusive with Journey Program.",
        IsRequired = false,
        Order = 2,
        Key = AttributeKey.StageGuid )]

    public class RunPersonJourneySync : ActionComponent
    {
        private static class AttributeKey
        {
            public const string Person = "Person";
            public const string JourneyProgram = "JourneyProgram";
            public const string StageGuid = "StageGuid";
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

            // Resolve scope: Stage Guid wins if set, otherwise fall back to Journey Program.
            Guid? stageGuid = ResolveStageGuid( action );
            int? stageId = null;
            int? calculationGroupId = null;

            if ( stageGuid.HasValue )
            {
                var stage = new StageService( rockContext ).Get( stageGuid.Value );
                if ( stage == null )
                {
                    errorMessages.Add( $"Stage Guid {stageGuid.Value} not found." );
                    return false;
                }
                stageId = stage.Id;
            }
            else
            {
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
                    errorMessages.Add( "Neither a valid Stage Guid nor Journey Program was supplied." );
                    return false;
                }
            }

            // Run the sync
            var syncService = new JourneyTrackService();
            var result = stageId.HasValue
                ? syncService.ProcessStageForPerson( stageId.Value, personId.Value )
                : syncService.ProcessGroupForPerson( calculationGroupId.Value, personId.Value );

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

        /// <summary>
        /// Resolves the optional Stage Guid from the WorkflowTextOrAttribute field.
        /// Two-step resolution against the single stored Guid value:
        ///   (a) treat the Guid as a pointer to another workflow attribute whose value is a
        ///       Stage Guid (the "Stage Guid Attribute" alternate input on this action);
        ///   (b) if the pointer doesn't resolve to a non-empty workflow attribute value,
        ///       fall back to treating the literal Guid as a Stage Guid directly.
        /// Returns null when neither resolves.
        /// </summary>
        private Guid? ResolveStageGuid( WorkflowAction action )
        {
            string raw = GetAttributeValue( action, AttributeKey.StageGuid );
            Guid? maybeGuid = raw.AsGuidOrNull();
            if ( !maybeGuid.HasValue )
            {
                return null;
            }

            // (a) Try as a workflow-attribute Guid pointer.
            string viaAttr = action.GetWorkflowAttributeValue( maybeGuid.Value );
            if ( !string.IsNullOrWhiteSpace( viaAttr ) )
            {
                var asStage = viaAttr.AsGuidOrNull();
                if ( asStage.HasValue )
                {
                    return asStage;
                }
            }
            // (b) Otherwise treat the literal as a Stage Guid directly.
            return maybeGuid;
        }
    }
}
