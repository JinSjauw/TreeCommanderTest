using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    internal static partial class TickFunctions
    {
        internal static NodeState TickLeaf(int nodeIndex, ref TickContext ctx)
        {
            BehaviourNodeType nodeType = ctx.nodeDatas[nodeIndex].nodeType;
            NodeMethod method = ctx.methodInstances[nodeIndex];
            if (method == null)
            {
                UnityEngine.Debug.LogError($"Method instance not found for node index {nodeIndex}. Returning FAILURE.");
                return NodeState.FAILURE;
            }

            BlackBoard bb = ctx.blackBoard;

            int savedOffset = bb.currentAgentOffset;
            bb.currentAgentOffset = ctx.agentIndex;

            method.ResolveInputsGeneric(bb);

            NodeState result;
            if (nodeType == BehaviourNodeType.ACTION)
                result = ((ActionMethod)method).Execute(ctx);
            else if (nodeType == BehaviourNodeType.CONDITION)
                result = ((ConditionMethod)method).Execute(ctx);
            else
                result = NodeState.FAILURE;

            method.WriteOutputsGeneric(bb);

            bb.currentAgentOffset = savedOffset;

            return result;
        }
    }
}
