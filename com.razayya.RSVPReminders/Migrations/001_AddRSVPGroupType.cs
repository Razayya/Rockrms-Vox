using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rock.Plugin;

using Rock.Web.Cache;
using Rock.Lava.Blocks;
using System.Security.AccessControl;
using Rock;

namespace com.razayya.RSVPReminders.Migrations
{
    public class AddRSVPGroupType : Migration
    {
        public override void Up()
        {
            RockMigrationHelper.AddGroupType("RSVP Reminder Master Group", "Group Type that must be inherited for the EnhancedRSVPReminder Job to function.",
                                             "Group", "Member", true, true, true, "", 0, null, 3, null, SystemGuid.GroupType.AUTO_RSVP_GROUP);
            RockMigrationHelper.AddGroupTypeGroupAttribute(SystemGuid.GroupType.AUTO_RSVP_GROUP, Rock.SystemGuid.FieldType.BOOLEAN, "Sends RSVP Emails",
                                             "", 0, "True", SystemGuid.Attribute.SEND_RSVP_EMAILS);
        }

        public override void Down()
        {
        }
    }
}
