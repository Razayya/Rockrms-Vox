using System.Collections.Generic;
using System.Data;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Field;
using Rock.Field.Types;
using Rock.ViewModels.Utility;

namespace com.razayya.FieldTypes.Field.Types
{
    /// <summary>
    /// Obsidian-capable replacement for the BEMA WorkflowExtensions "Select Extended"
    /// field type, rendered as a two-level tree picker. Reads the same attribute
    /// qualifiers (parent_values / child_values SQL returning Value, Text and
    /// ParentValue columns) and stores values in the same "parentValue|childValue"
    /// format, so an attribute can be repointed between the two field types without
    /// touching stored values or qualifiers. Stored values that don't resolve to a
    /// current query item (deactivated locations, or free text captured while the
    /// field rendered as a plain text box) are displayed as-is rather than dropped.
    /// </summary>
    [RockPlatformSupport( Rock.Utility.RockPlatform.WebForms, Rock.Utility.RockPlatform.Obsidian )]
    [Rock.SystemGuid.FieldTypeGuid( "4708A00F-A601-4B75-955C-B504A502483A" )]
    [MemoField( "Parent Values",
        Description = "SQL query returning Value and Text columns for the top-level items.",
        IsRequired = false,
        Order = 0,
        Key = ConfigurationKey.ParentValues )]
    [MemoField( "Child Values",
        Description = "SQL query returning Value, Text and ParentValue columns for the selectable child items.",
        IsRequired = false,
        Order = 1,
        Key = ConfigurationKey.ChildValues )]
    [TextField( "Parent Field Type",
        Description = "Legacy BEMA Select Extended qualifier. Not used by this field type; preserved so a repoint back remains possible.",
        IsRequired = false,
        Order = 2,
        Key = ConfigurationKey.ParentFieldType )]
    [TextField( "Child Field Type",
        Description = "Legacy BEMA Select Extended qualifier. Not used by this field type; preserved so a repoint back remains possible.",
        IsRequired = false,
        Order = 3,
        Key = ConfigurationKey.ChildFieldType )]
    [TextField( "Parent Repeat Columns",
        Description = "Legacy BEMA Select Extended qualifier. Not used by this field type; preserved so a repoint back remains possible.",
        IsRequired = false,
        Order = 4,
        Key = ConfigurationKey.ParentRepeatColumns )]
    [TextField( "Child Repeat Columns",
        Description = "Legacy BEMA Select Extended qualifier. Not used by this field type; preserved so a repoint back remains possible.",
        IsRequired = false,
        Order = 5,
        Key = ConfigurationKey.ChildRepeatColumns )]
    public class SelectExtendedTreeFieldType : UniversalItemTreePickerFieldType
    {
        internal static class ConfigurationKey
        {
            public const string ParentValues = "parent_values";
            public const string ChildValues = "child_values";
            public const string ParentFieldType = "parent_fieldtype";
            public const string ChildFieldType = "child_fieldtype";
            public const string ParentRepeatColumns = "parent_repeatColumns";
            public const string ChildRepeatColumns = "child_repeatColumns";
        }

        private const string ParentItemType = "parent";
        private const string ChildItemType = "child";

        /// <inheritdoc/>
        protected override List<string> GetSelectableItemTypes( Dictionary<string, string> privateConfigurationValues )
        {
            return new List<string> { ChildItemType };
        }

        /// <inheritdoc/>
        protected override List<TreeItemBag> GetTreeItems( UniversalItemTreePickerGetItemsOptions options )
        {
            var configurationValues = options.PrivateConfigurationValues ?? new Dictionary<string, string>();
            var children = GetQueryItems( configurationValues.GetValueOrNull( ConfigurationKey.ChildValues ) );

            var childrenByParent = children
                .Where( c => c.ParentValue.IsNotNullOrWhiteSpace() )
                .GroupBy( c => c.ParentValue )
                .ToDictionary( g => g.Key, g => g.ToList() );

            var requestedParentValue = options.PickerOptions?.ParentValue;

            if ( requestedParentValue.IsNotNullOrWhiteSpace() )
            {
                return GetChildBags( requestedParentValue, childrenByParent );
            }

            return GetQueryItems( configurationValues.GetValueOrNull( ConfigurationKey.ParentValues ) )
                .Select( p =>
                {
                    var childBags = GetChildBags( p.Value, childrenByParent );

                    return new TreeItemBag
                    {
                        Value = p.Value,
                        Text = p.Text,
                        Type = ParentItemType,
                        IsFolder = true,
                        HasChildren = childBags.Count > 0,
                        ChildCount = childBags.Count,
                        Children = childBags
                    };
                } )
                .ToList();
        }

        /// <inheritdoc/>
        protected override List<ListItemBag> GetItemBags( IEnumerable<string> values, Dictionary<string, string> privateConfigurationValues )
        {
            var bags = new List<ListItemBag>();
            List<QueryItem> children = null;

            foreach ( var value in values )
            {
                if ( value.IsNullOrWhiteSpace() )
                {
                    continue;
                }

                // Stored format is "parentValue|childValue"; a bare child value can
                // appear as a trailing piece of a legacy multi-selection.
                var pipeIndex = value.IndexOf( '|' );
                var childValue = pipeIndex >= 0 ? value.Substring( pipeIndex + 1 ) : value;

                if ( children == null )
                {
                    children = GetQueryItems( privateConfigurationValues?.GetValueOrNull( ConfigurationKey.ChildValues ) );
                }

                var match = children.FirstOrDefault( c => c.Value == childValue );

                bags.Add( new ListItemBag
                {
                    Value = value,
                    Text = match != null ? match.Text : value
                } );
            }

            return bags;
        }

        private static List<TreeItemBag> GetChildBags( string parentValue, Dictionary<string, List<QueryItem>> childrenByParent )
        {
            if ( !childrenByParent.TryGetValue( parentValue, out var children ) )
            {
                return new List<TreeItemBag>();
            }

            return children
                .Select( c => new TreeItemBag
                {
                    Value = $"{parentValue}|{c.Value}",
                    Text = c.Text,
                    Type = ChildItemType,
                    HasChildren = false
                } )
                .ToList();
        }

        private static List<QueryItem> GetQueryItems( string query )
        {
            var items = new List<QueryItem>();

            if ( query.IsNullOrWhiteSpace() )
            {
                return items;
            }

            DataTable table;

            try
            {
                table = DbService.GetDataTable( query, CommandType.Text, null );
            }
            catch
            {
                // A broken configured query should render an empty picker, not
                // crash attribute rendering for the whole page.
                return items;
            }

            if ( table == null || table.Columns.Count == 0 )
            {
                return items;
            }

            var valueColumn = table.Columns.Contains( "Value" ) ? table.Columns["Value"] : table.Columns[0];
            var textColumn = table.Columns.Contains( "Text" ) ? table.Columns["Text"] : ( table.Columns.Count > 1 ? table.Columns[1] : valueColumn );
            var parentColumn = table.Columns.Contains( "ParentValue" ) ? table.Columns["ParentValue"] : null;

            foreach ( DataRow row in table.Rows )
            {
                items.Add( new QueryItem
                {
                    Value = row[valueColumn].ToString(),
                    Text = row[textColumn].ToString(),
                    ParentValue = parentColumn != null ? row[parentColumn].ToString() : null
                } );
            }

            return items;
        }

        private class QueryItem
        {
            public string Value { get; set; }

            public string Text { get; set; }

            public string ParentValue { get; set; }
        }
    }

    /// <summary>
    /// Multiple-selection variant of <see cref="SelectExtendedTreeFieldType"/>.
    /// Selections store as a comma-delimited list of "parentValue|childValue" entries.
    /// </summary>
    [Rock.SystemGuid.FieldTypeGuid( "DD59123F-E2D5-499A-8E51-C736FEC27766" )]
    public class SelectExtendedTreeMultiFieldType : SelectExtendedTreeFieldType
    {
        /// <inheritdoc/>
        protected override bool IsMultipleSelection => true;
    }
}
