using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Runs the enemy's internal detection system and checks whether any detected
    /// target satisfies the range check (and optionally line-of-sight).
    /// Returns SUCCESS if at least one target passes all active checks.
    /// </summary>
    [NodeMethod("Enemy_Detected", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_Detected : ConditionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Radius",
                label = "Radius",
                kind = DynamicParamKind.Constant,
                index = 0,
                allowedTypes = new[] { typeof(float) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Operation",
                label = "Operation",
                kind = DynamicParamKind.Operation,
                index = 1,
                operationEnumType = typeof(RangeCheckOp)
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Check LOS",
                label = "Check LOS",
                kind = DynamicParamKind.Constant,
                index = 2,
                allowedTypes = new[] { typeof(bool) }
            },
        };

        private float radius;
        private RangeCheckOp operation;
        private bool checkLineOfSight;
        private EnemyController cachedController;
        private EnemyDetectionSystem cachedDetectionSystem;
        private Transform cachedAgentTransform;
        private bool controllerResolved;

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            int fieldIndex = 0;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                radius = fields[fieldIndex++].GetFloat();

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                operation = (RangeCheckOp)fields[fieldIndex++].value;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                checkLineOfSight = fields[fieldIndex].GetBool();
        }

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

            if (cachedDetectionSystem == null)
            {
                cachedDetectionSystem = cachedController.Detection;
            }

            if (cachedAgentTransform == null)
            {
                cachedAgentTransform = cachedController.Agent.transform;
            }

            if (cachedDetectionSystem == null || cachedAgentTransform == null) return NodeState.FAILURE;

            if (!cachedDetectionSystem.DetectTargets(radius))
                return NodeState.FAILURE;

            Vector2 agentPos = new Vector2(
                cachedAgentTransform.position.x,
                cachedAgentTransform.position.z);

            foreach (Transform target in cachedDetectionSystem.DetectedTargets)
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

                if (checkLineOfSight && !cachedController.HasLineOfSightToTarget(target))
                    continue;

                return NodeState.SUCCESS;
            }

            return NodeState.FAILURE;
        }
    }
}
