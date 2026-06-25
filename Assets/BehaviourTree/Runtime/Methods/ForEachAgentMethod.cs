using BehaviourTree.Core;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Iterates over all agents and ticks children for each one.
    /// Sets ctx.agentIndex so children transparently access the correct per-agent
    /// slot via bb.currentAgentOffset (managed by TickLeaf/TickComposite).
    /// </summary>
    [NodeMethod("ForEachAgent", allowedTreeType = AllowedTreeType.Commander)]
    public sealed class ForEachAgentMethod : CompositeMethod
    {
        public override NodeState Execute(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            if (node.firstChildIndex < 0) return NodeState.SUCCESS;

            int agentCount = ctx.agentCount;
            if (agentCount <= 0) return NodeState.FAILURE;

            int agentIndex = ctx.runningAgentIndex[nodeIndex];
            int childIndex = ctx.activeChildIndex[nodeIndex];
            int childCount = node.lastChildIndex - node.firstChildIndex + 1;

            for (; agentIndex < agentCount; agentIndex++)
            {
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
                }

                childIndex = 0;
            }

            ctx.runningAgentIndex[nodeIndex] = 0;
            ctx.activeChildIndex[nodeIndex] = 0;
            return NodeState.SUCCESS;
        }
    }
}
