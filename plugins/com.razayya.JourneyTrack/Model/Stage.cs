using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
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

        /// <summary>
        /// Optional JSON describing how this Stage's MediaWatched calculations are
        /// partitioned into ordered video sequences ("Media Groups") for the app's
        /// video-serving surface (see <see cref="StageMediaGroups"/>). Each named group
        /// is its own sequence; any active MediaWatched calc not listed in a group falls
        /// into an implicit "default" sequence ordered by calc <c>Order</c>. Sequences run
        /// in parallel; within a sequence a video is locked until the prior one is watched.
        ///
        /// <para>When null/blank there are no named groups, so every MediaWatched calc is
        /// in the default sequence — i.e. the whole Stage is one sequential playlist.</para>
        ///
        /// <para>Purely a presentation/serving concern consumed by
        /// <c>StageVideoData</c> — it does NOT affect engine evaluation or Stage
        /// completion (those are governed by <see cref="LogicTreeJson"/>).</para>
        /// </summary>
        [DataMember]
        public string MediaGroupsJson { get; set; }

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

    /// <summary>
    /// One named video sequence within a Stage's <see cref="Stage.MediaGroupsJson"/>.
    /// The <see cref="CalcIds"/> order IS the watch order — within a group, a video is
    /// locked until the prior one is watched. Different groups run in parallel.
    /// </summary>
    public class MediaGroup
    {
        /// <summary>Stable key for the group (editor-assigned, e.g. "g1"). Used so the
        /// app can address a sequence independently of its display name.</summary>
        public string Key { get; set; }

        /// <summary>Display name shown as the sequence header in the app.</summary>
        public string Name { get; set; }

        /// <summary>Ordered JourneyCalculation Ids in this sequence (array order = watch order).</summary>
        public List<int> CalcIds { get; set; } = new List<int>();
    }

    /// <summary>
    /// Root payload of <see cref="Stage.MediaGroupsJson"/>: the ordered list of named
    /// <see cref="MediaGroup"/> sequences. Any active MediaWatched calc not referenced
    /// here belongs to the implicit "default" sequence. Tolerant Parse/ToJson mirror the
    /// <c>LogicTree</c> helpers so callers never have to touch Newtonsoft directly.
    /// </summary>
    public class StageMediaGroups
    {
        /// <summary>The named sequences, in display order.</summary>
        public List<MediaGroup> Groups { get; set; } = new List<MediaGroup>();

        /// <summary>
        /// Parses <paramref name="json"/> into a <see cref="StageMediaGroups"/>. Returns an
        /// empty instance (never null) for null/blank/malformed input. Each group's
        /// <see cref="MediaGroup.CalcIds"/> is de-duplicated and a calc is kept only in the
        /// first group that references it (a calc belongs to at most one sequence).
        /// </summary>
        public static StageMediaGroups Parse( string json )
        {
            var result = new StageMediaGroups();
            if ( string.IsNullOrWhiteSpace( json ) )
            {
                return result;
            }

            StageMediaGroups parsed;
            try
            {
                parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<StageMediaGroups>( json );
            }
            catch
            {
                return result;
            }

            if ( parsed?.Groups == null )
            {
                return result;
            }

            var seen = new HashSet<int>();
            foreach ( var group in parsed.Groups )
            {
                if ( group == null )
                {
                    continue;
                }
                var calcIds = ( group.CalcIds ?? new List<int>() )
                    .Where( id => id > 0 && seen.Add( id ) )
                    .ToList();
                result.Groups.Add( new MediaGroup
                {
                    Key = string.IsNullOrWhiteSpace( group.Key ) ? null : group.Key.Trim(),
                    Name = group.Name?.Trim(),
                    CalcIds = calcIds
                } );
            }
            return result;
        }

        /// <summary>Serializes to JSON, or empty string when there are no groups with members.</summary>
        public static string ToJson( StageMediaGroups groups )
        {
            var nonEmpty = ( groups?.Groups ?? new List<MediaGroup>() )
                .Where( g => g != null && g.CalcIds != null && g.CalcIds.Count > 0 )
                .ToList();
            if ( nonEmpty.Count == 0 )
            {
                return string.Empty;
            }
            return Newtonsoft.Json.JsonConvert.SerializeObject( new StageMediaGroups { Groups = nonEmpty } );
        }
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
