using System;
using BehaviourTree.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Runs the enemy's detection system and checks whether any detected target
    /// satisfies the range check (and optionally line-of-sight).
    /// </summary>
    [NodeMethod("Enemy_Detected", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_Detected : ConditionMethod
    {
        /// <summary>Detection radius. Three-way row: constant, BB variable, or ScriptableObject field.</summary>
        [SharedVar(IsSOConstant = true)]
        public float radius;

        /// <summary>Range comparison applied to each detected target.</summary>
        public RangeCheckOp operation;

        /// <summary>When true, targets also require line of sight.</summary>
        public bool checkLineOfSight;

        private EnemyDetectionSystem cachedDetection;
        private Transform cachedAgentTransform;

        protected override void OnInitialize()
        {
            cachedDetection = GetComponentFromBB<EnemyDetectionSystem>();
            NavMeshAgent agent = GetComponentFromBB<NavMeshAgent>();
            if (agent != null) cachedAgentTransform = agent.transform;
        }

        public override NodeState Execute(TickContext ctx)
        {
            if (cachedDetection == null || cachedAgentTransform == null) return NodeState.FAILURE;

            if (!cachedDetection.DetectTargets(radius))
            {
                //Debug.LogError($"[Enemy_Detected] FAILURE: {cachedAgentTransform.name} failed to detect targets in radius {radius}");
                return NodeState.FAILURE;
            }


            Vector2 agentPos = new Vector2(
                cachedAgentTransform.position.x,
                cachedAgentTransform.position.z);

            foreach (Transform target in cachedDetection.DetectedTargets)
            {
                float distance = Vector2.Distance(
                    agentPos,
                    new Vector2(target.position.x, target.position.z));

                bool inRange = operation switch
                {
                    RangeCheckOp.LessThan    => distance < radius,
                    RangeCheckOp.GreaterThan => distance > radius,
                    _ => false
                };

                if (!inRange) continue;

                if (checkLineOfSight && !cachedDetection.HasLineOfSightToTarget(target))
                    continue;

                return NodeState.SUCCESS;
            }

            return NodeState.FAILURE;
        }
    }
}
