using System.Collections.Generic;
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    /// <summary>
    /// Contract tests every IBlackboardStorage implementation must satisfy.
    /// One TestFixture attribute per implementation.
    /// </summary>
    [TestFixture(typeof(ManagedBlackboardStorage))]
    [TestFixture(typeof(TypedBlackboardStorage))]
    public class BlackboardStorageContractTests<TStorage> where TStorage : IBlackboardStorage, new()
    {
        private enum TestState { Idle = 0, Running = 1 }

        private static List<BlackboardVariableBase> BuildVariables()
        {
            return new List<BlackboardVariableBase>
            {
                new BlackboardVariable<float>("health", 1, 100f),                  // slot 0
                new BlackboardVariable<int>("roles", 3, 7),                        // slots 1-3
                new BlackboardVariable<Vector3>("targetPos", 1, Vector3.one),      // slot 4
                new BlackboardVariable<string>("label", 1, "hello"),               // slot 5 (reference kind)
                new BlackboardVariable<TestState>("state", 1, TestState.Running),  // slot 6 (enum)
            };
        }

        private static TStorage CreateStorage()
        {
            var storage = new TStorage();
            storage.Initialize(BuildVariables());
            return storage;
        }

        [Test]
        public void Layout_SlotCountAndRangesMatchStrideExpansion()
        {
            TStorage storage = CreateStorage();
            Assert.AreEqual(7, storage.Count);

            storage.GetVariableSlotRange(0, out int baseSlot0, out int stride0);
            Assert.AreEqual(0, baseSlot0);
            Assert.AreEqual(1, stride0);

            storage.GetVariableSlotRange(1, out int baseSlot1, out int stride1);
            Assert.AreEqual(1, baseSlot1);
            Assert.AreEqual(3, stride1);

            storage.GetVariableSlotRange(4, out int baseSlot4, out int stride4);
            Assert.AreEqual(6, baseSlot4);
            Assert.AreEqual(1, stride4);
        }

        [Test]
        public void Seed_InitialValuesArePresent()
        {
            TStorage storage = CreateStorage();
            Assert.AreEqual(100f, storage.Get<float>(0));
            Assert.AreEqual(7, storage.Get<int>(1));
            Assert.AreEqual(7, storage.Get<int>(3)); // stride element
            Assert.AreEqual(Vector3.one, storage.Get<Vector3>(4));
            Assert.AreEqual("hello", storage.Get<string>(5));
            Assert.AreEqual(TestState.Running, storage.Get<TestState>(6));
        }

        [Test]
        public void TypedRoundtrip_ValueTypesAndRefs()
        {
            TStorage storage = CreateStorage();
            storage.Set(0, 42.5f);
            storage.Set(2, 99);
            storage.Set(4, new Vector3(1, 2, 3));
            storage.Set(5, "world");
            storage.Set(6, TestState.Idle);

            Assert.AreEqual(42.5f, storage.Get<float>(0));
            Assert.AreEqual(99, storage.Get<int>(2));
            Assert.AreEqual(new Vector3(1, 2, 3), storage.Get<Vector3>(4));
            Assert.AreEqual("world", storage.Get<string>(5));
            Assert.AreEqual(TestState.Idle, storage.Get<TestState>(6));
        }

        [Test]
        public void BoxedRoundtrip_EnumComesBackAsEnumNotInt()
        {
            TStorage storage = CreateStorage();
            storage.SetBoxed(6, TestState.Idle);
            object boxed = storage.GetBoxed(6);
            Assert.IsInstanceOf<TestState>(boxed);
            Assert.AreEqual(TestState.Idle, boxed);
        }

        [Test]
        public void BoxedRoundtrip_StrideElements()
        {
            TStorage storage = CreateStorage();
            storage.SetBoxed(1, 10);
            storage.SetBoxed(2, 20);
            storage.SetBoxed(3, 30);
            Assert.AreEqual(10, storage.GetBoxed(1));
            Assert.AreEqual(20, storage.GetBoxed(2));
            Assert.AreEqual(30, storage.GetBoxed(3));
        }

        [Test]
        public void TypeMismatch_SetIsRejected()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                TStorage storage = CreateStorage();
                storage.Set(0, 123); // int into float slot
                Assert.AreEqual(100f, storage.Get<float>(0)); // unchanged
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void SlotKinds_ValueAndReference()
        {
            TStorage storage = CreateStorage();
            Assert.AreEqual(BlackboardSlotKind.Value, storage.GetSlotKind(0));
            Assert.AreEqual(BlackboardSlotKind.Value, storage.GetSlotKind(6)); // enum = value type
            Assert.AreEqual(BlackboardSlotKind.Reference, storage.GetSlotKind(5)); // string
        }

        [Test]
        public void GetT_ConvertsStoredFloatToInt()
        {
            TStorage storage = CreateStorage();
            storage.Set(0, 5f);
            Assert.AreEqual(5, storage.Get<int>(0));
        }

        [Test]
        public void SetBoxed_NullOnValueSlotRejected_NullOnRefSlotAccepted()
        {
            TStorage storage = CreateStorage();
            storage.SetBoxed(0, null);
            Assert.AreEqual(100f, storage.Get<float>(0));

            storage.SetBoxed(5, null);
            Assert.IsNull(storage.GetBoxed(5));
        }

        [Test]
        public void OutOfRange_GetReturnsDefault()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                TStorage storage = CreateStorage();
                Assert.AreEqual(0f, storage.Get<float>(999));
                Assert.IsNull(storage.GetBoxed(999));
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
