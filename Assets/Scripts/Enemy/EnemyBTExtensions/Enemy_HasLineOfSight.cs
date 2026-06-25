using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Checks whether there is a clear line of sight between the enemy's eye
    /// transform and the target Transform via Physics.Linecast.
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

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            if (fields.Length >= 1 && fields[0].IsVariable)
                targetSlot = fields[0].value;
        }

        protected override void OnInitialize()
        {
            cachedDetection = GetComponentFromBB<EnemyDetectionSystem>();
        }

        public override NodeState Execute(TickContext ctx)
        {
            if (targetSlot < 0 || cachedDetection == null) return NodeState.FAILURE;

            object targetObj = BB.GetBoxed(targetSlot);
            Transform target = targetObj as Transform;
            if (target == null) return NodeState.FAILURE;

            return cachedDetection.HasLineOfSightToTarget(target)
                ? NodeState.SUCCESS
                : NodeState.FAILURE;
        }
    }
}
