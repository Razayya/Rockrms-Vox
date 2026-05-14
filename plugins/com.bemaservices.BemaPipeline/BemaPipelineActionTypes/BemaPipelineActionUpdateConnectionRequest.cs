using System;
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
using com.bemaservices.BemaPipeline.Rest.API;

namespace com.bemaservices.BemaPipeline.BemaPipelineActionTypes
{
    /// <summary>
    /// Updates properties on the associated Connection Request.
    /// </summary>
    [Description("Updates properties on the associated Connection Request.")]
    [Export(typeof(BemaPipelineActionTypeComponent))]
    [ExportMetadata("ComponentName", "Update Connection Request")]

    [ConnectionStateField("New Connection Request State",
        Key = AttributeKey.ConnectionRequestState,
        Category = AttributeCategories.ConnectionRequest,
        Description = "The new state for the connection request. <span class='tip tip-lava'></span>",
        IsRequired = false,
        Order = 1)]

    [IntegerField("Future Follow-Up Days",
        Key = AttributeKey.FutureFollowUpDays,
        Category = AttributeCategories.ConnectionRequest,
        Description = "If the new state is Future Follow-Up, the number of days in the future used to set the date. <span class='tip tip-lava'></span>",
        IsRequired = false,
        Order = 2)]

    [ConnectionStatusField ( "New Connection Request Status",
        "The new connection status for the connection request.  Note that this must match the Connection Opportunity Type for the Connection Request. <span class='tip tip-lava'></span>",
        false,
        "",
        false,
        Category = AttributeCategories.ConnectionRequest,
        Order = 3,
        Key = AttributeKey.ConnectionRequestStatus
        )]

    [PersonField ( "New Connector Person",
        Key = AttributeKey.ConnectorPerson,
        Category = AttributeCategories.ConnectionRequest,
        Description = "The new person to assign as connector for the connection request. <span class='tip tip-lava'></span>",
        IsRequired = false,
        Order = 4 )]

    [KeyValueListField("Connection Request Attributes",
        Key = AttributeKey.ConnectionRequestAttributes,
        Category = AttributeCategories.ConnectionRequest,
        Description = "Key/value list of connection request attributes to set with the given lava merge template. See the defined type’s help text for a listing of merge fields. <span class='tip tip-lava'></span>",
        IsRequired = false,
        DefaultValue = "",
        KeyPrompt = "Attribute Key",
        ValuePrompt = "Value (lava enabled)",
        Order = 5)]

    public class BemaPipelineActionUpdateConnectionRequest : BemaPipelineActionTypeComponent
    {
        #region Attribute Keys 

        /// <summary>
        /// Keys to use for Block Attributes
        /// </summary>
        private static class AttributeKey
        {
            public const string ConnectionRequestState = "State";
            public const string FutureFollowUpDays = "FutureFollowUpDays";
            public const string ConnectionRequestStatus = "Status";
            public const string ConnectorPerson = "ConnectorPerson";
            public const string ConnectionRequestAttributes = "ConnectionRequestAttributes";
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
            public const string ConnectionRequest = "ConnectionRequest";
        }

        #region Properties

        /// <summary>
        /// Gets the component title to be displayed to the user.
        /// </summary>
        /// <value>
        /// The component title to be displayed to the user.
        /// </value>
        public override string Title => "Update Connection Request";

        /// <summary>
        /// Gets the icon CSS class used to identify this component type.
        /// </summary>
        /// <value>
        /// The icon CSS class used to identify this component type.
        /// </value>
        public override string IconCssClass => "fa fa-plug";

        /// <summary>
        /// Gets the description of this Action.
        /// </summary>
        /// <value>
        /// The description of this Action.
        /// </value>
        public override string Description => "Updates properties/attributes on the associated Connection Request.";

        #endregion

        #region Base Method Overrides

        public override bool ProcessAction(RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages)
        {
            errorMessages = new List<string>();
            var crEntityType = EntityTypeCache.Get ( Rock.SystemGuid.EntityType.CONNECTION_REQUEST );

            if ( entity.TypeId != crEntityType.Id )
            {
                errorMessages.Add ( "Error - this action requires a Connection Request entity." );
                return false;
            }
            //var connectionRequest = new ConnectionRequestService ( rockContext ).Get ( entity.Id );
            var connectionRequest = action.BemaPipeline.GetPipelineEntity( rockContext ) as ConnectionRequest;

            if ( connectionRequest != null )
            {
                var newState = action.ActionTypeCache.GetAttributeValue ( AttributeKey.ConnectionRequestState ).AsIntegerOrNull ();
                var newStatus = action.ActionTypeCache.GetAttributeValue ( AttributeKey.ConnectionRequestStatus ).AsGuidOrNull ();
                var followUpDays = action.ActionTypeCache.GetAttributeValue ( AttributeKey.FutureFollowUpDays ).AsIntegerOrNull ();
                var newConnector = action.ActionTypeCache.GetAttributeValue ( AttributeKey.ConnectorPerson ).AsGuidOrNull ();

                if ( newState.HasValue )
                {
                    connectionRequest.ConnectionState = ( ConnectionState ) newState.Value;
                    if ( ( ConnectionState ) newState.Value == ConnectionState.FutureFollowUp )
                    {
                         connectionRequest.FollowupDate = System.DateTime.Now.AddDays ( followUpDays.HasValue ? followUpDays.Value : 1);
                    }
                }

                if ( newStatus != null )
                {
                    connectionRequest.ConnectionStatus = new ConnectionStatusService ( rockContext ).Get ( newStatus.Value );
                }

                if ( newConnector != null )
                {
                    connectionRequest.ConnectorPersonAliasId = new PersonAliasService ( rockContext ).Get ( newConnector.Value ).Id;
                }

                rockContext.SaveChanges ();

                // Handle attributes
                var crAttributesSettings = new List<KeyValuePair<string, object>> ();
                var crAttributes = action.ActionTypeCache.Attributes[AttributeKey.ConnectionRequestAttributes];
                if ( crAttributes != null )
                {
                    connectionRequest.LoadAttributes ();

                    if ( crAttributes.FieldType.Field is KeyValueListFieldType keyValueField )
                    {
                        crAttributesSettings = keyValueField.GetValuesFromString ( null,
                            action.ActionTypeCache.AttributeValues[AttributeKey.ConnectionRequestAttributes].Value,
                            crAttributes.QualifierValues,
                            false );
                    }



                    // Create a list of mergefields used to update attribute values
                    //var mergeValues = new Dictionary<string, object>
                    //{
                    //    { "ConnectionRequestId", action.BemaPipeline.EntityId },
                    //    { "PipelineId", action.BemaPipeline.BemaPipelineTypeId }
                    //};

                    var mergeValues = GetMergeFields( action, connectionRequest );

                    // Set any other connection request attributes that could have lava
                    foreach ( var attribute in crAttributesSettings )
                    {
                        connectionRequest.SetAttributeValue ( attribute.Key,
                            attribute.Value.ToString ().ResolveMergeFields ( mergeValues ) );
                    }

                    connectionRequest.SaveAttributeValues ();
                }

            }

            try
            {
                action.BemaPipelineActionState = BemaPipelineActionState.Completed;
                action.LastProcessedDateTime = RockDateTime.Now;
                action.CompletedDateTime = RockDateTime.Now;

                rockContext.SaveChanges ();
            }
            catch ( Exception ex )
            {
                errorMessages.Add ( string.Format ( "Could not complete pipeline action ('{0}')! {1}", action.Name, ex.Message ) );
                return false;
            }

            return true;
        }

        public override List<ActionLink> ActionLinks(RockContext rockContext, BemaPipelineAction action, IEntity entity, Person targetPerson, out List<string> errorMessages)
        {
            List<ActionLink> links = new List<ActionLink>();
            errorMessages = new List<string>();

            return links;
        }

        #endregion
    }
}

