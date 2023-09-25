
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Runtime.Serialization;
using com.bemaservices.BemaPipeline.Model;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace com.bemaservices.BemaPipeline.Web.Cache
{

    [Serializable]
    [DataContract]
    public class BemaPipelineTypeCache : ModelCache<BemaPipelineTypeCache, BemaPipelineType>
    {

        #region Properties

        private readonly object _obj = new object();

        [DataMember]
        public bool? IsActive { get; private set; }

        [DataMember]
        public string Name { get; private set; }

        [DataMember]
        public string Description { get; private set; }

        [DataMember]
        public int EntityTypeId { get; private set; }

        [DataMember]
        public EntityType EntityType { get; private set; }


        public List<BemaPipelineActionTypeCache> BemaPipelineActionTypes
        {
            get
            {
                var actionTypes = new List<BemaPipelineActionTypeCache>();

                if ( _actionTypeIds == null )
                {
                    lock ( _obj )
                    {
                        if ( _actionTypeIds == null )
                        {
                            using ( var rockContext = new RockContext() )
                            {
                                _actionTypeIds = new BemaPipelineActionTypeService( rockContext )
                                    .Queryable().AsNoTracking()
                                    .Where( a => a.BemaPipelineTypeId == Id )
                                    .Select( v => v.Id )
                                    .ToList();
                            }
                        }
                    }
                }

                if ( _actionTypeIds == null ) return actionTypes;

                foreach ( var id in _actionTypeIds )
                {
                    var actionType = BemaPipelineActionTypeCache.Get( id );
                    if ( actionType != null )
                    {
                        actionTypes.Add( actionType );
                    }
                }

                return actionTypes;
            }
        }
        private List<int> _actionTypeIds;

        #endregion

        #region Public Methods

        /// <summary>
        /// Copies from model.
        /// </summary>
        /// <param name="entity">The entity.</param>
        public override void SetFromEntity( IEntity entity )
        {
            base.SetFromEntity( entity );

            var bemaPipelineType = entity as BemaPipelineType;
            if ( bemaPipelineType == null ) return;

            IsActive = bemaPipelineType.IsActive;
            Name = bemaPipelineType.Name;
            Description = bemaPipelineType.Description;
            EntityTypeId = bemaPipelineType.EntityTypeId;
            EntityType = bemaPipelineType.EntityType;

            // set activityTypeIds to null so it load them all at once on demand
            _actionTypeIds = null;
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
        /// Removes a WorkflowActionForm from cache.
        /// </summary>
        /// <param name="id">The identifier.</param>
        public new static void Remove( int id )
        {
            var workflowType = Get( id );
            if ( workflowType != null )
            {
                foreach ( var actionType in workflowType.BemaPipelineActionTypes )
                {
                    BemaPipelineActionTypeCache.Remove( actionType.Id );
                }
            }

            Remove( id.ToString() );
        }

        #endregion
    }
}