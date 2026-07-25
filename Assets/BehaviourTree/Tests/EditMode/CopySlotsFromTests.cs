using System.Collections.Generic;
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class CopySlotsFromTests
    {
        private enum TestState { Idle = 0, Running = 1 }

        private static List<BlackboardVariableBase> BuildVariables()
        {
            return new List<BlackboardVariableBase>
            {
                new BlackboardVariable<float>("f", 2, 0f),                 // slots 0-1
                new BlackboardVariable<int>("roles", 4, 0),                // slots 2-5
                new BlackboardVariable<TestState>("state", 1, TestState.Idle), // slot 6
                new BlackboardVariable<string>("label", 1, ""),            // slot 7
            };
        }

        private static TypedBlackboardStorage CreateStorage()
        {
            var s = new TypedBlackboardStorage();
            s.Initialize(BuildVariables());
            return s;
        }

        [Test]
        public void RegionCopy_CopiesAllValuesAcrossArrays()
        {
            TypedBlackboardStorage src = CreateStorage();
            TypedBlackboardStorage dst = CreateStorage();

            src.Set(0, 1.5f);
            src.Set(1, 2.5f);
            src.Set(2, 10);
            src.Set(5, 40);
            src.Set(6, TestState.Running);
            src.Set(7, "copied");

            dst.CopySlotsFrom(src, 0, 0, 8);

            Assert.AreEqual(1.5f, dst.Get<float>(0));
            Assert.AreEqual(2.5f, dst.Get<float>(1));
            Assert.AreEqual(10, dst.Get<int>(2));
            Assert.AreEqual(40, dst.Get<int>(5));
            Assert.AreEqual(TestState.Running, dst.Get<TestState>(6));
            Assert.AreEqual("copied", dst.Get<string>(7));
        }

        [Test]
        public void SelfCopy_OverlappingRange_CompactsLikeMemmove()
        {
            // Agent-compaction pattern: shift elements down by one within a stride region.
            TypedBlackboardStorage s = CreateStorage();
            s.Set(2, 1);
            s.Set(3, 2);
            s.Set(4, 3);
            s.Set(5, 4);

            // Remove element at local index 0 (slot 2): copy [3..5] down to [2..4].
            s.CopySlotsFrom(s, 3, 2, 3);

            Assert.AreEqual(2, s.Get<int>(2));
            Assert.AreEqual(3, s.Get<int>(3));
            Assert.AreEqual(4, s.Get<int>(4));
        }

        [Test]
        public void DifferentLayoutOrder_CopiesToCorrectTypedArray()
        {
            // Different layout: same variables in different order, so the same
            // value type sits at different virtual slots. Run-detection must
            // land values in the right typed array regardless of slot numbering.
            TypedBlackboardStorage src = CreateStorage();

            var dstVars = new List<BlackboardVariableBase>
            {
                new BlackboardVariable<string>("label", 1, ""),            // slot 0
                new BlackboardVariable<float>("f", 2, 0f),                 // slots 1-2
                new BlackboardVariable<int>("roles", 4, 0),                // slots 3-6
                new BlackboardVariable<TestState>("state", 1, TestState.Idle), // slot 7
            };
            var dst = new TypedBlackboardStorage();
            dst.Initialize(dstVars);

            src.Set(0, 7.5f);                // src float slot 0
            dst.CopySlotsFrom(src, 0, 1, 1); // dst float slot is 1

            Assert.AreEqual(7.5f, dst.Get<float>(1));
        }
    }
}
