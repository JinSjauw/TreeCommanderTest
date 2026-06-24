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
            MonoBehaviour mb = (MonoBehaviour)BB;
            cachedAgent = mb.GetComponent<NavMeshAgent>();
            if (cachedAgent == null)
                cachedAgent = mb.GetComponentInChildren<NavMeshAgent>();
        }

        public override NodeState Execute()
        {
            if (cachedAgent == null) return NodeState.FAILURE;

            return !cachedAgent.pathPending
                && cachedAgent.remainingDistance <= cachedAgent.stoppingDistance + 0.1f
                ? NodeState.SUCCESS
                : NodeState.FAILURE;
        }
    }
}
