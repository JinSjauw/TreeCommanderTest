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
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Radius",
                label = "Radius",
                kind = DynamicParamKind.ScriptableObjectConstant,
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
        private EnemyDetectionSystem cachedDetection;
        private Transform cachedAgentTransform;

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

        protected override void OnInitialize()
        {
            cachedDetection = GetComponentFromBB<EnemyDetectionSystem>();
            NavMeshAgent agent = GetComponentFromBB<NavMeshAgent>();
            if (agent != null) cachedAgentTransform = agent.transform;
        }

        public override NodeState Execute()
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
