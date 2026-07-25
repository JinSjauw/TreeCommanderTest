using System;
using System.Reflection;
using UnityEngine;

namespace BehaviourTree.Core
{
    public abstract class NodeMethod
    {
        internal FieldBinding[] bindings;
        internal object[] boxedConstants;
        private IBlackBoardAccess bbAccess;
        private bool bbInitialized;

        protected IBlackBoardAccess BB => bbAccess;

        protected int GetSlotByName(string fieldName)
        {
            if (bindings == null) return -1;
            foreach (FieldBinding binding in bindings)
            {
                if (binding.fieldInfo != null && binding.fieldInfo.Name == fieldName)
                    return binding.bbSlotIndex;
            }
            return -1;
        }

        /// <summary>
        /// Resolves a component of type <typeparamref name="T"/> from the BlackBoard's GameObject.
        /// Tries GetComponent first, falls back to GetComponentInChildren.
        /// Returns null if BB is not a BlackBoard MonoBehaviour or the component is not found.
        /// </summary>
        protected T GetComponentFromBB<T>() where T : Component
        {
            BlackBoard bb = BB as BlackBoard;
            if (bb == null) return null;
            T comp = bb.GetComponent<T>();
            if (comp == null) comp = bb.GetComponentInChildren<T>();
            return comp;
        }

        public virtual int ParameterCount => GetDynamicParamDescriptors()?.Length ?? 0;

        public virtual DynamicParamDescriptor[] GetDynamicParamDescriptors() => null;

        public virtual void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants) { }

        /// <summary>
        /// Stable string identifier for this method. Defaults to the class name.
        /// Override or use [NodeMethod("name")] to customize.
        /// </summary>
        public virtual string MethodName
        {
            get
            {
                Type type = GetType();
                NodeMethodAttribute attr = type.GetCustomAttribute<NodeMethodAttribute>();
                return attr != null ? attr.methodName : type.Name;
            }
        }

        public void DeserializeFields(ReadOnlySpan<FieldData> fields, FieldBinding[] bindings, object[] boxedConstants)
        {
            // Clone bindings to avoid mutating the shared cached array from MethodRegistry
            if (bindings != null)
            {
                this.bindings = new FieldBinding[bindings.Length];
                for (int i = 0; i < bindings.Length; i++)
                {
                    FieldBinding source = bindings[i];
                    if (source != null)
                    {
                        this.bindings[i] = new FieldBinding
                        {
                            fieldInfo = source.fieldInfo,
                            fieldTypeName = source.fieldTypeName,
                            isOutput = source.isOutput,
                            bbSlotIndex = source.bbSlotIndex,
                            skipAutoResolve = source.skipAutoResolve
                        };
                    }
                }
            }
            else
            {
                this.bindings = null;
            }

            this.boxedConstants = boxedConstants;

            int bindingCount = this.bindings != null ? this.bindings.Length : 0;
            int fieldIndex = 0;
            for (int i = 0; i < bindingCount; i++)
            {
                if (fieldIndex >= fields.Length) break;

                FieldBinding binding = this.bindings[i];
                if (binding == null) continue;

                ref readonly FieldData fd = ref fields[fieldIndex];
                bool isVariableField = fd.IsVariable;

                if (fd.IsConstant)
                {
                    Type fieldType = binding.fieldInfo.FieldType;
                    if (IsPackedConstantType(fieldType))
                    {
                        object constValue = ReadConstant(fd, fieldType, boxedConstants);
                        binding.fieldInfo.SetValue(this, constValue);
                    }
                    binding.bbSlotIndex = -1;
                }
                else if (fd.IsBoxedConstant)
                {
                    Type fieldType = binding.fieldInfo.FieldType;
                    object constValue = fd.GetBoxedConstant<object>(boxedConstants);
                    if (constValue != null && fieldType.IsAssignableFrom(constValue.GetType()))
                        binding.fieldInfo.SetValue(this, constValue);
                    binding.bbSlotIndex = -1;
                }
                else
                {
                    binding.bbSlotIndex = fd.value;
                    binding.BindAccessors(GetType());
                }

                fieldIndex++;

                if (isVariableField && fieldIndex < fields.Length && fields[fieldIndex].IsStrideMarker)
                    fieldIndex++;
            }
        }

        public void ResolveInputsGeneric(IBlackBoardAccess bb)
        {
            bbAccess = bb;
            if (!bbInitialized)
            {
                bbInitialized = true;
                OnInitialize();
            }
            FieldBinding[] b = bindings;
            if (b == null) return;
            for (int i = 0; i < b.Length; i++)
            {
                if (b[i] != null && b[i].bbSlotIndex >= 0 && !b[i].skipAutoResolve)
                {
                    b[i].ReadFromBBGeneric(this, bb);
                }
            }
        }

        public void WriteOutputsGeneric(IBlackBoardAccess bb)
        {
            FieldBinding[] b = bindings;
            if (b == null) return;
            for (int i = 0; i < b.Length; i++)
            {
                if (b[i] != null && !b[i].skipAutoResolve)
                    b[i].WriteToBBGeneric(this, bb);
            }
        }

        /// <summary>
        /// Reads a variable slot from the baked FieldData span and advances the index.
        /// Handles the optional trailing stride marker that TreeBaker emits for variables
        /// with stride > 1. Returns -1 if no variable is found at the current position.
        /// </summary>
        protected static int ReadVariableSlot(ReadOnlySpan<FieldData> fields, ref int fieldIndex)
        {
            if (fieldIndex >= fields.Length || !fields[fieldIndex].IsVariable)
                return -1;

            int slot = fields[fieldIndex].value;
            fieldIndex++;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsStrideMarker)
                fieldIndex++;

            return slot;
        }

        /// <summary>
        /// Called once when BB becomes available for the first time (first tick).
        /// Override to resolve components, cache references, or perform one-time setup
        /// that requires access to the blackboard / agent GameObject.
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// Called by ConditionalAbort before resetting this node's state to INACTIVE.
        /// </summary>
        public virtual void OnAbort(IBlackBoardAccess bbAccess) { }

        /// <summary>
        /// Returns true when the given type can be stored in packed constant form (mode=0).
        /// Only int, float, bool, and enums fit in FieldData's 4-byte value field.
        /// Everything else (Vector3, GameObject, etc.) must use boxed constants (mode=2).
        /// </summary>
        private static bool IsPackedConstantType(Type fieldType)
        {
            if (fieldType == null) return false;
            return fieldType == typeof(int)
                || fieldType == typeof(uint)
                || fieldType == typeof(float)
                || fieldType == typeof(bool)
                || fieldType.IsEnum;
        }

        private static object ReadConstant(FieldData fd, Type fieldType, object[] boxedConstants)
        {
            if (fd.IsBoxedConstant && boxedConstants != null)
            {
                int index = fd.value;
                if (index >= 0 && index < boxedConstants.Length)
                    return boxedConstants[index];
                return null;
            }

            if (fieldType == typeof(float))
                return fd.GetFloat();
            if (fieldType == typeof(bool))
                return fd.GetBool();
            return fd.GetInt(); // int, enum, and fallback
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Action / Condition / Decorator
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Leaf node that may return RUNNING. Receives a read-only copy of TickContext.</summary>
    public abstract class ActionMethod : NodeMethod
    {
        public abstract NodeState Execute(Runtime.TickContext ctx);
    }

    /// <summary>Leaf node that returns SUCCESS or FAILURE immediately. Receives a read-only copy of TickContext.</summary>
    public abstract class ConditionMethod : NodeMethod
    {
        public abstract NodeState Execute(Runtime.TickContext ctx);
    }

    /// <summary>Wraps a child node and transforms its result.</summary>
    public abstract class DecoratorMethod : NodeMethod
    {
        public abstract NodeState Execute(NodeState childResult);
    }
}
