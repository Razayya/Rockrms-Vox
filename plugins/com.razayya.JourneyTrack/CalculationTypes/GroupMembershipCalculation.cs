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

            if ( !groupGuid.HasValue )
            {
                return results;
            }

            var groupService = new GroupService( rockContext );
            var selectedGroup = groupService.Get( groupGuid.Value );
            if ( selectedGroup == null )
            {
                return results;
            }

            // Build the set of group IDs to query.
            var groupIds = new HashSet<int> { selectedGroup.Id };
            if ( includeChildGroups )
            {
                var descendantIds = groupService.GetAllDescendentGroupIds( selectedGroup.Id, false );
                foreach ( var id in descendantIds )
                {
                    groupIds.Add( id );
                }
            }

            var memberService = new GroupMemberService( rockContext );
            var query = memberService.Queryable().AsNoTracking()
                .Where( gm => groupIds.Contains( gm.GroupId ) );

            if ( activeMembersOnly )
            {
                query = query.Where( gm => gm.GroupMemberStatus == GroupMemberStatus.Active );
            }

            if ( groupRoleGuid.HasValue )
            {
                query = query.Where( gm => gm.GroupRole.Guid == groupRoleGuid.Value );
            }

            if ( populationPersonIds != null && populationPersonIds.Count > 0 )
            {
                query = query.Where( gm => populationPersonIds.Contains( gm.PersonId ) );
            }

            var memberRows = query
                .Select( gm => new
                {
                    gm.PersonId,
                    gm.CreatedDateTime,
                    GroupRoleName = gm.GroupRole.Name,
                    GroupName = gm.Group.Name
                } )
                .ToList();

            var grouped = memberRows
                .GroupBy( r => r.PersonId );

            foreach ( var personGroup in grouped )
            {
                var memberships = personGroup
                    .OrderBy( r => r.CreatedDateTime )
                    .Select( r => new Dictionary<string, object>
                    {
                        { "GroupName", r.GroupName },
                        { "GroupRole", r.GroupRoleName },
                        { "JoinDate", r.CreatedDateTime }
                    } )
                    .ToList();

                var earliest = memberships.First();

                results[personGroup.Key] = new Dictionary<string, object>
                {
                    { "Matched", true },
                    { "GroupName", earliest["GroupName"] },
                    { "GroupRole", earliest["GroupRole"] },
                    { "JoinDate", earliest["JoinDate"] },
                    { "Groups", memberships },
                    { "GroupCount", memberships.Count }
                };
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
