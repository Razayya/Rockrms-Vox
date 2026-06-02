using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace com.razayya.JourneyTrack.Logic
{
    /// <summary>
    /// How a group node combines its children. Mirrors Rock's
    /// <see cref="Rock.Model.FilterExpressionType"/> (GroupAll / GroupAny /
    /// GroupAllFalse / GroupAnyFalse) so the model is familiar to anyone who has
    /// worked with DataView filters. The difference is purely in how it's
    /// evaluated: DataViewFilter folds children with Expression.AndAlso/OrElse to
    /// build SQL; JourneyTrack folds them with boolean AND/OR (and, at the stage
    /// level, set Intersect/Union) per person.
    /// </summary>
    public enum LogicGroupType
    {
        /// <summary>All children must be true (AND).</summary>
        All = 1,

        /// <summary>At least one child must be true (OR).</summary>
        Any = 2,

        /// <summary>All children must be false (NOT ANY).</summary>
        AllFalse = 3,

        /// <summary>At least one child must be false (NOT ALL).</summary>
        AnyFalse = 4
    }

    /// <summary>
    /// A node in a nested ANY/ALL logic tree, generic over the leaf payload so the
    /// tree machinery is written once and reused by every calc type that wants
    /// grouped logic. A node is EITHER a group (<see cref="Type"/> + <see cref="Children"/>)
    /// OR a leaf (<see cref="Leaf"/>) — never both.
    ///
    /// <para>JSON shape (see <see cref="LogicTree"/>):</para>
    /// <list type="bullet">
    ///   <item>Group: <c>{ "type": "Any", "children": [ ... ] }</c></item>
    ///   <item>Leaf: the bare leaf condition object (no <c>type</c>/<c>children</c>) —
    ///   identical to the legacy flat-array element, so existing configs and
    ///   raw-JSON authoring stay natural.</item>
    /// </list>
    /// </summary>
    /// <typeparam name="TLeaf">The calc-specific leaf condition type (e.g. FilterCondition, CompletionCriterion).</typeparam>
    public class LogicNode<TLeaf>
    {
        /// <summary>The combine type. Null on leaf nodes.</summary>
        public LogicGroupType? Type { get; set; }

        /// <summary>Child nodes. Null/empty on leaf nodes.</summary>
        public List<LogicNode<TLeaf>> Children { get; set; }

        /// <summary>The leaf payload. Null on group nodes.</summary>
        public TLeaf Leaf { get; set; }

        /// <summary>True when this node carries a leaf condition rather than a group.</summary>
        [JsonIgnore]
        public bool IsLeaf => !Type.HasValue;
    }

    /// <summary>
    /// Parses, evaluates, and serializes <see cref="LogicNode{TLeaf}"/> trees.
    ///
    /// <para><b>Backward compatible.</b> <see cref="Parse{TLeaf}"/> accepts BOTH the
    /// new nested object form and the legacy flat array form. A flat array is
    /// wrapped in a single synthetic root group whose type the caller supplies
    /// (e.g. PersonFilter passes All/Any based on its "Match All" toggle), so every
    /// existing calc config keeps evaluating exactly as before with no migration.</para>
    ///
    /// <para><b>Empty = no match.</b> A null/empty config, or a group with no
    /// children, evaluates to <c>false</c> (matches nobody) — matching the engine's
    /// existing "unconfigured calc matches nobody" behavior, and unlike
    /// DataViewFilter's "empty group = don't filter = match all", which would be
    /// dangerous here.</para>
    /// </summary>
    public static class LogicTree
    {
        /// <summary>
        /// Parse a stored config string into a root <see cref="LogicNode{TLeaf}"/>.
        /// Returns null when the config is blank or unparseable (caller treats as no-match).
        /// </summary>
        /// <param name="json">The stored JSON (legacy flat array or nested object).</param>
        /// <param name="legacyArrayGroupType">How to combine a legacy flat array (PersonFilter: All if Match All, else Any; Completion: All).</param>
        /// <param name="legacyLeafSelector">Optional filter applied to legacy-array leaves before wrapping (Completion uses this to keep only IsRequired criteria, preserving exact legacy semantics).</param>
        public static LogicNode<TLeaf> Parse<TLeaf>(
            string json,
            LogicGroupType legacyArrayGroupType,
            Func<List<TLeaf>, IEnumerable<TLeaf>> legacyLeafSelector = null )
        {
            if ( string.IsNullOrWhiteSpace( json ) )
            {
                return null;
            }

            JToken root;
            try
            {
                root = JToken.Parse( json );
            }
            catch
            {
                return null;
            }

            if ( root.Type == JTokenType.Array )
            {
                var leaves = root.ToObject<List<TLeaf>>() ?? new List<TLeaf>();
                if ( legacyLeafSelector != null )
                {
                    leaves = legacyLeafSelector( leaves ).ToList();
                }

                return new LogicNode<TLeaf>
                {
                    Type = legacyArrayGroupType,
                    Children = leaves
                        .Select( leaf => new LogicNode<TLeaf> { Leaf = leaf } )
                        .ToList()
                };
            }

            if ( root.Type == JTokenType.Object )
            {
                return ParseNode<TLeaf>( ( JObject ) root );
            }

            return null;
        }

        private static LogicNode<TLeaf> ParseNode<TLeaf>( JObject obj )
        {
            var typeTok = obj["type"] ?? obj["Type"];
            var childrenTok = obj["children"] ?? obj["Children"];

            // A node with a type or children property is a group; otherwise it's a bare leaf.
            if ( typeTok != null || childrenTok != null )
            {
                var node = new LogicNode<TLeaf>
                {
                    Type = ParseGroupType( typeTok ),
                    Children = new List<LogicNode<TLeaf>>()
                };

                if ( childrenTok is JArray arr )
                {
                    foreach ( var child in arr )
                    {
                        if ( child.Type == JTokenType.Object )
                        {
                            node.Children.Add( ParseNode<TLeaf>( ( JObject ) child ) );
                        }
                    }
                }

                return node;
            }

            return new LogicNode<TLeaf> { Leaf = obj.ToObject<TLeaf>() };
        }

        private static LogicGroupType ParseGroupType( JToken tok )
        {
            if ( tok == null )
            {
                return LogicGroupType.All;
            }

            var s = tok.ToString();
            if ( Enum.TryParse<LogicGroupType>( s, true, out var parsed ) )
            {
                return parsed;
            }

            if ( int.TryParse( s, out var n ) && Enum.IsDefined( typeof( LogicGroupType ), n ) )
            {
                return ( LogicGroupType ) n;
            }

            return LogicGroupType.All;
        }

        /// <summary>
        /// Recursively evaluate the tree for one subject using a leaf predicate.
        /// An empty/unconfigured group matches nobody (returns false).
        /// </summary>
        public static bool Evaluate<TLeaf>( LogicNode<TLeaf> node, Func<TLeaf, bool> leafEval )
        {
            if ( node == null )
            {
                return false;
            }

            if ( node.IsLeaf )
            {
                return node.Leaf != null && leafEval( node.Leaf );
            }

            var children = node.Children ?? new List<LogicNode<TLeaf>>();
            if ( children.Count == 0 )
            {
                // Unconfigured group → match nobody (intentionally NOT vacuous-true).
                return false;
            }

            switch ( node.Type.Value )
            {
                case LogicGroupType.All:
                    return children.All( c => Evaluate( c, leafEval ) );
                case LogicGroupType.Any:
                    return children.Any( c => Evaluate( c, leafEval ) );
                case LogicGroupType.AllFalse:
                    return children.All( c => !Evaluate( c, leafEval ) );
                case LogicGroupType.AnyFalse:
                    return children.Any( c => !Evaluate( c, leafEval ) );
                default:
                    return false;
            }
        }

        /// <summary>
        /// Flatten every leaf payload in the tree (used to batch-load the data a set
        /// of leaves needs before per-person evaluation).
        /// </summary>
        public static IEnumerable<TLeaf> GetLeaves<TLeaf>( LogicNode<TLeaf> node )
        {
            if ( node == null )
            {
                yield break;
            }

            if ( node.IsLeaf )
            {
                if ( node.Leaf != null )
                {
                    yield return node.Leaf;
                }
                yield break;
            }

            if ( node.Children == null )
            {
                yield break;
            }

            foreach ( var child in node.Children )
            {
                foreach ( var leaf in GetLeaves( child ) )
                {
                    yield return leaf;
                }
            }
        }

        /// <summary>True when the tree carries no leaves at all (treat as no-match).</summary>
        public static bool IsEmpty<TLeaf>( LogicNode<TLeaf> node )
        {
            return node == null || !GetLeaves( node ).Any();
        }

        /// <summary>
        /// Serialize a tree back to the nested JSON form (groups as
        /// <c>{ "type", "children" }</c>, leaves as their bare condition object).
        /// Used by the editors and ImportExport so a round-trip is lossless.
        /// </summary>
        public static string ToJson<TLeaf>( LogicNode<TLeaf> node )
        {
            var token = ToToken( node );
            return token?.ToString( Formatting.None ) ?? "[]";
        }

        private static JToken ToToken<TLeaf>( LogicNode<TLeaf> node )
        {
            if ( node == null )
            {
                return null;
            }

            if ( node.IsLeaf )
            {
                return node.Leaf == null ? null : JToken.FromObject( node.Leaf );
            }

            var children = new JArray();
            if ( node.Children != null )
            {
                foreach ( var child in node.Children )
                {
                    var childToken = ToToken( child );
                    if ( childToken != null )
                    {
                        children.Add( childToken );
                    }
                }
            }

            return new JObject
            {
                ["type"] = node.Type.Value.ToString(),
                ["children"] = children
            };
        }
    }
}
