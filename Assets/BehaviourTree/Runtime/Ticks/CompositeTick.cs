using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Tick function for composite nodes.
    /// Dispatches to the CompositeMethod instance stored in methodInstances.
    /// Manages bb.currentAgentOffset from ctx.agentIndex so the composite's own
    /// SharedVar fields resolve to the correct per-agent element.
    /// Composites themselves set ctx.agentIndex before ticking children.
    /// </summary>
    internal static partial class TickFunctions
    {
        internal static NodeState TickComposite(int nodeIndex, ref TickContext ctx)
        {
            NodeMethod method = ctx.methodInstances[nodeIndex];
            if (method is CompositeMethod composite)
            {
                BlackBoard bb = ctx.blackBoard;
                int savedOffset = bb.currentAgentOffset;
                bb.currentAgentOffset = ctx.agentIndex;

                composite.ResolveInputsGeneric(bb);
                NodeState result = composite.Execute(nodeIndex, ref ctx);
                composite.WriteOutputsGeneric(bb);

                bb.currentAgentOffset = savedOffset;
                return result;
            }

            UnityEngine.Debug.LogError($"Composite method instance not found for node index {nodeIndex}. Returning FAILURE.");
            return NodeState.FAILURE;
        }
    }
}
