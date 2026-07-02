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
    }
}
