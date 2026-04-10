using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Model;

namespace com.razayya.CustomPersonAttributeSyncEngine.Model
{
    /// <summary>
    /// Records the result of a single Calculation execution — whether triggered
    /// by the nightly job or on-demand from the UI.
    /// </summary>
    [Table( Constants.TableName.CalculationRun )]
    [DataContract]
    public class CalculationRun : Model<CalculationRun>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the Calculation that was executed.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int CalculationId { get; set; }

        /// <summary>
        /// Gets or sets when this run started.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public DateTime RunDateTime { get; set; }

        /// <summary>
        /// Gets or sets when this run completed.
        /// </summary>
        [DataMember]
        public DateTime? CompletedDateTime { get; set; }

        /// <summary>
        /// Gets or sets who triggered this run. Null when triggered by the nightly job.
        /// </summary>
        [DataMember]
        public int? RunByPersonAliasId { get; set; }

        /// <summary>
        /// Gets or sets the number of people in the evaluated population.
        /// </summary>
        [DataMember]
        public int PopulationCount { get; set; }

        /// <summary>
        /// Gets or sets the number of people who matched the calculation criteria.
        /// </summary>
        [DataMember]
        public int MatchedCount { get; set; }

        /// <summary>
        /// Gets or sets the number of attribute values that were written/changed.
        /// </summary>
        [DataMember]
        public int UpdatedCount { get; set; }

        /// <summary>
        /// Gets or sets the number of people skipped (no change needed or LeaveUnchanged).
        /// </summary>
        [DataMember]
        public int SkippedCount { get; set; }

        /// <summary>
        /// Gets or sets the number of errors encountered.
        /// </summary>
        [DataMember]
        public int ErrorCount { get; set; }

        /// <summary>
        /// Gets or sets whether the run completed without errors.
        /// </summary>
        [DataMember]
        public bool WasSuccessful { get; set; }

        /// <summary>
        /// Gets or sets error details or status information from the run.
        /// </summary>
        [DataMember]
        public string StatusMessage { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the Calculation.
        /// </summary>
        [DataMember]
        public virtual Calculation Calculation { get; set; }

        /// <summary>
        /// Gets or sets the PersonAlias who triggered the run.
        /// </summary>
        [DataMember]
        public virtual PersonAlias RunByPersonAlias { get; set; }

        #endregion
    }

    #region Entity Configuration

    public partial class CalculationRunConfiguration : EntityTypeConfiguration<CalculationRun>
    {
        public CalculationRunConfiguration()
        {
            this.HasRequired( r => r.Calculation ).WithMany().HasForeignKey( r => r.CalculationId ).WillCascadeOnDelete( true );
            this.HasOptional( r => r.RunByPersonAlias ).WithMany().HasForeignKey( r => r.RunByPersonAliasId ).WillCascadeOnDelete( false );
            this.HasEntitySetName( "CalculationRun" );
        }
    }

    #endregion
}
