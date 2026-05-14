using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Model;

namespace com.razayya.CustomPersonAttributeSyncEngine.Model
{
    /// <summary>
    /// A single calculation that evaluates a population and writes a computed value
    /// to a target Person Attribute. The calculation logic is determined by its
    /// CalculationType component (Attendance, PersonFilter, DataView, GroupMembership, Completion).
    /// </summary>
    [Table( Constants.TableName.Calculation )]
    [DataContract]
    public class Calculation : Model<Calculation>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the parent CalculationSubGroup Id.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int CalculationSubGroupId { get; set; }

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
        /// Gets or sets whether this calculation is active.
        /// </summary>
        [DataMember]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the display order within the parent sub-group.
        /// </summary>
        [DataMember]
        public int Order { get; set; }

        /// <summary>
        /// Gets or sets the target Person Attribute Id that this calculation writes to.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int PersonAttributeId { get; set; }

        /// <summary>
        /// Gets or sets the EntityType Id of the CalculationType component
        /// that provides the evaluation logic.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int CalculationTypeEntityTypeId { get; set; }

        /// <summary>
        /// Gets or sets the Lava template used to produce the attribute value
        /// when a person matches the calculation criteria. Merge fields are
        /// provided by the CalculationType component.
        /// </summary>
        [DataMember]
        public string ResultLavaTemplate { get; set; }

        /// <summary>
        /// Gets or sets the behavior when a person does not match the calculation criteria.
        /// </summary>
        [DataMember]
        public NoMatchBehavior NoMatchBehavior { get; set; } = NoMatchBehavior.LeaveUnchanged;

        /// <summary>
        /// Gets or sets the Lava template used to produce the attribute value
        /// when a person does not match. Only used when NoMatchBehavior is WriteLava.
        /// </summary>
        [DataMember]
        public string NoMatchLavaTemplate { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the parent CalculationSubGroup.
        /// </summary>
        [DataMember]
        public virtual CalculationSubGroup CalculationSubGroup { get; set; }

        /// <summary>
        /// Gets or sets the target Person Attribute.
        /// </summary>
        [DataMember]
        public virtual Rock.Model.Attribute PersonAttribute { get; set; }

        /// <summary>
        /// Gets or sets the EntityType of the CalculationType component.
        /// </summary>
        [DataMember]
        public virtual EntityType CalculationTypeEntityType { get; set; }

        #endregion
    }

    #region Entity Configuration

    public partial class CalculationConfiguration : EntityTypeConfiguration<Calculation>
    {
        public CalculationConfiguration()
        {
            this.HasRequired( c => c.CalculationSubGroup ).WithMany( sg => sg.Calculations ).HasForeignKey( c => c.CalculationSubGroupId ).WillCascadeOnDelete( true );
            this.HasRequired( c => c.PersonAttribute ).WithMany().HasForeignKey( c => c.PersonAttributeId ).WillCascadeOnDelete( false );
            this.HasRequired( c => c.CalculationTypeEntityType ).WithMany().HasForeignKey( c => c.CalculationTypeEntityTypeId ).WillCascadeOnDelete( false );
            this.HasEntitySetName( "Calculation" );
        }
    }

    #endregion
}
