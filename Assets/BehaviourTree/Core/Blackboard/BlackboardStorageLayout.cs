using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>Identifies which typed array a blackboard slot physically lives in.</summary>
    public enum BlackboardArrayId : byte
    {
        Object = 0,
        Int = 1,
        Float = 2,
        Bool = 3,
        Vector2 = 4,
        Vector3 = 5,
        Vector4 = 6,
        Color = 7,
        Quaternion = 8,
        Count // sentinel — length of the array-size table
    }

    /// <summary>Where a virtual slot's value physically lives.</summary>
    public readonly struct SlotLocation : IEquatable<SlotLocation>
    {
        public readonly BlackboardArrayId ArrayId;
        public readonly int LocalIndex;

        public SlotLocation(BlackboardArrayId arrayId, int localIndex)
        {
            ArrayId = arrayId;
            LocalIndex = localIndex;
        }

        public bool Equals(SlotLocation other) => ArrayId == other.ArrayId && LocalIndex == other.LocalIndex;
        public override bool Equals(object obj) => obj is SlotLocation other && Equals(other);
        public override int GetHashCode() => ((int)ArrayId << 24) ^ LocalIndex;
    }

    /// <summary>
    /// Builds the virtual-slot → typed-array mapping for a variable list.
    /// Virtual slot numbering is IDENTICAL to the legacy flat object[] layout
    /// (variable order, stride expansion). Only physical storage changes.
    /// </summary>
    public static class BlackboardStorageLayout
    {
        public const int ArrayCount = (int)BlackboardArrayId.Count;

        public static BlackboardArrayId Classify(Type type)
        {
            if (type == null) return BlackboardArrayId.Object;
            if (type == typeof(float)) return BlackboardArrayId.Float;
            if (type == typeof(bool)) return BlackboardArrayId.Bool;
            if (type == typeof(int)) return BlackboardArrayId.Int;
            if (type.IsEnum)
            {
                // Only int32-backed enums go to the int array; exotic backing
                // types stay boxed in the object array.
                return Enum.GetUnderlyingType(type) == typeof(int)
                    ? BlackboardArrayId.Int
                    : BlackboardArrayId.Object;
            }
            if (type == typeof(Vector2)) return BlackboardArrayId.Vector2;
            if (type == typeof(Vector3)) return BlackboardArrayId.Vector3;
            if (type == typeof(Vector4)) return BlackboardArrayId.Vector4;
            if (type == typeof(Color)) return BlackboardArrayId.Color;
            if (type == typeof(Quaternion)) return BlackboardArrayId.Quaternion;
            return BlackboardArrayId.Object;
        }

        public static void Build(
            IReadOnlyList<BlackboardVariableBase> variables,
            out SlotLocation[] map,
            out Type[] slotTypes,
            out BlackboardSlotKind[] slotKinds,
            out int[] arraySizes)
        {
            arraySizes = new int[ArrayCount];

            if (variables == null || variables.Count == 0)
            {
                map = null;
                slotTypes = null;
                slotKinds = null;
                return;
            }

            int totalSlots = 0;
            for (int i = 0; i < variables.Count; i++)
            {
                int stride = variables[i].Stride;
                totalSlots += (stride > 1) ? stride : 1;
            }

            map = new SlotLocation[totalSlots];
            slotTypes = new Type[totalSlots];
            slotKinds = new BlackboardSlotKind[totalSlots];

            // Single pass: arraySizes doubles as the next-free-local-index
            // counter while assigning, ending as the final per-array sizes.
            int slot = 0;
            for (int varIndex = 0; varIndex < variables.Count; varIndex++)
            {
                BlackboardVariableBase variable = variables[varIndex];
                Type type = variable.GetValueType();
                BlackboardArrayId arrayId = Classify(type);
                int stride = variable.Stride;
                int actualStride = (stride > 1) ? stride : 1;

                for (int element = 0; element < actualStride; element++)
                {
                    slotTypes[slot + element] = type;
                    slotKinds[slot + element] = (type != null && !type.IsValueType)
                        ? BlackboardSlotKind.Reference
                        : BlackboardSlotKind.Value;
                    map[slot + element] = new SlotLocation(arrayId, arraySizes[(int)arrayId]);
                    arraySizes[(int)arrayId]++;
                }

                slot += actualStride;
            }
        }
    }
}
