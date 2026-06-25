using System;
using System.Reflection;
using UnityEngine;

namespace BehaviourTree.Core
{
    // ═══════════════════════════════════════════════════════════════════
    // NodeMethod — abstract base
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Base class for all leaf-node methods. Inherit from ActionMethod,
    /// ConditionMethod, or DecoratorMethod to auto-register a new method.
    /// Public fields define the inspector schema. Fields with [SharedVar]
    /// are automatically resolved from the blackboard before Execute() and
    /// written back after.
    /// </summary>
    public abstract class NodeMethod
    {
        internal FieldBinding[] bindings;
        internal object[] boxedConstants;
        private IBlackBoardAccess bbAccess;
        private bool bbInitialized;

        /// <summary>Blackboard accessor. Available during Execute().</summary>
        protected IBlackBoardAccess BB => bbAccess;

        /// <summary>
        /// Looks up the raw BB slot index for a [SharedVar] field by its C# field name.
        /// Returns -1 if the field is not found or has no BB binding.
        /// Preferred over positional bindings[N].bbSlotIndex which breaks on reorder.
        /// </summary>
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

        /// <summary>
        /// Number of parameter slots this method expects. Default 0 means
        /// "determine from [SharedVar] field count". Override to a positive
        /// number for dynamic-type nodes that receive FieldData directly
        /// via <see cref="DeserializeParameters"/> without C# [SharedVar] fields.
        /// When <see cref="GetDynamicParamDescriptors"/> returns non-null, this value
        /// is derived from descriptors.Length automatically.
        /// </summary>
        public virtual int ParameterCount => GetDynamicParamDescriptors()?.Length ?? 0;

        /// <summary>
        /// Returns the parameter layout for dynamic-type nodes. Null means
        /// "use [SharedVar] C# field binding" (legacy path).
        /// Non-null means the editor renders a generic inspector from these descriptors
        /// and the baker knows this node uses <see cref="DeserializeParameters"/>.
        /// Each descriptor maps to one FieldData entry by position (descriptors[i] → entry i).
        /// </summary>
        public virtual DynamicParamDescriptor[] GetDynamicParamDescriptors() => null;

        /// <summary>
        /// Called once during tree initialization for nodes with dynamic parameters
        /// (<see cref="GetDynamicParamDescriptors"/> returns non-null).
        /// Receives FieldData and type names directly — the node stores slot indices / constant values
        /// in its own fields. No FieldBindings are created.
        /// Fields with mode=1 contain slot offsets; mode=0 contain packed constants (use fieldTypeNames
        /// to interpret float bit patterns); mode=2 contain boxed constants.
        /// </summary>
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

        /// <summary>
        /// Called once during tree initialization. Copies baked constants directly
        /// onto instance fields and stores BB slot indices for [SharedVar] fields.
        /// </summary>
        public void DeserializeFields(ReadOnlySpan<FieldData> fields, FieldBinding[] bindings)
        {
            DeserializeFields(fields, bindings, null);
        }

        /// <summary>
        /// Called once during tree initialization. Accepts optional boxedConstants
        /// array for constants larger than 4 bytes (Vector3, Color, custom types).
        /// </summary>
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
                    binding.CompileAccessors(GetType());
                }

                fieldIndex++;

                // TreeBaker injects a stride marker after variable fields with stride > 1.
                // Skip it so the next binding reads the correct FieldData entry.
                if (isVariableField && fieldIndex < fields.Length && fields[fieldIndex].IsStrideMarker)
                    fieldIndex++;
            }
        }

        /// <summary>
        /// Called by the framework before each Execute(). Copies BB values into
        /// [SharedVar] instance fields using GetBoxed/SetBoxed (supports any type).
        /// </summary>
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

        /// <summary>
        /// Called by the framework after each Execute(). Copies [SharedVar]
        /// instance fields back to the BB using GetBoxed/SetBoxed (supports any type).
        /// </summary>
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
        /// Called by AbortSubtree before resetting this node's state to INACTIVE.
        /// Override to clean up blackboard values or other shared state when a branch
        /// is aborted. Only use the provided bbAccess — instance fields are shared
        /// across agents and not safe to use here.
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
