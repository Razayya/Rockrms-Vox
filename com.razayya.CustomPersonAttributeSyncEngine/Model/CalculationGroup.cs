using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Model;
using Rock.Security;

namespace com.razayya.CustomPersonAttributeSyncEngine.Model
{
    /// <summary>
    /// Top-level securable container for a set of calculation sub-groups.
    /// Defines the base population filter (Record Status, Connection Status, Campus, DataView).
    /// </summary>
    [Table( Constants.TableName.CalculationGroup )]
    [DataContract]
    public class CalculationGroup : Model<CalculationGroup>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [Required]
        [MaxLength( 200 )]
        [DataMember( IsRequired = true )]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        [DataMember]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets whether this group is active.
        /// </summary>
        [DataMember]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the display order.
        /// </summary>
        [DataMember]
        public int Order { get; set; }

        /// <summary>
        /// Gets or sets the Record Status DefinedValue Id for population filtering.
        /// </summary>
        [DataMember]
        public int? RecordStatusValueId { get; set; }

        /// <summary>
        /// Gets or sets the Connection Status DefinedValue Id for population filtering.
        /// </summary>
        [DataMember]
        public int? ConnectionStatusValueId { get; set; }

        /// <summary>
        /// Gets or sets the Campus Id for population filtering.
        /// </summary>
        [DataMember]
        public int? CampusId { get; set; }

        /// <summary>
        /// Gets or sets the DataView Id for additional population filtering.
        /// </summary>
        [DataMember]
        public int? DataViewId { get; set; }

        /// <summary>
        /// Gets or sets the last time this group was processed by the sync job.
        /// </summary>
        [DataMember]
        public System.DateTime? LastRunDateTime { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the Record Status DefinedValue.
        /// </summary>
        [DataMember]
        public virtual DefinedValue RecordStatusValue { get; set; }

        /// <summary>
        /// Gets or sets the Connection Status DefinedValue.
        /// </summary>
        [DataMember]
        public virtual DefinedValue ConnectionStatusValue { get; set; }

        /// <summary>
        /// Gets or sets the Campus.
        /// </summary>
        [DataMember]
        public virtual Campus Campus { get; set; }

        /// <summary>
        /// Gets or sets the DataView.
        /// </summary>
        [DataMember]
        public virtual DataView DataView { get; set; }

        /// <summary>
        /// Gets or sets the child sub-groups.
        /// </summary>
        [DataMember]
        public virtual ICollection<CalculationSubGroup> CalculationSubGroups
        {
            get { return _calculationSubGroups ?? ( _calculationSubGroups = new Collection<CalculationSubGroup>() ); }
            set { _calculationSubGroups = value; }
        }
        private ICollection<CalculationSubGroup> _calculationSubGroups;

        #endregion

        #region Security

        /// <summary>
        /// Gets the supported security actions.
        /// </summary>
        public override Dictionary<string, string> SupportedActions
        {
            get
            {
                if ( _supportedActions == null )
                {
                    _supportedActions = new Dictionary<string, string>
                    {
                        { Authorization.VIEW, "The roles and/or users that have access to view this calculation group." },
                        { Authorization.EDIT, "The roles and/or users that have access to edit this calculation group." },
                        { Authorization.ADMINISTRATE, "The roles and/or users that have access to administrate this calculation group." }
                    };
                }

                return _supportedActions;
            }
        }
        private Dictionary<string, string> _supportedActions;

        #endregion
    }

    #region Entity Configuration

    public partial class CalculationGroupConfiguration : EntityTypeConfiguration<CalculationGroup>
    {
        public CalculationGroupConfiguration()
        {
            this.HasOptional( g => g.RecordStatusValue ).WithMany().HasForeignKey( g => g.RecordStatusValueId ).WillCascadeOnDelete( false );
            this.HasOptional( g => g.ConnectionStatusValue ).WithMany().HasForeignKey( g => g.ConnectionStatusValueId ).WillCascadeOnDelete( false );
            this.HasOptional( g => g.Campus ).WithMany().HasForeignKey( g => g.CampusId ).WillCascadeOnDelete( false );
            this.HasOptional( g => g.DataView ).WithMany().HasForeignKey( g => g.DataViewId ).WillCascadeOnDelete( false );
            this.HasEntitySetName( "CalculationGroup" );
        }
    }

    #endregion
}
