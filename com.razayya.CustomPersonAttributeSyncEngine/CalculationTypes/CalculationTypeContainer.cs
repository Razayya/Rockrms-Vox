using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using com.razayya.CustomPersonAttributeSyncEngine.Model;

using Rock.Data;
using Rock.Extension;
using Rock.Web.Cache;

namespace com.razayya.CustomPersonAttributeSyncEngine.CalculationTypes
{
    /// <summary>
    /// MEF container that discovers and manages all CalculationType components.
    /// </summary>
    public class CalculationTypeContainer : Container<CalculationTypeComponent, IComponentData>
    {
        private static readonly Lazy<CalculationTypeContainer> instance =
            new Lazy<CalculationTypeContainer>( () => new CalculationTypeContainer() );

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static CalculationTypeContainer Instance
        {
            get { return instance.Value; }
        }

        /// <summary>
        /// Forces a reloading of all the components and registers their attributes.
        /// </summary>
        public override void Refresh()
        {
            base.Refresh();

            int calculationEntityTypeId = EntityTypeCache.Get( typeof( Calculation ) ).Id;
            using ( var rockContext = new RockContext() )
            {
                foreach ( var calcType in this.Components )
                {
                    Type calcTypeType = calcType.Value.Value.GetType();
                    int componentEntityTypeId = EntityTypeCache.Get( calcTypeType ).Id;
                    Rock.Attribute.Helper.UpdateAttributes(
                        calcTypeType,
                        calculationEntityTypeId,
                        "CalculationTypeEntityTypeId",
                        componentEntityTypeId.ToString(),
                        rockContext );
                }
            }
        }

        /// <summary>
        /// Gets the component with the matching Entity Type Name.
        /// </summary>
        public static CalculationTypeComponent GetComponent( string entityType )
        {
            return Instance.GetComponentByEntity( entityType );
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        public static string GetComponentName( string entityType )
        {
            return Instance.GetComponentNameByEntity( entityType );
        }

        [ImportMany( typeof( CalculationTypeComponent ) )]
        protected override IEnumerable<Lazy<CalculationTypeComponent, IComponentData>> MEFComponents { get; set; }
    }
}
