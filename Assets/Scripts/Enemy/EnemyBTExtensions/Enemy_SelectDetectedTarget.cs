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
                titleLabel = "Selection Strategy",
                label = "Selection Strategy",
                kind = DynamicParamKind.Operation,
                index = 0,
                operationEnumType = typeof(SelectionStrategy)
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Output",
                label = "Output Slot",
                kind = DynamicParamKind.Variable,
                index = 1,
                allowedTypes = new[] { typeof(Transform), typeof(GameObject) }
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

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant) 
            {
                strategy = (SelectionStrategy)fields[fieldIndex].value;
                fieldIndex++;
            }
            
            // Read fieldTypeNames BEFORE ReadVariableSlot, since it advances fieldIndex
            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
            {
                FieldTypeHelper.TryGetSystemTypeFromName(fieldTypeNames[fieldIndex], out outputType);
            }

            targetSlot = ReadVariableSlot(fields, ref fieldIndex);
        }

        protected override void OnInitialize()
        {
            cachedDetection = GetComponentFromBB<EnemyDetectionSystem>();
        }

        public override NodeState Execute(TickContext ctx)
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
                BB.SetBoxed(targetSlot, null);
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
