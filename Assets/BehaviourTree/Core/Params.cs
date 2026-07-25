using System;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Fluent factory for DynamicParamDescriptor. Removes object-initializer
    /// boilerplate from GetDynamicParamDescriptors() overrides.
    /// </summary>
    public static class Params
    {
        /// <summary>Blackboard variable picker row.</summary>
        public static DynamicParamDescriptor Variable(string title, params Type[] allowedTypes) =>
            Base(title, DynamicParamKind.Variable, allowedTypes);

        /// <summary>Constant-or-variable row with C/V toggle button.</summary>
        public static DynamicParamDescriptor Toggle(string title, params Type[] allowedTypes) =>
            Base(title, DynamicParamKind.Toggle, allowedTypes);

        /// <summary>Constant value row.</summary>
        public static DynamicParamDescriptor Constant(string title, Type type) =>
            Base(title, DynamicParamKind.Constant, new[] { type });

        /// <summary>Enum operation dropdown, optionally filtered per resolved param type.</summary>
        public static DynamicParamDescriptor Operation<TOp>(string title, Func<Type, int[]> filter = null)
            where TOp : Enum
        {
            DynamicParamDescriptor d = Base(title, DynamicParamKind.Operation, null);
            d.operationEnumType = typeof(TOp);
            d.getAvailableOpIndices = filter;
            return d;
        }

        /// <summary>Three-way C/V/SO row (constant / variable / ScriptableObject field).</summary>
        public static DynamicParamDescriptor SOConstant(string title, params Type[] allowedTypes) =>
            Base(title, DynamicParamKind.ScriptableObjectConstant, allowedTypes);

        /// <summary>Hidden variable, auto-bound to a blackboard variable by name.</summary>
        public static DynamicParamDescriptor Hidden(string autoVariableName)
        {
            DynamicParamDescriptor d = Base(autoVariableName, DynamicParamKind.Variable, null);
            d.isHidden = true;
            d.autoVariableName = autoVariableName;
            return d;
        }

        /// <summary>Assigns sequential indices (the field is positional metadata only).</summary>
        public static DynamicParamDescriptor[] Build(params DynamicParamDescriptor[] descriptors)
        {
            for (int i = 0; i < descriptors.Length; i++)
                descriptors[i].index = i;
            return descriptors;
        }

        private static DynamicParamDescriptor Base(string title, DynamicParamKind kind, Type[] allowedTypes) =>
            new DynamicParamDescriptor
            {
                titleLabel = title,
                label = title,
                kind = kind,
                allowedTypes = allowedTypes,
            };
    }

    /// <summary>Fluent modifiers. Structs are copied — each returns the modified copy.</summary>
    public static class DynamicParamDescriptorExtensions
    {
        /// <summary>Mirror the resolved type of another param index.</summary>
        public static DynamicParamDescriptor SyncTypeFrom(this DynamicParamDescriptor d, int sourceIndex)
        {
            d.syncTypeFromIndex = sourceIndex;
            return d;
        }

        /// <summary>Mirror the element type of an array-typed source param (output is scalar).</summary>
        public static DynamicParamDescriptor SyncElementTypeFrom(this DynamicParamDescriptor d, int sourceIndex)
        {
            d.syncTypeFromIndex = sourceIndex;
            d.syncElementType = true;
            return d;
        }

        /// <summary>Row only visible while the referenced entry's boolValue is true.</summary>
        public static DynamicParamDescriptor VisibleWhen(this DynamicParamDescriptor d, int entryIndex)
        {
            d.visibilityDependsOnIndex = entryIndex;
            return d;
        }
    }
}
