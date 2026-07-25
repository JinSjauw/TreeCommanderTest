using System;

namespace BehaviourTree.Core
{
    public enum DynamicParamKind
    {
        Variable,
        Toggle,
        Constant,
        Operation,
        /// <summary>
        /// Constant value sourced from a field on a ScriptableObject in the tree's config sources list.
        /// Renders as a three-way C/V/SO toggle: constant / variable / SO-field constant.
        /// Resolved at bake time and packed as a regular FieldData constant — zero runtime overhead.
        /// </summary>
        ScriptableObjectConstant,
    }

    /// <summary>Which custom editor a constant-mode param uses instead of the raw value field.</summary>
    public enum ConstantEditorHint
    {
        None,
        RoleDropdown,
        OrderDropdown,
    }

    public struct DynamicParamDescriptor
    {
        public string titleLabel;
        public string label;
        public DynamicParamKind kind;
        public int index;
        public Type[] allowedTypes;
        public int? syncTypeFromIndex;
        public bool syncElementType;
        public Type operationEnumType;
        public Func<Type, int[]> getAvailableOpIndices;

        // ── Attribute-projection parity (set by ParamSchemaReflection; dynamic
        // methods may also set these via the Params builder) ──

        /// <summary>Exact reflected field name for attribute-projected params.
        /// Written to entry.fieldName so runtime GetSlotByName() keeps working.
        /// Null for hand-authored dynamic params (label-derived fallback).</summary>
        public string fieldName;

        /// <summary>Default array-ness ([SharedArray]). Mutable afterwards via the type filter button.</summary>
        public bool isArray;

        /// <summary>Hidden from inspector; variableName auto-bound from autoVariableName.</summary>
        public bool isHidden;
        public string autoVariableName;

        /// <summary>Custom constant editor (role/order dropdown) instead of the raw value field.</summary>
        public ConstantEditorHint constantEditor;

        /// <summary>When set, the row is only drawn while that entry's boolValue is true
        /// (replaces the hardcoded customTickValue rule).</summary>
        public int? visibilityDependsOnIndex;
    }
}
