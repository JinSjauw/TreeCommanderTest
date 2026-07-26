using System;
using System.Collections.Generic;
using System.Reflection;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Projects [SharedVar]/[SharedArray]/plain public fields of a NodeMethod
    /// subclass into DynamicParamDescriptors. Cached per type; invalidated when
    /// the method registry rebuilds. Editor-side use via NodeParamSchema —
    /// NodeMethod.GetDynamicParamDescriptors() intentionally stays null by
    /// default so runtime ParameterCount behavior is unchanged.
    /// </summary>
    public static class ParamSchemaReflection
    {
        private static readonly Dictionary<Type, DynamicParamDescriptor[]> cache =
            new Dictionary<Type, DynamicParamDescriptor[]>();

        public static DynamicParamDescriptor[] GetDescriptors(Type nodeMethodType)
        {
            if (cache.TryGetValue(nodeMethodType, out DynamicParamDescriptor[] d)) return d;
            d = Build(nodeMethodType);
            cache[nodeMethodType] = d;
            return d;
        }

        public static void Invalidate() => cache.Clear();

        private static DynamicParamDescriptor[] Build(Type type)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            if (fields.Length == 0) return null;

            var list = new List<DynamicParamDescriptor>(fields.Length);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                SharedVarAttribute sv = f.GetCustomAttribute<SharedVarAttribute>();
                bool isArray = f.GetCustomAttribute<SharedArrayAttribute>() != null;
                string title = char.ToUpper(f.Name[0]) + f.Name.Substring(1);

                DynamicParamDescriptor d;
                if (sv != null && sv.IsSOConstant)
                    d = Params.SOConstant(title, f.FieldType);
                else if (sv != null && sv.IsToggleVariable)
                    d = Params.Toggle(title, f.FieldType);
                else if (sv != null || isArray)
                    d = Params.Variable(title, f.FieldType);
                else
                    d = Params.Constant(title, f.FieldType);

                d.index = i;
                d.fieldName = f.Name;
                d.isArray = isArray;
                d.isHidden = sv?.IsHidden ?? false;
                d.autoVariableName = sv?.AutoVariableName;
                if (sv?.IsRoleDropdown == true) d.constantEditor = ConstantEditorHint.RoleDropdown;
                if (sv?.IsOrderDropdown == true) d.constantEditor = ConstantEditorHint.OrderDropdown;

                // Port of the former hardcoded editor rule: a field literally
                // named "customTickValue" is only visible when the preceding
                // bool entry is true.
                if (f.Name == "customTickValue" && i > 0 && fields[i - 1].FieldType == typeof(bool))
                    d.visibilityDependsOnIndex = i - 1;

                list.Add(d);
            }
            return list.ToArray();
        }
    }
}
