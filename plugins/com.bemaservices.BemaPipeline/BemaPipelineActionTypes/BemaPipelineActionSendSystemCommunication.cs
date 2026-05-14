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
    /// </summary>
    [Description( "Sends a System Communication" )]
    [Export( typeof( BemaPipelineActionTypeComponent ) )]
    [ExportMetadata( "ComponentName", "Send System Communication" )]

    [SystemCommunicationField( "System Communication",
        Key = AttributeKey.SystemCommunication,
        Description = "The System Communication to Send.",
        IsRequired = true,
        Category = AttributeCategories.Communication,
        Order = 0 )]

    [TextField( "Send To Email Addresses",
        Key = AttributeKey.SendToEmailAddresses,
        Description = "The recipient can either be a group, person or email addresses seperated by commas",
        IsRequired = true,
        DefaultValue = "",
        Category = AttributeCategories.Communication,
        Order = 1 )]

    [BooleanField( "Save Communication History",
        Key = AttributeKey.SaveCommunicationHistory,
        IsRequired = true,
        DefaultBooleanValue = true,
        Category = AttributeCategories.Communication,
        Order = 2 )]
    [CustomDropdownListField ( "Communication Type",
        "Should this communication be Email Only, SMS/Text Only, User Preference, Email Preferred, SMS/Text Preferred.  If Email or Text Preferred, that form of communications will be used if available, otherwise the other form will be used. Note that the System Communication must be configured for the chosen type.",
        "Email Only, SMS/Text Only, User Preference, Email Preferred, SMS/Text Preferred",
        Key = AttributeKey.CommunicationType,
        IsRequired = true,
        DefaultValue = "Email Only",
        Category = AttributeCategories.Communication,
        Order = 3 )]

    public class BemaPipelineActionSendSystemCommunication : BemaPipelineActionTypeComponent
    {
        #region Attribute Keys

        /// <summary>
        /// Keys to use for Block Attributes
        /// </summary>
        private static class AttributeKey
        {
            public const string CommunicationPage = "CommunicationPage";
            public const string SystemCommunication = "SystemCommunication";
            public const string SendToEmailAddresses = "SendToEmailAddresses";
            public const string SaveCommunicationHistory = "SaveCommunicationHistory";
            public const string CommunicationType = "CommuniationType";
        }

        public const string PhoneRegEx = @"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$";

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
        public override string Title => "Send System Communication";

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
        public override string Description => "Sends a System Communication";

        #endregion

        #region Base Method Overrides

        public override bool ProcessAction( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            var entityObject = action.BemaPipeline.GetPipelineEntity( rockContext );
            var mergeValues = GetMergeFields( action, entityObject );
            var emailRecipients = new List<RockMessageRecipient> ();
            var smsRecipients = new List<RockMessageRecipient> ();

            string SendToAddresses = GetAttributeValue( action.ActionTypeCache, AttributeKey.SendToEmailAddresses ).ResolveMergeFields( mergeValues );
            string communicationType = GetAttributeValue ( action.ActionTypeCache, AttributeKey.CommunicationType );

            foreach ( var sendToAddress in SendToAddresses.SplitDelimitedValues ().ToList () )
            {

                Guid? guid = sendToAddress.AsGuidOrNull();

                if ( guid.HasValue )
                {
                    var personAliasService = new PersonAliasService( rockContext );
                    var personService = new PersonService( rockContext );
                    Person person = null;

                    person = personAliasService.Queryable()
                                            .Where( a => a.Guid.Equals( guid.Value ) )
                                            .Select( a => a.Person )
                                            .FirstOrDefault();

                    if ( person == null )
                    {
                        person = personService.Get( guid.Value );
                    }

                    if ( person != null )
                    {
                        var hasValidEmail = false;
                        if ( string.IsNullOrWhiteSpace( person.Email ) )
                        {
                            errorMessages.Add( "Email was not sent: Recipient does not have an email address" );
                        }
                        else if ( !( person.IsEmailActive ) )
                        {
                            errorMessages.Add( "Email was not sent: Recipient email is not active" );
                        }
                        else if ( person.EmailPreference == EmailPreference.DoNotEmail )
                        {
                            errorMessages.Add( "Email was not sent: Recipient has requested 'Do Not Email'" );
                        }
                        else
                        {
                            hasValidEmail = true;
                        }

                        var hasValidSMS = false;
                        var mobileNumber = person.PhoneNumbers.Where ( p => p.NumberTypeValueId == DefinedValueCache.Get ( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE ).Id ).FirstOrDefault();
                        if ( mobileNumber == null )
                        {
                            errorMessages.Add ( "SMS was not sent: Recipient does not have a mobile number" );
                        }
                        else if ( !mobileNumber.IsMessagingEnabled )
                        {
                            errorMessages.Add ( "SMS was not sent: Recipient messaging is not enabled" );
                        }
                        else
                        {
                            hasValidSMS = true;
                        }

                        if ( hasValidEmail
                            && (
                                communicationType == "Email Only"
                                || communicationType == "Email Preferred"
                                || ( communicationType == "User Preference" && person.CommunicationPreference != CommunicationType.SMS )
                                || ( communicationType == "SMS/Text Preferred" && !hasValidSMS )
                                )
                            )
                        {
                            errorMessages.Clear ();
                            var personDict = new Dictionary<string, object> ( mergeValues );
                            personDict.AddOrReplace ( "Person", person );
                            emailRecipients.Add ( new RockEmailMessageRecipient ( person, personDict ) );
                        }
                        else if ( hasValidSMS
                            && (
                                communicationType == "SMS/Text Only"
                                || communicationType == "SMS/Text Preferred"
                                || ( communicationType == "User Preference" && person.CommunicationPreference == CommunicationType.SMS )
                                || ( communicationType == "Email Preferred" && !hasValidEmail)
                                )
                            )
                        {
                            errorMessages.Clear ();
                            var personDict = new Dictionary<string, object> ( mergeValues );
                            personDict.AddOrReplace ( "Person", person );
                            smsRecipients.Add ( new RockSMSMessageRecipient ( person, mobileNumber.Number, personDict ) );
                        }

                    }
                    else
                    {
                        //Check if Guid is a Group
                        IQueryable<GroupMember> qry = null;

                        if ( guid.HasValue )
                        {
                            qry = new GroupMemberService( rockContext ).GetByGroupGuid( guid.Value );
                        }

                        if( qry != null )
                        {
                            foreach ( var p in qry
                                                .Where( m => m.GroupMemberStatus == GroupMemberStatus.Active )
                                                .Select( m => m.Person ) )
                            {
                                var hasValidEmail = false;
                                if ( p.IsEmailActive &&
                                    p.EmailPreference != EmailPreference.DoNotEmail &&
                                    !string.IsNullOrWhiteSpace( p.Email ) )
                                {
                                    hasValidEmail = true;
                                }

                                var hasValidSMS = false;
                                var mobileNumber = p.PhoneNumbers.Where ( n => n.NumberTypeValueId == DefinedValueCache.Get ( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE ).Id ).FirstOrDefault ();
                                if ( mobileNumber != null && mobileNumber.IsMessagingEnabled )
                                {
                                    hasValidSMS = true;
                                }

                                if ( hasValidEmail
                                    && (
                                        communicationType == "Email Only"
                                        || communicationType == "Email Preferred"
                                        || ( communicationType == "User Preference" && person.CommunicationPreference != CommunicationType.SMS )
                                        || ( communicationType == "SMS/Text Preferred" && !hasValidSMS )
                                        )
                                    )
                                {
                                    var personDict = new Dictionary<string, object> ( mergeValues );
                                    personDict.AddOrReplace ( "Person", person );
                                    emailRecipients.Add ( new RockEmailMessageRecipient ( person, personDict ) );
                                }
                                else if ( hasValidSMS
                                    && (
                                        communicationType == "SMS/Text Only"
                                        || communicationType == "SMS/Text Preferred"
                                        || ( communicationType == "User Preference" && person.CommunicationPreference == CommunicationType.SMS )
                                        || ( communicationType == "Email Preferred" && !hasValidEmail)
                                        )
                                    )
                                {
                                    var personDict = new Dictionary<string, object> ( mergeValues );
                                    personDict.AddOrReplace ( "Person", person );
                                    smsRecipients.Add ( new RockSMSMessageRecipient ( person, mobileNumber.Number, personDict ) );
                                }
                            }
                        }
                    }
                }
                else
                {
                    if( sendToAddress.IsValidEmail() && communicationType != "SMS/Text Only" )
                    {
                        emailRecipients.Add( RockEmailMessageRecipient.CreateAnonymous( sendToAddress, mergeValues ) );
                    }
                    else if ( sendToAddress.IsDigitsOnly () && communicationType != "Email Only")
                    {
                        smsRecipients.Add ( RockSMSMessageRecipient.CreateAnonymous ( sendToAddress, mergeValues ) );
                    }
                }

            }

            if ( emailRecipients.Any () )
            {
                var emailMessage = new RockEmailMessage ( GetAttributeValue ( action.ActionTypeCache, AttributeKey.SystemCommunication ).AsGuid () );
                emailMessage.SetRecipients ( emailRecipients );
                emailMessage.CreateCommunicationRecord = GetAttributeValue ( action.ActionTypeCache, AttributeKey.SaveCommunicationHistory ).AsBoolean ();
                emailMessage.Send ();
                action.BemaPipelineActionState = BemaPipelineActionState.Completed;
            }
            if ( smsRecipients.Any ())
            {
                SystemCommunication systemComm = new SystemCommunicationService ( rockContext ).Get ( GetAttributeValue ( action.ActionTypeCache, AttributeKey.SystemCommunication ).AsGuid () );
                var smsMessage = new RockSMSMessage ( systemComm );
                smsMessage.SetRecipients ( smsRecipients );
                smsMessage.CreateCommunicationRecord = GetAttributeValue ( action.ActionTypeCache, AttributeKey.SaveCommunicationHistory ).AsBoolean ();
                smsMessage.Send ();
                action.BemaPipelineActionState = BemaPipelineActionState.Completed;
            }

            if ( ! emailRecipients.Any () && ! smsRecipients.Any () )
            {
                errorMessages.Add( "No Recipients Records Found. No Communication has been sent." );
                action.BemaPipelineActionState = BemaPipelineActionState.Skipped;
            }

            rockContext.SaveChanges();

            return true;
        }

        public override List<ActionLink> ActionLinks( RockContext rockContext, BemaPipelineAction action, IEntity entity, Person targetPerson, out List<string> errorMessages )
        {
            List<ActionLink> links = new List<ActionLink>();
            errorMessages = new List<string>();

            action.LoadAttributes();

            CommunicationService communicationService = new CommunicationService( rockContext );
            Rock.Model.Communication communication = null;

            var communicationGuid = action.GetAttributeValue( "Communication" ).AsGuidOrNull();

            if ( communicationGuid.HasValue )
            {
                communication = communicationService.Get( communicationGuid.Value );

                if ( communication != null )
                {
                    string btnClass = "btn-primary disabled";
                    string btnText = "View Communication";

                    Dictionary<string, string> qryParams = new Dictionary<string, string>();
                    qryParams.Add( "", communication.ToString() );
                    var pageReference = new PageReference( action.ActionTypeCache.GetAttributeValue( AttributeKey.CommunicationPage ), qryParams );
                    var pageUrl = pageReference.PageId > 0 ? pageReference.BuildUrl() : "";

                    links.Add( new ActionLink
                    {
                        ActionId = action.Id,
                        ButtonUtilityClass = btnClass,
                        IconCssClass = this.IconCssClass,
                        Label = btnText,
                        Url = pageUrl
                    } );
                }
            }

            return links;
        }

        #endregion

        #region Methods

        #endregion
    }
}

