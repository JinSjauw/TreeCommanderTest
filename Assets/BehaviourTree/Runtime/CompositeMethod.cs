using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public abstract class CompositeMethod : NodeMethod
    {
        public abstract NodeState Execute(int nodeIndex, ref TickContext ctx);
    }
}
