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
using PlanningCenterSDK.Apps.People;
using PlanningCenterSDK.Apps.People.Model;
using Rock.Reporting;
using Rock.Logging;
using com.razayya.PCOTeamSync.Utility;
using PlanningCenterSDK.Apps.Files;

namespace com.razayya.PCOTeamSync
{
    public class PCOTeamSync
    {
        private PeopleApp _peopleApp;
        private ServicesApp _servicesApp;
        private FilesApp _filesApp;

        private Dictionary<string,int> _suffixLookup;
        private Dictionary<string, int> _titleLookup;
        private Dictionary<string, int> _maritalStatusLookup;
        private Dictionary<string, int> _recordStatusLookup;
        private Dictionary<string, int> _phoneNumberTypeLookup;
        private List<NamePrefix> _pcoNamePrefixesLookup;
        private List<NameSuffix> _pcoNameSuffixesLookup;
        private List<MaritalStatus> _pcoMaritalStatusLookup;
        private string _serviceAppId;
        private int _personRecordTypeId;

        public PCOTeamSync(string applicationId, string secret)
        {
            _peopleApp = PeopleApp.GetInstance( applicationId, secret );
            _servicesApp = ServicesApp.GetInstance(applicationId, secret );
            _filesApp = FilesApp.GetInstance(applicationId, secret );

            _suffixLookup = DefinedTypeCache
                .Get( Rock.SystemGuid.DefinedType.PERSON_SUFFIX.AsGuid() )
                .DefinedValues
                .GroupBy( dv => dv.Value, StringComparer.OrdinalIgnoreCase )
                .ToDictionary( k => k.Key, v => v.First().Id, StringComparer.OrdinalIgnoreCase );
            _titleLookup = DefinedTypeCache
                .Get( Rock.SystemGuid.DefinedType.PERSON_TITLE.AsGuid() )
                .DefinedValues
                .GroupBy( dv => dv.Value, StringComparer.OrdinalIgnoreCase )
                .ToDictionary( k => k.Key, v => v.First().Id, StringComparer.OrdinalIgnoreCase );
            _maritalStatusLookup = DefinedTypeCache
                .Get( Rock.SystemGuid.DefinedType.PERSON_MARITAL_STATUS.AsGuid() )
                .DefinedValues
                .GroupBy( dv => dv.Value, StringComparer.OrdinalIgnoreCase )
                .ToDictionary( k => k.Key, v => v.First().Id, StringComparer.OrdinalIgnoreCase );
            _recordStatusLookup = DefinedTypeCache
                .Get( Rock.SystemGuid.DefinedType.PERSON_RECORD_STATUS.AsGuid() )
                .DefinedValues
                .GroupBy( dv => dv.Value, StringComparer.OrdinalIgnoreCase )
                .ToDictionary( k => k.Key, v => v.First().Id, StringComparer.OrdinalIgnoreCase );
            _phoneNumberTypeLookup = DefinedTypeCache
                .Get( Rock.SystemGuid.DefinedType.PERSON_PHONE_TYPE.AsGuid() )
                .DefinedValues
                .GroupBy( dv => dv.Value, StringComparer.OrdinalIgnoreCase )
                .ToDictionary( k => k.Key, v => v.First().Id, StringComparer.OrdinalIgnoreCase );
            _personRecordTypeId = DefinedValueCache
                .Get( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON.AsGuid() ).Id;

            // Get PCO Lookups
            _pcoNamePrefixesLookup = _peopleApp.NamePrefix.GetAllAsync().ConfigureAwait( false ).GetAwaiter().GetResult();
            _pcoNameSuffixesLookup = _peopleApp.NameSuffix.GetAllAsync().ConfigureAwait( false ).GetAwaiter().GetResult();
            _pcoMaritalStatusLookup = _peopleApp.MaritalStatus.GetAllAsync().ConfigureAwait( false ).GetAwaiter().GetResult();

            _serviceAppId = _peopleApp.App
                .GetAllAsync( new Dictionary<string, string>() { { "where[name]", "services" } } )
                .GetAwaiter()
                .GetResult()
                .FirstOrDefault()
                .Id;
        }

        public void ImportServiceTypes()
        {
            using ( var rockContext = new RockContext() )
            {
                var groupService = new GroupService( rockContext );
                var serviceTypeGroupTypeId = GroupTypeCache.Get( SystemGuid.GroupType.PCO_SERVICE_TYPE.AsGuid() ).Id;

                var rootGroup = groupService.GetByGuid( SystemGuid.Group.PCO_ROOT_GROUP.AsGuid() );

                var rockPCOServiceTypes = groupService.GetByGroupTypeId( serviceTypeGroupTypeId );

                var pcoServiceTypes = GetServiceTypes( );

                var missingRockGroups = pcoServiceTypes.Where( pco => !rockPCOServiceTypes.Any( r => r.ForeignId == pco.Id ) );

                var groupsToInsert = new List<Group>();

                foreach ( var serviceType in missingRockGroups )
                {
                    var group = new Group()
                    {
                        Name = serviceType.Name,
                        ForeignId = serviceType.Id,
                        GroupTypeId = serviceTypeGroupTypeId,
                        ParentGroupId = rootGroup.Id,
                        IsSystem = true
                    };
                    groupsToInsert.Add( group );
                }

                var pcoServiceTypeIds = pcoServiceTypes.Select( s => s.Id );

                var missingPCOServiceTypes = rockPCOServiceTypes.Where( r => r.ForeignId.HasValue && !pcoServiceTypeIds.Contains( r.ForeignId.Value ) );

                rockContext.BulkUpdate( missingPCOServiceTypes, g => new Group { IsArchived = true, ArchivedDateTime = RockDateTime.Now } );

                var archiveRockServiceTypes = rockPCOServiceTypes.Where( r => r.ForeignId.HasValue && pcoServiceTypeIds.Contains(r.ForeignId.Value ) && r.IsArchived );

                rockContext.BulkUpdate( archiveRockServiceTypes, g => new Group { IsArchived = false, ArchivedDateTime = null, ArchivedByPersonAliasId = null } );

                rockContext.BulkInsert( groupsToInsert );

            }
        }
        public void SyncTeamsAndPositions()
        {
            using ( var rockContext = new RockContext() )
            {
                var groupService = new GroupService( rockContext );

                var serviceTypeGroupType = GroupTypeCache.Get( SystemGuid.GroupType.PCO_SERVICE_TYPE.AsGuid() );
                var teamGroupType = GroupTypeCache.Get( SystemGuid.GroupType.PCO_TEAM.AsGuid() );
                var positionGroupType = GroupTypeCache.Get( SystemGuid.GroupType.PCO_POSITION.AsGuid() );

                // Sync Teams
                var rockTeams = groupService.GetByGroupTypeId( teamGroupType.Id );
                var pcoTeams = GetTeams( );

                var pcoTeamIds = pcoTeams.Select( t => t.Id ).ToList();

                // Add Missing Teams as Groups in Rock.
                var missingRockGroups = pcoTeams.Where( pco => !rockTeams.Any( r => r.ForeignId == pco.Id ) && pco.ServiceType.ArchivedAt == null );

                var groupsToInsert = new List<Group>();

                foreach ( var team in missingRockGroups )
                {
                    var serviceTypeGroup = groupService.Queryable().AsNoTracking().FirstOrDefault( g => g.ForeignId == team.ServiceType.Id && g.GroupTypeId == serviceTypeGroupType.Id );

                    var group = new Group()
                    {
                        Name = team.Name,
                        ForeignId = team.Id,
                        ParentGroupId = serviceTypeGroup.Id,
                        GroupTypeId = teamGroupType.Id,
                        IsSystem = true,
                        Guid = Guid.NewGuid()
                    };
                    groupsToInsert.Add( group );
                }

                // Archive Rock Groups that are no longer in PCO.
                var missingPCOTeams = rockTeams.Where( r => r.ForeignId.HasValue && !pcoTeamIds.Contains( r.ForeignId.Value ) );

                rockContext.BulkUpdate( missingPCOTeams, g => new Group { IsArchived = true, ArchivedDateTime = RockDateTime.Now } );

                // Unarchive Rock Groups that are present in PCO ( Not sure if this would ever actually happen ).
                var archivedRockTeams = rockTeams.Where( r => r.ForeignId.HasValue && pcoTeamIds.Contains( r.ForeignId.Value ) && r.IsArchived );

                rockContext.BulkUpdate( archivedRockTeams, g => new Group { IsArchived = false, ArchivedDateTime = null, ArchivedByPersonAliasId = null } );

                rockContext.BulkInsert( groupsToInsert );

                // Sync Positions
                var rockPositions = groupService.GetByGroupTypeId( positionGroupType.Id );
                var pcoPositions = pcoTeams.Where( t => t.ServiceType.ArchivedAt == null && t.ArchivedAt == null ).SelectMany( t => t.TeamPositions ).ToList();

                var pcoPositionIds = pcoPositions.Select( p => p.Id ).ToList();

                // Add Positions Teams as Groups in Rock.
                var missingRockPositions = pcoPositions.Where( pco => !rockPositions.Any( r => r.ForeignId == pco.Id ) );

                var positionGroupsToInsert = new List<Group>();

                foreach ( var position in missingRockPositions )
                {
                    var teamGroup = groupService.Queryable().AsNoTracking().FirstOrDefault( g => g.ForeignId == position.Team.Id && g.GroupTypeId == teamGroupType.Id );
                    var group = new Group()
                    {
                        Name = position.Name,
                        ForeignId = position.Id,
                        ParentGroup = teamGroup,
                        ParentGroupId = teamGroup.Id,
                        GroupTypeId = positionGroupType.Id,
                        IsSystem = true
                    };
                    positionGroupsToInsert.Add( group );
                }

                // Archive Rock Groups that are no longer in PCO.
                var missingPCOPositions = rockPositions.Where( r => r.ForeignId.HasValue && !pcoPositionIds.Contains( r.ForeignId.Value ) );

                rockContext.BulkUpdate( missingPCOPositions, g => new Group { IsArchived = true, ArchivedDateTime = RockDateTime.Now } );

                // Unarchive Rock Groups that are present in PCO ( Not sure if this would ever actually happen ).
                var archivedRockPositions = rockPositions.Where( r => r.ForeignId.HasValue && pcoPositionIds.Contains( r.ForeignId.Value ) && r.IsArchived );

                rockContext.BulkInsert( positionGroupsToInsert );

                // Sync Team Members and Position Assignements
                var searchKeyTypeValueId = DefinedValueCache.Get( SystemGuid.DefinedValue.PCO_ID_SEARCH_KEY_TYPE_VALUE ).Id;

                var personSearchKeyService = new PersonSearchKeyService( rockContext );
                var groupMemberService = new GroupMemberService( rockContext );

                var personMapper = personSearchKeyService
                    .Queryable()
                    .AsNoTracking()
                    .Where( psk => psk.SearchTypeValueId == searchKeyTypeValueId )
                    .ToMapper( psk => psk.PersonAlias.PersonId, psk => psk.SearchValue.ToIntSafe(0) );

                var positionMapper = groupService
                    .GetByGroupTypeId( positionGroupType.Id )
                    .AsNoTracking()
                    .ToMapper( g => g.Id, g => g.ForeignId );

                var pcoAssignements = pcoTeams.SelectMany( t => t.PersonTeamPositionsAssignments )
                    .ToDictionary( k => k.Id.ToIntSafe(0), k => k );

                var rockAssignements = groupMemberService
                    .Queryable(true, true)
                    .Where( gm => gm.GroupTypeId == positionGroupType.Id );

                var validRockAssignements = rockAssignements
                    .Where( gm => gm.ForeignId != null)
                    .ToDictionary( k => k.ForeignId.Value, k => k );

                var matchingAssignments = from rock in validRockAssignements
                                          join pco in pcoAssignements on rock.Key equals pco.Key
                                          select new { Rock = rock.Value, Pco = pco.Value };

                var rockAssignmentsToAddToPCO = rockAssignements
                    .Where( gm => gm.ForeignId == null && !gm.IsArchived )
                    .ToList();

                var pcoAssignementsToAddToRock = pcoAssignements.Keys
                    .Except( validRockAssignements.Keys )
                    .Select( k => pcoAssignements[k] )
                    .ToList();

                var pcoAssignementIds = pcoAssignements.Select( pa => pa.Key ).ToList();

                var rockAssignmentsToRemove = rockAssignements
                    .Where( rockAssign => rockAssign.ForeignId != null && !pcoAssignementIds.Contains( rockAssign.ForeignId.Value ) );

                // Process Updates
                foreach (var match in matchingAssignments)
                {
                    if (match.Rock.IsArchived)
                    {
                        // Remove assignement from PCO
                        _servicesApp.ServiceType.PersonTeamPositionAssignment( match.Pco.TeamPosition.Team.ServiceType.Id, match.Pco.TeamPosition.Id ).DeleteAsync( match.Pco.Id ).GetAwaiter().GetResult();
                    }
                }

                // Add Missing Assignements to PCO
                foreach( var rockAssignement in rockAssignmentsToAddToPCO )
                {

                    int? pcoPersonId = null;

                    // If the Pereson Doesn't exist in PCO, Create them.
                    if( !personMapper.ContainsNativeKey(rockAssignement.PersonId) )
                    {
                        var pcoPerson = new PlanningCenterSDK.Apps.People.Model.Person();
                        pcoPerson.Emails = new List<Email>();
                        pcoPerson.PhoneNumbers = new List<PlanningCenterSDK.Apps.People.Model.PhoneNumber>();
                        SetPersonPropertiesFromRockPerson( pcoPerson, rockAssignement.Person );

                        var newPerson = CreatePcoPerson( pcoPerson );

                        personSearchKeyService.Add( new PersonSearchKey()
                        {
                            PersonAliasId = rockAssignement.Person.PrimaryAliasId,
                            SearchTypeValueId = searchKeyTypeValueId,
                            SearchValue = newPerson.Id.ToString(),
                            ModifiedDateTime = RockDateTime.Now,
                            CreatedDateTime = RockDateTime.Now,
                        } );

                        pcoPersonId = newPerson.Id;
                        personMapper.Add( rockAssignement.PersonId, newPerson.Id );
                    }
                    else
                    {
                        pcoPersonId = personMapper.GetNativeKey( rockAssignement.PersonId );
                    }

                    // Double Check Missing PCO Assignements with the same Assignement Id and PersonId.
                    var pcoAssignement = pcoAssignementsToAddToRock.FirstOrDefault( pa => pa.Person.Id == pcoPersonId && pa.TeamPosition.Id == rockAssignement.Group.ForeignId );
                    if( pcoAssignement != null)
                    {
                        pcoAssignementsToAddToRock.Remove( pcoAssignement );
                    }
                    else
                    {
                        var serviceTypeId = rockAssignement.Group.ParentGroup.ParentGroup.ForeignId.Value;
                        var teamPositionId = rockAssignement.Group.ForeignId.Value;

                        var newPcoAssignement = new PersonTeamPositionAssignment()
                        {
                            Person = new PlanningCenterSDK.Apps.Services.Model.Person()
                            {
                                Id = pcoPersonId.Value
                            }
                        };

                        pcoAssignement = _servicesApp.ServiceType
                            .PersonTeamPositionAssignment(serviceTypeId, teamPositionId )
                            .CreateAsync( newPcoAssignement )
                            .GetAwaiter()
                            .GetResult();
                    }

                    rockAssignement.ForeignId = pcoAssignement.Id.ToIntSafe();

                }

                // Add Missing Assignements to Rock
                var groupMembersToAdd = new List<GroupMember>();
                foreach ( var pcoAssignement in  pcoAssignementsToAddToRock )
                {
                    var personId = personMapper.GetNativeKey( pcoAssignement.Person.Id );
                    var groupId = positionMapper.GetNativeKey( pcoAssignement.TeamPosition.Id );

                    //Double Check for a GroupMember with the same GroupId and PersonId first.
                    var groupMember = groupMemberService.Queryable( true, true ).FirstOrDefault( gm => gm.PersonId == personId && gm.GroupId == groupId );

                    if( groupMember != null )
                    {
                        groupMember.IsArchived = false;
                        groupMember.ArchivedDateTime = null;
                        groupMember.ArchivedByPersonAliasId = null;
                        groupMember.ForeignId = pcoAssignement.Id.ToIntSafe();
                    }
                    else
                    {
                        groupMembersToAdd.Add( new GroupMember()
                        {
                            GroupId = positionMapper.GetNativeKey( pcoAssignement.TeamPosition.Id ),
                            GroupMemberStatus = GroupMemberStatus.Active,
                            IsArchived = false,
                            PersonId = personId,
                            GroupRoleId = positionGroupType.DefaultGroupRoleId.GetValueOrDefault( 0 ),
                            GroupTypeId = positionGroupType.Id,
                            ForeignId = pcoAssignement.Id.ToIntSafe()
                        } );
                    } 
                }

                rockContext.BulkInsert( groupMembersToAdd );

                // Arhive group Members in Rock where Pco Assignements have been removed.
                if( rockAssignmentsToRemove.Any())
                {
                    rockContext.BulkUpdate( rockAssignmentsToRemove, g => new GroupMember { IsArchived = true, ArchivedDateTime = RockDateTime.Now } );
                }

                //Update Team Group Members
                var activePositionAssignments = groupMemberService
                .Queryable()
                .AsNoTracking()
                .Where( pa => !pa.IsArchived && pa.GroupTypeId == positionGroupType.Id )
                .ToList();

                // Fetch team assignments to check which ones need to be added or reactivated
                var allTeamAssignments = groupMemberService
                    .Queryable()
                    .Where( ta => ta.GroupTypeId == teamGroupType.Id );

                var teamAssignementsToReactivate = allTeamAssignments
                    .Where( gm => gm.IsArchived && allTeamAssignments.Any( ta => ta.PersonId == gm.PersonId ) );

                rockContext.BulkUpdate( teamAssignementsToReactivate, g => new GroupMember { IsArchived = false, ArchivedDateTime = null, ArchivedByPersonAliasId = null } );

                // Determine individuals to add or reactivate in team assignments
                var teamAssignmentsToAdd = activePositionAssignments
                    .Where( pa => !allTeamAssignments.Any( ta => ta.PersonId == pa.PersonId && ta.GroupId == pa.Group.ParentGroupId ) )
                    .GroupBy( gm => new { gm.PersonId, Group = gm.Group.ParentGroup } )
                    .Select( gm => new GroupMember()
                    {
                        GroupId = gm.Key.Group.Id,
                        PersonId = gm.Key.PersonId,
                        GroupMemberStatus = GroupMemberStatus.Active,
                        IsArchived = false,
                        GroupRoleId = gm.Key.Group.GroupType.DefaultGroupRoleId.GetValueOrDefault( 0 ),
                        GroupTypeId = gm.Key.Group.GroupTypeId,
                    } )
                    .ToList();

                rockContext.BulkInsert( teamAssignmentsToAdd );

                var teamAssignmentsToArchive = allTeamAssignments
                    .Where( ta => !ta.IsArchived && !activePositionAssignments.Any( pa => pa.PersonId == ta.PersonId && ta.Id == pa.Group.ParentGroupId ) );

                if(teamAssignementsToReactivate.Any())
                {
                    rockContext.BulkUpdate( teamAssignementsToReactivate, g => new GroupMember { IsArchived = true, ArchivedDateTime = RockDateTime.Now } );
                }

                rockContext.SaveChanges();
            }
        }
        public void SyncPeopleData( DateTime? modifiedSince = null )
        {
            using (var rockContext = new RockContext() )
            {
                modifiedSince = modifiedSince ?? DateTime.MinValue;

                var utcOffset = RockDateTime.OrgTimeZoneInfo.BaseUtcOffset;

                var personSearchKeyService = new PersonSearchKeyService( rockContext );
                var searchTypeValueId = DefinedValueCache.Get( SystemGuid.DefinedValue.PCO_ID_SEARCH_KEY_TYPE_VALUE.AsGuid() ).Id;
                var personLookUp = personSearchKeyService.Queryable().Where( sk => sk.SearchTypeValueId == searchTypeValueId ).ToDictionary( l => l.SearchValue, l => l );
                var pcoPeople = GetPeople( modifiedSince.Value );

                var missingPcoPeople = pcoPeople.Where( p => !personLookUp.ContainsKey( p.Id.ToString() ) );

                InsertMissingPeopleToRock( missingPcoPeople, rockContext );

                var peopleMap = pcoPeople
                    .Select( p => new { pcoPerson = p, PersonSearchKey = personLookUp.TryGetValue( p.Id.ToString(), out var lookupValue ) ? lookupValue : null } );

                var rockPeopleToUpdate = peopleMap
                    .Where( m => m.pcoPerson.UpdatedAt.Value.Add( utcOffset ) > m.PersonSearchKey.ModifiedDateTime && m.pcoPerson.UpdatedAt.Value.Add( utcOffset ) > m.PersonSearchKey.PersonAlias.Person.ModifiedDateTime )
                    .ToList();

                foreach( var personMap in rockPeopleToUpdate)
                {
                    SetPersonPropertiesFromPcoPerson( personMap.pcoPerson, personMap.PersonSearchKey.PersonAlias.Person );
                    personMap.PersonSearchKey.ModifiedDateTime = RockDateTime.Now;
                }

                var pcoPeopleToUpdate = peopleMap
                    .Where( m => m.PersonSearchKey.PersonAlias.Person.ModifiedDateTime > m.PersonSearchKey.ModifiedDateTime && m.PersonSearchKey.PersonAlias.Person.ModifiedDateTime > m.pcoPerson.UpdatedAt.Value.Add( utcOffset ) )
                    .ToList();

                foreach( var personMap in pcoPeopleToUpdate)
                {
                    SetPersonPropertiesFromRockPerson( personMap.pcoPerson, personMap.PersonSearchKey.PersonAlias.Person );
                    UpdatePcoPerson( personMap.pcoPerson );
                    personMap.PersonSearchKey.ModifiedDateTime = RockDateTime.Now;
                }

                rockContext.SaveChanges();

            }
        }
        public void SyncRockPerson( int id )
        {
            var rockContext = new RockContext();
            var personSearchKeyService = new PersonSearchKeyService( rockContext );
            var personService = new PersonService(rockContext );

            var searchTypeValueId = DefinedValueCache.Get( SystemGuid.DefinedValue.PCO_ID_SEARCH_KEY_TYPE_VALUE ).Id;

            var personSearchKey = personSearchKeyService.Queryable().FirstOrDefault( psk => psk.SearchTypeValueId == searchTypeValueId && psk.PersonAlias.PersonId == id );

            int? pcoPersonId = null;

            var rockPerson = personService.Get( id );


            if ( personSearchKey != null )
            {
                var pcoPerson = _peopleApp.Person.GetByIdAsync( personSearchKey.SearchValue.ToIntSafe() ).GetAwaiter().GetResult();
                if( pcoPerson != null )
                {
                    pcoPersonId = pcoPerson.Id;

                    var utcOffset = RockDateTime.OrgTimeZoneInfo.BaseUtcOffset;

                    if ( pcoPerson.UpdatedAt.Value.Add(utcOffset) > personSearchKey.ModifiedDateTime && pcoPerson.UpdatedAt.Value.Add( utcOffset ) > personSearchKey.PersonAlias.Person.ModifiedDateTime )
                    {
                        SetPersonPropertiesFromPcoPerson( pcoPerson, rockPerson );
                    } else if( personSearchKey.PersonAlias.Person.ModifiedDateTime > personSearchKey.ModifiedDateTime && personSearchKey.PersonAlias.Person.ModifiedDateTime > pcoPerson.UpdatedAt.Value.Add( utcOffset ) )
                    {
                        SetPersonPropertiesFromRockPerson( pcoPerson, rockPerson );
                        UpdatePcoPerson( pcoPerson );
                    }

                    personSearchKey.ModifiedDateTime = RockDateTime.Now;

                }
            }

            if(pcoPersonId == null)
            {
                
                var pcoPerson = new PlanningCenterSDK.Apps.People.Model.Person();
                pcoPerson.Emails = new List<Email>();
                pcoPerson.PhoneNumbers = new List<PlanningCenterSDK.Apps.People.Model.PhoneNumber>();
                SetPersonPropertiesFromRockPerson( pcoPerson, rockPerson );

                var newPerson = CreatePcoPerson( pcoPerson );

                if( personSearchKey == null )
                {
                    personSearchKeyService.Add( new PersonSearchKey()
                    {
                        PersonAliasId = rockPerson.PrimaryAliasId,
                        SearchTypeValueId = searchTypeValueId,
                        SearchValue = newPerson.Id.ToString(),
                        ModifiedDateTime = RockDateTime.Now,
                        CreatedDateTime = RockDateTime.Now,
                    } );
                }
                else
                {
                    personSearchKey.SearchValue = newPerson.Id.ToString();
                }

                pcoPersonId = newPerson.Id;
            }

            var pcoAssignements = _servicesApp.Person
                .PersonTeamPositionAssignment( pcoPersonId.Value )
                .GetAllAsync()
                .GetAwaiter()
                .GetResult()
                .ToDictionary( k => k.Id.ToIntSafe( 0 ), k => k );

            var groupService = new GroupService( rockContext );
            var groupMemberService = new GroupMemberService( rockContext );

            var positionGroupType = GroupTypeCache.Get( SystemGuid.GroupType.PCO_POSITION );
            var teamGroupType = GroupTypeCache.Get( SystemGuid.GroupType.PCO_TEAM );

            var positionMapper = groupService
                    .GetByGroupTypeId( positionGroupType.Id )
                    .AsNoTracking()
                    .ToMapper( g => g.Id, g => g.ForeignId );

            var rockAssignements = groupMemberService
                .Queryable( true, true )
                .Where( gm => gm.GroupTypeId == positionGroupType.Id && gm.PersonId == rockPerson.Id );

            var validRockAssignements = rockAssignements
                .Where( gm => gm.ForeignId != null )
                .ToDictionary( k => k.ForeignId.Value, k => k );

            var matchingAssignments = from rock in validRockAssignements
                                      join pco in pcoAssignements on rock.Key equals pco.Key
                                      select new { Rock = rock.Value, Pco = pco.Value };

            var rockAssignmentsToAddToPCO = rockAssignements
                .Where( gm => gm.ForeignId == null && !gm.IsArchived )
                .ToList();

            var pcoAssignementsToAddToRock = pcoAssignements.Keys
                .Except( validRockAssignements.Keys )
                .Select( k => pcoAssignements[k] )
                .ToList();

            var pcoAssignementIds = pcoAssignements.Select( pa => pa.Key ).ToList();

            var rockAssignmentsToRemove = rockAssignements
                .Where( rockAssign => rockAssign.ForeignId != null && !pcoAssignementIds.Contains( rockAssign.ForeignId.Value ) );

            // Process Updates
            foreach (var match in matchingAssignments)
            {
                if (match.Rock.IsArchived)
                {
                    // Remove assignement from PCO
                    _servicesApp.ServiceType.PersonTeamPositionAssignment( match.Pco.TeamPosition.Team.ServiceType.Id, match.Pco.TeamPosition.Id ).DeleteAsync( match.Pco.Id ).GetAwaiter().GetResult();
                }
            }

            // Add Missing Assignements to PCO
            foreach (var rockAssignement in rockAssignmentsToAddToPCO)
            {
                // Double Check Missing PCO Assignements with the same Assignement Id and PersonId.
                var pcoAssignement = pcoAssignementsToAddToRock.FirstOrDefault( pa => pa.Person.Id == pcoPersonId && pa.TeamPosition.Id == rockAssignement.Group.ForeignId );
                if (pcoAssignement != null)
                {
                    pcoAssignementsToAddToRock.Remove( pcoAssignement );
                }
                else
                {
                    var serviceTypeId = rockAssignement.Group.ParentGroup.ParentGroup.ForeignId.Value;
                    var teamPositionId = rockAssignement.Group.ForeignId.Value;

                    var newPcoAssignement = new PersonTeamPositionAssignment()
                    {
                        Person = new PlanningCenterSDK.Apps.Services.Model.Person()
                        {
                            Id = pcoPersonId.Value
                        }
                    };

                    pcoAssignement = _servicesApp.ServiceType
                        .PersonTeamPositionAssignment( serviceTypeId, teamPositionId )
                        .CreateAsync( newPcoAssignement )
                        .GetAwaiter()
                        .GetResult();
                }

                rockAssignement.ForeignId = pcoAssignement.Id.ToIntSafe();

            }

            // Add Missing Assignements to Rock
            var groupMembersToAdd = new List<GroupMember>();
            foreach (var pcoAssignement in pcoAssignementsToAddToRock)
            {
                var personId = rockPerson.Id;
                var groupId = positionMapper.GetNativeKey( pcoAssignement.TeamPosition.Id );

                //Double Check for a GroupMember with the same GroupId and PersonId first.
                var groupMember = groupMemberService.Queryable( true, true ).FirstOrDefault( gm => gm.PersonId == personId && gm.GroupId == groupId );

                if (groupMember != null)
                {
                    groupMember.IsArchived = false;
                    groupMember.ArchivedDateTime = null;
                    groupMember.ArchivedByPersonAliasId = null;
                    groupMember.ForeignId = pcoAssignement.Id.ToIntSafe();
                }
                else
                {
                    groupMembersToAdd.Add( new GroupMember()
                    {
                        GroupId = positionMapper.GetNativeKey( pcoAssignement.TeamPosition.Id ),
                        GroupMemberStatus = GroupMemberStatus.Active,
                        IsArchived = false,
                        PersonId = personId,
                        GroupRoleId = positionGroupType.DefaultGroupRoleId.GetValueOrDefault( 0 ),
                        GroupTypeId = positionGroupType.Id,
                        ForeignId = pcoAssignement.Id.ToIntSafe()
                    } );
                }
            }

            rockContext.BulkInsert( groupMembersToAdd );

            // Arhive group Members in Rock where Pco Assignements have been removed.
            if (rockAssignmentsToRemove.Any())
            {
                rockContext.BulkUpdate( rockAssignmentsToRemove, g => new GroupMember { IsArchived = true, ArchivedDateTime = RockDateTime.Now } );
            }

            //Update Team Group Members
            var activePositionAssignments = groupMemberService
            .Queryable()
            .AsNoTracking()
            .Where( pa => !pa.IsArchived && pa.GroupTypeId == positionGroupType.Id && pa.PersonId == rockPerson.Id )
            .ToList();

            // Fetch team assignments to check which ones need to be added or reactivated
            var allTeamAssignments = groupMemberService
                .Queryable()
                .Where( ta => ta.GroupTypeId == teamGroupType.Id );

            var teamAssignementsToReactivate = allTeamAssignments
                .Where( gm => gm.IsArchived && allTeamAssignments.Any( ta => ta.PersonId == gm.PersonId ) );

            rockContext.BulkUpdate( teamAssignementsToReactivate, g => new GroupMember { IsArchived = false, ArchivedDateTime = null, ArchivedByPersonAliasId = null } );

            // Determine individuals to add or reactivate in team assignments
            var teamAssignmentsToAdd = activePositionAssignments
                .Where( pa => !allTeamAssignments.Any( ta => ta.PersonId == pa.PersonId && ta.GroupId == pa.Group.ParentGroupId ) )
                .GroupBy( gm => new { gm.PersonId, Group = gm.Group.ParentGroup } )
                .Select( gm => new GroupMember()
                {
                    GroupId = gm.Key.Group.Id,
                    PersonId = gm.Key.PersonId,
                    GroupMemberStatus = GroupMemberStatus.Active,
                    IsArchived = false,
                    GroupRoleId = gm.Key.Group.GroupType.DefaultGroupRoleId.GetValueOrDefault( 0 ),
                    GroupTypeId = gm.Key.Group.GroupTypeId,
                } )
                .ToList();

            rockContext.BulkInsert( teamAssignmentsToAdd );

            var teamAssignmentsToArchive = allTeamAssignments
                .Where( ta => !ta.IsArchived && !activePositionAssignments.Any( pa => pa.PersonId == ta.PersonId && ta.Id == pa.Group.ParentGroupId ) );

            if (teamAssignementsToReactivate.Any())
            {
                rockContext.BulkUpdate( teamAssignementsToReactivate, g => new GroupMember { IsArchived = true, ArchivedDateTime = RockDateTime.Now } );
            }

            rockContext.SaveChanges();



        }
        public void SyncPcoPerson( int id )
        {
            // Future Idea.  Maybe setup a webhook, so that when people get updated in PCO, we can update them in Rock in real time.
            throw new NotImplementedException();
        }

        // Helper Functions
        private PlanningCenterSDK.Apps.People.Model.Person CreatePcoPerson( PlanningCenterSDK.Apps.People.Model.Person pcoPerson )
        {
            var uploadPerson = new PlanningCenterSDK.Apps.People.Model.Person()
            {
                GivenName = pcoPerson.GivenName,
                FirstName = pcoPerson.FirstName,
                NickName = pcoPerson.NickName,
                LastName = pcoPerson.LastName,
                MiddleName = pcoPerson.MiddleName,
                Birthdate = pcoPerson.Birthdate,
                Anniversary = pcoPerson.Anniversary,
                Grade = pcoPerson.Grade,
                Child = pcoPerson.Child,
                GraduationYear = pcoPerson.GraduationYear,
                SiteAdministrator = pcoPerson.SiteAdministrator,
                AccountingAdministrator = pcoPerson.AccountingAdministrator,
                PeoplePermissions = pcoPerson.PeoplePermissions,
                Membership = pcoPerson.Membership,
                InactivedAt = pcoPerson.InactivedAt,
                Status = pcoPerson.Status,
                MedicalNotes = pcoPerson.MedicalNotes,
            };

            if( pcoPerson.MaritalStatus != null )
            {
                uploadPerson.MaritalStatus = new MaritalStatus()
                {
                    Id = pcoPerson.MaritalStatus.Id,
                }; 
            }

            if( pcoPerson.NamePrefix != null )
            {
                uploadPerson.NamePrefix = new NamePrefix()
                {
                    Id = pcoPerson.NamePrefix.Id
                };
            }

            if (pcoPerson.NameSuffix != null)
            {
                uploadPerson.NameSuffix = new NameSuffix()
                {
                    Id = pcoPerson.NameSuffix.Id
                };
            }

            if (pcoPerson.AvatarUploadStream != null)
            {
                var avatar = _filesApp.File.UploadAsync( pcoPerson.AvatarUploadStream, pcoPerson.Avatar ).GetAwaiter().GetResult().FirstOrDefault();
                pcoPerson.Avatar = avatar.Id;
            }

            PlanningCenterSDK.Apps.People.Model.Person newPerson = null;
            try
            {
                newPerson = _peopleApp.Person.CreateAsync( uploadPerson ).ConfigureAwait( false ).GetAwaiter().GetResult();
            }
            catch( Exception ex )
            {
                RockLogger.Log.Error( ex.Message );
            }
            
            var personEmail = _peopleApp.Person.Email( newPerson.Id );
            var personPhone = _peopleApp.Person.PhoneNumber( newPerson.Id );

            foreach ( var email in pcoPerson.Emails)
            {
                personEmail.CreateAsync( email ).ConfigureAwait( false ).GetAwaiter().GetResult();
            }

            foreach (var number in pcoPerson.PhoneNumbers)
            {
                personPhone.CreateAsync( number ).ConfigureAwait( false ).GetAwaiter().GetResult();
            }

            _peopleApp.Person.PersonApp( newPerson.Id ).CreateAsync( new PersonApp()
            {
                App = new App()
                {
                    Id = _serviceAppId
                }
            } ).ConfigureAwait( false ).GetAwaiter().GetResult();

            return newPerson;

        }
        private void UpdatePcoPerson( PlanningCenterSDK.Apps.People.Model.Person pcoPerson )
        {
            var uploadPerson = new PlanningCenterSDK.Apps.People.Model.Person()
            {
                GivenName = pcoPerson.GivenName,
                FirstName = pcoPerson.FirstName,
                NickName = pcoPerson.NickName,
                LastName = pcoPerson.LastName,
                MiddleName = pcoPerson.MiddleName,
                Birthdate = pcoPerson.Birthdate,
                Anniversary = pcoPerson.Anniversary,
                Grade = pcoPerson.Grade,
                Child = pcoPerson.Child,
                GraduationYear = pcoPerson.GraduationYear,
                SiteAdministrator = pcoPerson.SiteAdministrator,
                AccountingAdministrator = pcoPerson.AccountingAdministrator,
                PeoplePermissions = pcoPerson.PeoplePermissions,
                Membership = pcoPerson.Membership,
                InactivedAt = pcoPerson.InactivedAt,
                Status = pcoPerson.Status,
                MedicalNotes = pcoPerson.MedicalNotes,
            };

            if (pcoPerson.MaritalStatus != null)
            {
                uploadPerson.MaritalStatus = new MaritalStatus()
                {
                    Id = pcoPerson.MaritalStatus.Id,
                };
            }

            if (pcoPerson.NamePrefix != null)
            {
                uploadPerson.NamePrefix = new NamePrefix()
                {
                    Id = pcoPerson.NamePrefix.Id
                };
            }

            if (pcoPerson.NameSuffix != null)
            {
                uploadPerson.NameSuffix = new NameSuffix()
                {
                    Id = pcoPerson.NameSuffix.Id
                };
            }

            if (pcoPerson.AvatarUploadStream != null)
            {
                var avatar = _filesApp.File
                    .UploadAsync( pcoPerson.AvatarUploadStream, pcoPerson.Avatar )
                    .GetAwaiter()
                    .GetResult()
                    .FirstOrDefault();
                uploadPerson.Avatar = avatar.Id;
            }

            var updatedPcoPerson = _peopleApp.Person.UpdateAsync( uploadPerson, pcoPerson.Id.ToString() ).ConfigureAwait( false ).GetAwaiter().GetResult();

            pcoPerson.UpdatedAt = updatedPcoPerson.UpdatedAt;

            var personEmail = _peopleApp.Person.Email( updatedPcoPerson.Id );
            var personPhone = _peopleApp.Person.PhoneNumber( updatedPcoPerson.Id );

            foreach (var email in pcoPerson.Emails)
            {
                var emailToUpload = new Email()
                {
                    Id = email.Id,
                    Address = email.Address,
                    Location = email.Location,
                    Primary = email.Primary
                };

                if(emailToUpload.Id == null || emailToUpload.Id == string.Empty)
                {
                    personEmail.CreateAsync( emailToUpload ).ConfigureAwait( false ).GetAwaiter().GetResult();
                }
                else
                {
                    _peopleApp.Email.UpdateAsync( emailToUpload, emailToUpload.Id ).ConfigureAwait( false ).GetAwaiter().GetResult();
                }
               
            }

            foreach (var number in pcoPerson.PhoneNumbers)
            {
                var phoneNumberToUpload = new PlanningCenterSDK.Apps.People.Model.PhoneNumber()
                {
                    Id = number.Id,
                    Number = number.Number,
                    Carrier = number.Carrier,
                    Location = number.Location,
                    Primary = number.Primary
                };

                if(phoneNumberToUpload.Id == null || phoneNumberToUpload.Id == string.Empty)
                {
                    personPhone.CreateAsync( phoneNumberToUpload ).ConfigureAwait( false ).GetAwaiter().GetResult();
                }
                else
                {
                    _peopleApp.PhoneNumber.UpdateAsync( phoneNumberToUpload, phoneNumberToUpload.Id ).ConfigureAwait( false ).GetAwaiter().GetResult();
                }
                
            }
        }
        private List<ServiceType> GetServiceTypes()
        {
            return _servicesApp.ServiceType.GetAllAsync().ConfigureAwait( false ).GetAwaiter().GetResult();
        }
        private List<Team> GetTeams()
        {

            var parameters = new Dictionary<string, string>
            {
                { "include", "team_positions,service_type,person_team_position_assignments" }
            };

            return _servicesApp.Team.GetAllAsync( parameters ).ConfigureAwait( false ).GetAwaiter().GetResult();
        }
        private List<PlanningCenterSDK.Apps.People.Model.Person> GetPeople(DateTime modifiedSince)
        {

            var parameters = new Dictionary<string, string>
            {
                { "include", "emails,marital_status,addresses,phone_numbers,name_prefix,name_suffix" },
                { "where[updated_at][gte]", modifiedSince.ToString("yyyy-MM-dd") }
            };

            return _peopleApp.Person.GetAllAsync( parameters ).ConfigureAwait( false ).GetAwaiter().GetResult();
        }
        private void InsertMissingPeopleToRock(IEnumerable<PlanningCenterSDK.Apps.People.Model.Person> pcoPeopleToInsert, RockContext rockContext )
        {

            var personService = new PersonService( rockContext );
            var personAliasService = new PersonAliasService( rockContext );

            var foreignKey = "PCO";

            var searchTypeValueId = DefinedValueCache.Get( SystemGuid.DefinedValue.PCO_ID_SEARCH_KEY_TYPE_VALUE.AsGuid() ).Id;

            var peopleToInsert = new List<Rock.Model.Person>();
            var searchKeysToAdd = new List<PersonSearchKey>();

            foreach (var pcoPerson in pcoPeopleToInsert)
            {
                var rockPerson = new Rock.Model.Person()
                {
                    ForeignKey = foreignKey,
                    ForeignId = pcoPerson.Id
                };

                SetPersonPropertiesFromPcoPerson( pcoPerson, rockPerson );

                peopleToInsert.Add( rockPerson );
            }

            var peopleToInsertLookup = peopleToInsert.ToDictionary( k => k.ForeignId.Value, v => v );

            rockContext.BulkInsert( peopleToInsert );

            var qryAllPersons = personService.Queryable( true, true );
            var personAliasServiceQry = personAliasService.Queryable();

            var personAliasesToInsert = Converter.ConvertModelWithLogging( qryAllPersons, () => {
                return qryAllPersons.Where( p => p.ForeignId.HasValue && p.ForeignKey == foreignKey && !p.Aliases.Any() && !personAliasServiceQry.Any( pa => pa.AliasPersonId == p.Id ) )
                    .Select( x => new { x.Id, x.Guid, x.ForeignId } )
                    .ToList()
                    .Select( person => new PersonAlias { AliasPersonId = person.Id, AliasPersonGuid = person.Guid, PersonId = person.Id, ForeignId = person.ForeignId, ForeignKey = foreignKey } ).ToList();
            }, false );

            rockContext.BulkInsert( personAliasesToInsert );

            var personAliasIdLookupFromPersonId = new PersonAliasService( rockContext ).Queryable().Where( a => a.Person.ForeignId.HasValue && a.Person.ForeignKey == foreignKey && a.PersonId == a.AliasPersonId )
            .Select( a => new { PersonAliasId = a.Id, a.ForeignId } ).ToDictionary( k => k.ForeignId, v => v.PersonAliasId );

            // PersonSearchKeys
            List<PersonSearchKey> personSearchKeysToInsert = new List<PersonSearchKey>();

            foreach (var person in peopleToInsert)
            {
                var personAliasId = personAliasIdLookupFromPersonId.GetValueOrNull( person.ForeignId );
                if (personAliasId.HasValue)
                {
                    var newPersonSearchKey = new PersonSearchKey()
                    {
                        PersonAliasId = personAliasId.Value,
                        SearchValue = person.ForeignId.Value.ToString(),
                        SearchTypeValueId = searchTypeValueId,
                        ModifiedDateTime = RockDateTime.Now,
                        CreatedDateTime = RockDateTime.Now
                    };

                    personSearchKeysToInsert.Add( newPersonSearchKey );
                }
            }

            rockContext.BulkInsert( personSearchKeysToInsert );

            // PhoneNumbers
            var phoneNumbersToInsert = new List<Rock.Model.PhoneNumber>();

            foreach (var person in peopleToInsert)
            {
                foreach (var phoneNumberImport in person.PhoneNumbers)
                {
                    phoneNumberImport.PersonId = person.Id;
                    phoneNumbersToInsert.Add( phoneNumberImport );
                }
            }

            rockContext.BulkInsert( phoneNumbersToInsert );
        }
        private void SetPersonPropertiesFromPcoPerson(PlanningCenterSDK.Apps.People.Model.Person ImportPerson, Rock.Model.Person person )
        {
            person.FirstName = ImportPerson.GivenName;
            person.LastName = ImportPerson.LastName;
            person.NickName = ImportPerson.FirstName;
            person.MiddleName = ImportPerson.MiddleName;
            person.AnniversaryDate = ImportPerson.Anniversary;
            person.Gender = Gender.Unknown;
            person.Email = ImportPerson.Emails.FirstOrDefault( e => e.Primary )?.Address ?? "";
            person.IsEmailActive = true;
            person.RecordTypeValueId = _personRecordTypeId;
            person.GraduationYear = ImportPerson.GraduationYear;

            person.RecordStatusValueId = _recordStatusLookup.GetValueOrNull( ImportPerson.Status );

            if ( ImportPerson.NamePrefix != null)
            {
                person.TitleValueId = _titleLookup.GetValueOrNull( ImportPerson.NamePrefix.Value );
            }

            if (ImportPerson.NameSuffix != null)
            {
                person.SuffixValueId = _suffixLookup.GetValueOrNull( ImportPerson.NameSuffix.Value );
            }

            if( ImportPerson.MaritalStatus != null)
            {
                person.MaritalStatusValueId = _maritalStatusLookup.GetValueOrNull( ImportPerson.MaritalStatus.Value );
            }

            foreach (var number in ImportPerson.PhoneNumbers)
            {
                var phoneNumber = new Rock.Model.PhoneNumber()
                {
                    NumberTypeValueId = _phoneNumberTypeLookup.GetValueOrNull( number.Location ),
                    CountryCode = number.CountryCode,
                    Number = number.Number.Left(20)
                };
                person.PhoneNumbers.Add( phoneNumber );
            }

            if (person.NickName.IsNullOrWhiteSpace())
            {
                person.NickName = person.FirstName;
            }

            if (ImportPerson.Birthdate != null)
            {
                person.SetBirthDate( ImportPerson.Birthdate );
            }

            if (ImportPerson.Avatar != null)
            {
                try
                {
                    person.SetPhotoFromUrl( new Uri( ImportPerson.Avatar ) );
                }
                catch { }
            }
        }
        private void SetPersonPropertiesFromRockPerson( PlanningCenterSDK.Apps.People.Model.Person pcoPerson, Rock.Model.Person rockPerson )
        {
            pcoPerson.FirstName = rockPerson.NickName;
            pcoPerson.LastName = rockPerson.LastName;
            pcoPerson.MiddleName = rockPerson.MiddleName;
            pcoPerson.Anniversary = rockPerson.AnniversaryDate;
            pcoPerson.Birthdate = rockPerson.BirthDate;
            pcoPerson.GraduationYear = rockPerson.GraduationYear;

            foreach (var email in pcoPerson.Emails)
            {
                email.Primary = false;
            }

            if( rockPerson.PhotoUrl != null )
            {
                pcoPerson.AvatarUploadStream = rockPerson.Photo.ContentStream;
                pcoPerson.Avatar = rockPerson.Photo.FileName;
                
            }

            var pcoEmail = pcoPerson.Emails.FirstOrDefault( e => e.Location == "Home" );
            if (pcoEmail == null)
            {
                pcoEmail = new Email()
                {
                    Address = rockPerson.Email,
                    Location = "Home"
                };

                pcoPerson.Emails.Add( pcoEmail );
            }

            pcoEmail.Primary = true;

            if (rockPerson.TitleValueId != null)
            {
                var namePrefix = _pcoNamePrefixesLookup.FirstOrDefault( np => np.Value == rockPerson.TitleValue.Value );

                if(namePrefix == null)
                {
                    var newNamePrefix = new NamePrefix()
                    {
                        Value = rockPerson.TitleValue.Value
                    };
                    namePrefix = _peopleApp.NamePrefix.CreateAsync( newNamePrefix ).ConfigureAwait( false ).GetAwaiter().GetResult();
                    _pcoNamePrefixesLookup.Add( namePrefix );
                }

                pcoPerson.NamePrefix = new NamePrefix()
                {
                    Id = namePrefix.Id
                };
            }

            if (rockPerson.SuffixValueId != null)
            {
                var nameSuffix = _pcoNameSuffixesLookup.FirstOrDefault( np => np.Value == rockPerson.SuffixValue.Value );

                if (nameSuffix == null)
                {
                    var newNameSuffix = new NameSuffix()
                    {
                        Value = rockPerson.SuffixValue.Value
                    };
                    nameSuffix = _peopleApp.NameSuffix.CreateAsync( newNameSuffix ).ConfigureAwait( false ).GetAwaiter().GetResult();
                    _pcoNameSuffixesLookup.Add( nameSuffix );
                }

                pcoPerson.NameSuffix = new NameSuffix()
                {
                    Id = nameSuffix.Id
                };
            }

            if (rockPerson.MaritalStatusValueId != null)
            {
                var maritalStatus = _pcoMaritalStatusLookup.FirstOrDefault( np => np.Value == rockPerson.MaritalStatusValue.Value );

                if (maritalStatus == null)
                {
                    var newMaritalStatus = new MaritalStatus()
                    {
                        Value = rockPerson.MaritalStatusValue.Value
                    };
                    maritalStatus = _peopleApp.MaritalStatus.CreateAsync( newMaritalStatus ).ConfigureAwait( false ).GetAwaiter().GetResult();
                    _pcoMaritalStatusLookup.Add( maritalStatus );
                }

                pcoPerson.MaritalStatus = new MaritalStatus()
                {
                    Id = maritalStatus.Id
                };
            }

            foreach (var number in rockPerson.PhoneNumbers)
            {
                var pcoNumber = pcoPerson.PhoneNumbers.FirstOrDefault( pn => pn.Location.ToLower() == number.NumberTypeValue.Value.ToLower() );

                if( pcoNumber == null )
                {
                    pcoNumber = new PlanningCenterSDK.Apps.People.Model.PhoneNumber
                    {
                        Location = number.NumberTypeValue.Value,
                    };
                }

                pcoNumber.Number = number.Number.Left( 20 );
                pcoNumber.Primary = number.NumberTypeValue.Value.ToLower() == "mobile" ? true : false;


                pcoPerson.PhoneNumbers.Add( pcoNumber );
            }

            pcoPerson.GivenName = rockPerson.FirstName;

        }
    }
}
