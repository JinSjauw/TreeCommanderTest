using System;

namespace BehaviourTree.Core
{
    // ═══════════════════════════════════════════════════════════════════
    // DynamicParamDescriptor — self-describing parameter layout for
    // dynamic-type nodes that use DeserializeParameters instead of
    // [SharedVar] C# fields. Each descriptor maps to one FieldData entry.
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Kind of a dynamic parameter in the node inspector.</summary>
    public enum DynamicParamKind
    {
        /// <summary>Blackboard variable picker with inline S+F buttons.</summary>
        Variable,
        /// <summary>Toggle between constant (typed field) and variable (dropdown). Shows C/V button.</summary>
        Toggle,
        /// <summary>Read-only constant value. No variable binding.</summary>
        Constant,
        /// <summary>Enum dropdown for operation selection (e.g. Equal, Less, Greater).</summary>
        Operation,
        /// <summary>
        /// Constant value sourced from a field on a ScriptableObject in the tree's config sources list.
        /// Renders as a three-way C/V/SO toggle: constant / variable / SO-field constant.
        /// Resolved at bake time and packed as a regular FieldData constant — zero runtime overhead.
        /// </summary>
        ScriptableObjectConstant,
    }

    /// <summary>
    /// Describes one parameter slot for a dynamic-type node method.
    /// Returned by <see cref="NodeMethod.GetDynamicParamDescriptors"/>.
    /// When non-null, the editor renders a generic inspector from these descriptors
    /// instead of requiring per-node hardcoded switch cases.
    /// </summary>
    public struct DynamicParamDescriptor
    {
        /// <summary>Bold header shown above the control row. When non-null, rendered as "TitleLabel : Type".</summary>
        public string titleLabel;

        /// <summary>UI label shown next to the control in the inspector.</summary>
        public string label;

        /// <summary>Controls what UI is rendered for this parameter.</summary>
        public DynamicParamKind kind;

        /// <summary>Index into the fieldEntries array for this parameter.</summary>
        public int index;

        /// <summary>
        /// When non-null, the F (filter) popup only shows these types.
        /// Null means any type is allowed. Ignored for non-Variable / non-Toggle kinds.
        /// </summary>
        public Type[] allowedTypes;

        /// <summary>
        /// When set, this entry's type will automatically sync to match the resolved
        /// type of the entry at the specified index. The editor propagates type changes
        /// from the source entry to this entry whenever the source type is updated.
        /// </summary>
        public int? syncTypeFromIndex;

        /// <summary>
        /// For Operation kind: the enum type to render as a dropdown.
        /// Only used when kind == DynamicParamKind.Operation.
        /// </summary>
        public Type operationEnumType;

        /// <summary>
        /// For Operation kind: optional per-type filter. Receives the current variable
        /// type and returns the subset of enum value indices to display in the dropdown.
        /// Return null to show all enum values. Only used when kind == DynamicParamKind.Operation.
        /// </summary>
        public Func<Type, int[]> getAvailableOpIndices;
    }
}
