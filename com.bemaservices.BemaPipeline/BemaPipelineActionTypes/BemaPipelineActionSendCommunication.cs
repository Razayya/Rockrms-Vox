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
using System;
using Rock.Communication;

namespace com.bemaservices.BemaPipeline.BemaPipelineActionTypes
{
    /// <summary>
    /// Processes Action by launching a workflow.
    /// </summary>have 
    [Description ( "Sends a Communication" )]
    [Export ( typeof ( BemaPipelineActionTypeComponent ) )]
    [ExportMetadata ( "ComponentName", "Send Communication" )]

    [CommunicationTemplateField ( "Communication Template",
        Key = AttributeKey.CommunicationTemplate,
        Description = "The Communication Template to use in creating the Communication.",
        IsRequired = false,
        Category = AttributeCategories.Communication,
        Order = 0 )]

    [TextField ( "Send To Email Addresses",
        Key = AttributeKey.SendToEmailAddresses,
        Description = "The recipient can either be a group, person or email addresses seperated by commas",
        IsRequired = true,
        DefaultValue = "",
        Category = AttributeCategories.Communication,
        Order = 1 )]

    [BooleanField ( "Save Communication History",
        Key = AttributeKey.SaveCommunicationHistory,
        IsRequired = true,
        DefaultBooleanValue = true,
        Category = AttributeCategories.Communication,
        Order = 2 )]

    [LinkedPage ( "Communication Page",
        Key = AttributeKey.CommunicationPage,
        Category = AttributeCategories.Communication,
        Description = "Page to redirect to for sending the communication.",
        IsRequired = true,
        DefaultValue = "7E8408B2-354C-4A5A-8707-36754AE80B9A",
        Order = 3 )]

    [LinkedPage ( "Communication History Page",
        Key = AttributeKey.CommunicationHistoryPage,
        Category = AttributeCategories.Communication,
        Description = "Page to redirect to for showing the communication history.",
        IsRequired = true,
        DefaultValue = "2a22d08d-73a8-4aaf-ac7e-220e8b2e7857",
        Order = 3 )]

    public class BemaPipelineActionSendCommunication : BemaPipelineActionTypeComponent
    {
        #region Attribute Keys

        /// <summary>
        /// Keys to use for Block Attributes
        /// </summary>
        private static class AttributeKey
        {
            public const string CommunicationPage = "CommunicationPage";
            public const string CommunicationHistoryPage = "CommunicationHistoryPage";
            public const string CommunicationTemplate = "CommunicationTemplate";
            public const string SendToEmailAddresses = "SendToEmailAddresses";
            public const string SaveCommunicationHistory = "SaveCommunicationHistory";
            public const string Communication = "Communication";
        }

        /// <summary>
        /// Categories for the attributes
        /// </summary>
        protected class AttributeCategories : BaseAttributeCategories
        {
            /// <summary>
            /// The Workflow category
            /// </summary>
            public const string Communication = "Communication";
        }


        #endregion Attribute Keys

        #region Properties

        /// <summary>
        /// Gets the component title to be displayed to the user.
        /// </summary>
        /// <value>
        /// The component title to be displayed to the user.
        /// </value>
        public override string Title => "Send Communication";

        /// <summary>
        /// Gets the icon CSS class used to identify this component type.
        /// </summary>
        /// <value>
        /// The icon CSS class used to identify this component type.
        /// </value>
        public override string IconCssClass => "fa fa-envelope";

        /// <summary>
        /// Gets the description of this SMS Action.
        /// </summary>
        /// <value>
        /// The description of this SMS Action.
        /// </value>
        public override string Description => "Sends a Communication using a template";

        #endregion

        #region Base Method Overrides

        public override bool ProcessAction( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {
            errorMessages = new List<string> ();

            var entityObject = action.BemaPipeline.GetPipelineEntity ( rockContext );
            var mergeValues = GetMergeFields ( action, entityObject );
            var recipients = new List<CommunicationRecipient> ();

            action.LoadAttributes ();
            var existingCommunicationGuid = action.GetAttributeValue ( AttributeKey.Communication ).AsGuidOrNull ();

            if ( existingCommunicationGuid.HasValue )
            {
                // Check if communication has been sent
                var existingCommunication = new CommunicationService ( rockContext ).Get ( existingCommunicationGuid.Value );
                if ( existingCommunication != null && existingCommunication.Status == CommunicationStatus.Approved )
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            string SendToAdresses = GetAttributeValue ( action.ActionTypeCache, AttributeKey.SendToEmailAddresses ).ResolveMergeFields ( mergeValues );

            Guid? guid = SendToAdresses.AsGuidOrNull ();

            if ( guid.HasValue )
            {
                var personAliasService = new PersonAliasService ( rockContext );
                var personService = new PersonService ( rockContext );
                Person person = null;

                person = personAliasService.Queryable ()
                                        .Where ( a => a.Guid.Equals ( guid.Value ) )
                                        .Select ( a => a.Person )
                                        .FirstOrDefault ();

                if ( person == null )
                {
                    person = personService.Get ( guid.Value );
                }

                if ( person != null )
                {
                    if ( string.IsNullOrWhiteSpace ( person.Email ) )
                    {
                        errorMessages.Add ( "Email was not sent: Recipient does not have an email address" );
                    }
                    else if ( !( person.IsEmailActive ) )
                    {
                        errorMessages.Add ( "Email was not sent: Recipient email is not active" );
                    }
                    else if ( person.EmailPreference == EmailPreference.DoNotEmail )
                    {
                        errorMessages.Add ( "Email was not sent: Recipient has requested 'Do Not Email'" );
                    }
                    else
                    {
                        var personDict = new Dictionary<string, object> ( mergeValues );
                        personDict.AddOrReplace ( "Person", person );
                        var communicationRecipient = new CommunicationRecipient ();
                        communicationRecipient.PersonAlias = person.PrimaryAlias;
                        communicationRecipient.Status = CommunicationRecipientStatus.Pending;
                        recipients.Add ( communicationRecipient );
                    }
                }
                else
                {
                    //Check if Guid is a Group
                    IQueryable<GroupMember> qry = null;

                    if ( guid.HasValue )
                    {
                        qry = new GroupMemberService ( rockContext ).GetByGroupGuid ( guid.Value );
                    }

                    if ( qry != null )
                    {
                        foreach ( var p in qry
                                            .Where ( m => m.GroupMemberStatus == GroupMemberStatus.Active )
                                            .Select ( m => m.Person ) )
                        {
                            if ( p.IsEmailActive &&
                                p.EmailPreference != EmailPreference.DoNotEmail &&
                                !string.IsNullOrWhiteSpace ( p.Email ) )
                            {
                                var personDict = new Dictionary<string, object> ( mergeValues );
                                personDict.AddOrReplace ( "Person", person );
                                var communicationRecipient = new CommunicationRecipient ();
                                communicationRecipient.PersonAlias = p.PrimaryAlias;
                                communicationRecipient.Status = CommunicationRecipientStatus.Pending;
                                recipients.Add ( communicationRecipient );
                            }
                        }
                    }
                }
            }
            else
            {
                // communicaitons don't support anonymous recipients - display error
                errorMessages.Add ( "Email was not sent: Communications must be sent to a Person in Rock." );

                //var recipientList = SendToAdresses.SplitDelimitedValues ().ToList ();
                //foreach ( string recipient in recipientList )
                //{
                //    if ( recipient.IsValidEmail () )
                //    {
                //        recipients.Add ( RockEmailMessageRecipient.CreateAnonymous ( recipient, mergeValues ) );
                //    }
                //}
            }

            if ( recipients.Any () )
            {
                var communication = new Rock.Model.Communication () { Status = CommunicationStatus.Transient };
                //communication.CreatedByPersonAlias = this.CurrentPersonAlias;
                //communication.CreatedByPersonAliasId = this.CurrentPersonAliasId;
                //communication.SenderPersonAlias = this.CurrentPersonAlias;
                //communication.SenderPersonAliasId = CurrentPersonAliasId;
                var template = GetAttributeValue ( action.ActionTypeCache, AttributeKey.CommunicationTemplate ).AsGuidOrNull ();
                if ( template.HasValue )
                {
                    communication.CommunicationTemplate = new CommunicationTemplateService ( rockContext ).Get ( template.Value );
                    communication.CommunicationTemplateId = communication.CommunicationTemplate.Id;
                }

                recipients.ForEach ( t => communication.Recipients.Add ( t ) );

                var communicationService = new CommunicationService ( rockContext );
                communicationService.Add ( communication );

                rockContext.SaveChanges ();


                // Save communication guid to attribute
                var communicationAttribute = action.Attributes.Where ( a => a.Key == AttributeKey.Communication );
                if ( communicationAttribute.Count () == 0 )
                {
                    new AttributeService ( rockContext ).Add ( new Rock.Model.Attribute
                    {
                        Name = AttributeKey.Communication,
                        Key = AttributeKey.Communication,
                        Guid = System.Guid.NewGuid (),
                        FieldTypeId = FieldTypeCache.Get ( Rock.SystemGuid.FieldType.TEXT ).Id,
                        EntityTypeId = EntityTypeCache.Get ( typeof ( BemaPipelineAction ) ).Id,
                        EntityTypeQualifierColumn = "BemaPipelineActionTypeId",
                        EntityTypeQualifierValue = action.BemaPipelineActionTypeId.ToString ()
                    } );
                    rockContext.SaveChanges ();
                    action.LoadAttributes ( rockContext );
                }
                action.SetAttributeValue ( AttributeKey.Communication, communication.Guid.ToString () );
                action.SaveAttributeValue ( AttributeKey.Communication, rockContext );

                action.BemaPipelineActionState = BemaPipelineActionState.WaitingOnItems;
                rockContext.SaveChanges ();

                // redirect to communication page will be done through the links, so we're done for now.

                return false;
            }
            else
            {
                errorMessages.Add ( "No Recipients Records Found. No Communication has been sent." );
                action.BemaPipelineActionState = BemaPipelineActionState.Skipped;
            }

            rockContext.SaveChanges ();

            return true;
        }

        public override List<ActionLink> ActionLinks( RockContext rockContext, BemaPipelineAction action, IEntity entity, Person targetPerson, out List<string> errorMessages )
        {
            List<ActionLink> links = new List<ActionLink> ();
            errorMessages = new List<string> ();

            action.LoadAttributes ();

            CommunicationService communicationService = new CommunicationService ( rockContext );
            Rock.Model.Communication communication = null;

            var communicationGuid = action.GetAttributeValue ( AttributeKey.Communication ).AsGuidOrNull ();

            if ( communicationGuid.HasValue )
            {
                communication = communicationService.Get ( communicationGuid.Value );

                if ( communication != null )
                {
                    if ( communication.Status == CommunicationStatus.Approved
                        || communication.Status == CommunicationStatus.PendingApproval
                        || communication.Status == CommunicationStatus.Denied )
                    {
                        string btnClass = "btn-primary";
                        string btnText = "View Communication";

                        Dictionary<string, string> qryParams = new Dictionary<string, string> ();
                        qryParams.Add ( "CommunicationId", communication.Id.ToString () );
                        var pageReference = new PageReference ( action.ActionTypeCache.GetAttributeValue ( AttributeKey.CommunicationHistoryPage ), qryParams );
                        var pageUrl = pageReference.PageId > 0 ? pageReference.BuildUrl () : "";

                        links.Add ( new ActionLink
                        {
                            ActionId = action.Id,
                            ButtonUtilityClass = btnClass,
                            IconCssClass = this.IconCssClass,
                            Label = btnText,
                            Url = pageUrl
                        } );
                    }
                    else if ( communication.Status == CommunicationStatus.Transient
                        || communication.Status == CommunicationStatus.Draft )
                    {
                        string btnClass = "btn-primary";
                        string btnText = "Send Communication";

                        Dictionary<string, string> qryParams = new Dictionary<string, string> ();
                        qryParams.Add ( "CommunicationId", communication.Id.ToString () );
                        var pageReference = new PageReference ( action.ActionTypeCache.GetAttributeValue ( AttributeKey.CommunicationPage ), qryParams );
                        var pageUrl = pageReference.PageId > 0 ? pageReference.BuildUrl () : "";

                        links.Add ( new ActionLink
                        {
                            ActionId = action.Id,
                            ButtonUtilityClass = btnClass,
                            IconCssClass = this.IconCssClass,
                            Label = btnText,
                            Url = pageUrl
                        } );
                    }
                }
            }

            return links;
        }

        #endregion

        #region Methods

        #endregion
    }
}

