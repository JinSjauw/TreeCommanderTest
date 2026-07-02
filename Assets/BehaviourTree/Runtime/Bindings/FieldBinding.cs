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

        /// <summary>True if CompileAccessors ran successfully and both delegates are ready.</summary>
        public bool IsCompiled => readDelegate != null && writeDelegate != null;
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

                MethodInfo getMethod = typeof(IBlackBoardAccess).GetMethod("Get").MakeGenericMethod(fieldType);
                MethodCallExpression getCall = Expression.Call(bbParam, getMethod, slotConst);
                BinaryExpression readBody = Expression.Assign(fieldExpr, getCall);
                readDelegate = Expression.Lambda<Action<NodeMethod, IBlackBoardAccess>>(readBody, instParam, bbParam).Compile();

                if (isOutput)
                {
                    MethodInfo setMethod = typeof(IBlackBoardAccess).GetMethod("Set").MakeGenericMethod(fieldType);
                    MethodCallExpression setCall = Expression.Call(bbParam, setMethod, slotConst, fieldExpr);
                    writeDelegate = Expression.Lambda<Action<NodeMethod, IBlackBoardAccess>>(setCall, instParam, bbParam).Compile();
                }
            }
            catch
            {

            }
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
