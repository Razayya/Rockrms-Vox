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
    /// Evaluates whether persons are active members of any group belonging to the
    /// specified group type(s), optionally filtered by group role.
    /// </summary>
    [Description( "Checks whether a person is a member of any group under the selected group types." )]


    [GroupTypesField( "Group Types",
        Description = "The group types to check membership for.",
        IsRequired = true,
        Order = 0,
        Key = AttributeKey.GroupTypes_Membership )]

    [GroupRoleField( "",
        "Group Role",
        Description = "Optional. Only include members with this role.",
        IsRequired = false,
        Order = 1,
        Key = AttributeKey.GroupRole )]

    [BooleanField( "Active Members Only",
        Description = "When enabled, only active group members are included.",
        IsRequired = true,
        DefaultBooleanValue = true,
        Order = 2,
        Key = AttributeKey.ActiveMembersOnly )]

    public class GroupTypeMembershipCalculation : JourneyCalculationTypeComponent
    {
        /// <inheritdoc/>
        public override string Title => "Group Type Membership";

        /// <inheritdoc/>
        public override string IconCssClass => "fa fa-users";

        // Per-(groupTypes set, role, activeOnly) cache.
        private struct GtmRow
        {
            public System.DateTime? CreatedDateTime;
            public string GroupRoleName;
            public string GroupName;
        }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (System.DateTime CachedAt, Dictionary<int, List<GtmRow>> Map)> _gtmCache
            = new System.Collections.Concurrent.ConcurrentDictionary<string, (System.DateTime, Dictionary<int, List<GtmRow>>)>();
        private static readonly System.TimeSpan _cacheTtl = System.TimeSpan.FromSeconds( 30 );

        private static Dictionary<int, List<GtmRow>> GetMembershipMap(
            List<System.Guid> groupTypeGuids, System.Guid? roleGuid, bool activeOnly, RockContext rockContext )
        {
            var key = string.Join( ",", groupTypeGuids.OrderBy( g => g ) ) + "|" + roleGuid + "|" + activeOnly;
            if ( _gtmCache.TryGetValue( key, out var cached )
                && ( System.DateTime.UtcNow - cached.CachedAt ) < _cacheTtl )
            {
                return cached.Map;
            }

            var query = new GroupMemberService( rockContext ).Queryable().AsNoTracking()
                .Where( gm => groupTypeGuids.Contains( gm.Group.GroupType.Guid ) );
            if ( activeOnly )
                query = query.Where( gm => gm.GroupMemberStatus == GroupMemberStatus.Active );
            if ( roleGuid.HasValue )
                query = query.Where( gm => gm.GroupRole.Guid == roleGuid.Value );

            var rows = query.Select( gm => new
            {
                gm.PersonId,
                gm.CreatedDateTime,
                GroupRoleName = gm.GroupRole.Name,
                GroupName = gm.Group.Name
            } ).ToList();

            var map = rows
                .GroupBy( r => r.PersonId )
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy( r => r.CreatedDateTime )
                          .Select( r => new GtmRow { CreatedDateTime = r.CreatedDateTime, GroupRoleName = r.GroupRoleName, GroupName = r.GroupName } )
                          .ToList() );

            _gtmCache[key] = ( System.DateTime.UtcNow, map );
            return map;
        }

        /// <summary>
        /// Single-person memberships for render-path evaluations: a fresh shared
        /// entry is a free hit; otherwise query only this person's GroupMember
        /// rows instead of building the all-persons map. The shared cache is not
        /// written here — population runs keep their own rebuild cadence.
        /// </summary>
        private static List<GtmRow> GetPersonMemberships(
            List<System.Guid> groupTypeGuids, System.Guid? roleGuid, bool activeOnly, int personId, RockContext rockContext )
        {
            var key = string.Join( ",", groupTypeGuids.OrderBy( g => g ) ) + "|" + roleGuid + "|" + activeOnly;
            if ( _gtmCache.TryGetValue( key, out var cached )
                && ( System.DateTime.UtcNow - cached.CachedAt ) < _cacheTtl )
            {
                return cached.Map.TryGetValue( personId, out var hit ) ? hit : new List<GtmRow>();
            }

            var query = new GroupMemberService( rockContext ).Queryable().AsNoTracking()
                .Where( gm => gm.PersonId == personId && groupTypeGuids.Contains( gm.Group.GroupType.Guid ) );
            if ( activeOnly )
                query = query.Where( gm => gm.GroupMemberStatus == GroupMemberStatus.Active );
            if ( roleGuid.HasValue )
                query = query.Where( gm => gm.GroupRole.Guid == roleGuid.Value );

            return query.Select( gm => new
                {
                    gm.CreatedDateTime,
                    GroupRoleName = gm.GroupRole.Name,
                    GroupName = gm.Group.Name
                } )
                .ToList()
                .OrderBy( r => r.CreatedDateTime )
                .Select( r => new GtmRow { CreatedDateTime = r.CreatedDateTime, GroupRoleName = r.GroupRoleName, GroupName = r.GroupName } )
                .ToList();
        }

        private static Dictionary<string, object> BuildMembershipEntry( List<GtmRow> memberships )
        {
            var memList = memberships.Select( r => new Dictionary<string, object>
            {
                { "GroupName", r.GroupName },
                { "GroupRole", r.GroupRoleName },
                { "JoinDate", r.CreatedDateTime }
            } ).ToList();
            var earliest = memList.First();

            return new Dictionary<string, object>
            {
                { "Matched", true },
                { "GroupName", earliest["GroupName"] },
                { "GroupRole", earliest["GroupRole"] },
                { "JoinDate", earliest["JoinDate"] },
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

            var groupTypeGuids = calc.GetAttributeValue( AttributeKey.GroupTypes_Membership )
                .SplitDelimitedValues()
                .AsGuidList();
            var groupRoleGuid = calc.GetAttributeValue( AttributeKey.GroupRole ).AsGuidOrNull();
            var activeMembersOnly = calc.GetAttributeValue( AttributeKey.ActiveMembersOnly ).AsBoolean();

            if ( !groupTypeGuids.Any() ) return results;
            if ( populationPersonIds == null || populationPersonIds.Count == 0 ) return results;

            // Render-path fast path: a single-person evaluation queries only that
            // person's memberships rather than the all-persons map.
            if ( populationPersonIds.Count == 1 )
            {
                var singlePersonId = populationPersonIds.First();
                var personMemberships = GetPersonMemberships( groupTypeGuids, groupRoleGuid, activeMembersOnly, singlePersonId, rockContext );
                if ( personMemberships.Count > 0 )
                {
                    results[singlePersonId] = BuildMembershipEntry( personMemberships );
                }
                return results;
            }

            var membershipMap = GetMembershipMap( groupTypeGuids, groupRoleGuid, activeMembersOnly, rockContext );
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
                new MergeFieldInfo { Name = "JoinDate", Description = "Earliest group membership creation date.", DataType = "DateTime" },
                new MergeFieldInfo { Name = "GroupCount", Description = "Total number of matching groups.", DataType = "Integer" },
                new MergeFieldInfo { Name = "Groups", Description = "Array of all memberships. Each has GroupName, GroupRole, JoinDate. Use: {% for g in Groups %}{{ g.GroupName }}{% endfor %}", DataType = "Array" }
            };
        }
    }
}
