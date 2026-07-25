using System.Collections.Generic;
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class BlackboardStorageLayoutTests
    {
        private enum IntEnum { A = 0, B = 1 }
        private enum LongEnum : long { A = 0, B = 1 }
        private struct CustomStruct { public int x; }

        [Test]
        public void Classify_KnownTypes()
        {
            Assert.AreEqual(BlackboardArrayId.Float, BlackboardStorageLayout.Classify(typeof(float)));
            Assert.AreEqual(BlackboardArrayId.Int, BlackboardStorageLayout.Classify(typeof(int)));
            Assert.AreEqual(BlackboardArrayId.Bool, BlackboardStorageLayout.Classify(typeof(bool)));
            Assert.AreEqual(BlackboardArrayId.Vector2, BlackboardStorageLayout.Classify(typeof(Vector2)));
            Assert.AreEqual(BlackboardArrayId.Vector3, BlackboardStorageLayout.Classify(typeof(Vector3)));
            Assert.AreEqual(BlackboardArrayId.Vector4, BlackboardStorageLayout.Classify(typeof(Vector4)));
            Assert.AreEqual(BlackboardArrayId.Color, BlackboardStorageLayout.Classify(typeof(Color)));
            Assert.AreEqual(BlackboardArrayId.Quaternion, BlackboardStorageLayout.Classify(typeof(Quaternion)));
        }

        [Test]
        public void Classify_EnumsAndFallbacks()
        {
            Assert.AreEqual(BlackboardArrayId.Int, BlackboardStorageLayout.Classify(typeof(IntEnum)));
            Assert.AreEqual(BlackboardArrayId.Object, BlackboardStorageLayout.Classify(typeof(LongEnum)));
            Assert.AreEqual(BlackboardArrayId.Object, BlackboardStorageLayout.Classify(typeof(CustomStruct)));
            Assert.AreEqual(BlackboardArrayId.Object, BlackboardStorageLayout.Classify(typeof(Transform)));
            Assert.AreEqual(BlackboardArrayId.Object, BlackboardStorageLayout.Classify(typeof(string)));
            Assert.AreEqual(BlackboardArrayId.Object, BlackboardStorageLayout.Classify(typeof(double)));
            Assert.AreEqual(BlackboardArrayId.Object, BlackboardStorageLayout.Classify(null));
        }

        [Test]
        public void Build_VirtualSlotsMatchLegacyFlatLayout()
        {
            var variables = new List<BlackboardVariableBase>
            {
                new BlackboardVariable<float>("health", 1, 0f),            // slot 0 → floats[0]
                new BlackboardVariable<int>("roles", 3, 0),                // slots 1-3 → ints[0..2]
                new BlackboardVariable<float>("speed", 1, 0f),             // slot 4 → floats[1]
                new BlackboardVariable<Transform>("target", 1, null),      // slot 5 → objects[0]
                new BlackboardVariable<IntEnum>("state", 1, IntEnum.A),    // slot 6 → ints[3]
            };

            BlackboardStorageLayout.Build(variables, out SlotLocation[] map, out System.Type[] slotTypes,
                out BlackboardSlotKind[] slotKinds, out int[] sizes);

            Assert.AreEqual(7, map.Length);
            Assert.AreEqual(new SlotLocation(BlackboardArrayId.Float, 0), map[0]);
            Assert.AreEqual(new SlotLocation(BlackboardArrayId.Int, 0), map[1]);
            Assert.AreEqual(new SlotLocation(BlackboardArrayId.Int, 1), map[2]);
            Assert.AreEqual(new SlotLocation(BlackboardArrayId.Int, 2), map[3]);
            Assert.AreEqual(new SlotLocation(BlackboardArrayId.Float, 1), map[4]);
            Assert.AreEqual(new SlotLocation(BlackboardArrayId.Object, 0), map[5]);
            Assert.AreEqual(new SlotLocation(BlackboardArrayId.Int, 3), map[6]);

            Assert.AreEqual(2, sizes[(int)BlackboardArrayId.Float]);
            Assert.AreEqual(4, sizes[(int)BlackboardArrayId.Int]);
            Assert.AreEqual(1, sizes[(int)BlackboardArrayId.Object]);

            Assert.AreEqual(BlackboardSlotKind.Reference, slotKinds[5]);
            Assert.AreEqual(BlackboardSlotKind.Value, slotKinds[6]); // enum = value type
            Assert.AreEqual(typeof(IntEnum), slotTypes[6]);
        }

        [Test]
        public void Build_NullAndEmpty_ProduceNullMap()
        {
            BlackboardStorageLayout.Build(null, out SlotLocation[] map, out _, out _, out int[] sizes);
            Assert.IsNull(map);
            Assert.AreEqual(BlackboardStorageLayout.ArrayCount, sizes.Length);

            BlackboardStorageLayout.Build(new List<BlackboardVariableBase>(), out map, out _, out _, out _);
            Assert.IsNull(map);
        }
    }
}
