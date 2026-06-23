using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Checks whether the NavMeshAgent has reached its current destination.
    /// Returns SUCCESS when arrived, FAILURE while still moving.
    /// </summary>
    [NodeMethod("Enemy_HasArrived", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_HasArrived : ConditionMethod
    {
        private EnemyController cachedController;
        private bool controllerResolved;

        public override NodeState Execute()
        {
            if (!controllerResolved)
            {
                BlackBoard bb = BB as BlackBoard;
                if (bb != null)
                    cachedController = bb.GetComponent<EnemyController>();
                controllerResolved = true;
            }
            if (cachedController == null) return NodeState.FAILURE;

            return cachedController.HasArrivedAtDestination()
                ? NodeState.SUCCESS
                : NodeState.FAILURE;
        }
    }
}
