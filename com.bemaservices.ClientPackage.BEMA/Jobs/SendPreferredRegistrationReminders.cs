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
using System.Linq;
using System.Text;
using System.Web;

using Quartz;

using Rock.Attribute;
using Rock.Communication;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Rock.Jobs
{

    /// <summary>
    /// Job to process event registration reminders
    /// </summary>
    [DisplayName( "Preferred Registration Reminder" )]
    [Description( "Send any registration reminders that are due to be sent via the user's preferred method of communication." )]
    [SystemCommunicationField( "System Communication", "The communication to send.", true, "", "", 0, "SystemCommunication" )]
    [IntegerField( "Expire Date", "The number of days past the registration reminder to refrain from sending the email. This would only be used if something went wrong and acts like a safety net to prevent sending the reminder after the fact.", true, 1, key: "ExpireDate" )]
    [DisallowConcurrentExecution]
    public class SendPreferredRegistrationReminders : IJob
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SendPreferredRegistrationReminders"/> class.
        /// </summary>
        public SendPreferredRegistrationReminders()
        {
        }

        /// <summary>
        /// Executes the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        public virtual void Execute( IJobExecutionContext context )
        {
            JobDataMap dataMap = context.JobDetail.JobDataMap;

            var expireDays = dataMap.GetString( "ExpireDate" ).AsIntegerOrNull() ?? 1;
            var publicAppRoot = GlobalAttributesCache.Get().GetValue( "PublicApplicationRoot" );
            var systemCommunicationGuid = dataMap.GetString( "SystemCommunication" ).AsGuid();

            int remindersSent = 0;
            var errors = new List<string>();

            var results = new StringBuilder();
            var result = new SendMessageResult();

            using ( var rockContext = new RockContext() )
            {
                var systemCommunication = new SystemCommunicationService( rockContext ).Get( systemCommunicationGuid );
                if ( systemCommunication != null )
                {
                    var isSmsEnabled = MediumContainer.HasActiveSmsTransport() && !string.IsNullOrWhiteSpace( systemCommunication.SMSMessage );

                    DateTime now = RockDateTime.Now;
                    DateTime expireDate = now.AddDays( expireDays * -1 );

                    foreach ( var instance in new RegistrationInstanceService( rockContext )
                        .Queryable( "RegistrationTemplate,Registrations" )
                        .Where( i =>
                            i.IsActive &&
                            i.RegistrationTemplate.IsActive &&
                            !i.ReminderSent &&
                            i.SendReminderDateTime.HasValue &&
                            i.SendReminderDateTime <= now &&
                            i.SendReminderDateTime >= expireDate )
                        .ToList() )
                    {
                        var template = instance.RegistrationTemplate;

                        foreach ( var registration in instance.Registrations
                            .Where( r =>
                                !r.IsTemporary &&
                                r.ConfirmationEmail != null &&
                                r.ConfirmationEmail != string.Empty ) )
                        {
                            try
                            {
                                var mediumType = Rock.Model.Communication.DetermineMediumEntityTypeId(
                                    ( int ) CommunicationType.Email,
                                    ( int ) CommunicationType.SMS,
                                    ( int ) CommunicationType.PushNotification,
                                    registration.PersonAlias.Person.CommunicationPreference );

                                var mergeObjects = Rock.Lava.LavaHelper.GetCommonMergeFields( null, registration.PersonAlias.Person );
                                mergeObjects.Add( "RegistrationInstance", registration.RegistrationInstance );
                                mergeObjects.Add( "Registration", registration );

                                var sendResult = CommunicationHelper.SendMessage( registration.PersonAlias.Person, mediumType, systemCommunication, mergeObjects );

                                result.MessagesSent += sendResult.MessagesSent;
                                result.Errors.AddRange( sendResult.Errors );
                                result.Warnings.AddRange( sendResult.Warnings );
                            }
                            catch ( Exception exception )
                            {
                                ExceptionLogService.LogException( exception );
                                continue;
                            }
                        }

                        // Even if an error occurs, still mark as completed to prevent _everyone_ being sent the reminder multiple times due to a single failing address


                        instance.SendReminderDateTime = now;
                        instance.ReminderSent = true;

                        rockContext.SaveChanges();
                    }
                }

                results.AppendLine( $"{result.MessagesSent} attendance reminders sent." );
                results.Append( FormatWarningMessages( result.Warnings ) );
                context.Result = results.ToString();

                if ( errors.Any() )
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine();
                    sb.Append( string.Format( "{0} Errors: ", errors.Count() ) );
                    errors.ForEach( e => { sb.AppendLine(); sb.Append( e ); } );
                    string errorMessage = sb.ToString();
                    context.Result += errorMessage;
                    var exception = new Exception( errorMessage );
                    System.Web.HttpContext context2 = HttpContext.Current;
                    ExceptionLogService.LogException( exception, context2 );
                    throw exception;
                }
            }
        }

        private StringBuilder FormatWarningMessage( string warning )
        {
            var errorMessages = new List<string> { warning };
            return FormatMessages( errorMessages, "Warning" );
        }

        private StringBuilder FormatWarningMessages( List<string> warnings )
        {
            return FormatMessages( warnings, "Warning" );
        }

        private StringBuilder FormatMessages( List<string> messages, string label )
        {
            StringBuilder sb = new StringBuilder();
            if ( messages.Any() )
            {
                var pluralizedLabel = label.PluralizeIf( messages.Count > 1 );
                sb.AppendLine( $"{messages.Count} {pluralizedLabel}:" );
                messages.ForEach( w => { sb.AppendLine( w ); } );
            }
            return sb;
        }
    }
}