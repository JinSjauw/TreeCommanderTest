using System;
using BehaviourTree.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Unified operation enum for CompareVariable. The editor shows only
    /// operations applicable to the selected variable type.
    /// Values are stable for serialization — do not reorder.
    /// </summary>
    public enum VariableCompareOp
    {
        Equal = 0,
        NotEqual = 1,
        Less = 2,
        LessOrEqual = 3,
        Greater = 4,
        GreaterOrEqual = 5,
        MagnitudeLess = 6,
        MagnitudeLessOrEqual = 7,
        MagnitudeGreater = 8,
        MagnitudeGreaterOrEqual = 9,
    }

    /// <summary>
    /// Writes a value (constant or from another variable) into a blackboard variable.
    /// Supports all types. Use inside ForEachAgent to write per-agent squad data.
    /// Field entries:
    ///   0: "target" — isVariable=true, the variable to write to
    ///   1: "value"  — constant (mode=0/2) or variable (mode=1) source
    /// </summary>
    [NodeMethod("SetVariable")]
    public sealed class SetVariable : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor { titleLabel = "Target", label = "Target", kind = DynamicParamKind.Variable, index = 0 },
            new DynamicParamDescriptor { titleLabel = "Value",  label = "Value",  kind = DynamicParamKind.Toggle,   index = 1, syncTypeFromIndex = 0 },
        };

        private int targetSlot = -1;
        private int valueSourceSlot = -1;
        private object constantValue;
        private bool hasConstantValue;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            int fieldIndex = 0;

            targetSlot = VariableMethodHelper.ReadVariableSlot(fields, ref fieldIndex);

            if (fieldIndex < fields.Length)
            {
                if (fields[fieldIndex].IsVariable)
                {
                    valueSourceSlot = VariableMethodHelper.ReadVariableSlot(fields, ref fieldIndex);
                }
                else
                {
                    hasConstantValue = true;
                    constantValue = VariableMethodHelper.ReadConstant(fields, ref fieldIndex, fieldTypeNames, boxedConstants);
                }
            }
        }

        public override NodeState Execute()
        {
            if (targetSlot < 0) return NodeState.FAILURE;

            object value;
            if (valueSourceSlot >= 0)
                value = BB.GetBoxed(valueSourceSlot);
            else if (hasConstantValue)
                value = constantValue;
            else
                return NodeState.FAILURE;

            // Debug.Log($"[SetVariable] slot={targetSlot} value={value ?? "null"} (type={value?.GetType().Name ?? "null"})");
            BB.SetBoxed(targetSlot, value);
            return NodeState.SUCCESS;
        }
    }

    /// <summary>
    /// Resets a blackboard variable to its type default (null/zero/Vector3.zero).
    /// Field entries:
    ///   0: "target" — isVariable=true, the variable to clear
    /// </summary>
    [NodeMethod("ClearVariable")]
    public sealed class ClearVariable : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor { titleLabel = "Target", label = "Target", kind = DynamicParamKind.Variable, index = 0 },
        };

        private int targetSlot = -1;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            if (fields.Length >= 1 && fields[0].IsVariable)
                targetSlot = fields[0].value;
        }

        public override NodeState Execute()
        {
            if (targetSlot < 0) return NodeState.FAILURE;

            // Write null — managed blackboard storage zeroes value-type slots on null
            BB.SetBoxed(targetSlot, null);
            return NodeState.SUCCESS;
        }
    }

    /// <summary>
    /// Logs the value of a blackboard variable to the Unity console.
    /// For array variables (stride > 1), logs each element individually.
    /// Useful for debugging inside ForEachAgent.
    /// Field entries:
    ///   0: "variable" — isVariable=true, the variable to log
    ///   1: (array only) packed constant stride, auto-emitted by TreeBaker
    /// </summary>
    [NodeMethod("LogVariable")]
    public sealed class LogVariable : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor { titleLabel = "Variable", label = "Variable", kind = DynamicParamKind.Variable, index = 0 },
        };  // LogVariable

        private int variableSlot = -1;
        private int stride = 1;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            if (fields.Length >= 1 && fields[0].IsVariable)
                variableSlot = fields[0].value;

            // TreeBaker emits stride marker for variables with stride > 1
            if (fields.Length >= 2 && fields[1].IsStrideMarker)
                stride = fields[1].value;
        }

        public override NodeState Execute()
        {
            if (variableSlot < 0) return NodeState.FAILURE;

            if (stride > 1)
            {
                object[] values = new object[stride];
                for (int i = 0; i < stride; i++)
                    values[i] = BB.GetBoxed(variableSlot + i);
                Debug.Log($"[LogVariable] [{string.Join(", ", values)}]");
            }
            else
            {
                Debug.Log($"[LogVariable] {BB.GetBoxed(variableSlot)}");
            }

            return NodeState.SUCCESS;
        }
    }

    /// <summary>
    /// Compares a blackboard variable against a value (constant or from another variable).
    /// Returns SUCCESS if the comparison is true, FAILURE otherwise.
    /// Field entries:
    ///   0: "a"         — isVariable=true, the variable to compare
    ///   1: "b"         — constant (mode=0/2) or variable (mode=1) compare value
    ///   2: "operation" — packed constant (int), the VariableCompareOp
    /// </summary>
    [NodeMethod("CompareVariable")]
    public sealed class CompareVariable : ConditionMethod
    {
        private static bool IsVectorType(Type t) => t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4);
        private const int MagnitudeOpStart = 6; // MagnitudeLess

        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor { titleLabel = "Operand A", label = "Operand A", kind = DynamicParamKind.Variable, index = 0 },
            new DynamicParamDescriptor { titleLabel = "Compare With", label = "Compare With", kind = DynamicParamKind.Toggle, index = 1, syncTypeFromIndex = 0 },
            new DynamicParamDescriptor
            {
                titleLabel = "Operation", label = "Operation", kind = DynamicParamKind.Operation, index = 2,
                operationEnumType = typeof(VariableCompareOp),
                getAvailableOpIndices = (type) =>
                {
                    // Magnitude ops only apply to Vector types
                    if (type != null && IsVectorType(type)) return null; // all ops
                    int[] nonMag = new int[MagnitudeOpStart];
                    for (int i = 0; i < MagnitudeOpStart; i++) nonMag[i] = i;
                    return nonMag;
                }
            },
        };

        private int slotA = -1;
        private int slotB = -1;
        private object constantB;
        private bool hasConstantB;
        private VariableCompareOp operation;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            int fieldIndex = 0;

            slotA = VariableMethodHelper.ReadVariableSlot(fields, ref fieldIndex);

            if (fieldIndex < fields.Length)
            {
                if (fields[fieldIndex].IsVariable)
                {
                    slotB = VariableMethodHelper.ReadVariableSlot(fields, ref fieldIndex);
                }
                else
                {
                    hasConstantB = true;
                    constantB = VariableMethodHelper.ReadConstant(fields, ref fieldIndex, fieldTypeNames, boxedConstants);
                }
            }

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                operation = (VariableCompareOp)fields[fieldIndex].value;
        }

        public override NodeState Execute()
        {
            if (slotA < 0) return NodeState.FAILURE;

            object a = BB.GetBoxed(slotA);
            object b = slotB >= 0 ? BB.GetBoxed(slotB) : (hasConstantB ? constantB : null);

            bool result = EvaluateCompare(a, b, operation);
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        // ── Comparison helpers ──

        private static bool EvaluateCompare(object a, object b, VariableCompareOp op)
        {
            if (a == null && b == null)
                return op == VariableCompareOp.Equal;
            if (a == null || b == null)
                return op == VariableCompareOp.NotEqual;

            if (a is int ai && b is int bi)
                return CompareNumeric(ai, bi, op);
            if (TryToFloat(a, out float af) && TryToFloat(b, out float bf))
                return CompareNumericF(af, bf, op);

            if (op >= VariableCompareOp.MagnitudeLess && op <= VariableCompareOp.MagnitudeGreaterOrEqual)
            {
                float magA = GetMagnitude(a);
                float magB = GetMagnitude(b);
                return CompareNumericF(magA, magB, op);
            }

            return op switch
            {
                VariableCompareOp.Equal => a.Equals(b),
                VariableCompareOp.NotEqual => !a.Equals(b),
                _ => false
            };
        }

        private static bool CompareNumeric(int a, int b, VariableCompareOp op) => op switch
        {
            VariableCompareOp.Equal => a == b,
            VariableCompareOp.NotEqual => a != b,
            VariableCompareOp.Less => a < b,
            VariableCompareOp.LessOrEqual => a <= b,
            VariableCompareOp.Greater => a > b,
            VariableCompareOp.GreaterOrEqual => a >= b,
            _ => false
        };

        private static bool CompareNumericF(float a, float b, VariableCompareOp op) => op switch
        {
            VariableCompareOp.Equal => Mathf.Approximately(a, b),
            VariableCompareOp.NotEqual => !Mathf.Approximately(a, b),
            VariableCompareOp.Less => a < b,
            VariableCompareOp.LessOrEqual => a <= b,
            VariableCompareOp.Greater => a > b,
            VariableCompareOp.GreaterOrEqual => a >= b,
            _ => false
        };

        private static bool TryToFloat(object value, out float result)
        {
            if (value is float f) { result = f; return true; }
            if (value is int i) { result = i; return true; }
            result = 0f;
            return false;
        }

        private static float GetMagnitude(object value)
        {
            if (value is Vector3 v3) return v3.magnitude;
            if (value is Vector2 v2) return v2.magnitude;
            return 0f;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Generic check operations
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Unified check op. The editor shows only ops applicable to the selected variable type.</summary>
    public enum VariableCheckOp
    {
        // Bool
        IsTrue = 0,
        IsFalse = 1,
        // Numeric + Vector
        IsZero = 2,
        IsNotZero = 3,
        // Reference types
        IsNull = 4,
        IsNotNull = 5,
        // GameObject/Component
        IsActive = 6,
        IsInactive = 7,
    }

    public enum EdgeDetectOp
    {
        Rising = 0,
        Falling = 1,
    }

    /// <summary>
    /// Checks a blackboard variable against a condition (IsTrue, IsNull, IsZero, IsActive, etc.).
    /// Available operations depend on the variable type.
    /// </summary>
    [NodeMethod("CheckVariable")]
    public sealed class CheckVariable : ConditionMethod
    {
        private static bool IsBoolType(Type t) => t == typeof(bool);
        private static bool IsNumericOrVectorType(Type t) => t == typeof(int) || t == typeof(float) || t == typeof(Vector2) || t == typeof(Vector3);
        private static bool IsReferenceType(Type t) => t == typeof(GameObject) || t == typeof(Transform) || (t != null && t.IsClass && t != typeof(string));

        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor { titleLabel = "Variable", label = "Variable", kind = DynamicParamKind.Variable, index = 0 },
            new DynamicParamDescriptor
            {
                titleLabel = "Condition", label = "Condition", kind = DynamicParamKind.Operation, index = 1,
                operationEnumType = typeof(VariableCheckOp),
                getAvailableOpIndices = (type) =>
                {
                    if (type == null) return null;
                    if (IsBoolType(type)) return new[] { 0, 1 };           // IsTrue, IsFalse
                    if (IsNumericOrVectorType(type)) return new[] { 2, 3 }; // IsZero, IsNotZero
                    if (IsReferenceType(type)) return new[] { 4, 5, 6, 7 }; // IsNull, IsNotNull, IsActive, IsInactive
                    return null;
                }
            },
        };

        private int variableSlot = -1;
        private VariableCheckOp operation;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            if (fields.Length >= 1 && fields[0].IsVariable)
                variableSlot = fields[0].value;
            if (fields.Length >= 2 && fields[1].IsConstant)
                operation = (VariableCheckOp)fields[1].value;
        }

        public override NodeState Execute()
        {
            if (variableSlot < 0) return NodeState.FAILURE;

            object value = BB.GetBoxed(variableSlot);
            bool result = operation switch
            {
                VariableCheckOp.IsTrue    => value is bool b && b,
                VariableCheckOp.IsFalse   => value is bool b2 && !b2,
                VariableCheckOp.IsZero    => IsZeroValue(value),
                VariableCheckOp.IsNotZero => !IsZeroValue(value),
                VariableCheckOp.IsNull    => value == null || (value is UnityEngine.Object uo && uo == null),
                VariableCheckOp.IsNotNull => value != null && (!(value is UnityEngine.Object uo2) || uo2 != null),
                VariableCheckOp.IsActive  => IsActiveInHierarchy(value),
                VariableCheckOp.IsInactive=> !IsActiveInHierarchy(value),
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        private static bool IsZeroValue(object value)
        {
            if (value == null) return true;
            if (value is int i) return i == 0;
            if (value is float f) return Mathf.Approximately(f, 0f);
            if (value is Vector2 v2) return v2.sqrMagnitude < 0.0001f;
            if (value is Vector3 v3) return v3.sqrMagnitude < 0.0001f;
            return false;
        }

        private static bool IsActiveInHierarchy(object value)
        {
            if (value is GameObject go) return go != null && go.activeInHierarchy;
            if (value is Component comp) return comp != null && comp.gameObject.activeInHierarchy;
            return false;
        }
    }

    /// <summary>
    /// Returns SUCCESS if the variable's value changed since the last tick.
    /// Works with any type — compares via Equals.
    /// </summary>
    [NodeMethod("HasChanged")]
    public sealed class HasChanged : ConditionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor { titleLabel = "Variable", label = "Variable", kind = DynamicParamKind.Variable, index = 0 },
        };

        private int variableSlot = -1;
        private object previousValue;  // HasChanged
        private bool hasPrevious;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            if (fields.Length >= 1 && fields[0].IsVariable)
                variableSlot = fields[0].value;
        }

        public override NodeState Execute()
        {
            if (variableSlot < 0) return NodeState.FAILURE;

            object current = BB.GetBoxed(variableSlot);

            if (!hasPrevious)
            {
                previousValue = current;
                hasPrevious = true;
                return NodeState.FAILURE;
            }

            bool changed = !Equals(current, previousValue);
            previousValue = current;
            return changed ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    /// <summary>
    /// Detects rising (false→true) or falling (true→false) edges on a boolean variable.
    /// </summary>
    [NodeMethod("EdgeDetect")]
    public sealed class EdgeDetect : ConditionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor { titleLabel = "Variable", label = "Variable", kind = DynamicParamKind.Variable, index = 0 },
            new DynamicParamDescriptor
            {
                titleLabel = "Edge", label = "Edge", kind = DynamicParamKind.Operation, index = 1,
                operationEnumType = typeof(EdgeDetectOp),
            },
        };

        private int variableSlot = -1;
        private EdgeDetectOp operation;  // EdgeDetect
        private bool previous;
        private bool hasPrevious;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            if (fields.Length >= 1 && fields[0].IsVariable)
                variableSlot = fields[0].value;
            if (fields.Length >= 2 && fields[1].IsConstant)
                operation = (EdgeDetectOp)fields[1].value;
        }

        public override NodeState Execute()
        {
            if (variableSlot < 0) return NodeState.FAILURE;

            object value = BB.GetBoxed(variableSlot);
            bool current = value is bool b && b;

            if (!hasPrevious)
            {
                previous = current;
                hasPrevious = true;
                return NodeState.FAILURE;
            }

            bool triggered = operation switch
            {
                EdgeDetectOp.Rising  => current && !previous,
                EdgeDetectOp.Falling => !current && previous,
                _ => false
            };
            previous = current;
            return triggered ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    /// <summary>
    /// Toggles a boolean blackboard variable.
    /// </summary>
    [NodeMethod("Toggle")]
    public sealed class Toggle : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                label = "Variable", kind = DynamicParamKind.Variable, index = 0,
                allowedTypes = new[] { typeof(bool) }
            },
        };

        private int variableSlot = -1;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            if (fields.Length >= 1 && fields[0].IsVariable)
                variableSlot = fields[0].value;
        }

        public override NodeState Execute()
        {
            if (variableSlot < 0) return NodeState.FAILURE;

            object value = BB.GetBoxed(variableSlot);
            bool current = value is bool b && b;
            BB.SetBoxed(variableSlot, !current);
            return NodeState.SUCCESS;
        }
    }

    /// <summary>
    /// Writes a Transform's position into a Vector2 or Vector3 variable.
    /// Output type is auto-detected from the target variable.
    /// </summary>
    [NodeMethod("SetFromTransform")]
    public sealed class SetFromTransform : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor { titleLabel = "Target", label = "Target", kind = DynamicParamKind.Variable, index = 0 },
            new DynamicParamDescriptor
            {
                titleLabel = "Source", label = "Source", kind = DynamicParamKind.Variable, index = 1,
                allowedTypes = new[] { typeof(Transform) }
            },
        };

        private int targetSlot = -1;
        private int sourceSlot = -1;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            int fieldIndex = 0;
            targetSlot = VariableMethodHelper.ReadVariableSlot(fields, ref fieldIndex);
            sourceSlot = VariableMethodHelper.ReadVariableSlot(fields, ref fieldIndex);
        }

        public override NodeState Execute()
        {
            if (targetSlot < 0 || sourceSlot < 0) return NodeState.FAILURE;

            object sourceValue = BB.GetBoxed(sourceSlot);
            Transform source = sourceValue as Transform;
            if (source == null) return NodeState.FAILURE;

            object targetValue = BB.GetBoxed(targetSlot);
            if (targetValue is Vector3)
                BB.SetBoxed(targetSlot, source.position);
            else if (targetValue is Vector2)
                BB.SetBoxed(targetSlot, new Vector2(source.position.x, source.position.z));
            else
                return NodeState.FAILURE;

            return NodeState.SUCCESS;
        }
    }

    /// <summary>
    /// Moves a NavMeshAgent toward a target position (Vector2, Vector3, or Transform).
    /// The NavMeshAgent is auto-resolved from the BlackBoard component's GameObject or its children.
    /// Returns RUNNING while moving, SUCCESS when the destination is reached.
    /// </summary>
    [NodeMethod("MoveTo")]
    public sealed class MoveTo : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                label = "Target", kind = DynamicParamKind.Variable, index = 0,
                allowedTypes = new[] { typeof(Vector2), typeof(Vector3), typeof(Transform) }
            },
        };

        private int targetSlot = -1;
        private NavMeshAgent agent;
        private bool agentResolved;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            if (fields.Length >= 1 && fields[0].IsVariable)
                targetSlot = fields[0].value;
        }

        public override NodeState Execute()
        {
            if (targetSlot < 0) return NodeState.FAILURE;

            object target = BB.GetBoxed(targetSlot);
            if (target == null) return NodeState.FAILURE;

            //Debug.Log("HELE");

            Vector3 destination;
            if (target is Transform t)
                destination = t.position;
            else if (target is Vector3 v3)
                destination = v3;
            else if (target is Vector2 v2)
                destination = new Vector3(v2.x, 0f, v2.y);
            else
                return NodeState.FAILURE;

            if (!agentResolved)
            {
                agent = GetComponentFromBB<NavMeshAgent>();
                agentResolved = true;
            }

            if (agent == null || !agent.isOnNavMesh)
                return NodeState.FAILURE;

            agent.isStopped = false;
            agent.SetDestination(destination);

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                return NodeState.SUCCESS;

            return NodeState.RUNNING;
        }

        public override void OnAbort(IBlackBoardAccess bbAccess)
        {
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Shared helpers for dynamic-type nodes
    // ═══════════════════════════════════════════════════════════════

    internal static class VariableMethodHelper
    {
        /// <summary>
        /// Reads a variable slot from the field stream at the current position.
        /// If the variable is an array, TreeBaker emitted a stride constant right after it.
        /// We only consume the stride when there are more entries after it (multi-param nodes).
        /// Returns the slot index and advances <paramref name="fieldIndex"/> past the consumed entries.
        /// </summary>
        public static int ReadVariableSlot(ReadOnlySpan<FieldData> fields, ref int fieldIndex)
        {
            if (fieldIndex >= fields.Length || !fields[fieldIndex].IsVariable)
                return -1;

            int slot = fields[fieldIndex].value;
            fieldIndex++;

            // A trailing stride marker is emitted by TreeBaker for variables with stride > 1.
            if (fieldIndex < fields.Length && fields[fieldIndex].IsStrideMarker)
                fieldIndex++; // consume stride

            return slot;
        }

        /// <summary>
        /// Reads a constant value from the field stream at the current position.
        /// Advances <paramref name="fieldIndex"/> by 1.
        /// </summary>
        public static object ReadConstant(ReadOnlySpan<FieldData> fields, ref int fieldIndex,
            string[] fieldTypeNames, object[] boxedConstants)
        {
            if (fieldIndex >= fields.Length)
                return null;

            FieldData fd = fields[fieldIndex];
            fieldIndex++;

            return ReadConstantValue(fd, fieldTypeNames, boxedConstants, fieldIndex - 1);
        }

        /// <summary>
        /// Reads a constant value from a single FieldData entry, using the parallel
        /// fieldTypeNames array to decode packed mode=0 values (int, float, bool, enum).
        /// Mode=2 (boxed) constants are read from the boxedConstants list.
        /// </summary>
        private static object ReadConstantValue(FieldData fd, string[] fieldTypeNames, object[] boxedConstants, int fieldIndex)
        {
            if (fd.IsBoxedConstant)
            {
                return fd.GetBoxedConstant<object>(boxedConstants);
            }

            if (fd.IsConstant)
            {
                string typeName = (fieldTypeNames != null && fieldIndex < fieldTypeNames.Length)
                    ? fieldTypeNames[fieldIndex]
                    : null;

                Type type = string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);
                if (type == typeof(int)) return fd.value;
                if (type == typeof(uint)) return (uint)fd.value;
                if (type == typeof(float)) return fd.GetFloat();
                if (type == typeof(bool)) return fd.value != 0;
                if (type != null && type.IsEnum) return Enum.ToObject(type, fd.value);
                // Unknown type — treat as raw int
                return fd.value;
            }

            return null;
        }
    }
}
