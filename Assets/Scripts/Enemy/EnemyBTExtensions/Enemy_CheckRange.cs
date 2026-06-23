using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Checks whether the 2D distance between the enemy's NavMeshAgent and a target
    /// Transform satisfies the given comparison against a radius.
    /// Radius can be a constant float or a blackboard float variable (C/V toggle).
    /// </summary>
    [NodeMethod("Enemy_CheckRange", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_CheckRange : ConditionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Target",
                label = "Input",
                kind = DynamicParamKind.Variable,
                index = 0,
                allowedTypes = new[] { typeof(Transform), typeof(GameObject), typeof(Vector3) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Radius",
                label = "Value",
                kind = DynamicParamKind.Toggle,
                index = 1,
                allowedTypes = new[] { typeof(float) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Operation",
                label = "Operation",
                kind = DynamicParamKind.Operation,
                index = 2,
                operationEnumType = typeof(RangeCheckOp)
            },
        };

        private int targetSlot = -1;
        private int radiusSlot = -1;
        private RangeCheckOp operation;
        
        private float constantRadius;
        private bool hasConstantRadius;
        private EnemyController cachedController;
        private bool controllerResolved;

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            int fieldIndex = 0;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
                targetSlot = fields[fieldIndex++].value;

            if (fieldIndex < fields.Length)
            {
                if (fields[fieldIndex].IsVariable)
                {
                    radiusSlot = fields[fieldIndex++].value;
                }
                else
                {
                    hasConstantRadius = true;
                    constantRadius = fields[fieldIndex].GetFloat();
                    fieldIndex++;
                }
            }

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                operation = (RangeCheckOp)fields[fieldIndex].value;
        }

        public override NodeState Execute()
        {
            if (targetSlot < 0) return NodeState.FAILURE;

            if (!controllerResolved)
            {
                BlackBoard bb = BB as BlackBoard;
                if (bb != null)
                    cachedController = bb.GetComponent<EnemyController>();
                controllerResolved = true;
            }
            if (cachedController == null) return NodeState.FAILURE;

            object targetObj = BB.GetBoxed(targetSlot);
            Vector3 target;
            if (targetObj is Transform transform)
                target = transform.position;
            else if (targetObj is GameObject gameObject)
                target = gameObject.transform.position;
            else if (targetObj is Vector3 vector)
                target = vector;
            else
                return NodeState.FAILURE;

            float radius;
            if (radiusSlot >= 0)
            {
                object radiusObj = BB.GetBoxed(radiusSlot);
                radius = radiusObj is float f ? f : 0f;
            }
            else if (hasConstantRadius)
            {
                radius = constantRadius;
            }
            else
            {
                return NodeState.FAILURE;
            }

            float distance = Vector2.Distance(
                new Vector2(cachedController.Agent.transform.position.x, cachedController.Agent.transform.position.z),
                new Vector2(target.x, target.z));

            bool result = operation switch
            {
                RangeCheckOp.LessThan    => distance < radius,
                RangeCheckOp.GreaterThan => distance > radius,
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }
}
