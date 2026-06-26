using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Commander condition node. Checks a squad-data array across all registered agents.
    /// Evaluates a condition (IsAnyNull / IsAnyNotNull) on each agent's slot value.
    /// Returns SUCCESS as soon as ANY element matches the selected operation.
    ///
    /// Uses ctx.agentCount (set by CommanderTreeRunner) to iterate only active agent
    /// indices — unlike CheckVariableArray which uses a stride marker.
    ///
    /// Sentinel values written by CompactAndInvalidateSquadSlots on agent removal:
    ///   int: -1, float: 0f, Vector3: zero, bool: false, UnityEngine.Object: destroyed
    /// These are treated as "null"/invalid when the corresponding operation is applied.
    /// </summary>
    [NodeMethod("CheckSquadData", allowedTreeType = AllowedTreeType.Commander)]
    public sealed class CheckSquadData : ConditionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Squad Data Array",
                label = "Input",
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
                titleLabel = "Operation",
                label = "Operation",
                kind = DynamicParamKind.Operation,
                index = 1,
                operationEnumType = typeof(ArrayCheckOp),
            },
        };

        private int variableSlot = -1;
        private ArrayCheckOp operation;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            if (fields.Length >= 1 && fields[0].IsVariable)
                variableSlot = fields[0].value;

            // Field 1 (or 2 if stride marker exists between): ArrayCheckOp enum constant
            int opFieldIndex = fields.Length >= 2 && fields[1].IsStrideMarker ? 2 : 1;
            if (fields.Length > opFieldIndex && fields[opFieldIndex].IsConstant)
                operation = (ArrayCheckOp)fields[opFieldIndex].value;
        }

        public override NodeState Execute(TickContext ctx)
        {
            if (variableSlot < 0)
                return NodeState.FAILURE;

            int agentCount = ctx.agentCount;

            for (int i = 0; i < agentCount; i++)
            {
                object value = BB.GetBoxedRaw(variableSlot + i);

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
