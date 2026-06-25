using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Tick functions for leaf nodes (Action and Condition).
    /// Resolves inputs from the blackboard, executes the method, then writes outputs back.
    /// Manages bb.currentAgentOffset from ctx.agentIndex so leaves access the correct
    /// per-agent element transparently.
    /// </summary>
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

            // Apply the current agent index as BB offset so SharedVar fields and dynamic
            // methods resolve to the correct per-agent element. Restore afterwards.
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
