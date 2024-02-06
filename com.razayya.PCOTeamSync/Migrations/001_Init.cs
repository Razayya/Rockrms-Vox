using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rock.Lava.RockLiquid.Blocks;
using Rock.Plugin;
using Rock.Web.Cache;

namespace com.razayya.PCOTeamSync.Migrations
{
    [MigrationNumber( 1, "1.13.0" )]
    public class Init : Migration
    {
        public override void Up()
        {

            RockMigrationHelper.UpdateGroup(
                Guid.NewGuid().ToString(), // Needs to be a guid, but there is no parent group.
                Rock.SystemGuid.GroupType.GROUPTYPE_GENERAL,
                "Planning Center Online",
                "Where you can find all your Planning Center Online Services, Teams, and Positions.  Anyone added to a position will be added to that position and team in PCO.",
                Guid.NewGuid().ToString(), //Needs to be a real Guid, but don't want assign to a campus.
                0,
                SystemGuid.Group.PCO_ROOT_GROUP,
                true,
                false,
                true );

            RockMigrationHelper.UpdateGroupType(
                "PCO Service Type",
                "Planning Center Online Service Types.  This is used by the Planning Center Team Sync Tool, and shouldn't be used to create any normal Rock Groups.",
                "Service",
                "",
                Guid.NewGuid().ToString(),
                false,
                false,
                true,
                "fa fa-place-of-worship",
                0,
                "",
                0,
                "",
                SystemGuid.GroupType.PCO_SERVICE_TYPE
                , true );

            RockMigrationHelper.UpdateGroupType(
                "PCO Team",
                "Planning Center Online Teams. This is used by the Planning Center Team Sync Tool, and shouldn't be used to create any normal Rock Groups.",
                "Team",
                "",
                Guid.NewGuid().ToString(),
                false,
                false,
                true,
                "fa fa-user-friends",
                0,
                "",
                0,
                "",
                SystemGuid.GroupType.PCO_TEAM
                , true );

            RockMigrationHelper.UpdateGroupType(
                "PCO Team Position",
                "Planning Center Online Team Position. This is used by the Planning Center Team Sync Tool, and shouldn't be used to create any normal Rock Groups.  Adding people to this Group Type will add them to Planning Center Online.",
                "Position",
                "",
                Guid.NewGuid().ToString(),
                false,
                false,
                true,
                "fa fa-user-tag",
                0,
                "",
                0,
                "",
                SystemGuid.GroupType.PCO_POSITION
                , true );

            RockMigrationHelper.UpdateDefinedValue(
                Rock.SystemGuid.DefinedType.PERSON_SEARCH_KEYS,
                "Planning Center Online Id",
                "An Id to the a Planning Center Online Person related to this person.  This is used to matched People in Rock to their records in Planning Center.",
                SystemGuid.DefinedValue.PCO_ID_SEARCH_KEY_TYPE_VALUE,
                true );

            Sql( $@"
            UPDATE GroupType SET
                EnableGroupHistory = 1
            WHERE
                [Guid] in ('{SystemGuid.GroupType.PCO_POSITION}', '{SystemGuid.GroupType.PCO_TEAM}' )" );

        }
        public override void Down()
        {
            RockMigrationHelper.DeleteDefinedValue( SystemGuid.DefinedValue.PCO_ID_SEARCH_KEY_TYPE_VALUE );

            RockMigrationHelper.DeleteGroupType( SystemGuid.GroupType.PCO_POSITION );
            RockMigrationHelper.DeleteGroupType( SystemGuid.GroupType.PCO_TEAM );
            RockMigrationHelper.DeleteGroupType( SystemGuid.GroupType.PCO_SERVICE_TYPE );

            RockMigrationHelper.DeleteGroup( SystemGuid.Group.PCO_ROOT_GROUP );
        }
    }
}