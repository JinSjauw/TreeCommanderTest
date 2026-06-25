using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Array check operations. Evaluated per-element across a variable array.
    /// The node returns SUCCESS as soon as ANY element matches the condition.
    /// </summary>
    public enum ArrayCheckOp
    {
        /// <summary>Any element is null, zero, false, or destroyed UnityEngine.Object.</summary>
        IsAnyNull = 0,
        /// <summary>Any element is not null, non-zero, true, and alive (if UnityEngine.Object).</summary>
        IsAnyNotNull = 1,
    }

    /// <summary>
    /// Condition node. Iterates all elements of an array variable and evaluates
    /// a condition on each element. Returns SUCCESS as soon as ANY element matches.
    ///
    /// Works with any array variable: squad-data (e.g. DetectedEnemy[]), plain arrays,
    /// or multi-slot variables with stride > 1. The array length is auto-detected
    /// from the variable's stride marker emitted by TreeBaker.
    ///
    /// Use with a Priority composite: when this node returns SUCCESS, the Priority
    /// aborts lower-priority branches and switches to the reaction subtree.
    /// </summary>
    [NodeMethod("CheckVariableArray")]
    public sealed class CheckVariableArray : ConditionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Array Variable",
                label = "Variable",
                kind = DynamicParamKind.Variable,
                index = 0,
                allowedTypes = new[]
                {
                    typeof(Transform[]), typeof(GameObject[]),
                    typeof(bool[]), typeof(int[]), typeof(float[]),
                    typeof(Vector2[]), typeof(Vector3[]),
                },
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Condition",
                label = "Condition",
                kind = DynamicParamKind.Operation,
                index = 1,
                operationEnumType = typeof(ArrayCheckOp),
            },
        };

        private int variableSlot = -1;
        private int elementCount = 1;
        private ArrayCheckOp operation;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            // Field 0: array variable slot
            if (fields.Length >= 1 && fields[0].IsVariable)
                variableSlot = fields[0].value;

            // TreeBaker emits a stride marker after array variables (stride > 1)
            if (fields.Length >= 2 && fields[1].IsStrideMarker)
                elementCount = fields[1].value;

            // Field 2 (or 1 if no stride): ArrayCheckOp enum constant
            int opFieldIndex = fields.Length >= 2 && fields[1].IsStrideMarker ? 2 : 1;
            if (fields.Length > opFieldIndex && fields[opFieldIndex].IsConstant)
                operation = (ArrayCheckOp)fields[opFieldIndex].value;
        }

        public override NodeState Execute()
        {
            if (variableSlot < 0)
                return NodeState.FAILURE;

            for (int i = 0; i < elementCount; i++)
            {
                object value = BB.GetBoxed(variableSlot + i);
                if (Evaluate(value, operation))
                    return NodeState.SUCCESS;
            }

            return NodeState.FAILURE;
        }

        private static bool Evaluate(object value, ArrayCheckOp op) => op switch
        {
            ArrayCheckOp.IsAnyNull    => IsNullValue(value),
            ArrayCheckOp.IsAnyNotNull => !IsNullValue(value),
            _ => false,
        };

        private static bool IsNullValue(object value)
        {
            if (value == null) return true;
            if (value is int i && i == 0) return true;
            if (value is float f && Mathf.Approximately(f, 0f)) return true;
            if (value is Vector2 v2 && v2.sqrMagnitude < 0.0001f) return true;
            if (value is Vector3 v3 && v3.sqrMagnitude < 0.0001f) return true;
            if (value is bool b && !b) return true;
            if (value is UnityEngine.Object uo && uo == null) return true;
            return false;
        }
    }
}
