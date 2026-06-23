using BehaviourTree.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Stops the NavMeshAgent immediately (sets isStopped = true).
    /// </summary>
    [NodeMethod("Enemy_StopMovement", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_StopMovement : ActionMethod
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

            cachedAgent.isStopped = true;
            return NodeState.SUCCESS;
        }
    }
}
