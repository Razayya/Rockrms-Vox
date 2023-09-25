using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
using System.Runtime.Serialization;
using com.bemaservices.BemaPipeline.Web.Cache;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace com.bemaservices.BemaPipeline.Model
{
    [Table( "_com_bemaservices_BemaPipeline_BemaPipeline" )]
    [DataContract]
    public class BemaPipeline : Rock.Data.Model<BemaPipeline>, Rock.Data.IRockEntity
    {

        #region Entity Properties

        [DataMember]
        public int BemaPipelineTypeId { get; set; }

        [DataMember]
        public int EntityId { get; set; }

        [DataMember]
        public BemaPipelineState BemaPipelineState { get; set; }

        [DataMember]
        public DateTime? ActivatedDateTime { get; set; }

        [DataMember]
        public DateTime? LastProcessedDateTime { get; set; }

        [DataMember]
        public DateTime? CompletedDateTime { get; set; }

        [DataMember]
        public string AdditionalData { get; set; }

        [DataMember]
        [NotAudited]

        public bool IsProcessing { get; set; }

        #endregion

        #region Virtual Properties

        public virtual BemaPipelineType BemaPipelineType { get; set; }

        public virtual BemaPipelineTypeCache PipelineTypeCache
        {
            get
            {
                if ( BemaPipelineTypeId > 0 )
                {
                    return BemaPipelineTypeCache.Get( BemaPipelineTypeId );
                }
                else if ( BemaPipelineType != null )
                {
                    return BemaPipelineTypeCache.Get( BemaPipelineType.Id );
                }
                return null;
            }
        }

        [LavaInclude]
        public virtual IEnumerable<BemaPipelineAction> ActiveActions
        {
            get
            {
                return this.BemaPipelineActions
                    .Where( a => !a.CompletedDateTime.HasValue && a.ActionTypeCache.IsActive)
                    .ToList()
                    .OrderBy( a => a.ActionTypeCache != null ? a.ActionTypeCache.Order : 0 );
            }
        }

        [DataMember]
        [NotMapped]
        public virtual bool IsActive
        {
            get
            {
                return !CompletedDateTime.HasValue;
            }
            private set { }
        }

        [DataMember]
        [NotMapped]
        public virtual string Name
        {
            get
            {
                return ToString();
            }
        }

        [LavaInclude]
        public virtual ICollection<BemaPipelineAction> BemaPipelineActions
        {
            get { return _bemaPipelineActions ?? ( _bemaPipelineActions = new Collection<BemaPipelineAction>() ); }
            set { _bemaPipelineActions = value; }
        }
        private ICollection<BemaPipelineAction> _bemaPipelineActions;

        #endregion

        internal bool ProcessActions( RockContext rockContext, out List<string> errorMessages )
        {
            DateTime processStartTime = RockDateTime.Now;

            errorMessages = new List<string>();

            foreach ( var action in this.ActiveActions )
            {
                List<string> actionErrorMessages;
                bool actionSuccess = action.Process( rockContext, out actionErrorMessages );
                if ( actionErrorMessages.Any() )
                {
                    if ( action.ActionTypeCache != null )
                    {
                        errorMessages.Add( string.Format( "Error in Action: {0} ", action.ActionTypeCache.Name ) );
                    }

                    errorMessages.AddRange( actionErrorMessages );
                }

                // If action completed this activity, exit
                if ( !IsActive )
                {
                    break;
                }
            }

            this.LastProcessedDateTime = RockDateTime.Now;

            if ( !this.ActiveActions.Any() )
            {
                MarkComplete();
            }

            return errorMessages.Count == 0;
        }

        public virtual void MarkComplete()
        {
            MarkComplete( BemaPipelineState.Completed );
        }

        public virtual void MarkComplete( BemaPipelineState bemaPipelineState )
        {
            foreach ( var action in this.BemaPipelineActions )
            {
                action.CompletedDateTime = RockDateTime.Now;
            }

            CompletedDateTime = RockDateTime.Now;
            BemaPipelineState = bemaPipelineState;
        }

        public IEntity GetPipelineEntity( RockContext rockContext = null )
        {
            IEntity entityObject = null;
            if ( rockContext == null )
            {
                rockContext = new RockContext();
            }

            // Get the entity type
            EntityTypeCache entityType = EntityTypeCache.Get( PipelineTypeCache.EntityTypeId );
            if ( entityType != null )
            {
                // Get the entity
                EntityTypeService entityTypeService = new EntityTypeService( rockContext );
                entityObject = entityTypeService.GetEntity( PipelineTypeCache.EntityTypeId, EntityId );
            }

            return entityObject;
        }

        public override string ToString()
        {
            return string.Format(
                "{0}:{1} - {1}:{2} - Pipeline:{3}"
                , PipelineTypeCache.EntityType.Name
                , EntityId
                , PipelineTypeCache.Name
                , BemaPipelineTypeId
                , Id
                );
        }

        #region Static Methods


        public static BemaPipeline Activate( BemaPipelineTypeCache bemaPipelineTypeCache, int entityId )
        {
            using ( var rockContext = new RockContext() )
            {
                return Activate( bemaPipelineTypeCache, entityId, rockContext );
            }
        }


        public static BemaPipeline Activate( BemaPipelineTypeCache bemaPipelineTypeCache, int entityId, RockContext rockContext )
        {
            var bemaPipeline = new BemaPipeline();
            bemaPipeline.BemaPipelineTypeId = bemaPipelineTypeCache.Id;
            bemaPipeline.EntityId = entityId;
            bemaPipeline.ActivatedDateTime = RockDateTime.Now;
            bemaPipeline.BemaPipelineState = BemaPipelineState.Active;
            bemaPipeline.IsProcessing = false;

            foreach ( var actionType in bemaPipelineTypeCache.BemaPipelineActionTypes.Where( a => a.IsActive == true ).OrderBy( a => a.Order ) )
            {
                var newAction = BemaPipelineAction.Activate( actionType, bemaPipeline, rockContext );
                if ( newAction != null )
                {
                    bemaPipeline.BemaPipelineActions.Add( newAction );
                }
            }

            return bemaPipeline;
        }

        #endregion

    }

    public enum BemaPipelineState
    {
        Inactive = 0,
        Active = 1,
        Completed = 2
    }

    #region Entity Configuration


    public partial class BemaPipelineConfiguration : EntityTypeConfiguration<BemaPipeline>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BemaPipelineConfiguration"/> class.
        /// </summary> 
        public BemaPipelineConfiguration()
        {
            this.HasRequired( p => p.BemaPipelineType ).WithMany( p => p.BemaPipelines ).HasForeignKey( p => p.BemaPipelineTypeId ).WillCascadeOnDelete( true );

            // IMPORTANT!!
            this.HasEntitySetName( "BemaPipeline" );
        }
    }

    #endregion

}