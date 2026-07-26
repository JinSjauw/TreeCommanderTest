using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    public static class MethodRegistry
    {
        /// <summary>Fires after the registry finishes scanning assemblies. Subscribe to invalidate caches.</summary>
        public static event Action OnRegistryRebuilt;

        private static readonly Dictionary<string, Type> methodTypeMap = new();
        private static readonly Dictionary<Type, FieldBinding[]> bindingCache = new();

        static MethodRegistry()
        {
            Build();
        }

        public static void Rebuild()
        {
            methodTypeMap.Clear();
            bindingCache.Clear();
            Build();
        }

        private static void Build()
        {
            Debug.Log("[MethodRegistry] Registering class-based methods...");

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract) continue;
                    if (!typeof(NodeMethod).IsAssignableFrom(type)) continue;

                    try
                    {
                        // Resolve method name from type metadata — no instantiation needed.
                        NodeMethodAttribute attr = type.GetCustomAttribute<NodeMethodAttribute>();
                        string methodName = attr != null ? attr.methodName : type.Name;

                        if (string.IsNullOrWhiteSpace(methodName))
                        {
                            Debug.LogError($"[MethodRegistry] '{type.Name}' has a null/empty method name. Skipping.");
                            continue;
                        }

                        if (methodTypeMap.ContainsKey(methodName))
                        {
                            Debug.LogWarning($"[MethodRegistry] Duplicate method name '{methodName}': {type.Name} conflicts with {methodTypeMap[methodName].Name}. Skipping.");
                            continue;
                        }

                        methodTypeMap[methodName] = type;
                        bindingCache[type] = CreateBindings(type);

                        if (bindingCache[type].Length > 0)
                        {
                            NodeMethod temp = (NodeMethod)Activator.CreateInstance(type);
                            if (temp.ParameterCount > 0)
                            {
                                Debug.LogError(
                                    $"[MethodRegistry] Node method '{methodName}' ({type.Name}) " +
                                    "has both [SharedVar] fields and DynamicParamDescriptor[] — " +
                                    "these are mutually exclusive. Remove one or the other.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MethodRegistry] Failed to register '{type.Name}': {ex.Message}");
                    }
                }
            }

            OnRegistryRebuilt?.Invoke();
        }

        // ── Public API ─────────────────────────────────────────────

        public static IReadOnlyCollection<string> GetMethodNames() => methodTypeMap.Keys;

        public static Type GetMethodType(string methodName)
        {
            methodTypeMap.TryGetValue(methodName, out Type type);
            return type;
        }

        public static FieldBinding[] GetBindings(Type methodType)
        {
            bindingCache.TryGetValue(methodType, out FieldBinding[] bindings);
            return bindings;
        }

        public static FieldBinding[] GetBindings(string methodName)
        {
            Type type = GetMethodType(methodName);
            return type != null ? GetBindings(type) : null;
        }

        public static NodeMethod CreateInstance(string methodName)
        {
            if (string.IsNullOrEmpty(methodName)) return null;
            Type type = GetMethodType(methodName);
            if (type == null)
            {
                Debug.LogError($"[MethodRegistry] Unknown method: '{methodName}'");
                return null;
            }
            return (NodeMethod)Activator.CreateInstance(type);
        }

        public static bool IsClassMethod(string methodName) => methodTypeMap.ContainsKey(methodName);

        /// <summary>
        /// Checks whether a method is compatible with the given tree type
        /// </summary>
        public static bool IsMethodAllowed(string methodName, AllowedTreeType treeType)
        {
            Type type = GetMethodType(methodName);
            if (type == null) return false;
            NodeMethodAttribute attr = type.GetCustomAttribute<NodeMethodAttribute>();
            if (attr == null) return true;
            return attr.allowedTreeType == AllowedTreeType.Any || attr.allowedTreeType == treeType;
        }

        /// <summary>
        /// Returns true if the method is restricted to Commander trees only
        /// </summary>
        public static bool IsCommanderOnly(string methodName)
        {
            Type type = GetMethodType(methodName);
            if (type == null) return false;
            NodeMethodAttribute attr = type.GetCustomAttribute<NodeMethodAttribute>();
            return attr != null && attr.allowedTreeType == AllowedTreeType.Commander;
        }

        public static BehaviourNodeType GetCategory(Type methodType)
        {
            if (typeof(ActionMethod).IsAssignableFrom(methodType))    return BehaviourNodeType.ACTION;
            if (typeof(ConditionMethod).IsAssignableFrom(methodType)) return BehaviourNodeType.CONDITION;
            if (typeof(DecoratorMethod).IsAssignableFrom(methodType)) return BehaviourNodeType.DECORATOR;
            if (typeof(CompositeMethod).IsAssignableFrom(methodType)) return BehaviourNodeType.COMPOSITE;
            return BehaviourNodeType.ACTION;
        }

        public static BehaviourNodeType GetCategory(string methodName)
        {
            Type type = GetMethodType(methodName);
            return type != null ? GetCategory(type) : BehaviourNodeType.ACTION;
        }

        // ── Internal ───────────────────────────────────────────────

        /// <summary>
        /// Builds the binding descriptors for all public instance fields of a method
        /// type. Used by the registry cache, the runtime deserializer, and the
        /// editor-side accessor generator (must stay the single source of field selection).
        /// </summary>
        public static FieldBinding[] CreateBindings(Type methodType)
        {
            FieldInfo[] fields = methodType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            List<FieldBinding> list = new List<FieldBinding>(fields.Length);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                SharedVarAttribute sharedVar = field.GetCustomAttribute<SharedVarAttribute>();
                bool isSharedVar = sharedVar != null;
                bool isOutput = isSharedVar;
                if (isSharedVar && (sharedVar.IsToggleVariable || sharedVar.IsSOConstant))
                    isOutput = false;

                list.Add(new FieldBinding
                {
                    fieldInfo = field,
                    fieldTypeName = field.FieldType.AssemblyQualifiedName,
                    isOutput = isOutput,
                    bbSlotIndex = -1,
                    skipAutoResolve = isSharedVar && sharedVar.SkipAutoResolve
                });
            }

            return list.ToArray();
        }
    }
}
