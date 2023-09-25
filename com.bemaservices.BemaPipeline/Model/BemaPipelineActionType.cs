using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
using System.Runtime.Serialization;
using com.bemaservices.BemaPipeline.Web.Cache;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace com.bemaservices.BemaPipeline.Model
{
    [Table("_com_bemaservices_BemaPipeline_BemaPipelineActionType")]
    [DataContract]
    public class BemaPipelineActionType : Rock.Data.Model<BemaPipelineActionType>, Rock.Data.IRockEntity, Rock.Data.IOrdered, IHasAttributes, IHasInheritedAttributes
    {

        #region Entity Properties


        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Description { get; set; }

        [DataMember]
        public bool IsActive { get; set; }

        [DataMember]
        public int Order { get; set; }

        [DataMember]
        public int ComponentEntityTypeId { get; set; }

        [DataMember]
        public int BemaPipelineTypeId { get; set; }

        #endregion

        #region ICacheable

        /// <summary>
        /// Gets the cache object associated with this Entity
        /// </summary>
        /// <returns></returns>
        public IEntityCache GetCacheObject()
        {
            return BemaPipelineActionTypeCache.Get(Id);
        }

        /// <summary>
        /// Updates any Cache Objects that are associated with this entity
        /// </summary>
        /// <param name="entityState">State of the entity.</param>
        /// <param name="dbContext">The database context.</param>
        public void UpdateCache(EntityState entityState, Rock.Data.DbContext dbContext)
        {
            BemaPipelineActionTypeCache.UpdateCachedEntity(Id, entityState);
        }

        #endregion ICacheable

        #region Virtual Properties

        [LavaInclude]
        public virtual BemaPipelineType BemaPipelineType { get; set; }

        [LavaInclude]
        public virtual ICollection<BemaPipelineAction> BemaPipelineActions
        {
            get { return _bemaPipelineActions ?? (_bemaPipelineActions = new Collection<BemaPipelineAction>()); }
            set { _bemaPipelineActions = value; }
        }
        private ICollection<BemaPipelineAction> _bemaPipelineActions;

        #endregion
        public List<AttributeCache> GetInheritedAttributesForQualifier( Rock.Data.RockContext rockContext, int entityTypeId, string entityTypeQualifierColumn )
        {
            var attributes = new List<AttributeCache>();
            //
            // Walk each group type and generate a list of matching attributes.
            //
            foreach ( var entityAttribute in AttributeCache.AllForEntityType( entityTypeId ) )
            {
                // group type ids exist and qualifier is for a group type id
                if ( string.Compare(entityAttribute.EntityTypeQualifierColumn, entityTypeQualifierColumn, true ) == 0 )
                {
                    int componentEntityTypeId = int.MinValue;
                    if ( int.TryParse(entityAttribute.EntityTypeQualifierValue, out componentEntityTypeId) && this.ComponentEntityTypeId == componentEntityTypeId)
                    {
                        attributes.Add(entityAttribute);
                    }
                }
            }

            return attributes.OrderBy( a => a.Order ).ToList();
        }

        public override List<AttributeCache> GetInheritedAttributes( Rock.Data.RockContext rockContext )
        {
            var component = EntityTypeCache.Get( this.ComponentEntityTypeId );
            if( component == null && this.ComponentEntityTypeId > 0 )
            {
                // Handle no compoenent in cache
                return null;
            }

            if( component != null )
            {
                return AttributeCache.AllForEntityType( component.Id ).ToList();
            }

            return null;
        }

    }

    #region Entity Configuration


    public partial class BemaPipelineActionTypeConfiguration : EntityTypeConfiguration<BemaPipelineActionType>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BemaPipelineActionTypeConfiguration"/> class.
        /// </summary> 
        public BemaPipelineActionTypeConfiguration()
        {
            this.HasRequired( p => p.BemaPipelineType ).WithMany(p=> p.BemaPipelineActionTypes).HasForeignKey( p => p.BemaPipelineTypeId ).WillCascadeOnDelete( true );

            // IMPORTANT!!
            this.HasEntitySetName("BemaPipelineActionType");
        }
    }

    #endregion

}