using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Model;

namespace com.razayya.JourneyTrack.Model
{
    /// <summary>
    /// One row per person + transition-event for which a SystemCommunication
    /// should be sent. SendJourneyCommunications picks up rows where
    /// SentDateTime IS NULL and dispatches them in batches.
    /// </summary>
    [Table( Constants.TableName.JourneyCommunicationLog )]
    [DataContract]
    public class JourneyCommunicationLog : Model<JourneyCommunicationLog>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets which level of object queued this row.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public CommunicationContextType ContextType { get; set; }

        /// <summary>
        /// Gets or sets the Id of the JourneyCalculation / Stage / JourneyProgram
        /// that triggered the queue (interpretation depends on ContextType).
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int ContextId { get; set; }

        /// <summary>
        /// Gets or sets the recipient's PersonAlias Id.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int PersonAliasId { get; set; }

        /// <summary>
        /// Gets or sets the SystemCommunication Id to dispatch.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int SystemCommunicationId { get; set; }

        /// <summary>
        /// Gets or sets when this row was queued.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public DateTime QueuedDateTime { get; set; }

        /// <summary>
        /// Gets or sets when the SystemCommunication was actually sent.
        /// Null until the dispatch job runs.
        /// </summary>
        [DataMember]
        public DateTime? SentDateTime { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the recipient PersonAlias.
        /// </summary>
        [DataMember]
        public virtual Rock.Model.PersonAlias PersonAlias { get; set; }

        /// <summary>
        /// Gets or sets the SystemCommunication template.
        /// </summary>
        [DataMember]
        public virtual Rock.Model.SystemCommunication SystemCommunication { get; set; }

        #endregion
    }

    #region Entity Configuration

    public partial class JourneyCommunicationLogConfiguration : EntityTypeConfiguration<JourneyCommunicationLog>
    {
        public JourneyCommunicationLogConfiguration()
        {
            this.HasRequired( l => l.PersonAlias ).WithMany().HasForeignKey( l => l.PersonAliasId ).WillCascadeOnDelete( false );
            this.HasRequired( l => l.SystemCommunication ).WithMany().HasForeignKey( l => l.SystemCommunicationId ).WillCascadeOnDelete( false );
            this.HasEntitySetName( "JourneyCommunicationLog" );
        }
    }

    #endregion
}
