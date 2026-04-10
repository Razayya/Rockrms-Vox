using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
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
    [Export( typeof( CalculationTypeComponent ) )]
    [ExportMetadata( "ComponentName", "Group Membership" )]

    [GroupTypeField( "Group Type",
        Description = "The group type to check membership for. Leave blank if specifying a specific group.",
        IsRequired = false,
        Order = 0,
        Key = AttributeKey.GroupTypeOrGroup )]

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

            var groupTypeGuid = GetAttributeValue( AttributeKey.GroupTypeOrGroup ).AsGuidOrNull();
            var groupRoleGuid = GetAttributeValue( AttributeKey.GroupRole ).AsGuidOrNull();
            var activeMembersOnly = GetAttributeValue( AttributeKey.ActiveMembersOnly ).AsBoolean();

            if ( !groupTypeGuid.HasValue )
            {
                return results;
            }

            var memberService = new GroupMemberService( rockContext );
            var query = memberService.Queryable().AsNoTracking()
                .Where( gm => gm.Group.GroupType.Guid == groupTypeGuid.Value );

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

            var memberSummary = query
                .GroupBy( gm => gm.PersonId )
                .Select( g => new
                {
                    PersonId = g.Key,
                    JoinDate = g.Min( gm => gm.CreatedDateTime ),
                    GroupRole = g.FirstOrDefault().GroupRole.Name,
                    GroupName = g.FirstOrDefault().Group.Name
                } )
                .ToList();

            foreach ( var summary in memberSummary )
            {
                results[summary.PersonId] = new Dictionary<string, object>
                {
                    { "Matched", true },
                    { "JoinDate", summary.JoinDate },
                    { "GroupRole", summary.GroupRole },
                    { "GroupName", summary.GroupName }
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
                new MergeFieldInfo { Name = "JoinDate", Description = "Earliest group membership creation date.", DataType = "DateTime" },
                new MergeFieldInfo { Name = "GroupRole", Description = "The person's group role name.", DataType = "String" },
                new MergeFieldInfo { Name = "GroupName", Description = "The name of the group.", DataType = "String" }
            };
        }
    }
}
