using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Model;

namespace com.razayya.JourneyTrack.Model
{
    /// <summary>
    /// Represents a step/stage within a JourneyProgram. Contains ordered Calculations.
    /// Can optionally scope its population to one or more prerequisite SubGroups' Completion passers.
    /// </summary>
    [Table( Constants.TableName.Stage )]
    [DataContract]
    public class Stage : Model<Stage>, IRockEntity, Rock.Data.IOrdered
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the parent JourneyProgram Id.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int JourneyProgramId { get; set; }

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
        /// Gets or sets the comma-delimited list of prerequisite Stage Ids.
        /// When set, this sub-group's working population is the intersection of all
        /// prerequisite sub-groups' Completion JourneyCalculation passers.
        /// When empty, the parent group's base population is used.
        /// </summary>
        [DataMember]
        [MaxLength( 500 )]
        public string PrerequisiteStageIds { get; set; }

        /// <summary>
        /// Gets or sets an optional DataView Id for additional population narrowing
        /// beyond the parent group's base population.
        /// </summary>
        [DataMember]
        public int? AdditionalDataViewId { get; set; }

        /// <summary>
        /// Gets or sets the SystemCommunication Id sent when a person transitions
        /// to passing this Stage's Completion calc (false -> true). Null disables.
        /// </summary>
        [DataMember]
        public int? OnCompleteSystemCommunicationId { get; set; }

        /// <summary>
        /// Optional nested ANY/ALL logic tree (JSON) that combines this Stage's
        /// calculations into the passer set. Leaves reference a child
        /// JourneyCalculation by Id (<c>{ "calcId": N }</c>); groups combine with
        /// All (Intersect) / Any (Union) / AllFalse / AnyFalse, mirroring a
        /// DataView filter tree but folded in set-space.
        ///
        /// <para>When null/blank, the legacy gate applies: a Completion calc (if
        /// present) defines the passers, otherwise the intersection of all
        /// non-Completion calcs. See <c>JourneyTrackService.ProcessSubGroupInternal</c>.</para>
        /// </summary>
        [DataMember]
        public string LogicTreeJson { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the parent JourneyProgram.
        /// </summary>
        [DataMember]
        public virtual JourneyProgram JourneyProgram { get; set; }

        /// <summary>
        /// Gets or sets the additional DataView.
        /// </summary>
        [DataMember]
        public virtual DataView AdditionalDataView { get; set; }

        /// <summary>
        /// Gets or sets the child calculations.
        /// </summary>
        [DataMember]
        public virtual ICollection<JourneyCalculation> Calculations
        {
            get { return _calculations ?? ( _calculations = new Collection<JourneyCalculation>() ); }
            set { _calculations = value; }
        }
        private ICollection<JourneyCalculation> _calculations;

        /// <summary>
        /// Gets or sets the SystemCommunication sent when a person completes this Stage.
        /// </summary>
        [DataMember]
        public virtual Rock.Model.SystemCommunication OnCompleteSystemCommunication { get; set; }

        #endregion
    }

    /// <summary>
    /// Leaf payload for a Stage's logic tree — references one of the Stage's child
    /// JourneyCalculations by Id. The calc's matched-person set is the leaf's value;
    /// the tree folds those sets with set operations. (Newtonsoft matches the
    /// <c>calcId</c> JSON property case-insensitively.)
    /// </summary>
    public class StageLogicLeaf
    {
        /// <summary>The Id of a child JourneyCalculation in this Stage.</summary>
        public int CalcId { get; set; }
    }

    #region Entity Configuration

    public partial class StageConfiguration : EntityTypeConfiguration<Stage>
    {
        public StageConfiguration()
        {
            this.HasRequired( sg => sg.JourneyProgram ).WithMany( g => g.Stages ).HasForeignKey( sg => sg.JourneyProgramId ).WillCascadeOnDelete( true );
            this.HasOptional( sg => sg.AdditionalDataView ).WithMany().HasForeignKey( sg => sg.AdditionalDataViewId ).WillCascadeOnDelete( false );
            this.HasOptional( sg => sg.OnCompleteSystemCommunication ).WithMany().HasForeignKey( sg => sg.OnCompleteSystemCommunicationId ).WillCascadeOnDelete( false );
            this.HasEntitySetName( "Stage" );
        }
    }

    #endregion
}
