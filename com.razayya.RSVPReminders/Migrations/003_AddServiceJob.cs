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
    [MigrationNumber(3, "1.15.0")]
    public class AddServiceJob : Migration
    {
        public override void Up()
        {
            RockMigrationHelper.AddPostUpdateServiceJob("Auto RSVP Reminders", "This job will send and populate RSVPs for groups that inherit from the Auto RSVP Group type configured.", typeof(Jobs.AutoRSVPReminders).ToString(), Constants.CRON.AutoRSVPCronExpression, SystemGuid.ServiceJob.AUTO_RSVP_JOB);
        }

        public override void Down()
        {
        }
    }
}
