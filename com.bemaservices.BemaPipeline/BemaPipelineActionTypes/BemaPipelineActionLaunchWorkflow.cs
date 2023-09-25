using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;

using Rock.Attribute;
using Rock.Field.Types;
using Rock.Web.Cache;

using com.bemaservices.BemaPipeline.Attribute;
using com.bemaservices.BemaPipeline.Model;
using com.bemaservices.BemaPipeline.Web.Cache;

using Rock.Data;
using Rock.Model;
using Rock;
using System.Linq;
using System.Data.Entity;
using com.bemaservices.BemaPipeline.Rest.API;
using Rock.Security;
using Rock.Web;

namespace com.bemaservices.BemaPipeline.BemaPipelineActionTypes
{
    /// <summary>
    /// Processes Action by launching a workflow.
    /// </summary>
    [Description( "Launches a workflow." )]
    [Export( typeof( BemaPipelineActionTypeComponent ) )]
    [ExportMetadata( "ComponentName", "Launch Workflow" )]

    [WorkflowTypeField( "Workflow Type",
        Key = AttributeKey.WorkflowType,
        Category = AttributeCategories.Workflow,
        Description = "The type of workflow to launch.",
        AllowMultiple = false,
        IsRequired = true,
        Order = 1 )]

    [TextField( "Workflow Name Template",
        Key = AttributeKey.WorkflowNameTemplate,
        Category = AttributeCategories.Workflow,
        Description = "The lava template to use for setting the workflow name. See the defined type's help text for a listing of merge fields. <span class='tip tip-lava'></span>",
        IsRequired = false,
        Order = 2 )]

    [KeyValueListField( "Workflow Attributes",
        Key = AttributeKey.WorkflowAttributes,
        Category = AttributeCategories.Workflow,
        Description = "Key/value list of workflow attributes to set with the given lava merge template.<span class='tip tip-lava'></span><br/>Merge Fields:<ul><li>Pipeline</li><li><code>PipelineEntityTypeName</code><br/>i.e. ConnectionRequest</li><li>EntityType</li><li>Action</li><li>ActionType</li><li>PreviousActions</li><li>LastAction</li><li>FutureActions</li><li>NextAction</li></ul>",
        IsRequired = false,
        DefaultValue = "",
        KeyPrompt = "Attribute Key",
        ValuePrompt = "Merge Template",
        Order = 3 )]

    [LinkedPage( "Workflow Entry Page",
        Key = AttributeKey.WorkflowEntryPage,
        Category = AttributeCategories.Workflow,
        Description = "Page to redirect to for workflow entry",
        IsRequired = true,
        DefaultValue = "0550d2aa-a705-4400-81ff-ab124fdf83d7,3d71f9da-9d5e-4e41-9363-b56b1784dcd4",
        Order = 4 )]

    public class BemaPipelineActionLaunchWorkflow : BemaPipelineActionTypeComponent
    {
        #region Attribute Keys

        /// <summary>
        /// Keys to use for Block Attributes
        /// </summary>
        private static class AttributeKey
        {
            public const string WorkflowType = "WorkflowType";
            public const string WorkflowNameTemplate = "WorkflowNameTemplate";
            public const string WorkflowAttributes = "WorkflowAttributes";
            public const string WorkflowEntryPage = "WorkflowEntryPage";
        }


        #endregion Attribute Keys

        /// <summary>
        /// Categories for the attributes
        /// </summary>
        protected class AttributeCategories : BaseAttributeCategories
        {
            /// <summary>
            /// The Workflow category
            /// </summary>
            public const string Workflow = "Workflow";
        }

        #region Properties

        /// <summary>
        /// Gets the component title to be displayed to the user.
        /// </summary>
        /// <value>
        /// The component title to be displayed to the user.
        /// </value>
        public override string Title => "Launch Workflow";

        /// <summary>
        /// Gets the icon CSS class used to identify this component type.
        /// </summary>
        /// <value>
        /// The icon CSS class used to identify this component type.
        /// </value>
        public override string IconCssClass => "fa fa-gears";

        /// <summary>
        /// Gets the description of this SMS Action.
        /// </summary>
        /// <value>
        /// The description of this SMS Action.
        /// </value>
        public override string Description => "Launches a workflow to process a message.";

        #endregion

        #region Base Method Overrides

        public override bool ProcessAction( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            action.LoadAttributes();

            var workflowTypeGuid = action.ActionTypeCache.GetAttributeValue( AttributeKey.WorkflowType ).AsGuid();
            var workflowType = WorkflowTypeCache.Get( workflowTypeGuid );

            if ( workflowType == null )
            {
                errorMessages.Add( string.Format( "Configuration Error: Workflow type was not configured or specified correctly." ) );
                return false;
            }

            if ( !( workflowType.IsActive ?? true ) )
            {
                errorMessages.Add( string.Format( "Error: Workflow Type '{0}' is not active.", WorkflowType.FriendlyTypeName ) );
                return false;
            }

            WorkflowService workflowService = new WorkflowService( rockContext );
            Rock.Model.Workflow workflow = null;
            var workflowGuid = action.GetAttributeValue( "Workflow" ).AsGuidOrNull();

            if ( workflowGuid.HasValue )
            {
                workflow = workflowService.Queryable()
                        .Where( w => w.Guid.Equals( workflowGuid.Value ) && w.WorkflowTypeId == workflowType.Id )
                        .FirstOrDefault();
            }

            var entityObject = action.BemaPipeline.GetPipelineEntity( rockContext );

            if ( workflow == null )
            {
                string workflowName = "New " + workflowType.WorkTerm;
                workflow = Rock.Model.Workflow.Activate( workflowType, workflowName, rockContext );
                if ( workflow == null )
                {
                    errorMessages.Add( string.Format( "Workflow Activation Error: Workflow could not be activated." ) );
                    return false;
                }

                //
                // Get the list of workflow attributes to set.
                //
                var workflowAttributesSettings = new List<KeyValuePair<string, object>>();
                var workflowAttributes = action.ActionTypeCache.Attributes["WorkflowAttributes"];
                if ( workflowAttributes != null )
                {
                    if ( workflowAttributes.FieldType.Field is KeyValueListFieldType keyValueField )
                    {
                        workflowAttributesSettings = keyValueField.GetValuesFromString( null,
                        action.ActionTypeCache.GetAttributeValue( AttributeKey.WorkflowAttributes ),
                        workflowAttributes.QualifierValues,
                        false );
                    }
                }

                var mergeValues = GetMergeFields( action, entityObject );

                foreach ( var attribute in workflowAttributesSettings )
                {
                    var value = attribute.Value.ToString().ResolveMergeFields( mergeValues );
                    workflow.SetAttributeValue( attribute.Key,
                        value );
                }

                // Set the workflow name
                var nameTemplate = GetAttributeValue( action.ActionTypeCache, AttributeKey.WorkflowNameTemplate );
                var name = nameTemplate.ResolveMergeFields( mergeValues );
                if ( name.IsNotNullOrWhiteSpace() )
                {
                    workflow.Name = name;
                }

                // Save workflow guid to attribute
                var workflowAttribute = action.Attributes.Where( a => a.Key == "Workflow" );
                if ( workflowAttribute == null )
                {
                    new AttributeService( rockContext ).Add( new Rock.Model.Attribute
                    {
                        Key = "Workflow",
                        Guid = System.Guid.NewGuid(),
                        EntityTypeId = EntityTypeCache.Get( typeof( BemaPipelineAction ) ).Id,
                        EntityTypeQualifierColumn = "BemaPipelineActionTypeId",
                        EntityTypeQualifierValue = action.BemaPipelineActionTypeId.ToString()
                    } );
                    rockContext.SaveChanges();
                    action.LoadAttributes( rockContext );
                }
                action.SetAttributeValue( "Workflow", workflow.Guid.ToString() );
                action.SaveAttributeValue( "Workflow", rockContext );

            }

            workflowService.Process( workflow, entityObject, out errorMessages );

            if ( workflow.CompletedDateTime != null && workflow.CompletedDateTime <= RockDateTime.Now )
            {
                action.BemaPipelineActionState = BemaPipelineActionState.Completed;
                action.CompletedDateTime = RockDateTime.Now;
            }

            rockContext.SaveChanges();

            // Check if the Workflow Saved to the Database.  If 
            if( !( workflow.Id > 0 ) )
            {
                action.BemaPipelineActionState = BemaPipelineActionState.Completed;
                action.CompletedDateTime = RockDateTime.Now;

                rockContext.SaveChanges();

                return true;
            }
            return !workflow.IsActive;

        }

        public override bool TimedOut( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {

            CompleteWorkflow( rockContext, action );

            return base.TimedOut( rockContext, action, entity, out errorMessages );
        }

        public override bool Completed( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {
            CompleteWorkflow(rockContext, action );

            return base.Completed( rockContext, action, entity, out errorMessages );
        }

        public override bool Skipped( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {
            CompleteWorkflow( rockContext, action );

            return base.Completed( rockContext, action, entity, out errorMessages );
        }

        public override bool ActionTypeArchived( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {
            CompleteWorkflow( rockContext, action );

            return base.Completed( rockContext, action, entity, out errorMessages );
        }

        public override List<ActionLink> ActionLinks( RockContext rockContext, BemaPipelineAction action, IEntity entity, Person targetPerson, out List<string> errorMessages )
        {
            List<ActionLink> links = new List<ActionLink>();
            errorMessages = new List<string>();

            if ( action.BemaPipelineActionState == BemaPipelineActionState.ReadyForManualAction ||
                action.BemaPipelineActionState == BemaPipelineActionState.ReadyToProcess )
            {

                var workflowTypeGuid = action.ActionTypeCache.GetAttributeValue( AttributeKey.WorkflowType ).AsGuidOrNull();
                if( !workflowTypeGuid.HasValue )
                {
                    errorMessages.Add( "Workflow Type is not properly configured" );
                    return links;
                }
                var workflowType = WorkflowTypeCache.Get( workflowTypeGuid.Value );

                if( workflowType == null )
                {
                    errorMessages.Add( string.Format( "Unable to find Workflow Type with Guid: {0}", workflowTypeGuid ) );
                    return links;
                }

                var workflowGuid = action.GetAttributeValue( "Workflow" ).AsGuidOrNull();

                bool currentWorkflowExists = ( workflowGuid.HasValue && ( workflowType.IsActive ?? true ) );
                bool canCreateNewWorkflow = ( !action.CompletedDateTime.HasValue && ( workflowType.IsActive ?? true ) );

                if ( currentWorkflowExists )
                {
                    var workflow = new WorkflowService( rockContext ).Get( workflowGuid.Value );

                    if ( workflow != null && !workflow.CompletedDateTime.HasValue )
                    {
                        var activeWorkflowActivitiesList = workflow.Activities.Where( a => a.IsActive ).Where( a =>
                        {
                            if ( !a.AssignedGroupId.HasValue && !a.AssignedPersonAliasId.HasValue )
                            {
                                // not assigned
                                return true;
                            }

                            if ( a.AssignedPersonAlias != null && a.AssignedPersonAlias.PersonId == targetPerson.Id )
                            {
                                // assigned to current person
                                return true;
                            }

                            if ( a.AssignedGroup != null && a.AssignedGroup.Members.Any( m => m.PersonId == targetPerson.Id ) )
                            {
                                // Assigned to a group that the current user is a member of
                                return true;
                            }

                            return false;
                        } );

                        activeWorkflowActivitiesList = activeWorkflowActivitiesList.OrderBy( a => a.ActivityTypeCache.Order ).ToList();

                        foreach ( var activity in activeWorkflowActivitiesList )
                        {
                            if ( activity.ActivityTypeCache.IsAuthorized( Authorization.VIEW, targetPerson ) )
                            {
                                var nextFormAction = activity.ActiveActions
                                    .Where( workflowAction => (workflowAction.ActionTypeCache.WorkflowForm != null || workflowAction.ActionTypeCache.WorkflowAction is Rock.Workflow.Action.ElectronicSignature) && workflowAction.IsCriteriaValid )
                                    .FirstOrDefault();
                                if ( nextFormAction != null )
                                {
                                    string btnClass = nextFormAction.CompletedDateTime.HasValue ? "btn-primary disabled" : "btn-primary";
                                    string btnText = nextFormAction.ActionTypeCache.Name;

                                    Dictionary<string, string> qryParams = new Dictionary<string, string>();
                                    qryParams.Add( "WorkflowTypeId", workflowType.Id.ToString() );
                                    qryParams.Add( "WorkflowId", workflow.Id.ToString() );
                                    qryParams.Add( "ActionId", nextFormAction.Id.ToString() );
                                    var pageReference = new PageReference( action.ActionTypeCache.GetAttributeValue( AttributeKey.WorkflowEntryPage ), qryParams );
                                    var pageUrl = pageReference.PageId > 0 ? pageReference.BuildUrl() : "";

                                    links.Add( new ActionLink
                                    {
                                        ActionId = action.Id,
                                        ButtonUtilityClass = btnClass,
                                        IconCssClass = workflowType.IconCssClass,
                                        Label = btnText,
                                        Url = pageUrl
                                    } );
                                }
                            }
                        }
                    }
                    else if ( workflow != null )
                    {
                        string btnClass = "btn-info";
                        string btnText = workflow.Name;

                        Dictionary<string, string> qryParams = new Dictionary<string, string>();
                        qryParams.Add( "WorkflowTypeId", workflowType.Id.ToString() );
                        qryParams.Add( "WorkflowId", workflow.Id.ToString() );
                        var pageReference = new PageReference( action.ActionTypeCache.GetAttributeValue( AttributeKey.WorkflowEntryPage ), qryParams );
                        var pageUrl = pageReference.PageId > 0 ? pageReference.BuildUrl() : "";

                        links.Add( new ActionLink
                        {
                            ActionId = action.Id,
                            ButtonUtilityClass = btnClass,
                            IconCssClass = workflowType.IconCssClass,
                            Label = btnText,
                            Url = pageUrl
                        } );
                    }
                }
                else if ( canCreateNewWorkflow )
                {
                    //Try to create link to workflow entry for possible non-persisted workflow creation, since action is not marked complete.
                    string btnClass = "btn-primary";
                    string btnText = workflowType.Name;


                    // Set up query string parameters to pass along with Workflow Entry page link
                    Dictionary<string, string> qryParams = new Dictionary<string, string>();
                    qryParams.Add( "WorkflowTypeId", workflowType.Id.ToString() );

                    // Merge workflow attributes from action type config
                    //
                    // Get the list of workflow attributes to set.
                    //
                    var workflowAttributesSettings = new List<KeyValuePair<string, object>>();
                    var workflowAttributes = action.ActionTypeCache.Attributes["WorkflowAttributes"];
                    if ( workflowAttributes != null )
                    {
                        if ( workflowAttributes.FieldType.Field is KeyValueListFieldType keyValueField )
                        {
                            workflowAttributesSettings = keyValueField.GetValuesFromString( null,
                            action.ActionTypeCache.GetAttributeValue( AttributeKey.WorkflowAttributes ),
                            workflowAttributes.QualifierValues,
                            false );
                        }
                    }

                    var entityObject = action.BemaPipeline.GetPipelineEntity( rockContext );

                    var mergeValues = GetMergeFields( action, entityObject );

                    foreach ( var attribute in workflowAttributesSettings )
                    {
                        var value = attribute.Value.ToString().ResolveMergeFields( mergeValues );
                        qryParams.Add( attribute.Key, value );
                    }

                    var pageReference = new PageReference( action.ActionTypeCache.GetAttributeValue( AttributeKey.WorkflowEntryPage ), qryParams );
                    var pageUrl = pageReference.PageId > 0 ? pageReference.BuildUrl() : "";

                    links.Add( new ActionLink
                    {
                        ActionId = action.Id,
                        ButtonUtilityClass = btnClass,
                        IconCssClass = workflowType.IconCssClass,
                        Label = btnText,
                        Url = pageUrl
                    } );
                }
            }

            return links;
        }

        #endregion

        #region Methods

        private void CompleteWorkflow( RockContext rockContext, BemaPipelineAction action )
        {
            action.LoadAttributes();
            WorkflowService workflowService = new WorkflowService( rockContext );

            var workflowGuid = action.GetAttributeValue( "Workflow" ).AsGuidOrNull();

            if ( workflowGuid.HasValue )
            {
                var workflow = workflowService.Get( workflowGuid.Value );

                if ( workflow != null && !workflow.CompletedDateTime.HasValue )
                {
                    workflow.Status = "Completed By Pipeline";
                    workflow.CompletedDateTime = RockDateTime.Now;

                    rockContext.SaveChanges();
                }
            }

            
        }

        #endregion
    }
}

