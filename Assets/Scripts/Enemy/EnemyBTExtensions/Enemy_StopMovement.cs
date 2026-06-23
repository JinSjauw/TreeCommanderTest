using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Stops the NavMeshAgent immediately (sets isStopped = true).
    /// </summary>
    [NodeMethod("Enemy_StopMovement", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_StopMovement : ActionMethod
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

            cachedController.StopMoving();
            return NodeState.SUCCESS;
        }
    }
}
