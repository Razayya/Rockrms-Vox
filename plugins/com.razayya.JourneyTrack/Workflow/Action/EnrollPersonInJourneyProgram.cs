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
    /// Adds (or re-activates) a person's enrollment in a JourneyTrack Program.
    /// Idempotent — re-running for an already-enrolled active row is a no-op.
    /// </summary>
    [ActionCategory( "Razayya > JourneyTrack" )]
    [Description( "Enrolls a person in a Journey Program (adds or re-activates a JourneyProgramEnrollment row)." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Enroll Person In Journey Program" )]

    [WorkflowAttribute( "Person",
        Description = "The person to enroll.",
        IsRequired = true,
        Order = 0,
        Key = AttributeKey.Person,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.PersonFieldType" } )]

    [WorkflowAttribute( "Journey Program",
        Description = "The Program to enroll the person in.",
        IsRequired = true,
        Order = 1,
        Key = AttributeKey.JourneyProgram,
        FieldTypeClassNames = new string[] { "com.razayya.JourneyTrack.Field.Types.JourneyProgramFieldType" } )]

    [TextField( "Source",
        Description = "Optional label stored on the enrollment row (e.g. 'WF494', 'Mobile', 'Manual'). Defaults to 'Workflow'.",
        IsRequired = false,
        DefaultValue = "Workflow",
        Order = 2,
        Key = AttributeKey.Source )]

    public class EnrollPersonInJourneyProgram : ActionComponent
    {
        private static class AttributeKey
        {
            public const string Person = "Person";
            public const string JourneyProgram = "JourneyProgram";
            public const string Source = "Source";
        }

        public override bool Execute( RockContext rockContext, WorkflowAction action, object entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            // Resolve the Person (workflow Person attribute stores a PersonAlias Guid)
            int? personAliasId = null;
            int? personId = null;
            var personAttrValue = GetAttributeValue( action, AttributeKey.Person );
            var personAttrGuid = personAttrValue.AsGuidOrNull();
            if ( personAttrGuid.HasValue )
            {
                var personAliasGuidStr = action.GetWorkflowAttributeValue( personAttrGuid.Value );
                var personAliasGuid = personAliasGuidStr.AsGuidOrNull();
                if ( personAliasGuid.HasValue )
                {
                    var pa = new PersonAliasService( rockContext ).Get( personAliasGuid.Value );
                    if ( pa != null )
                    {
                        personAliasId = pa.Id;
                        personId = pa.PersonId;
                    }
                }
            }

            if ( !personAliasId.HasValue )
            {
                errorMessages.Add( "Could not resolve a valid Person from the workflow attribute." );
                return false;
            }

            // Resolve the Journey Program
            int? programId = null;
            var programAttrValue = GetAttributeValue( action, AttributeKey.JourneyProgram );
            var programAttrGuid = programAttrValue.AsGuidOrNull();
            if ( programAttrGuid.HasValue )
            {
                var programGuidStr = action.GetWorkflowAttributeValue( programAttrGuid.Value );
                var programGuid = programGuidStr.AsGuidOrNull();
                if ( programGuid.HasValue )
                {
                    programId = new JourneyProgramService( rockContext ).Queryable()
                        .Where( g => g.Guid == programGuid.Value )
                        .Select( g => ( int? ) g.Id )
                        .FirstOrDefault();
                }
            }

            if ( !programId.HasValue )
            {
                errorMessages.Add( "Could not resolve a valid Journey Program from the workflow attribute." );
                return false;
            }

            var source = GetAttributeValue( action, AttributeKey.Source );
            if ( string.IsNullOrWhiteSpace( source ) ) source = "Workflow";

            // Idempotent upsert: find any existing enrollment row for this (Program, Person);
            // activate it if inactive, otherwise leave alone. Only insert when absent.
            var enrollmentService = new JourneyProgramEnrollmentService( rockContext );
            var existing = enrollmentService.Queryable()
                .Where( e => e.JourneyProgramId == programId.Value
                    && e.PersonAlias.PersonId == personId.Value )
                .OrderByDescending( e => e.Id )
                .FirstOrDefault();

            var nowUtc = RockDateTime.Now;
            if ( existing == null )
            {
                enrollmentService.Add( new JourneyProgramEnrollment
                {
                    JourneyProgramId = programId.Value,
                    PersonAliasId = personAliasId.Value,
                    EnrolledDateTime = nowUtc,
                    IsActive = true,
                    Source = source,
                    EnrolledByPersonAliasId = action.Activity?.Workflow?.InitiatorPersonAliasId
                } );
                action.AddLogEntry( "Created new enrollment row." );
            }
            else if ( !existing.IsActive )
            {
                existing.IsActive = true;
                existing.UnenrolledDateTime = null;
                existing.ModifiedDateTime = nowUtc;
                action.AddLogEntry( "Re-activated existing enrollment row Id=" + existing.Id + "." );
            }
            else
            {
                action.AddLogEntry( "Person already actively enrolled (Id=" + existing.Id + "); no-op." );
            }

            rockContext.SaveChanges();
            return true;
        }
    }
}
