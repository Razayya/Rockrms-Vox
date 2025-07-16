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
    //[MigrationNumber(3, "1.15.0")]
    //TODO: Get Valid values for this to add the job in on reregistry.
    public class AddServiceJob : Migration
    {
        public override void Up()
        {
            RockMigrationHelper.AddPostUpdateServiceJob("", "", "", "", "");
        }

        public override void Down()
        {
        }
    }
}
