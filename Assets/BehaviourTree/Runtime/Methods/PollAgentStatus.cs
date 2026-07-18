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
    /// Agent count is read dynamically from ctx.agentCount (set by
    /// CommanderTreeRunner each frame). Slots with value -1 or 0 that
    /// correspond to unregistered agents are treated as neutral.
    /// </summary>
    [NodeMethod("PollAgentStatus", allowedTreeType = AllowedTreeType.Commander)]
    public sealed class PollAgentStatus : ActionMethod
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

        /// <summary>
        /// Prevents double-fire: after returning SUCCESS, refuses another SUCCESS
        /// until at least one agent has reported Running(2), confirming the new
        /// movement cycle has started.
        /// </summary>
        private bool consumed;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            // Stride marker (fields[1]) is consumed by field index but ignored —
            // agentCount comes from ctx each frame.
            if (fields.Length >= 1 && fields[0].IsVariable)
            {
                statusSlot = fields[0].value;
            }
        }

        public override NodeState Execute(TickContext ctx)
        {
            if (statusSlot < 0)
            {
                Debug.LogError($"PollAgentStatus: statusSlot {statusSlot} is invalid.");
                return NodeState.FAILURE;
            }

            int agentCount = ctx.agentCount;
            bool anyRunning = false;

            for (int i = 0; i < agentCount; i++)
            {
                object val = BB.GetBoxedRaw(statusSlot + i);
                int status = val is int iv ? iv : -1;
                switch (status)
                {
                    case 1: return NodeState.FAILURE;  // any one failed → fail
                    case 2:
                        anyRunning = true;
                        consumed = false;               // agent acknowledged new movement → reset gate
                        break;
                    // 0: arrived, -1: unregistered → neutral
                }
            }

            if (anyRunning)
                return NodeState.RUNNING;

            // All agents report 0 (arrived / idle)
            if (consumed)
                return NodeState.RUNNING;  // already returned SUCCESS for this arrival cycle

            consumed = true;
            return NodeState.SUCCESS;      // one shot per arrival cycle
        }
    }
}
