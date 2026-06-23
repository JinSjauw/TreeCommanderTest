using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Checks whether there is a clear line of sight (no obstacles) between
    /// the enemy's eye transform and the target Transform.
    /// Uses Physics.Linecast against the Ground layer.
    /// </summary>
    [NodeMethod("Enemy_HasLineOfSight", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_HasLineOfSight : ConditionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Target",
                label = "Target",
                kind = DynamicParamKind.Variable,
                index = 0,
                allowedTypes = new[] { typeof(Transform) }
            },
        };

        private int targetSlot = -1;
        private EnemyDetectionSystem cachedDetection;
        private bool detectionResolved;

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            if (fields.Length >= 1 && fields[0].IsVariable)
                targetSlot = fields[0].value;
        }

        public override NodeState Execute()
        {
            if (targetSlot < 0) return NodeState.FAILURE;

            if (!detectionResolved)
            {
                cachedDetection = ((MonoBehaviour)BB).GetComponent<EnemyDetectionSystem>();
                if (cachedDetection == null)
                    cachedDetection = ((MonoBehaviour)BB).GetComponentInChildren<EnemyDetectionSystem>();
                detectionResolved = true;
            }
            if (cachedDetection == null) return NodeState.FAILURE;

            object targetObj = BB.GetBoxed(targetSlot);
            Transform target = targetObj as Transform;
            if (target == null) return NodeState.FAILURE;

            return cachedDetection.HasLineOfSightToTarget(target)
                ? NodeState.SUCCESS
                : NodeState.FAILURE;
        }
    }
}
