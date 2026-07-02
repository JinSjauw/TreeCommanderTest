using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Per-child completion state for Parallel nodes. Tracks
    /// whether each child has finished (SUCCESS/FAILURE) or is still running.
    /// </summary>
    internal struct ParallelChildState
    {
        public NodeState result;
    }

    /// <summary>
    /// Per-frame snapshot for tick evaluation. Mutable arrays (nodeStates,
    /// activeChildIndex) are stored on TreeEvaluator and passed by ref.
    /// </summary>
    public struct TickContext
    {
        public NodeData[] nodeDatas;
        public NodeMethod[] methodInstances;
        public NodeState[] nodeStates;
        public int[] activeChildIndex;
        public int[] runningAgentIndex;
        public int agentCount;
        public int agentIndex;

        public BlackBoard blackBoard;
        public bool[] lastConditionResult;
        public bool[] tickedThisFrame;
    }

    /// <summary>
    /// Signature for a static tick function. Each node type has one.
    /// </summary>
    internal delegate NodeState TickHandler(int nodeIndex, ref TickContext ctx);

    internal static class TickDispatcher
    {
        private static readonly TickHandler[] handlers;

        static TickDispatcher()
        {
            handlers = new TickHandler[6];
            handlers[(int)BehaviourNodeType.ACTION] = TickFunctions.TickLeaf;
            handlers[(int)BehaviourNodeType.CONDITION] = TickFunctions.TickLeaf;
            handlers[(int)BehaviourNodeType.DECORATOR] = TickFunctions.TickDecorator;
            handlers[(int)BehaviourNodeType.SUBTREE] = TickFunctions.TickSubtree;
        }

        public static NodeState TickNode(int nodeIndex, ref TickContext ctx)
        {
            if(nodeIndex < 0 || nodeIndex >= ctx.nodeDatas.Length) return NodeState.FAILURE;
            BehaviourNodeType nodeType = ctx.nodeDatas[nodeIndex].nodeType;
            TickHandler handler = handlers[(int)nodeType];
            NodeState result;
            if (handler != null)
            {
                result = handler(nodeIndex, ref ctx);
            }
            else
            {
                result = TickFunctions.TickComposite(nodeIndex, ref ctx);
            }
            ctx.tickedThisFrame[nodeIndex] = true;
            ctx.nodeStates[nodeIndex] = result;
            return result;
        }
    }
}
