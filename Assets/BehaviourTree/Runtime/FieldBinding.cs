using System;
using System.Linq.Expressions;
using System.Reflection;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Core
{
    // ═══════════════════════════════════════════════════════════════════
    // FieldBinding
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Describes one serializable field on a NodeMethod subclass.
    /// Handles reading/writing to the blackboard for [SharedVar] fields.
    /// Created by MethodRegistry during type scanning; populated by
    /// NodeMethod.DeserializeFields during tree init.
    /// </summary>
    public sealed class FieldBinding
    {
        public FieldInfo fieldInfo;

        /// <summary>
        /// Assembly-qualified type name of this field.
        /// Resolved from fieldInfo.FieldType when created by MethodRegistry.
        /// </summary>
        public string fieldTypeName;

        /// <summary>-1 = constant (value set directly on field); >=0 = BB slot index</summary>
        public int bbSlotIndex = -1;

        /// <summary>If true, the field value is written back to BB after Execute.</summary>
        public bool isOutput = true;

        /// <summary>
        /// When true, ResolveInputsGeneric / WriteOutputsGeneric skip this field.
        /// The field still receives a bbSlotIndex during bake (GetSlotByName works).
        /// </summary>
        public bool skipAutoResolve = false;

        /// <summary>Resolved System.Type for this binding (lazy).</summary>
        public Type ResolvedType =>
            resolvedType ?? (resolvedType = ResolveType());
        private Type resolvedType;

        private Type ResolveType()
        {
            if (!string.IsNullOrEmpty(fieldTypeName))
                return Type.GetType(fieldTypeName);
            if (fieldInfo != null)
                return fieldInfo.FieldType;
            return null;
        }

        /// <summary>Compiled delegate for zero-allocation field read (null → fall back to reflection).</summary>
        private Action<NodeMethod, IBlackBoardAccess> readDelegate;

        /// <summary>Compiled delegate for zero-allocation field write (null → fall back to reflection).</summary>
        private Action<NodeMethod, IBlackBoardAccess> writeDelegate;

        /// <summary>True if CompileAccessors ran successfully and both delegates are ready.</summary>
        public bool IsCompiled => readDelegate != null && writeDelegate != null;

        /// <summary>
        /// TEMPORARY: Attempts to compile typed read/write delegates via Expression trees.
        /// Called once during tree init, after <see cref="bbSlotIndex"/> is assigned.
        /// Falls back silently — existing reflection path handles unsupported platforms.
        /// This (and the similar code in TrackedBinding) gets deleted when we move to DOTS
        /// with typed NativeArray&lt;T&gt; storage — no type-erased object[] to bridge across.
        /// </summary>
        public void CompileAccessors(Type declaringType)
        {
            if (bbSlotIndex < 0 || fieldInfo == null || skipAutoResolve) return;
            Type fieldType = fieldInfo.FieldType;
            if (fieldType == null) return;

            try
            {
                ParameterExpression instParam = Expression.Parameter(typeof(NodeMethod), "inst");
                ParameterExpression bbParam = Expression.Parameter(typeof(IBlackBoardAccess), "bb");
                UnaryExpression castInst = Expression.Convert(instParam, declaringType);
                MemberExpression fieldExpr = Expression.Field(castInst, fieldInfo);
                ConstantExpression slotConst = Expression.Constant(bbSlotIndex);

                // Read: ((ConcreteType)inst).field = bb.Get<T>(bbSlotIndex)
                MethodInfo getMethod = typeof(IBlackBoardAccess).GetMethod("Get")
                    .MakeGenericMethod(fieldType);
                MethodCallExpression getCall = Expression.Call(bbParam, getMethod, slotConst);
                BinaryExpression readBody = Expression.Assign(fieldExpr, getCall);
                readDelegate = Expression.Lambda<Action<NodeMethod, IBlackBoardAccess>>(
                    readBody, instParam, bbParam).Compile();

                // Write: bb.Set<T>(bbSlotIndex, ((ConcreteType)inst).field)
                // Skip when isOutput is false (toggle variables) — the non-compiled path in
                // WriteToBBGeneric checks isOutput and correctly skips the write.
                if (isOutput)
                {
                    MethodInfo setMethod = typeof(IBlackBoardAccess).GetMethod("Set")
                        .MakeGenericMethod(fieldType);
                    MethodCallExpression setCall = Expression.Call(bbParam, setMethod, slotConst, fieldExpr);
                    writeDelegate = Expression.Lambda<Action<NodeMethod, IBlackBoardAccess>>(
                        setCall, instParam, bbParam).Compile();
                }
            }
            catch
            {
                // AOT / IL2CPP — delegates remain null, reflection fallback handles it.
                // TEMPORARY: this entire try/catch goes away with DOTS typed storage.
            }
        }

        // ── Generic read/write (uses GetBoxed/SetBoxed — with type coercion) ──

        public void ReadFromBBGeneric(NodeMethod instance, IBlackBoardAccess bb)
        {
            if (readDelegate != null)
            {
                readDelegate(instance, bb);
                return;
            }

            if (bbSlotIndex < 0) return;
            object value = bb.GetBoxed(bbSlotIndex);
            if (value != null)
            {
                Type fieldType = fieldInfo.FieldType;
                Type valueType = value.GetType();
                if (fieldType != valueType && !fieldType.IsAssignableFrom(valueType))
                {
                    try { value = Convert.ChangeType(value, fieldType); }
                    catch
                    {
#if UNITY_EDITOR
                        Debug.LogWarning($"[FieldBinding] Cannot convert BB value '{value}' ({valueType.Name}) to field type '{fieldType.Name}' for field '{fieldInfo.Name}'");
#endif
                        return;
                    }
                }
            }
            fieldInfo.SetValue(instance, value);
        }

        public void WriteToBBGeneric(NodeMethod instance, IBlackBoardAccess bb)
        {
            if (writeDelegate != null)
            {
                writeDelegate(instance, bb);
                return;
            }

            if (!isOutput || bbSlotIndex < 0) return;
            object value = fieldInfo.GetValue(instance);
            if (value != null)
            {
                Type bbType = ResolvedType;
                if (bbType != null)
                {
                    Type valueType = value.GetType();
                    if (bbType != valueType && !bbType.IsAssignableFrom(valueType))
                    {
                        try { value = Convert.ChangeType(value, bbType); }
                        catch
                        {
#if UNITY_EDITOR
                            Debug.LogWarning($"[FieldBinding] Cannot convert field value '{value}' ({valueType.Name}) to BB type '{bbType.Name}' for field '{fieldInfo.Name}'");
#endif
                            return;
                        }
                    }
                }
            }
            bb.SetBoxed(bbSlotIndex, value);
        }
    }
}
