using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Commander condition node. Polls a squad-data int status array
    /// (e.g. AgentStatus) across all registered agents.
    ///
    /// Returns:
    ///   SUCCESS — all agents report 0 (arrived / success / inactive-unregistered)
    ///   FAILURE — any agent reports 1 (failed / blocked)
    ///   RUNNING — at least one agent reports 2 (still running / moving)
    ///
    /// Slots with value -1 or 0 that correspond to unregistered agents are
    /// treated as not-participating (neutral).
    /// </summary>
    [NodeMethod("PollAgentStatus", allowedTreeType = AllowedTreeType.Commander)]
    public sealed class PollAgentStatus : ConditionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Status Array",
                label = "Input",
                kind = DynamicParamKind.Variable,
                index = 0,
                allowedTypes = new[] { typeof(int[]) },
            },
        };

        private int statusSlot = -1;
        private int agentCount = 1;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            if (fields.Length >= 1 && fields[0].IsVariable)
            {
                statusSlot = fields[0].value;
                if (fields.Length >= 2 && fields[1].IsStrideMarker)
                    agentCount = fields[1].value;
            }
        }

        public override NodeState Execute()
        {
            if (statusSlot < 0)
                return NodeState.FAILURE;

            bool anyRunning = false;

            for (int i = 0; i < agentCount; i++)
            {
                object val = BB.GetBoxedRaw(statusSlot + i);
                int status = val is int iv ? iv : -1;

                switch (status)
                {
                    case 1: return NodeState.FAILURE;  // any one failed → fail
                    case 2: anyRunning = true; break;   // still moving
                    // 0: arrived, -1: unregistered → neutral
                }
            }

            return anyRunning ? NodeState.RUNNING : NodeState.SUCCESS;
        }
    }
}
