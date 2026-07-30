using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Model;

namespace com.razayya.JourneyTrack.Model
{
    /// <summary>
    /// A single person's enrollment in a JourneyProgram. Drives the base population
    /// for programs with RequiresEnrollment = true. Soft-unenroll via IsActive=false +
    /// UnenrolledDateTime so we keep history.
    /// </summary>
    [Table( Constants.TableName.JourneyProgramEnrollment )]
    [DataContract]
    public class JourneyProgramEnrollment : Model<JourneyProgramEnrollment>, IRockEntity
    {
        #region Entity Properties

        [Required]
        [DataMember( IsRequired = true )]
        public int JourneyProgramId { get; set; }

        [Required]
        [DataMember( IsRequired = true )]
        public int PersonAliasId { get; set; }

        [DataMember]
        public System.DateTime EnrolledDateTime { get; set; }

        [DataMember]
        public System.DateTime? UnenrolledDateTime { get; set; }

        [DataMember]
        public bool IsActive { get; set; } = true;

        [DataMember]
        public int? EnrolledByPersonAliasId { get; set; }

        [MaxLength( 100 )]
        [DataMember]
        public string Source { get; set; }

        [DataMember]
        public string Note { get; set; }

        /// <summary>
        /// Persisted per-stage progress: a JSON {stageId: passed} map maintained by
        /// the engine on every sync that evaluates this person (single-person page
        /// syncs merge the stages they evaluated; full program runs rewrite all).
        /// Read surfaces (personjourneyprogress) serve this instead of re-running
        /// the engine. Null = never evaluated; readers fall back to a live pass.
        /// </summary>
        [DataMember]
        public string StageStatusJson { get; set; }

        /// <summary>
        /// When <see cref="StageStatusJson"/> was last written.
        /// </summary>
        [DataMember]
        public System.DateTime? StageStatusModifiedDateTime { get; set; }

        #endregion

        #region Navigation

        [DataMember]
        public virtual JourneyProgram JourneyProgram { get; set; }

        [DataMember]
        public virtual PersonAlias PersonAlias { get; set; }

        [DataMember]
        public virtual PersonAlias EnrolledByPersonAlias { get; set; }

        #endregion
    }

    public partial class JourneyProgramEnrollmentConfiguration : EntityTypeConfiguration<JourneyProgramEnrollment>
    {
        public JourneyProgramEnrollmentConfiguration()
        {
            this.HasRequired( e => e.JourneyProgram ).WithMany().HasForeignKey( e => e.JourneyProgramId ).WillCascadeOnDelete( true );
            this.HasRequired( e => e.PersonAlias ).WithMany().HasForeignKey( e => e.PersonAliasId ).WillCascadeOnDelete( false );
            this.HasOptional( e => e.EnrolledByPersonAlias ).WithMany().HasForeignKey( e => e.EnrolledByPersonAliasId ).WillCascadeOnDelete( false );
            this.HasEntitySetName( "JourneyProgramEnrollment" );
        }
    }
}
