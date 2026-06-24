using System;
using BehaviourTree.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BehaviourTree.Runtime.Methods
{
    public enum PatrolMode
    {
        Sequential,
        Random
    }

    /// <summary>
    /// Extracts a Vector3 position from a collection element.
    /// Accepts: Transform (children positions), GameObject (children positions),
    /// Transform[], GameObject[], Vector3[].
    /// Always outputs a Vector3.
    /// Supports Sequential and Random modes.
    /// </summary>
    [NodeMethod("ExtractPosition", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class ExtractPosition : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Collection",
                label = "Collection",
                kind = DynamicParamKind.Variable,
                index = 0,
                allowedTypes = new[] { typeof(Transform), typeof(GameObject),
                    typeof(Transform[]), typeof(GameObject[]), typeof(Vector3[]) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Output",
                label = "Output",
                kind = DynamicParamKind.Variable,
                index = 1,
                allowedTypes = new[] { typeof(Vector3) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Mode",
                label = "Mode",
                kind = DynamicParamKind.Operation,
                index = 2,
                operationEnumType = typeof(PatrolMode)
            },
        };

        private int inputSlot = -1;
        private int outputSlot = -1;
        private PatrolMode mode;
        private int lastIndex = -1;

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            int fieldIndex = 0;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
                inputSlot = fields[fieldIndex++].value;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
                outputSlot = fields[fieldIndex++].value;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                mode = (PatrolMode)fields[fieldIndex].value;
        }

        public override NodeState Execute()
        {
            if (inputSlot < 0 || outputSlot < 0) return NodeState.FAILURE;

            object input = BB.GetBoxed(inputSlot);
            if (input == null) return NodeState.FAILURE;

            int count = GetCount(input);
            if (count == 0) return NodeState.FAILURE;

            int index = GetIndex(count);
            Vector3 position = GetPositionAt(input, index);

            BB.SetBoxed(outputSlot, position);
            return NodeState.SUCCESS;
        }

        private int GetIndex(int count)
        {
            if (mode == PatrolMode.Random)
            {
                lastIndex = Random.Range(0, count);
                return lastIndex;
            }
            else
            {
                lastIndex = (lastIndex + 1) % count;
                return lastIndex;
            }
        }

        private static int GetCount(object collection)
        {
            if (collection == null) return 0;

            return collection switch
            {
                Transform t          => t.childCount,
                GameObject go        => go.transform.childCount,
                Transform[] tArr     => tArr.Length,
                GameObject[] goArr   => goArr.Length,
                Vector3[] vArr       => vArr.Length,
                _ => 0
            };
        }

        private static Vector3 GetPositionAt(object collection, int index)
        {
            return collection switch
            {
                Transform t          => index < t.childCount ? t.GetChild(index).position : Vector3.zero,
                GameObject go        => index < go.transform.childCount ? go.transform.GetChild(index).position : Vector3.zero,
                Transform[] tArr     => index < tArr.Length ? tArr[index].position : Vector3.zero,
                GameObject[] goArr   => index < goArr.Length ? goArr[index].transform.position : Vector3.zero,
                Vector3[] vArr       => index < vArr.Length ? vArr[index] : Vector3.zero,
                _ => Vector3.zero
            };
        }
    }
}
