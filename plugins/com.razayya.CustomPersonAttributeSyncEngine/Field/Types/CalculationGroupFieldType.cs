using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

using com.razayya.CustomPersonAttributeSyncEngine.Model;

using Rock;
using Rock.Data;
using Rock.Field;
using Rock.Web.UI.Controls;

namespace com.razayya.CustomPersonAttributeSyncEngine.Field.Types
{
    /// <summary>
    /// Field type that stores a CalculationGroup reference as a Guid string.
    /// Renders as a dropdown of active Calculation Groups.
    /// </summary>
    public class CalculationGroupFieldType : FieldType, IEntityFieldType
    {
        #region Formatting

        /// <inheritdoc/>
        public override string GetTextValue( string privateValue, Dictionary<string, string> privateConfigurationValues )
        {
            if ( string.IsNullOrWhiteSpace( privateValue ) )
            {
                return string.Empty;
            }

            Guid? guid = privateValue.AsGuidOrNull();
            if ( guid.HasValue )
            {
                using ( var rockContext = new RockContext() )
                {
                    var group = new CalculationGroupService( rockContext ).Get( guid.Value );
                    if ( group != null )
                    {
                        return group.Name;
                    }
                }
            }

            return string.Empty;
        }

        /// <inheritdoc/>
        public override string FormatValue( Control parentControl, string value, Dictionary<string, ConfigurationValue> configurationValues, bool condensed )
        {
            return GetTextValue( value, configurationValues.ToDictionary( cv => cv.Key, cv => cv.Value.Value ) );
        }

        #endregion

        #region Edit Control

        /// <inheritdoc/>
        public override Control EditControl( Dictionary<string, ConfigurationValue> configurationValues, string id )
        {
            var ddl = new RockDropDownList { ID = id };
            ddl.Items.Add( new ListItem() );

            using ( var rockContext = new RockContext() )
            {
                var groups = new CalculationGroupService( rockContext ).Queryable()
                    .Where( g => g.IsActive )
                    .OrderBy( g => g.Order )
                    .ThenBy( g => g.Name )
                    .Select( g => new { g.Guid, g.Name } )
                    .ToList();

                foreach ( var group in groups )
                {
                    ddl.Items.Add( new ListItem( group.Name, group.Guid.ToString() ) );
                }
            }

            return ddl;
        }

        /// <inheritdoc/>
        public override string GetEditValue( Control control, Dictionary<string, ConfigurationValue> configurationValues )
        {
            var ddl = control as RockDropDownList;
            if ( ddl != null )
            {
                return ddl.SelectedValue;
            }

            return null;
        }

        /// <inheritdoc/>
        public override void SetEditValue( Control control, Dictionary<string, ConfigurationValue> configurationValues, string value )
        {
            var ddl = control as RockDropDownList;
            if ( ddl != null )
            {
                ddl.SetValue( value );
            }
        }

        #endregion

        #region IEntityFieldType

        /// <inheritdoc/>
        public int? GetEditValueAsEntityId( Control control, Dictionary<string, ConfigurationValue> configurationValues )
        {
            Guid? guid = GetEditValue( control, configurationValues ).AsGuidOrNull();
            if ( guid.HasValue )
            {
                using ( var rockContext = new RockContext() )
                {
                    var group = new CalculationGroupService( rockContext ).Get( guid.Value );
                    if ( group != null )
                    {
                        return group.Id;
                    }
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public void SetEditValueFromEntityId( Control control, Dictionary<string, ConfigurationValue> configurationValues, int? id )
        {
            string guidValue = string.Empty;
            if ( id.HasValue )
            {
                using ( var rockContext = new RockContext() )
                {
                    var group = new CalculationGroupService( rockContext ).Get( id.Value );
                    if ( group != null )
                    {
                        guidValue = group.Guid.ToString();
                    }
                }
            }

            SetEditValue( control, configurationValues, guidValue );
        }

        /// <inheritdoc/>
        public Rock.Data.IEntity GetEntity( string value )
        {
            return GetEntity( value, null );
        }

        /// <inheritdoc/>
        public Rock.Data.IEntity GetEntity( string value, RockContext rockContext )
        {
            Guid? guid = value.AsGuidOrNull();
            if ( guid.HasValue )
            {
                rockContext = rockContext ?? new RockContext();
                return new CalculationGroupService( rockContext ).Get( guid.Value );
            }

            return null;
        }

        #endregion
    }
}
