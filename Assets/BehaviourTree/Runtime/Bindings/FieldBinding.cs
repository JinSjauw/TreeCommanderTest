using System;
using System.Linq.Expressions;
using System.Reflection;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Core
{
    public sealed class FieldBinding
    {
        public FieldInfo fieldInfo;

        /// <summary>
        /// Assembly-qualified type name of this field.
        /// Resolved from fieldInfo.FieldType when created by MethodRegistry.
        /// </summary>
        public string fieldTypeName;

        /// <summary> -1 = constant (value set directly on field); >=0 = BB slot index</summary>
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

        /// <summary>True once a read delegate exists (write delegate is output-only).</summary>
        public bool IsCompiled => readDelegate != null;

        /// <summary>
        /// Production bind path: use the build-time-generated accessor when one is
        /// registered for (declaringType, field) with a matching field type;
        /// otherwise fall back to Expression.Compile (editor iteration).
        /// </summary>
        public void BindAccessors(Type declaringType)
        {
            if (bbSlotIndex < 0 || fieldInfo == null || skipAutoResolve) return;

            if (GeneratedAccessorRegistry.TryGet(declaringType, fieldInfo,
                    out GeneratedAccessorRegistry.ReadAccessor read,
                    out GeneratedAccessorRegistry.WriteAccessor write))
            {
                int slot = bbSlotIndex;
                readDelegate = (m, bb) => read(m, bb, slot);
                if (isOutput && write != null)
                    writeDelegate = (m, bb) => write(m, bb, slot);
                return;
            }

#if UNITY_EDITOR
            UnityEngine.Debug.LogWarning(
                $"[FieldBinding] No generated accessor for {declaringType.Name}.{fieldInfo.Name} " +
                "(or stale field type) — falling back to Expression.Compile. " +
                "Regenerate via 'Behaviour Tree/Generate Binding Accessors'.");
#endif
            CompileAccessors(declaringType);
        }

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

                Expression readValue = BuildReadExpression(bbParam, slotConst, fieldType);
                readDelegate = Expression.Lambda<Action<NodeMethod, IBlackBoardAccess>>(
                    Expression.Assign(fieldExpr, readValue), instParam, bbParam).Compile();

                if (isOutput)
                {
                    MethodCallExpression writeCall = BuildWriteCall(bbParam, slotConst, fieldExpr, fieldType);
                    writeDelegate = Expression.Lambda<Action<NodeMethod, IBlackBoardAccess>>(
                        writeCall, instParam, bbParam).Compile();
                }
            }
            catch
            {

            }
        }

        /// <summary>
        /// Builds the BB-read expression for a field: typed accessor when the field
        /// type maps to one (allocation-free), generic Get&lt;T&gt; otherwise.
        /// Enums read as int and are cast back to the enum type.
        /// </summary>
        private static Expression BuildReadExpression(ParameterExpression bbParam, ConstantExpression slotConst, Type fieldType)
        {
            MethodInfo typedGetter = TypedAccessorMap.GetGetter(fieldType);
            if (typedGetter != null)
            {
                Expression call = Expression.Call(bbParam, typedGetter, slotConst);
                return fieldType.IsEnum ? Expression.Convert(call, fieldType) : call;
            }

            MethodInfo genericGet = typeof(IBlackBoardAccess).GetMethod("Get").MakeGenericMethod(fieldType);
            return Expression.Call(bbParam, genericGet, slotConst);
        }

        private static MethodCallExpression BuildWriteCall(ParameterExpression bbParam, ConstantExpression slotConst, MemberExpression fieldExpr, Type fieldType)
        {
            MethodInfo typedSetter = TypedAccessorMap.GetSetter(fieldType);
            if (typedSetter != null)
            {
                Expression valueExpr = fieldType.IsEnum
                    ? Expression.Convert(fieldExpr, typeof(int))
                    : (Expression)fieldExpr;
                return Expression.Call(bbParam, typedSetter, slotConst, valueExpr);
            }

            MethodInfo genericSet = typeof(IBlackBoardAccess).GetMethod("Set").MakeGenericMethod(fieldType);
            return Expression.Call(bbParam, genericSet, slotConst, fieldExpr);
        }

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
