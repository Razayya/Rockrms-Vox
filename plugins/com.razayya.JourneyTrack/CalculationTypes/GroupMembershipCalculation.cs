using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.JourneyTrack.Constants;
using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;

namespace com.razayya.JourneyTrack.CalculationTypes
{
    /// <summary>
    /// Evaluates whether persons are active members of a specific group,
    /// optionally including its child groups, and filtered by group role.
    /// </summary>
    [Description( "Checks whether a person is a member of a specific group, optionally including child groups." )]

    [GroupField( "Group",
        Description = "The group to check membership for.",
        IsRequired = true,
        Order = 0,
        Key = AttributeKey.Group )]

    [BooleanField( "Include Child Groups",
        Description = "When enabled, membership in any child group of the selected group is also included.",
        IsRequired = true,
        DefaultBooleanValue = false,
        Order = 1,
        Key = AttributeKey.IncludeChildGroups )]

    [GroupRoleField( "",
        "Group Role",
        Description = "Optional. Only include members with this role.",
        IsRequired = false,
        Order = 2,
        Key = AttributeKey.GroupRole_GroupMembership )]

    [BooleanField( "Active Members Only",
        Description = "When enabled, only active group members are included.",
        IsRequired = true,
        DefaultBooleanValue = true,
        Order = 3,
        Key = AttributeKey.ActiveMembersOnly_GroupMembership )]

    public class GroupMembershipCalculation : JourneyCalculationTypeComponent
    {
        /// <inheritdoc/>
        public override string Title => "Group Membership";

        /// <inheritdoc/>
        public override string IconCssClass => "fa fa-user-check";

        // Per-(group, includeChildren, role, activeOnly) cache of all matching memberships.
        // Cache is keyed by config, value is all qualifying GroupMember rows grouped by PersonId.
        // JoinDate is the effective date added: DateTimeAdded when set, else CreatedDateTime
        // (imported memberships can carry a CreatedDateTime long after the real add date).
        private struct MembershipRow
        {
            public System.DateTime? JoinDate;
            public string GroupRoleName;
            public string GroupName;
        }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (System.DateTime CachedAt, Dictionary<int, List<MembershipRow>> Map)> _gmCache
            = new System.Collections.Concurrent.ConcurrentDictionary<string, (System.DateTime, Dictionary<int, List<MembershipRow>>)>();
        private static readonly System.TimeSpan _cacheTtl = System.TimeSpan.FromSeconds( 30 );

        private static Dictionary<int, List<MembershipRow>> GetMembershipMap(
            System.Guid groupGuid, bool includeChildren, System.Guid? roleGuid, bool activeOnly, RockContext rockContext )
        {
            var key = $"{groupGuid}|{includeChildren}|{roleGuid}|{activeOnly}";
            if ( _gmCache.TryGetValue( key, out var cached )
                && ( System.DateTime.UtcNow - cached.CachedAt ) < _cacheTtl )
            {
                return cached.Map;
            }

            var groupService = new GroupService( rockContext );
            var selectedGroup = groupService.Get( groupGuid );
            if ( selectedGroup == null )
            {
                var empty = new Dictionary<int, List<MembershipRow>>();
                _gmCache[key] = ( System.DateTime.UtcNow, empty );
                return empty;
            }

            var groupIds = new HashSet<int> { selectedGroup.Id };
            if ( includeChildren )
            {
                foreach ( var id in groupService.GetAllDescendentGroupIds( selectedGroup.Id, false ) )
                {
                    groupIds.Add( id );
                }
            }

            var query = new GroupMemberService( rockContext ).Queryable().AsNoTracking()
                .Where( gm => groupIds.Contains( gm.GroupId ) );
            if ( activeOnly )
                query = query.Where( gm => gm.GroupMemberStatus == GroupMemberStatus.Active );
            if ( roleGuid.HasValue )
                query = query.Where( gm => gm.GroupRole.Guid == roleGuid.Value );

            var rows = query.Select( gm => new
            {
                gm.PersonId,
                gm.CreatedDateTime,
                gm.DateTimeAdded,
                GroupRoleName = gm.GroupRole.Name,
                GroupName = gm.Group.Name
            } ).ToList();

            var map = rows
                .GroupBy( r => r.PersonId )
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy( r => r.DateTimeAdded ?? r.CreatedDateTime )
                          .Select( r => new MembershipRow { JoinDate = r.DateTimeAdded ?? r.CreatedDateTime, GroupRoleName = r.GroupRoleName, GroupName = r.GroupName } )
                          .ToList() );

            _gmCache[key] = ( System.DateTime.UtcNow, map );
            return map;
        }

        /// <summary>
        /// Single-person memberships for render-path evaluations: a fresh shared
        /// entry is a free hit; otherwise resolve the group id set and query only
        /// this person's GroupMember rows instead of building the all-persons map.
        /// The shared cache is not written here.
        /// </summary>
        private static List<MembershipRow> GetPersonMemberships(
            System.Guid groupGuid, bool includeChildren, System.Guid? roleGuid, bool activeOnly, int personId, RockContext rockContext )
        {
            var key = $"{groupGuid}|{includeChildren}|{roleGuid}|{activeOnly}";
            if ( _gmCache.TryGetValue( key, out var cached )
                && ( System.DateTime.UtcNow - cached.CachedAt ) < _cacheTtl )
            {
                return cached.Map.TryGetValue( personId, out var hit ) ? hit : new List<MembershipRow>();
            }

            var groupService = new GroupService( rockContext );
            var selectedGroup = groupService.Get( groupGuid );
            if ( selectedGroup == null )
            {
                return new List<MembershipRow>();
            }

            var groupIds = new HashSet<int> { selectedGroup.Id };
            if ( includeChildren )
            {
                foreach ( var id in groupService.GetAllDescendentGroupIds( selectedGroup.Id, false ) )
                {
                    groupIds.Add( id );
                }
            }

            var query = new GroupMemberService( rockContext ).Queryable().AsNoTracking()
                .Where( gm => gm.PersonId == personId && groupIds.Contains( gm.GroupId ) );
            if ( activeOnly )
                query = query.Where( gm => gm.GroupMemberStatus == GroupMemberStatus.Active );
            if ( roleGuid.HasValue )
                query = query.Where( gm => gm.GroupRole.Guid == roleGuid.Value );

            return query.Select( gm => new
                {
                    gm.CreatedDateTime,
                    gm.DateTimeAdded,
                    GroupRoleName = gm.GroupRole.Name,
                    GroupName = gm.Group.Name
                } )
                .ToList()
                .OrderBy( r => r.DateTimeAdded ?? r.CreatedDateTime )
                .Select( r => new MembershipRow { JoinDate = r.DateTimeAdded ?? r.CreatedDateTime, GroupRoleName = r.GroupRoleName, GroupName = r.GroupName } )
                .ToList();
        }

        private static Dictionary<string, object> BuildMembershipEntry( List<MembershipRow> memberships )
        {
            // Memberships arrive ordered by effective join date ascending (nulls first),
            // so First() is the earliest join and Last() the most recent.
            var memList = memberships.Select( r => new Dictionary<string, object>
            {
                { "GroupName", r.GroupName },
                { "GroupRole", r.GroupRoleName },
                { "JoinDate", r.JoinDate }
            } ).ToList();
            var earliest = memList.First();
            var latest = memList.Last();

            return new Dictionary<string, object>
            {
                { "Matched", true },
                { "GroupName", earliest["GroupName"] },
                { "GroupRole", earliest["GroupRole"] },
                { "JoinDate", earliest["JoinDate"] },
                { "LastJoinDate", latest["JoinDate"] },
                { "LastGroupName", latest["GroupName"] },
                { "Groups", memList },
                { "GroupCount", memList.Count }
            };
        }

        /// <inheritdoc/>
        public override Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            JourneyCalculation calc,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            var groupGuid = calc.GetAttributeValue( AttributeKey.Group ).AsGuidOrNull();
            var includeChildGroups = calc.GetAttributeValue( AttributeKey.IncludeChildGroups ).AsBoolean();
            var groupRoleGuid = calc.GetAttributeValue( AttributeKey.GroupRole_GroupMembership ).AsGuidOrNull();
            var activeMembersOnly = calc.GetAttributeValue( AttributeKey.ActiveMembersOnly_GroupMembership ).AsBoolean();

            if ( !groupGuid.HasValue ) return results;
            if ( populationPersonIds == null || populationPersonIds.Count == 0 ) return results;

            // Render-path fast path: a single-person evaluation queries only that
            // person's memberships rather than the all-persons map.
            if ( populationPersonIds.Count == 1 )
            {
                var singlePersonId = populationPersonIds.First();
                var personMemberships = GetPersonMemberships( groupGuid.Value, includeChildGroups, groupRoleGuid, activeMembersOnly, singlePersonId, rockContext );
                if ( personMemberships.Count > 0 )
                {
                    results[singlePersonId] = BuildMembershipEntry( personMemberships );
                }
                return results;
            }

            var membershipMap = GetMembershipMap( groupGuid.Value, includeChildGroups, groupRoleGuid, activeMembersOnly, rockContext );
            if ( membershipMap.Count == 0 ) return results;

            foreach ( var personId in populationPersonIds )
            {
                if ( !membershipMap.TryGetValue( personId, out var memberships ) || memberships.Count == 0 ) continue;

                results[personId] = BuildMembershipEntry( memberships );
            }

            return results;
        }

        /// <inheritdoc/>
        public override List<MergeFieldInfo> GetMergeFields()
        {
            return new List<MergeFieldInfo>
            {
                new MergeFieldInfo { Name = "Matched", Description = "True if person is a member.", DataType = "Boolean" },
                new MergeFieldInfo { Name = "GroupName", Description = "Name of the earliest-joined group.", DataType = "String" },
                new MergeFieldInfo { Name = "GroupRole", Description = "Role in the earliest-joined group.", DataType = "String" },
                new MergeFieldInfo { Name = "JoinDate", Description = "Earliest effective join date (DateTimeAdded, falling back to record creation date).", DataType = "DateTime" },
                new MergeFieldInfo { Name = "LastJoinDate", Description = "Most recent effective join date across matching memberships.", DataType = "DateTime" },
                new MergeFieldInfo { Name = "LastGroupName", Description = "Name of the most recently joined group.", DataType = "String" },
                new MergeFieldInfo { Name = "GroupCount", Description = "Total number of matching groups.", DataType = "Integer" },
                new MergeFieldInfo { Name = "Groups", Description = "Array of all memberships. Each has GroupName, GroupRole, JoinDate. Use: {% for g in Groups %}{{ g.GroupName }}{% endfor %}", DataType = "Array" }
            };
        }
    }
}
