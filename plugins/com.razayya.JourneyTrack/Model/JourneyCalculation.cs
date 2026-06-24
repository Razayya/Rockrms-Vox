using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Model;

namespace com.razayya.JourneyTrack.Model
{
    /// <summary>
    /// A single JourneyCalculation that evaluates a population and writes a computed value
    /// to a target Person Attribute. The JourneyCalculation logic is determined by its
    /// CalculationType component (Attendance, PersonFilter, DataView, GroupMembership, Completion).
    /// </summary>
    [Table( Constants.TableName.JourneyCalculation )]
    [DataContract]
    public class JourneyCalculation : Model<JourneyCalculation>, IRockEntity, Rock.Data.IOrdered
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the parent Stage Id.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int StageId { get; set; }

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
        /// Gets or sets whether this JourneyCalculation is active.
        /// </summary>
        [DataMember]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the display order within the parent sub-group.
        /// </summary>
        [DataMember]
        public int Order { get; set; }

        /// <summary>
        /// Gets or sets the target Person Attribute Id that this JourneyCalculation writes to.
        /// When null the calculation is "transient" — its match result is consumed in-memory
        /// by Stage and Program rollups but is never written to a Person Attribute.
        /// </summary>
        [DataMember]
        public int? PersonAttributeId { get; set; }

        /// <summary>
        /// Gets or sets the EntityType Id of the CalculationType component
        /// that provides the evaluation logic.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int CalculationTypeEntityTypeId { get; set; }

        /// <summary>
        /// Gets or sets the SystemCommunication Id to send when a person transitions
        /// to matching this calculation (false -> true). Null disables.
        /// </summary>
        [DataMember]
        public int? OnMatchSystemCommunicationId { get; set; }

        /// <summary>
        /// Gets or sets the Lava template used to produce the attribute value
        /// when a person matches the JourneyCalculation criteria. Merge fields are
        /// provided by the CalculationType component.
        /// </summary>
        [DataMember]
        public string ResultLavaTemplate { get; set; }

        /// <summary>
        /// Gets or sets the behavior when a person does not match the JourneyCalculation criteria.
        /// </summary>
        [DataMember]
        public NoMatchBehavior NoMatchBehavior { get; set; } = NoMatchBehavior.LeaveUnchanged;

        /// <summary>
        /// Gets or sets the Lava template used to produce the attribute value
        /// when a person does not match. Only used when NoMatchBehavior is WriteLava.
        /// </summary>
        [DataMember]
        public string NoMatchLavaTemplate { get; set; }

        /// <summary>
        /// When true, the engine skips evaluating this calculation entirely for any
        /// person who already has a non-blank value stored in the target Person
        /// Attribute. "Write once, then leave alone" semantics. Has no effect for
        /// transient (sink-optional) calculations — nothing to check against.
        /// </summary>
        [DataMember]
        public bool SkipIfTargetHasValue { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the parent Stage.
        /// </summary>
        [DataMember]
        public virtual Stage Stage { get; set; }

        /// <summary>
        /// Gets or sets the target Person Attribute. Null when this is a transient calculation.
        /// </summary>
        [DataMember]
        public virtual Rock.Model.Attribute PersonAttribute { get; set; }

        /// <summary>
        /// Gets or sets the EntityType of the CalculationType component.
        /// </summary>
        [DataMember]
        public virtual EntityType CalculationTypeEntityType { get; set; }

        /// <summary>
        /// Gets or sets the SystemCommunication sent on match transitions.
        /// </summary>
        [DataMember]
        public virtual Rock.Model.SystemCommunication OnMatchSystemCommunication { get; set; }

        #endregion
    }

    #region Entity Configuration

    public partial class JourneyCalculationConfiguration : EntityTypeConfiguration<JourneyCalculation>
    {
        public JourneyCalculationConfiguration()
        {
            this.HasRequired( c => c.Stage ).WithMany( sg => sg.Calculations ).HasForeignKey( c => c.StageId ).WillCascadeOnDelete( true );
            this.HasOptional( c => c.PersonAttribute ).WithMany().HasForeignKey( c => c.PersonAttributeId ).WillCascadeOnDelete( false );
            this.HasRequired( c => c.CalculationTypeEntityType ).WithMany().HasForeignKey( c => c.CalculationTypeEntityTypeId ).WillCascadeOnDelete( false );
            this.HasOptional( c => c.OnMatchSystemCommunication ).WithMany().HasForeignKey( c => c.OnMatchSystemCommunicationId ).WillCascadeOnDelete( false );
            this.HasEntitySetName( "JourneyCalculation" );
        }
    }

    #endregion
}
