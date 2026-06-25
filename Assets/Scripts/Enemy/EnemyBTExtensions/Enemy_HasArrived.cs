using BehaviourTree.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Checks whether the NavMeshAgent has reached its current destination.
    /// </summary>
    [NodeMethod("Enemy_HasArrived", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_HasArrived : ConditionMethod
    {
        private NavMeshAgent cachedAgent;

        protected override void OnInitialize()
        {
            cachedAgent = GetComponentFromBB<NavMeshAgent>();
        }

        public override NodeState Execute(TickContext ctx)
        {
            if (cachedAgent == null) return NodeState.FAILURE;

            return !cachedAgent.pathPending
                && cachedAgent.remainingDistance <= cachedAgent.stoppingDistance + 0.1f
                ? NodeState.SUCCESS
                : NodeState.FAILURE;
        }
    }
}
