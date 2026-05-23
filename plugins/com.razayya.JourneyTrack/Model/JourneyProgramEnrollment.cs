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
