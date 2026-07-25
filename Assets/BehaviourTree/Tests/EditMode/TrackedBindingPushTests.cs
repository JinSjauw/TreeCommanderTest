using BehaviourTree.Core;
using BehaviourTree.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class TrackedBindingPushTests
    {
        private class StubComp : MonoBehaviour
        {
            public float health;
            public int count;
        }

        private GameObject compGo;
        private GameObject bbGo;
        private BlackBoard bb;
        private BlackboardDefinition def;

        [SetUp]
        public void SetUp()
        {
            compGo = new GameObject("comp");
            def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<float>("health", 1, 0f)); // slot 0
            def.sharedVariables.Add(new BlackboardVariable<int>("count", 1, 0));      // slot 1
            bbGo = new GameObject("bb");
            bb = bbGo.AddComponent<BlackBoard>();
            bb.Initialize(def);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(compGo);
            Object.DestroyImmediate(bbGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void FloatMember_PushesThroughTypedAccessor()
        {
            StubComp comp = compGo.AddComponent<StubComp>();
            comp.health = 88f;

            var binding = new TrackedBinding
            {
                targetComponent = comp,
                memberName = nameof(StubComp.health),
                isProperty = false
            };
            binding.cachedFieldInfo = typeof(StubComp).GetField(nameof(StubComp.health));

            BehaviourTreeRunnerBase.CompileTrackedBindingDelegate(binding);

            Assert.IsNotNull(binding.typedPushDelegate);
            binding.typedPushDelegate(bb, 0);
            Assert.AreEqual(88f, bb.GetFloat(0));
        }

        [Test]
        public void IntMember_PushesThroughTypedAccessor()
        {
            StubComp comp = compGo.AddComponent<StubComp>();
            comp.count = 12;

            var binding = new TrackedBinding
            {
                targetComponent = comp,
                memberName = nameof(StubComp.count),
                isProperty = false
            };
            binding.cachedFieldInfo = typeof(StubComp).GetField(nameof(StubComp.count));

            BehaviourTreeRunnerBase.CompileTrackedBindingDelegate(binding);

            Assert.IsNotNull(binding.typedPushDelegate);
            binding.typedPushDelegate(bb, 1);
            Assert.AreEqual(12, bb.GetInt(1));
        }
    }
}
