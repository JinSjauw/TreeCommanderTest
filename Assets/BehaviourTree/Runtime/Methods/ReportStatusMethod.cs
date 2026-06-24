using System;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Status values that an agent can report to its AgentStatus variable.
    /// </summary>
    public enum AgentStatusValue
    {
        Success = 0,
        Failure = 1,
        Running = 2,
    }

    /// <summary>
    /// Agent action node. Writes a status value to a target int variable
    /// (typically AgentStatus) on the agent's blackboard. The squad bridge
    /// then syncs it to the squad BB where the commander's PollAgentStatus reads it.
    /// </summary>
    [NodeMethod("ReportStatus", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class ReportStatusMethod : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Report Channel",
                label = "Target",
                kind = DynamicParamKind.Variable,
                index = 0,
                allowedTypes = new[] { typeof(int) },
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Status",
                label = "Value",
                kind = DynamicParamKind.Operation,
                index = 1,
                operationEnumType = typeof(AgentStatusValue),
            },
        };

        private int targetSlot = -1;
        private AgentStatusValue statusValue = AgentStatusValue.Success;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            int fieldIndex = 0;

            // Field 0: Target (Variable)
            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
            {
                targetSlot = VariableMethodHelper.ReadVariableSlot(fields, ref fieldIndex);
            }

            // Field 1: Status (Operation enum)
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
            {
                statusValue = (AgentStatusValue)fields[fieldIndex].value;
                fieldIndex++;
            }
        }

        public override NodeState Execute()
        {
            if (targetSlot < 0) return NodeState.FAILURE;

            BB.SetBoxed(targetSlot, (int)statusValue);
            return NodeState.SUCCESS;
        }
    }
}
