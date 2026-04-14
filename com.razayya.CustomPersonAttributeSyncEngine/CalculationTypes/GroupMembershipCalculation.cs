using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.CustomPersonAttributeSyncEngine.Constants;
using com.razayya.CustomPersonAttributeSyncEngine.Model;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;

namespace com.razayya.CustomPersonAttributeSyncEngine.CalculationTypes
{
    /// <summary>
    /// Evaluates whether persons are active members of a specified group or group type,
    /// optionally filtered by group role.
    /// </summary>
    [Description( "Checks whether a person is a member of a specified group or group type." )]


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

    public class GroupMembershipCalculation : CalculationTypeComponent
    {
        /// <inheritdoc/>
        public override string Title => "Group Membership";

        /// <inheritdoc/>
        public override string IconCssClass => "fa fa-users";

        /// <inheritdoc/>
        public override Dictionary<int, Dictionary<string, object>> Evaluate(
            RockContext rockContext,
            Calculation calculation,
            HashSet<int> populationPersonIds )
        {
            var results = new Dictionary<int, Dictionary<string, object>>();

            var groupTypeGuids = calculation.GetAttributeValue( AttributeKey.GroupTypes_Membership )
                .SplitDelimitedValues()
                .AsGuidList();
            var groupRoleGuid = calculation.GetAttributeValue( AttributeKey.GroupRole ).AsGuidOrNull();
            var activeMembersOnly = calculation.GetAttributeValue( AttributeKey.ActiveMembersOnly ).AsBoolean();

            if ( !groupTypeGuids.Any() )
            {
                return results;
            }

            var memberService = new GroupMemberService( rockContext );
            var query = memberService.Queryable().AsNoTracking()
                .Where( gm => groupTypeGuids.Contains( gm.Group.GroupType.Guid ) );

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

            // Project flat columns that EF6 can safely translate, then group in memory.
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
