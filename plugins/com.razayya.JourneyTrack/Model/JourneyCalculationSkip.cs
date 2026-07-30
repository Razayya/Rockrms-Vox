using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Model;

namespace com.razayya.JourneyTrack.Model
{
    /// <summary>
    /// A manual, per-person skip of a single JourneyCalculation, created by staff from
    /// the person profile. The engine treats an active skip like a "Skip If" filter
    /// match: the person is folded into the calc's matched set (passes stage completion
    /// and downstream gating) while the sink attribute is left blank. Restore via
    /// IsActive=false + RemovedDateTime so we keep the who/when audit trail; skip
    /// attribution rides the standard CreatedByPersonAliasId / CreatedDateTime.
    /// </summary>
    [Table( Constants.TableName.JourneyCalculationSkip )]
    [DataContract]
    public class JourneyCalculationSkip : Model<JourneyCalculationSkip>, IRockEntity
    {
        #region Entity Properties

        [Required]
        [DataMember( IsRequired = true )]
        public int JourneyCalculationId { get; set; }

        [Required]
        [DataMember( IsRequired = true )]
        public int PersonAliasId { get; set; }

        [DataMember]
        public bool IsActive { get; set; } = true;

        [DataMember]
        public string Note { get; set; }

        [DataMember]
        public System.DateTime? RemovedDateTime { get; set; }

        [DataMember]
        public int? RemovedByPersonAliasId { get; set; }

        #endregion

        #region Navigation

        [DataMember]
        public virtual JourneyCalculation JourneyCalculation { get; set; }

        [DataMember]
        public virtual PersonAlias PersonAlias { get; set; }

        [DataMember]
        public virtual PersonAlias RemovedByPersonAlias { get; set; }

        #endregion
    }

    public partial class JourneyCalculationSkipConfiguration : EntityTypeConfiguration<JourneyCalculationSkip>
    {
        public JourneyCalculationSkipConfiguration()
        {
            this.HasRequired( s => s.JourneyCalculation ).WithMany().HasForeignKey( s => s.JourneyCalculationId ).WillCascadeOnDelete( true );
            this.HasRequired( s => s.PersonAlias ).WithMany().HasForeignKey( s => s.PersonAliasId ).WillCascadeOnDelete( false );
            this.HasOptional( s => s.RemovedByPersonAlias ).WithMany().HasForeignKey( s => s.RemovedByPersonAliasId ).WillCascadeOnDelete( false );
            this.HasEntitySetName( "JourneyCalculationSkip" );
        }
    }
}
