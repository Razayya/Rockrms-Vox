
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using com.bemaservices.BemaPipeline.Model;
using Rock.Data;
using Rock.Extension;
using Rock.Model;
using Rock.Web.Cache;

namespace com.bemaservices.BemaPipeline.BemaPipelineActionTypes
{
   
    public class BemaPipelineActionTypeContainer : Container<BemaPipelineActionTypeComponent, IComponentData>
    {
        /// <summary>
        /// Singleton instance
        /// </summary>
        private static readonly Lazy<BemaPipelineActionTypeContainer> instance =
            new Lazy<BemaPipelineActionTypeContainer>( () => new BemaPipelineActionTypeContainer() );

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <value>
        /// The instance.
        /// </value>
        public static BemaPipelineActionTypeContainer Instance
        {
            get { return instance.Value; }
        }

        /// <summary>
        /// Forces a reloading of all the components
        /// </summary>
        public override void Refresh()
        {
            base.Refresh();

            // Create any attributes that need to be created
            int bemaPipelineActionTypeEntityTypeId = EntityTypeCache.Get( typeof( BemaPipelineActionType ) ).Id;
            using ( var rockContext = new RockContext() )
            {
                foreach ( var actionType in this.Components )
                {
                    Type actionTypeType = actionType.Value.Value.GetType();
                    int actionTypeComponentEntityTypeId = EntityTypeCache.Get( actionTypeType ).Id;
                    Rock.Attribute.Helper.UpdateAttributes( actionTypeType, bemaPipelineActionTypeEntityTypeId, "BemaPipelineActionTypeEntityTypeId", actionTypeComponentEntityTypeId.ToString(), rockContext );
                }
            }
        }

        /// <summary>
        /// Gets the component with the matching Entity Type Name.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <returns></returns>
        public static BemaPipelineActionTypeComponent GetComponent( string entityType )
        {
            return Instance.GetComponentByEntity( entityType );
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <returns></returns>
        public static string GetComponentName( string entityType )
        {
            return Instance.GetComponentNameByEntity( entityType );
        }

        /// <summary>
        /// Gets or sets the MEF components.
        /// </summary>
        /// <value>
        /// The MEF components.
        /// </value>
        [ImportMany( typeof(BemaPipelineActionTypeComponent) )]
        protected override IEnumerable<Lazy<BemaPipelineActionTypeComponent, IComponentData>> MEFComponents { get; set; }
    }
}