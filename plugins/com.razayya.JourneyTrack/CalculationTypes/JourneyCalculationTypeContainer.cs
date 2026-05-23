using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using com.razayya.JourneyTrack.Model;

using Rock.Data;
using Rock.Extension;
using Rock.Web.Cache;

namespace com.razayya.JourneyTrack.CalculationTypes
{
    /// <summary>
    /// MEF container that discovers and manages all CalculationType components.
    /// </summary>
    public class JourneyCalculationTypeContainer : Container<JourneyCalculationTypeComponent, IComponentData>
    {
        private static readonly Lazy<JourneyCalculationTypeContainer> instance =
            new Lazy<JourneyCalculationTypeContainer>( () => new JourneyCalculationTypeContainer() );

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static JourneyCalculationTypeContainer Instance
        {
            get { return instance.Value; }
        }

        /// <summary>
        /// Forces a reloading of all the components and registers their attributes.
        /// </summary>
        public override void Refresh()
        {
            base.Refresh();

            int calculationEntityTypeId = EntityTypeCache.Get( typeof( JourneyCalculation ) ).Id;
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
        public static JourneyCalculationTypeComponent GetComponent( string entityType )
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

        [ImportMany( typeof( JourneyCalculationTypeComponent ) )]
        protected override IEnumerable<Lazy<JourneyCalculationTypeComponent, IComponentData>> MEFComponents { get; set; }
    }
}
