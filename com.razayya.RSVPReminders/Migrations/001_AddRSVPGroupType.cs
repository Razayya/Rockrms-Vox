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
    [MigrationNumber(1, "1.15.0")]
    public class AddRSVPGroupType : Migration
    {
        public override void Up()
        {
            RockMigrationHelper.AddGroupType("Auto RSVP Reminder Group", "Group Type that must be inherited for the AutoRSVPReminder Job to function.",
                                             "Group", "Member", true, true, true, "", 0, null, 3, null, SystemGuid.Guids.AUTO_RSVP_GROUP);
            RockMigrationHelper.AddGroupTypeGroupAttribute(SystemGuid.Guids.AUTO_RSVP_GROUP, Rock.SystemGuid.FieldType.BOOLEAN, "Sends RSVP Emails",
                                             "", 0, "False", SystemGuid.GroupAttribute.SEND_RSVP_EMAILS, true);
        }

        public override void Down()
        {
        }
    }
}
