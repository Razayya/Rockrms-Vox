using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Model;

namespace com.razayya.CustomPersonAttributeSyncEngine.Model
{
    /// <summary>
    /// Represents a step/stage within a CalculationGroup. Contains ordered Calculations.
    /// Can optionally scope its population to the previous SubGroup's Completion result (funnel behavior).
    /// </summary>
    [Table( Constants.TableName.CalculationSubGroup )]
    [DataContract]
    public class CalculationSubGroup : Model<CalculationSubGroup>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the parent CalculationGroup Id.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int CalculationGroupId { get; set; }

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
        /// Gets or sets whether this sub-group is active.
        /// </summary>
        [DataMember]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the display order within the parent group.
        /// </summary>
        [DataMember]
        public int Order { get; set; }

        /// <summary>
        /// Gets or sets whether this sub-group scopes its population to the previous
        /// sub-group's Completion calculation passers. When true, only people who passed
        /// the prior sub-group's Completion calculation will be evaluated.
        /// </summary>
        [DataMember]
        public bool ScopeToPreviousSubGroup { get; set; } = true;

        /// <summary>
        /// Gets or sets an optional DataView Id for additional population narrowing
        /// beyond the parent group's base population.
        /// </summary>
        [DataMember]
        public int? AdditionalDataViewId { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the parent CalculationGroup.
        /// </summary>
        [DataMember]
        public virtual CalculationGroup CalculationGroup { get; set; }

        /// <summary>
        /// Gets or sets the additional DataView.
        /// </summary>
        [DataMember]
        public virtual DataView AdditionalDataView { get; set; }

        /// <summary>
        /// Gets or sets the child calculations.
        /// </summary>
        [DataMember]
        public virtual ICollection<Calculation> Calculations
        {
            get { return _calculations ?? ( _calculations = new Collection<Calculation>() ); }
            set { _calculations = value; }
        }
        private ICollection<Calculation> _calculations;

        #endregion
    }

    #region Entity Configuration

    public partial class CalculationSubGroupConfiguration : EntityTypeConfiguration<CalculationSubGroup>
    {
        public CalculationSubGroupConfiguration()
        {
            this.HasRequired( sg => sg.CalculationGroup ).WithMany( g => g.CalculationSubGroups ).HasForeignKey( sg => sg.CalculationGroupId ).WillCascadeOnDelete( true );
            this.HasOptional( sg => sg.AdditionalDataView ).WithMany().HasForeignKey( sg => sg.AdditionalDataViewId ).WillCascadeOnDelete( false );
            this.HasEntitySetName( "CalculationSubGroup" );
        }
    }

    #endregion
}
