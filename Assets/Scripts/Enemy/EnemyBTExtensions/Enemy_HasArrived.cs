using BehaviourTree.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Checks whether the NavMeshAgent has reached its current destination.
    /// Returns SUCCESS when arrived, FAILURE while still moving.
    /// </summary>
    [NodeMethod("Enemy_HasArrived", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_HasArrived : ConditionMethod
    {
        private NavMeshAgent cachedAgent;
        private bool agentResolved;

        public override NodeState Execute()
        {
            if (!agentResolved)
            {
                cachedAgent = ((MonoBehaviour)BB).GetComponent<NavMeshAgent>();
                if (cachedAgent == null)
                    cachedAgent = ((MonoBehaviour)BB).GetComponentInChildren<NavMeshAgent>();
                agentResolved = true;
            }
            if (cachedAgent == null) return NodeState.FAILURE;

            return !cachedAgent.pathPending
                && cachedAgent.remainingDistance <= cachedAgent.stoppingDistance + 0.1f
                ? NodeState.SUCCESS
                : NodeState.FAILURE;
        }
    }
}
