using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.razayya.RSVPReminders.Constants
{
    public static class AttributeKey
    {
        public const string AutoRSVPGroupType = "AutoRSVPGroupType";
        public const string SendReminders = "SendReminders";
        public const string SendsRsvpEmails = "SendsRsvpEmails";
        public const string ShowDebug = "ShowDebug";
    }

    public static class CRON
    {
        public const string AutoRSVPCronExpression = "0 0 7 1/1 * ? *"; //Every 4 Hours
        
    }

}
