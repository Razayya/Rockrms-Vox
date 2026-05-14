using Rock.Plugin;

namespace com.razayya.RSVPReminders.Migrations
{
    [MigrationNumber(4, "1.15.0")]
    public class AddRsvpInvitationCommunication : Migration
    {
        public override void Up()
        {
            RockMigrationHelper.UpdateSystemCommunication(
                            "RSVP",
                            "RSVP Invitation",
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            "Invitation to {{ Group.Name }}",
                            @"{{ 'Global' | Attribute:'EmailHeader' }}
            <p>Hello {{ Person.NickName }},</p>
            <p>You are invited to {{ Group.Name }} on {{ Occurrence.OccurrenceDate | Date:'dddd, MMMM d, yyyy' }}.</p>
            <table class='rsvp-outerwrap' border='0' cellpadding='0' width='100%' style='min-width:100%;'>
              <tr>
                <td style='padding-top:0; padding-right:0; padding-bottom:0; padding-left:0;' valign='top' align='center' class='rsvp-innerwrap'>
                  <table border='0' cellpadding='0' cellspacing='0'>
                    <tr>
                      <td>
                        <table border='0' cellpadding='0' cellspacing='0' class='accept-button-shell' style='display: inline-table; border-collapse: separate !important; border-radius: 3px; background-color: #16C98D;'>
                          <tr>
                            <td align='center' valign='middle' class='rsvp-accept-content' style='font-family: Arial; font-size: 16px; padding: 15px;'>
                              <a class='rsvp-accept-link' title='Accept' href='{{ 'Global' | Attribute:'PublicApplicationRoot' }}RSVP?p={{ Person | PersonActionIdentifier:'RSVP' }}&AcceptButtonText=Accept&AcceptButtonColor=%2316C98D&AcceptButtonFontColor=%23FFFFFF&DeclineButtonText=Decline&DeclineButtonColor=%23D4442E&DeclineButtonFontColor=%23FFFFFF&AttendanceOccurrenceId={{ Occurrence.Id }}&isAccept=1' target='_blank' rel='noopener noreferrer' style='font-weight: bold; letter-spacing: normal; line-height: 100%; text-align: center; text-decoration: none; color: #FFFFFF;'>Accept</a>
                            </td>
                          </tr>
                        </table>
                      </td>
                      <td style='padding-left: 10px;'>
                        <table border='0' cellpadding='0' cellspacing='0' class='decline-button-shell' style='display: inline-table; border-collapse: separate !important; border-radius: 3px; background-color: #D4442E;'>
                          <tr>
                            <td align='center' valign='middle' class='rsvp-decline-content' style='font-family: Arial; font-size: 16px; padding: 15px;'>
                              <a class='rsvp-decline-link' title='Decline' href='{{ 'Global' | Attribute:'PublicApplicationRoot' }}RSVP?p={{ Person | PersonActionIdentifier:'RSVP' }}&AcceptButtonText=Accept&AcceptButtonColor=%2316C98D&AcceptButtonFontColor=%23FFFFFF&DeclineButtonText=Decline&DeclineButtonColor=%23D4442E&DeclineButtonFontColor=%23FFFFFF&AttendanceOccurrenceId={{ Occurrence.Id }}&isAccept=0' target='_blank' style='font-weight: bold; letter-spacing: normal; line-height: 100%; text-align: center; text-decoration: none; color: #FFFFFF;'>Decline</a>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            <p>Thanks!</p>
            <p>{{ 'Global' | Attribute:'OrganizationName' }}</p>
            {{ 'Global' | Attribute:'EmailFooter' }}",
                SystemGuid.SystemCommunication.RSVP_INVITATION,
                true);
        }

        public override void Down()
        {
        }
    }
}