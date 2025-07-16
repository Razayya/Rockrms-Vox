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
    [MigrationNumber( 2, "1.15.0" )]
    public class PopulateInheritedGroupTypeForRSVP : Migration
    {
        public override void Up()
        {
            Sql(@"
            DECLARE @GroupTypeId INT = (SELECT TOP 1 Id FROM [GroupType] WHERE [Guid] = '"+SystemGuid.Guids.AUTO_RSVP_GROUP.ToString()+ @"')

            ;WITH RSVPGroupTypes AS
                (SELECT Id,InheritedGroupTypeId 
                FROM GroupType
                WHERE EnableRsvp = 1),
                TypesWithInheritedGroupTypes AS
                (SELECT * FROM RSVPGroupTypes
                
                UNION ALL
                
                SELECT GT.Id,GT.InheritedGroupTypeId
                FROM GroupType GT 
                JOIN RSVPGroupTypes CTE
                	ON CTE.InheritedGroupTypeId = GT.Id
                
                UNION ALL
                
                SELECT GT.Id,GT.InheritedGroupTypeId
                FROM GroupType GT
                JOIN TypesWithInheritedGroupTypes IGT
                	ON IGT.InheritedGroupTypeId = GT.Id
                )
                
             UPDATE GT
             SET InheritedGroupTypeId = @GroupTypeId
             FROM GroupType GT
             JOIN TypesWithInheritedGroupTypes CTE
             	ON GT.Id = CTE.Id
             WHERE GT.InheritedGroupTypeId IS NULL

            ");
        }

        public override void Down()
        {
        }
    }
}
