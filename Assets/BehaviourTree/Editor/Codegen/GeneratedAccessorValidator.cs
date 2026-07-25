using System;
using System.Text;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.EditorTools.Codegen
{
    /// <summary>
    /// Reports NodeMethod fields that have no generated accessor (or a stale one).
    /// Staleness happens when a field type changes without regenerating — the runtime
    /// silently falls back to Expression.Compile, which breaks AOT builds.
    /// </summary>
    public static class GeneratedAccessorValidator
    {
        [MenuItem("Behaviour Tree/Validate Binding Accessors")]
        public static void Validate()
        {
            // Ensure the generated table is populated outside playmode.
            // The generated file lives in Assembly-CSharp (no asmdef in the folder).
            Type generated = Type.GetType("BehaviourTree.Generated.GeneratedBindingAccessors, Assembly-CSharp");
            generated?.GetMethod("RegisterAll")?.Invoke(null, null);

            int missing = 0;
            int ok = 0;
            var report = new StringBuilder();

            foreach (string methodName in MethodRegistry.GetMethodNames())
            {
                Type type = MethodRegistry.GetMethodType(methodName);
                if (type == null) continue;

                foreach (FieldBinding b in MethodRegistry.CreateBindings(type))
                {
                    if (b?.fieldInfo == null || b.skipAutoResolve) continue;

                    if (GeneratedAccessorRegistry.TryGet(type, b.fieldInfo, out _, out _))
                    {
                        ok++;
                    }
                    else
                    {
                        missing++;
                        report.AppendLine($"  {type.FullName}.{b.fieldInfo.Name} ({b.fieldInfo.FieldType.Name})");
                    }
                }
            }

            if (missing == 0)
            {
                Debug.Log($"[GeneratedAccessorValidator] All {ok} bindable fields have generated accessors.");
            }
            else
            {
                Debug.LogWarning(
                    $"[GeneratedAccessorValidator] {missing} field(s) missing/stale (of {ok + missing}):\n{report}" +
                    "Run 'Behaviour Tree/Generate Binding Accessors'.");
            }
        }
    }
}
