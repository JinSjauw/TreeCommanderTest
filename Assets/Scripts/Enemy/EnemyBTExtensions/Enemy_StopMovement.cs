using BehaviourTree.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Stops the NavMeshAgent immediately.
    /// </summary>
    [NodeMethod("Enemy_StopMovement", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_StopMovement : ActionMethod
    {
        private NavMeshAgent cachedAgent;

        protected override void OnInitialize()
        {
            MonoBehaviour mb = (MonoBehaviour)BB;
            cachedAgent = mb.GetComponent<NavMeshAgent>();
            if (cachedAgent == null)
                cachedAgent = mb.GetComponentInChildren<NavMeshAgent>();
        }

        public override NodeState Execute()
        {
            if (cachedAgent == null) return NodeState.FAILURE;
            cachedAgent.isStopped = true;
            return NodeState.SUCCESS;
        }
    }
}
