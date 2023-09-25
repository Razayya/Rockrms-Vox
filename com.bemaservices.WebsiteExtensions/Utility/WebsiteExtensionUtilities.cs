using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.bemaservices.WebsiteExtensions.Model;
using Rock;
using Rock.Web.Cache;

namespace com.bemaservices.WebsiteExtensions.Utility
{
    public partial class WebsiteExtensionUtilities
    {
        public static AttributeInformation GetAttributeInformation( int requestedAttributeId )
        {
            var attributeInformation = new AttributeInformation();

            var attributeCache = AttributeCache.Get( requestedAttributeId );
            if ( attributeCache != null )
            {
                attributeInformation.Id = attributeCache.Id;
                attributeInformation.Name = attributeCache.Name;
                attributeInformation.Key = attributeCache.Key;
                attributeInformation.IsPublic = attributeCache.IsPublic;
                attributeInformation.FieldType = attributeCache.FieldType.Name;

                attributeInformation.Options = new List<FilterOption>();
                var configurationValues = attributeCache.QualifierValues;
                if ( attributeCache.FieldType.Guid == Rock.SystemGuid.FieldType.DEFINED_VALUE.AsGuid() )
                {
                    int? definedTypeId = configurationValues != null && configurationValues.ContainsKey( "definedtype" ) ? configurationValues["definedtype"].Value.AsIntegerOrNull() : null;
                    bool useDescription = configurationValues != null && configurationValues.ContainsKey( "displaydescription" ) && configurationValues["displaydescription"].Value.AsBoolean();
                    var dt = DefinedTypeCache.Get( definedTypeId.Value );
                    var definedValuesList = dt?.DefinedValues
                        .Where( a => a.IsActive )
                        .OrderBy( v => v.Order ).ThenBy( v => v.Value ).ToList();

                    if ( definedValuesList != null && definedValuesList.Any() )
                    {
                        foreach ( var definedValue in definedValuesList )
                        {
                            var filterOption = new FilterOption();
                            filterOption.Text = useDescription && definedValue.Description.IsNotNullOrWhiteSpace() ? definedValue.Description : definedValue.Value;
                            filterOption.Value = definedValue.Guid.ToString();
                            attributeInformation.Options.Add( filterOption );
                        }
                    }
                }
                else if ( attributeCache.FieldType.Guid == Rock.SystemGuid.FieldType.BOOLEAN.AsGuid() )
                {
                    string trueText = "Yes";
                    var trueQualifierValue = attributeCache.QualifierValues["truetext"];
                    if ( trueQualifierValue != null )
                    {
                        trueText = trueQualifierValue.Value.ToStringOrDefault( "Yes" );
                    }

                    string falseText = "No";
                    var falseQualifierValue = attributeCache.QualifierValues["falsetext"];
                    if ( falseQualifierValue != null )
                    {
                        falseText = falseQualifierValue.Value.ToStringOrDefault( "No" );
                    }

                    var trueOption = new FilterOption();
                    trueOption.Text = trueText;
                    trueOption.Value = "True";
                    attributeInformation.Options.Add( trueOption );

                    var falseOption = new FilterOption();
                    falseOption.Text = falseText;
                    falseOption.Value = "False";
                    attributeInformation.Options.Add( falseOption );
                }
                else
                {
                    var configuredValues = Rock.Field.Helper.GetConfiguredValues( configurationValues ); // I am not sure what needs to be done here.
                    foreach ( var configuredValue in configuredValues )
                    {
                        var filterOption = new FilterOption();
                        filterOption.Text = configuredValue.Key;
                        filterOption.Value = configuredValue.Value;
                        attributeInformation.Options.Add( filterOption );
                    }
                }
            }

            return attributeInformation;
        }

        public static List<AttributeFilter> GetAttributeFilters( string attributeFilters = "" )
        {
            var attributeFilterList = new List<AttributeFilter>();

            var attributeList = attributeFilters.Split( '|' );
            foreach ( var attribute in attributeList )
            {
                var keyValueArray = attribute.Split( '^' );
                if ( keyValueArray.Length == 2 )
                {
                    var attributeFilter = new AttributeFilter();
                    attributeFilter.AttributeKey = keyValueArray[0];
                    attributeFilter.FilterValues = keyValueArray[1].ToUpper().SplitDelimitedValues().ToList();
                    attributeFilterList.Add( attributeFilter );
                }
            }

            return attributeFilterList;
        }

    }
}
