using System.Collections.Generic;
using BehaviourTree.Core;
using NUnit.Framework;

namespace BehaviourTree.Tests
{
    public class SlotVersionTests
    {
        private static TypedBlackboardStorage CreateStorage()
        {
            var s = new TypedBlackboardStorage();
            s.Initialize(new List<BlackboardVariableBase>
            {
                new BlackboardVariable<float>("f", 1, 1f),   // slot 0
                new BlackboardVariable<int>("i", 2, 0),      // slots 1-2
            });
            return s;
        }

        [Test]
        public void Versions_StartAtZero_AfterInitializeAndSeeding()
        {
            TypedBlackboardStorage s = CreateStorage();
            Assert.AreEqual(0, s.GetSlotVersion(0));
            Assert.AreEqual(0, s.GetSlotVersion(1));
        }

        [Test]
        public void TypedSetter_BumpsOnlyThatSlot()
        {
            TypedBlackboardStorage s = CreateStorage();
            ((IBlackboardTypedAccess)s).SetFloat(0, 2f);
            Assert.AreEqual(1, s.GetSlotVersion(0));
            Assert.AreEqual(0, s.GetSlotVersion(1));
        }

        [Test]
        public void GenericSet_AndBoxedSet_Bump()
        {
            TypedBlackboardStorage s = CreateStorage();
            s.Set(0, 3f);
            s.SetBoxed(1, 7);
            Assert.AreEqual(1, s.GetSlotVersion(0));
            Assert.AreEqual(1, s.GetSlotVersion(1));
        }

        [Test]
        public void Reads_DoNotBump()
        {
            TypedBlackboardStorage s = CreateStorage();
            _ = s.Get<float>(0);
            _ = s.GetBoxed(1);
            Assert.AreEqual(0, s.GetSlotVersion(0));
            Assert.AreEqual(0, s.GetSlotVersion(1));
        }

        [Test]
        public void CopySlotsFrom_BumpsDestinationRange()
        {
            TypedBlackboardStorage src = CreateStorage();
            TypedBlackboardStorage dst = CreateStorage();
            dst.CopySlotsFrom(src, 1, 1, 2); // copies slots 1-2
            Assert.AreEqual(0, dst.GetSlotVersion(0));
            Assert.AreEqual(1, dst.GetSlotVersion(1));
            Assert.AreEqual(1, dst.GetSlotVersion(2));
        }
    }
}
