using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.UI;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_razayya.JourneyTrack.Controls
{
    /// <summary>
    /// Cascading editor for the Group Attendance calculation: a multi-select group-type
    /// picker that scopes a multi-select group picker. The Group Types selection is purely
    /// a filter to narrow the Groups list — the calculation evaluates attendance against the
    /// explicitly-chosen Groups, which are the only thing persisted (as a comma-delimited
    /// list of Group Guids in the calc's "Groups" attribute). On reload the type filter is
    /// re-derived from the saved groups, so it always reflects the current selection.
    ///
    /// Mirrors the FilterConditionsEditor / StageLogicEditor pattern: declared in markup (so
    /// it survives postbacks without the dynamic-control rebuild dance) and seeded by the
    /// detail page only while <see cref="HasInSessionState"/> is false.
    /// </summary>
    public partial class GroupAttendancePicker : UserControl
    {
        #region Public API

        /// <summary>
        /// The selected Group Guids as a comma-delimited string — the calculation's attendance
        /// target. Setting seeds the pickers (derives the type filter from the groups).
        /// </summary>
        public string Value
        {
            get
            {
                EnsureGroupTypesBound();
                var ids = gpGroups.SelectedValuesAsInt().Where( i => i > 0 ).Distinct().ToList();
                if ( ids.Count == 0 )
                {
                    return string.Empty;
                }

                using ( var rockContext = new RockContext() )
                {
                    var guids = new GroupService( rockContext ).Queryable().AsNoTracking()
                        .Where( g => ids.Contains( g.Id ) )
                        .Select( g => g.Guid )
                        .ToList();
                    return string.Join( ",", guids );
                }
            }
            set
            {
                HasInSessionState = true;
                SeedFromGroupGuids( value );
            }
        }

        /// <summary>True once the editor holds seed/user state in ViewState (guards re-seeding).</summary>
        public bool HasInSessionState
        {
            get { return ( ViewState["GAE_HasState"] as bool? ) ?? false; }
            private set { ViewState["GAE_HasState"] = value; }
        }

        /// <summary>Validation: require at least one group to be selected.</summary>
        public List<string> GetValidationErrors()
        {
            var errors = new List<string>();
            if ( gpGroups.SelectedValuesAsInt().All( i => i <= 0 ) )
            {
                errors.Add( "Select at least one Group to check attendance for." );
            }
            return errors;
        }

        /// <summary>
        /// Renders a saved Group Attendance selection (comma-delimited Group Guids) as a
        /// friendly bulleted list of group names for the detail page's view mode.
        /// </summary>
        public static string FormatSummaryHtml( string groupGuidsCsv )
        {
            var guids = ( groupGuidsCsv ?? string.Empty ).SplitDelimitedValues().AsGuidList();
            if ( !guids.Any() )
            {
                return "<span class='text-muted'>No groups selected</span>";
            }

            List<string> names;
            using ( var rockContext = new RockContext() )
            {
                names = new GroupService( rockContext ).Queryable().AsNoTracking()
                    .Where( g => guids.Contains( g.Guid ) )
                    .OrderBy( g => g.Name )
                    .Select( g => g.Name )
                    .ToList();
            }

            if ( !names.Any() )
            {
                return "<span class='text-muted'>No groups selected</span>";
            }

            return "<ul class='list-unstyled'>" + string.Join( "",
                names.Select( n => "<li><i class='fa fa-users-class'></i> " + System.Web.HttpUtility.HtmlEncode( n ) + "</li>" ) ) + "</ul>";
        }

        #endregion

        #region Postback Handlers

        protected void gtpGroupTypes_SelectedIndexChanged( object sender, EventArgs e )
        {
            HasInSessionState = true;
            EnsureGroupTypesBound();
            ApplyIncludedGroupTypes();
        }

        #endregion

        #region Internal

        private bool _groupTypesBound;

        /// <summary>
        /// Populate the group-type checkbox list once. A CheckBoxList persists its Items in
        /// ViewState across postbacks, so we only load when empty (initial seed) to avoid
        /// clobbering the user's selection on a later postback.
        /// </summary>
        private void EnsureGroupTypesBound()
        {
            if ( _groupTypesBound )
            {
                return;
            }
            if ( gtpGroupTypes.Items.Count == 0 )
            {
                gtpGroupTypes.SetGroupTypes( GroupTypeCache.All().OrderBy( gt => gt.Name ) );
            }
            _groupTypesBound = true;
        }

        /// <summary>Scope the group picker to the currently-selected group types (ViewState-backed, persists).</summary>
        private void ApplyIncludedGroupTypes()
        {
            gpGroups.IncludedGroupTypeIds = gtpGroupTypes.SelectedGroupTypeIds ?? new List<int>();
        }

        private void SeedFromGroupGuids( string groupGuidsCsv )
        {
            EnsureGroupTypesBound();

            var guids = ( groupGuidsCsv ?? string.Empty ).SplitDelimitedValues().AsGuidList();
            var groups = new List<Group>();
            if ( guids.Any() )
            {
                using ( var rockContext = new RockContext() )
                {
                    groups = new GroupService( rockContext ).Queryable().AsNoTracking()
                        .Where( g => guids.Contains( g.Guid ) )
                        .ToList();
                }
            }

            // Derive the type filter from the chosen groups so the picker scope reflects the
            // saved selection (the types themselves are not persisted separately).
            gtpGroupTypes.SelectedGroupTypeIds = groups.Select( g => g.GroupTypeId ).Distinct().ToList();
            ApplyIncludedGroupTypes();

            gpGroups.SetValues( groups );
        }

        #endregion
    }
}
