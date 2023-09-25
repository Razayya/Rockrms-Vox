using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;
using Rock.Data;
using Rock.Model;
using Rock.Security;

namespace com.bemaservices.BemaPipeline.Model
{
    [Table("_com_bemaservices_BemaPipeline_BemaPipelineType")]
    [DataContract]
    public class BemaPipelineType : Rock.Data.Model<BemaPipelineType>, Rock.Data.IRockEntity
    {

        #region Entity Properties

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Description { get; set; }

        [DataMember]
        public int EntityTypeId { get; set; }

        [DataMember]
        public bool IsActive { get; set; }

        #endregion

        #region Virtual Properties

        public virtual EntityType EntityType { get; set; }

        [LavaInclude]
        public virtual ICollection<BemaPipeline> BemaPipelines
        {
            get { return _bemaPipelines ?? (_bemaPipelines = new Collection<BemaPipeline>()); }
            set { _bemaPipelines = value; }
        }
        private ICollection<BemaPipeline> _bemaPipelines;

        [LavaInclude]
        public virtual ICollection<BemaPipelineActionType> BemaPipelineActionTypes
        {
            get { return _bemaPipelineActionTypes ?? (_bemaPipelineActionTypes = new Collection<BemaPipelineActionType>()); }
            set { _bemaPipelineActionTypes = value; }
        }
        private ICollection<BemaPipelineActionType> _bemaPipelineActionTypes;

        /// <summary>
        /// A dictionary of actions that this class supports and the description of each.
        /// Special key: 'Manage' for managing pipeline process with special commands.
        /// </summary>
        public override Dictionary<string, string> SupportedActions
        {
            get
            {
                if ( _supportedActions == null )
                {
                    _supportedActions = new Dictionary<string, string>();
                    _supportedActions.Add( Authorization.VIEW, "The roles and/or users that have access to view." );
                    _supportedActions.Add( "Manage", "The roles and/or users that have access to manage the pipeline process." );
                    _supportedActions.Add( Authorization.EDIT, "The roles and/or users that have access to edit." );
                    _supportedActions.Add( Authorization.ADMINISTRATE, "The roles and/or users that have access to administrate." );
                }

                return _supportedActions;
            }
        }

        private Dictionary<string, string> _supportedActions;

        #endregion

    }

    #region Entity Configuration


    public partial class BemaPipelineTypeConfiguration : EntityTypeConfiguration<BemaPipelineType>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BemaPipelineTypeConfiguration"/> class.
        /// </summary> 
        public BemaPipelineTypeConfiguration()
        {
            this.HasRequired( p => p.EntityType ).WithMany().HasForeignKey( p => p.EntityTypeId ).WillCascadeOnDelete( true );


            // IMPORTANT!!
            this.HasEntitySetName("BemaPipelineType");
        }
    }

    #endregion

}