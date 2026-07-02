using BehaviourTree.Core;

namespace BehaviourTree.Runtime.Methods
{
    [NodeMethod("ForEachRole", allowedTreeType = AllowedTreeType.Commander)]
    public sealed class ForEachRoleMethod : CompositeMethod
    {
        [SharedVar(IsHidden = true, AutoVariableName = "AgentRoles", SkipAutoResolve = true)]
        public int agentRoleSlot;

        [SharedVar(isToggleVariable: true, IsRoleDropdown = true)]
        public int targetRoleSlot;

        public override NodeState Execute(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            if (node.firstChildIndex < 0) return NodeState.SUCCESS;

            BlackBoard bb = ctx.blackBoard;

            int roleSlot = GetSlotByName(nameof(agentRoleSlot));
            if (roleSlot < 0) return NodeState.FAILURE;

            int targetRoleInt = targetRoleSlot;

            int agentCount = ctx.agentCount;
            if (agentCount <= 0) return NodeState.FAILURE;

            int agentIndex = ctx.runningAgentIndex[nodeIndex];
            int childIndex = ctx.activeChildIndex[nodeIndex];
            int childCount = node.lastChildIndex - node.firstChildIndex + 1;

            for (; agentIndex < agentCount; agentIndex++)
            {
                object roleBoxed = bb.GetBoxedRaw(roleSlot + agentIndex);
                int roleInt = roleBoxed is int roleVal ? roleVal : 0;
                if (roleInt != targetRoleInt)
                {
                    continue;
                }

                ctx.agentIndex = agentIndex;

                for (; childIndex < childCount; childIndex++)
                {
                    int globalChildIndex = node.firstChildIndex + childIndex;
                    NodeState result = TickDispatcher.TickNode(globalChildIndex, ref ctx);

                    if (result == NodeState.RUNNING)
                    {
                        ctx.runningAgentIndex[nodeIndex] = agentIndex;
                        ctx.activeChildIndex[nodeIndex] = childIndex;
                        return NodeState.RUNNING;
                    }

                    // SUCCESS or FAILURE — continue to next child
                }

                childIndex = 0;
            }

            ctx.runningAgentIndex[nodeIndex] = 0;
            ctx.activeChildIndex[nodeIndex] = 0;
            return NodeState.SUCCESS;
        }
    }
}
