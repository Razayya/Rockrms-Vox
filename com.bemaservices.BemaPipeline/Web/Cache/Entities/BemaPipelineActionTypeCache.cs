using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using com.bemaservices.BemaPipeline.BemaPipelineActionTypes;
using com.bemaservices.BemaPipeline.Model;
using Rock.Communication.SmsActions;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace com.bemaservices.BemaPipeline.Web.Cache
{

    [Serializable]
    [DataContract]
    public class BemaPipelineActionTypeCache : ModelCache<BemaPipelineActionTypeCache, BemaPipelineActionType>
    {
        #region Properties

        /// <summary>
        /// Gets or sets the name of the action.
        /// </summary>
        /// <value>
        /// The name of the action.
        /// </value>
        [DataMember]
        public string Name { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is active.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is active; otherwise, <c>false</c>.
        /// </value>
        [DataMember]
        public bool IsActive { get; private set; }

        /// <summary>
        /// Gets or sets the order of this action in the system.
        /// </summary>
        /// <value>
        /// The order of this action in the system.
        /// </value>
        [DataMember]
        public int Order { get; private set; }

        /// <summary>
        /// Gets or sets the identifier for the entity type that handles this action's logic.
        /// </summary>
        /// <value>
        /// The identifier for the entity type that handles this action's logic.
        /// </value>
        [DataMember]
        public int ComponentEntityTypeId { get; private set; }

        [DataMember]
        public bool ContinueAfterProcessing { get; private set; }

        [DataMember]
        public int BemaPipelineTypeId { get; private set; }

        [DataMember]
        public BemaPipelineTypeCache BemaPipelineType { get; private set; }

		[LavaInclude]
        public BemaPipelineActionTypeComponent BemaPipelineActionTypeComponent
        {
            get
            {
                if (_bemaPipelineActionTypeComponent == null )
                {
                    var entityTypeCache = EntityTypeCache.Get(ComponentEntityTypeId);

                    if ( entityTypeCache != null )
                    {
                        _bemaPipelineActionTypeComponent = (BemaPipelineActionTypeComponent) Activator.CreateInstance( entityTypeCache.GetEntityType() );
                    }
                }

                return _bemaPipelineActionTypeComponent;
            }
        }
        private BemaPipelineActionTypeComponent _bemaPipelineActionTypeComponent = null;


        #endregion

        #region Public Methods

        /// <summary>
        /// Set's the cached objects properties from the model/entities properties.
        /// </summary>
        /// <param name="entity">The entity.</param>
        public override void SetFromEntity( IEntity entity )
        {
            base.SetFromEntity( entity );

            var bemaPipelineActionType = entity as BemaPipelineActionType;
            if (bemaPipelineActionType == null )
            {
                return;
            }

            Name = bemaPipelineActionType.Name;
            IsActive = bemaPipelineActionType.IsActive;
            Order = bemaPipelineActionType.Order;
            ComponentEntityTypeId = bemaPipelineActionType.ComponentEntityTypeId;
            BemaPipelineTypeId = bemaPipelineActionType.BemaPipelineTypeId;
            BemaPipelineType = BemaPipelineTypeCache.Get( bemaPipelineActionType.BemaPipelineTypeId );
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return Name;
        }

        #endregion

        #region Static Methods

        /// <summary>
        /// Gets all the instances of this type of model/entity that are currently in cache.
        /// </summary>
        /// <returns></returns>
        public new static List<BemaPipelineActionTypeCache> All()
        {
            // use 'new' to override the base All since we want to sort actions
            return ModelCache<BemaPipelineActionTypeCache, BemaPipelineActionType>.All().OrderBy( a => a.Order ).ToList();
        }

        /// <summary>
        /// Gets all the instances of this type of model/entity that are currently in cache.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <returns></returns>
        public new static List<BemaPipelineActionTypeCache> All( RockContext rockContext )
        {
            // use 'new' to override the base All since we want to sort actions
            return ModelCache<BemaPipelineActionTypeCache, BemaPipelineActionType>.All( rockContext ).OrderBy( a => a.Order ).ToList();
        }

        #endregion
    }
}