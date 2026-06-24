using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Runs target detection and selects one target by strategy.
    /// Writes the selected target to a blackboard variable (Transform or GameObject).
    /// </summary>
    [NodeMethod("Enemy_SelectDetectedTarget", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_SelectDetectedTarget : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Output",
                label = "Output Slot",
                kind = DynamicParamKind.Variable,
                index = 0,
                allowedTypes = new[] { typeof(Transform), typeof(GameObject) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Selection Strategy",
                label = "Selection Strategy",
                kind = DynamicParamKind.Operation,
                index = 1,
                operationEnumType = typeof(SelectionStrategy)
            },
        };

        private SelectionStrategy strategy;
        private int targetSlot = -1;
        private Type outputType;
        private EnemyDetectionSystem cachedDetection;

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            int fieldIndex = 0;

            // Read fieldTypeNames BEFORE ReadVariableSlot, since it advances fieldIndex
            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
                FieldTypeHelper.TryGetSystemTypeFromName(fieldTypeNames[fieldIndex], out outputType);

            targetSlot = ReadVariableSlot(fields, ref fieldIndex);

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                strategy = (SelectionStrategy)fields[fieldIndex].value;
        }

        protected override void OnInitialize()
        {
            cachedDetection = GetComponentFromBB<EnemyDetectionSystem>();
        }

        public override NodeState Execute()
        {
            if (targetSlot < 0 || outputType == null || cachedDetection == null)
            {
                Debug.LogError($"Enemy_SelectDetectedTarget: Invalid parameters {targetSlot}, {outputType}, {cachedDetection}");
                return NodeState.FAILURE;
            }

            if (!cachedDetection.DetectTargets())
            {
                Debug.Log($"No targets detected for {strategy}");
                return NodeState.FAILURE;
            }

            Transform selected = cachedDetection.GetTarget(strategy);
            if (selected == null) 
            {
                Debug.LogError($"Enemy_SelectDetectedTarget: No target selected for {strategy}");
                return NodeState.FAILURE;
            }
            object result = outputType == typeof(GameObject)
                ? selected.gameObject
                : selected;

            BB.SetBoxed(targetSlot, result);
            return NodeState.SUCCESS;
        }
    }
}
