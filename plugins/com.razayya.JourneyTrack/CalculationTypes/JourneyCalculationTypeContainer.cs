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
            new Lazy<JourneyCalculationTypeContainer>( () =>
            {
                var c = new JourneyCalculationTypeContainer();
                // Auto-register each calc-type component's config attributes (the [GroupField],
                // [DataViewField], [IntegerField], etc. decorators on the calc-type classes).
                // Without this, the JourneyCalculationDetail block renders only the panel title
                // with no editor controls below it, because calc.LoadAttributes() returns nothing.
                try { c.Refresh(); } catch { /* swallow on cold-start race; first UI hit will rerun via GetComponent */ }
                return c;
            } );

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
