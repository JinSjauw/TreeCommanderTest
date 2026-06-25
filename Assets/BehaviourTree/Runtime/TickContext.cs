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
    /// nodeStates is populated by TickDispatcher.TickNode() as a side-effect;
    /// tick functions do not read it.
    /// </summary>
    public struct TickContext
    {
        public NodeData[] nodeDatas;
        public NodeMethod[] methodInstances;
        public NodeState[] nodeStates;
        public int[] activeChildIndex;
        /// <summary>
        /// Per-node running agent index for ForEachAgent composites.
        /// Saves loop position across RUNNING frames so the composite resumes
        /// on the correct agent on the next tick.
        /// </summary>
        public int[] runningAgentIndex;
        /// <summary>Total number of agents in the current commander's squad. Set by CommanderTreeRunner before evaluation.</summary>
        public int agentCount;

        /// <summary>
        /// Per-agent offset used by leaves and composites during a single tick.
        /// Composites set this before ticking children (ForEachAgent, SelectAgent, etc.).
        /// TickLeaf and TickComposite apply it to bb.currentAgentOffset before resolve/execute/write,
        /// then restore. Default 0 means access element 0 of every variable.
        /// </summary>
        public int agentIndex;

        public BlackBoard blackBoard;

        /// <summary>
        /// Per-node last condition result for conditional abort transition detection.
        /// Indexed the same as nodeDatas. false = condition was not met last time it was checked.
        /// </summary>
        public bool[] lastConditionResult;

        /// <summary>
        /// Per-node flag set by TickDispatcher when a node is ticked this frame.
        /// Used by TreeEvaluator to clear stale nodeStates for unticked nodes
        /// without affecting RUNNING state that conditional abort depends on.
        /// </summary>
        public bool[] tickedThisFrame;
    }

    /// <summary>
    /// Signature for a static tick function. Each node type has one.
    /// </summary>
    internal delegate NodeState TickHandler(int nodeIndex, ref TickContext ctx);

    /// <summary>
    /// Dispatches TickNode calls to the appropriate static tick function
    /// based on node type. Uses a fixed-size delegate table for speed.
    /// </summary>
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
            // COMPOSITE, ROOT — left null,
            // TickNode falls through to TickComposite via methodInstances.
        }

        /// <summary>
        /// Ticks the node at nodeIndex, returning its result.
        /// Dispatches to the registered tick handler for the node's type.
        /// </summary>
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
