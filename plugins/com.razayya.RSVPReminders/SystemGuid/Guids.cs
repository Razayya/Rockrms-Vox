using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.razayya.RSVPReminders.SystemGuid
{
    public static class GroupType
    {
        public const string AUTO_RSVP_GROUP = "E99EA6F3-F6F6-47F5-8B1B-30665535FB90";
    }

    public static class GroupAttribute
    {
        public const string SEND_RSVP_EMAILS = "D391A559-9F15-4646-8500-7E88531C34DC";
        public const string LAST_AUTO_RSVP_RUN = "2E3F82B3-78AA-4988-A599-6601F2A38C8F";
    }

    public static class ServiceJob
    {
        public const string AUTO_RSVP_JOB = "429B06CA-037F-466E-BF12-3A0364B60849";
    }

    public static class SystemCommunication
    {
        public const string RSVP_INVITATION = "8A001F97-7B72-441E-A678-88D09E1BE7F8";
    }
}
