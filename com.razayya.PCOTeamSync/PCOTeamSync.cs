using System.Data.Entity;
using System.Threading.Tasks;
using Rock.Data;
using Rock.Model;
using Rock;
using Rock.Web.Cache;
using System.Collections.Generic;
using PlanningCenterSDK.Apps.Services.Model;
using PlanningCenterSDK.Apps.Services;
using System;
using System.Linq;
using Rock.Utility;

namespace com.razayya.PCOTeamSync
{
    public static class PCOTeamSync
    {
        public static int SyncServiceTypesAsync()
        {
            using ( var rockContext = new RockContext() )
            {
                var groupService = new GroupService( rockContext );
                var serviceTypeGroupTypeId = GroupTypeCache.Get( SystemGuid.GroupType.PCO_SERVICE_TYPE.AsGuid() ).Id;

                var rootGroup = groupService.GetByGuid( SystemGuid.Group.PCO_ROOT_GROUP.AsGuid() );

                var rockPCOServiceTypes = groupService.GetByGroupTypeId( serviceTypeGroupTypeId ).ToList();

                var pcoServiceTypes = GetServiceTypesAsync( rockContext );

                var missingRockGroups = pcoServiceTypes.Where( pco => !rockPCOServiceTypes.Any( r => r.ForeignId == pco.Id ) );

                foreach ( var serviceType in missingRockGroups )
                {
                    var group = new Group()
                    {
                        Name = serviceType.Name,
                        ForeignId = serviceType.Id,
                        GroupTypeId = serviceTypeGroupTypeId,
                        ParentGroup = rootGroup
                    };
                    groupService.Add( group );
                }

                var missingPCOServiceTypes = rockPCOServiceTypes.Where( r => !pcoServiceTypes.Any( pco => pco.Id == r.ForeignId ) );

                foreach( var group in missingPCOServiceTypes )
                {
                    groupService.Archive( group, null, false );
                }

                var archiveRockServiceTypes = rockPCOServiceTypes.Where( r => pcoServiceTypes.Any( pco => r.Id == r.ForeignId ) && r.IsArchived );
                foreach( var group in archiveRockServiceTypes )
                {
                    group.IsArchived = false;
                    group.ArchivedDateTime = null;
                    group.ArchivedByPersonAliasId = null;
                }

                return rockContext.SaveChanges();
            }
        }

        public static int SyncTeamsAndPositionsAsync()
        {
            using ( var rockContext = new RockContext() )
            {
                var groupService = new GroupService( rockContext );

                var serviceTypeGroupTypeId = GroupTypeCache.Get( SystemGuid.GroupType.PCO_SERVICE_TYPE.AsGuid() ).Id;
                var teamGroupTypeId = GroupTypeCache.Get( SystemGuid.GroupType.PCO_TEAM.AsGuid() ).Id;
                var positionGroupTypeId = GroupTypeCache.Get( SystemGuid.GroupType.PCO_POSITION.AsGuid() ).Id;


                // Sync Teams
                var rockTeams = groupService.GetByGroupTypeId( teamGroupTypeId ).ToList();
                var pcoTeams = GetTeamsAsync( rockContext );

                // Add Missing Teams as Groups in Rock.
                var missingRockGroups = pcoTeams.Where( pco => !rockTeams.Any( r => r.ForeignId == pco.Id ) && pco.ServiceType.ArchivedAt == null );

                foreach ( var team in missingRockGroups )
                {
                    var serviceTypeGroup = groupService.Queryable().AsNoTracking().FirstOrDefault( g => g.ForeignId == team.ServiceType.Id && g.GroupTypeId == serviceTypeGroupTypeId );

                    var group = new Group()
                    {
                        Name = team.Name,
                        ForeignId = team.Id,
                        ParentGroupId = serviceTypeGroup.Id,
                        GroupTypeId = teamGroupTypeId
                    };
                    groupService.Add( group );
                }

                // Archive Rock Groups that are no longer in PCO.
                var missingPCOTeams = rockTeams.Where( r => !pcoTeams.Any( pco => pco.Id == r.ForeignId ) );

                foreach ( var group in missingPCOTeams )
                {
                    groupService.Archive( group, null, false );
                }

                // Unarchive Rock Groups that are present in PCO ( Not sure if this would ever actually happen ).
                var archivedRockTeams = rockTeams.Where( r => pcoTeams.Any( pco => r.Id == r.ForeignId ) && r.IsArchived );
                foreach ( var group in archivedRockTeams )
                {
                    group.IsArchived = false;
                    group.ArchivedDateTime = null;
                    group.ArchivedByPersonAliasId = null;
                }

                // Sync Positions
                var rockPositions = groupService.GetByGroupTypeId( positionGroupTypeId ).ToList();
                var pcoPositions = pcoTeams.Where( t => t.ServiceType.ArchivedAt == null && t.ArchivedAt == null ).SelectMany( t => t.TeamPositions ).ToList();

                // Add Positions Teams as Groups in Rock.
                var missingRockPositions = pcoPositions.Where( pco => !rockPositions.Any( r => r.ForeignId == pco.Id ) );

                foreach ( var position in missingRockPositions )
                {
                    //var teamGroup = groupService.Queryable().AsNoTracking().FirstOrDefault( g => g.ForeignId == position.Team.Id && g.GroupTypeId == teamGroupTypeId );
                    var group = new Group()
                    {
                        Name = position.Name,
                        ForeignId = position.Id,
                        //ParentGroup = teamGroup,
                        GroupTypeId = teamGroupTypeId
                    };
                    groupService.Add( group );
                }

                // Archive Rock Groups that are no longer in PCO.
                var missingPCOPositions = rockPositions.Where( r => !pcoPositions.Any( pco => pco.Id == r.ForeignId ) );

                foreach ( var group in missingPCOPositions )
                {
                    groupService.Archive( group, null, false );
                }

                // Unarchive Rock Groups that are present in PCO ( Not sure if this would ever actually happen ).
                var archivedRockPositions = rockPositions.Where( r => pcoPositions.Any( pco => r.Id == r.ForeignId ) && r.IsArchived );
                foreach ( var group in archivedRockTeams )
                {
                    group.IsArchived = false;
                    group.ArchivedDateTime = null;
                    group.ArchivedByPersonAliasId = null;
                }

                var groupsUpdates = rockContext.SaveChanges();

                var newRockPCOPosistions = groupService.GetByGroupTypeId( positionGroupTypeId );
                foreach ( var group in newRockPCOPosistions )
                {
                    var pcoTeam = pcoPositions.FirstOrDefault( p => p.Id == group.ForeignId ).Team;
                    var rockPcoTeam = groupService.Queryable().FirstOrDefault( g => g.GroupTypeId == teamGroupTypeId && g.ForeignId == pcoTeam.Id );
                    group.ParentGroupId = rockPcoTeam?.Id;
                    group.ParentGroup = rockPcoTeam;
                }

                rockContext.SaveChanges();

                return groupsUpdates;
            }
        }

        private static List<ServiceType> GetServiceTypesAsync( RockContext rockContext )
        {

            List<ServiceType> serviceTypes = new List<ServiceType>();

            var definedValueService = new DefinedValueService( rockContext );
            var definedPcoAccounts = definedValueService.GetByDefinedTypeGuid( SystemGuid.DefinedType.PCO_ACCOUNTS.AsGuid() ).ToList();

            foreach ( var pcoAccount in definedPcoAccounts )
            {

                pcoAccount.LoadAttributes();

                var applicationId = pcoAccount.GetAttributeValue( AttributeCache.Get( SystemGuid.Attribute.PCO_ACCOUNT_APPLICATION_ID.AsGuid() ).Key );
                var secret = pcoAccount.GetAttributeValue( AttributeCache.Get( SystemGuid.Attribute.PCO_ACCOUNT_SECRET.AsGuid() ).Key );

                var pcoServiceApp = ServicesApp.GetInstance( applicationId, secret );

                var accountServiceTypes = pcoServiceApp.ServiceType.GetAllAsync().GetAwaiter().GetResult();

                serviceTypes.AddRange( accountServiceTypes );
            }

            return serviceTypes;
        }

        private static List<Team> GetTeamsAsync( RockContext rockContext )
        {

            List<Team> teams = new List<Team>();

            var definedValueService = new DefinedValueService( rockContext );
            var definedPcoAccounts = definedValueService.GetByDefinedTypeGuid( SystemGuid.DefinedType.PCO_ACCOUNTS.AsGuid() ).ToList();

            foreach ( var pcoAccount in definedPcoAccounts )
            {

                pcoAccount.LoadAttributes();

                var applicationId = pcoAccount.GetAttributeValue( AttributeCache.Get( SystemGuid.Attribute.PCO_ACCOUNT_APPLICATION_ID.AsGuid() ).Key );
                var secret = pcoAccount.GetAttributeValue( AttributeCache.Get( SystemGuid.Attribute.PCO_ACCOUNT_SECRET.AsGuid() ).Key );

                var pcoServiceApp = ServicesApp.GetInstance( applicationId, secret );

                var parameters = new Dictionary<string, string>
                {
                    { "include", "team_positions,service_type" }
                };

                var accountServiceTypes = pcoServiceApp.Team.GetAllAsync( parameters ).GetAwaiter().GetResult();

                teams.AddRange( accountServiceTypes );
            }

            return teams;
        }
    }
}
