using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
using System.Runtime.Serialization;
using com.bemaservices.BemaPipeline.BemaPipelineActionTypes;
using com.bemaservices.BemaPipeline.Web.Cache;
using Rock;
using Rock.Data;
using Rock.Model;
namespace com.bemaservices.BemaPipeline.Model
{
    [Table( "_com_bemaservices_BemaPipeline_BemaPipelineAction" )]
    [DataContract]
    public class BemaPipelineAction : Rock.Data.Model<BemaPipelineAction>, Rock.Data.IRockEntity
    {

        #region Entity Properties

        [DataMember]
        public int BemaPipelineId { get; set; }

        [DataMember]
        public int? BemaPipelineActionTypeId { get; set; }

        [DataMember]
        public BemaPipelineActionState BemaPipelineActionState { get; set; }

        [DataMember]
        public DateTime? ActivatedDateTime { get; set; }

        [DataMember]
        public DateTime? LastProcessedDateTime { get; set; }

        [DataMember]
        public DateTime? CompletedDateTime { get; set; }

        #endregion

        #region Virtual Properties
        [LavaInclude]
        public virtual BemaPipeline BemaPipeline { get; set; }
        [LavaInclude]
        public virtual BemaPipelineActionType BemaPipelineActionType { get; set; }
        [LavaInclude]
        public virtual BemaPipelineActionTypeCache ActionTypeCache
        {
            get
            {
                if ( BemaPipelineActionTypeId.HasValue && BemaPipelineActionTypeId > 0 )
                {
                    return BemaPipelineActionTypeCache.Get( BemaPipelineActionTypeId.Value );
                }
                else if ( BemaPipelineActionType != null )
                {
                    return BemaPipelineActionTypeCache.Get( BemaPipelineActionType.Id );
                }
                return null;
            }
        }

        public bool ShouldDisplayAction
        {
            get
            {
                var actionType = this.ActionTypeCache;
                if ( actionType == null )
                {
                    throw new SystemException( string.Format( "ActionTypeId: {0} could not be loaded.", this.BemaPipelineActionTypeId ) );
                }

                BemaPipelineActionTypeComponent actionComponent = actionType.BemaPipelineActionTypeComponent;
                if ( actionComponent == null )
                {
                    throw new SystemException( string.Format( "The component does not exist, or is not active" ) );
                }

                var rockContext = new RockContext();
                IEntity entity = BemaPipeline.GetPipelineEntity( rockContext );
                if ( entity == null )
                {
                    throw new SystemException( string.Format( "The pipeline entity does not exist, or is not active" ) );
                }

                return actionComponent.ShouldDisplayAction( rockContext, this, entity );
            }
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

        public override string ToString()
        {
            return string.Format(
                "{0}:{1} - {1}:{2} - Pipeline:{3} - {4}:{5} - Action:{6}"
                , BemaPipeline.PipelineTypeCache.EntityType.Name
                , BemaPipeline.EntityId
                , BemaPipeline.PipelineTypeCache.Name
                , BemaPipeline.PipelineTypeCache.Id
                , BemaPipeline.Id
                , ActionTypeCache.Name
                , ActionTypeCache.Id
                , Id
                );
        }

        #endregion

        internal virtual bool Process( RockContext rockContext, out List<string> errorMessages, bool manualAction = false )
        {
            errorMessages = new List<string>();


            IEntity entity = BemaPipeline.GetPipelineEntity( rockContext );
            if ( entity == null )
            {
                errorMessages.Add( string.Format( "The pipeline entity does not exist, or is not active" ) );
                return false;
            }

            var actionType = this.ActionTypeCache;
            BemaPipelineActionTypeComponent actionComponent = null;
            if ( actionType != null )
            {
                actionComponent = actionType.BemaPipelineActionTypeComponent;
                if ( actionComponent != null )
                {
                    return actionComponent.ProcessState( rockContext, this, entity, out errorMessages, manualAction );
                }
                else
                {
                    errorMessages.Add( string.Format( "The component does not exist, or is not active" ) );
                }
            }
            else
            {
                errorMessages.Add( string.Format( "ActionTypeId: {0} could not be loaded.", this.BemaPipelineActionTypeId ) );
            }

            BemaPipelineActionState = BemaPipelineActionState.ActionTypeArchived;
            LastProcessedDateTime = RockDateTime.Now;
            CompletedDateTime = RockDateTime.Now;
            rockContext.SaveChanges();         
            return true;
        }

        #region Methods
        /// <summary>
        /// Get a list of all inherited Attributes that should be applied to this entity.
        /// </summary>
        /// <returns>A list of all inherited AttributeCache objects.</returns>
        public override List<Rock.Web.Cache.AttributeCache> GetInheritedAttributes( Rock.Data.RockContext rockContext )
        {
            var bemaPipelineActionType = this.BemaPipelineActionType;
            if ( bemaPipelineActionType == null && this.BemaPipelineActionTypeId > 0 )
            {
                bemaPipelineActionType = new BemaPipelineActionTypeService( rockContext )
                    .Queryable().AsNoTracking()
                    .FirstOrDefault( g => g.Id == this.BemaPipelineActionTypeId );
            }

            if ( bemaPipelineActionType != null )
            {
                return bemaPipelineActionType.GetInheritedAttributesForQualifier( rockContext, TypeId, "ComponentEntityTypeId" );
            }

            return null;
        }
        #endregion

        #region Static Methods


        internal static BemaPipelineAction Activate( BemaPipelineActionTypeCache actionTypeCache, BemaPipeline bemaPipeline )
        {
            using ( var rockContext = new RockContext() )
            {
                return Activate( actionTypeCache, bemaPipeline, rockContext );
            }
        }

        internal static BemaPipelineAction Activate( BemaPipelineActionTypeCache actionTypeCache, BemaPipeline bemaPipeline, RockContext rockContext )
        {
            var action = new BemaPipelineAction();
            action.BemaPipelineActionTypeId = actionTypeCache.Id;
            action.BemaPipelineActionState = BemaPipelineActionState.WaitingOnItems;

            return action;
        }

        #endregion

    }

    public enum BemaPipelineActionState
    {

        WaitingOnItems = 4,
        ReadyToProcess = 1,
        ReadyForManualAction = 5,

        Skipped = 0,
        Completed = 2,
        TimedOut = 3,
        ActionTypeArchived = 6
    }

    #region Entity Configuration


    public partial class BemaPipelineActionConfiguration : EntityTypeConfiguration<BemaPipelineAction>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BemaPipelineActionConfiguration"/> class.
        /// </summary> 
        public BemaPipelineActionConfiguration()
        {
            this.HasRequired( p => p.BemaPipeline ).WithMany( p => p.BemaPipelineActions ).HasForeignKey( p => p.BemaPipelineId ).WillCascadeOnDelete( true );
            this.HasOptional( p => p.BemaPipelineActionType ).WithMany( p => p.BemaPipelineActions ).HasForeignKey( p => p.BemaPipelineActionTypeId );

            // IMPORTANT!!
            this.HasEntitySetName( "BemaPipelineAction" );
        }
    }

    #endregion

}