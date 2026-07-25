using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class TrackedAccessorRegistryTests
    {
        private class StubComp : MonoBehaviour { public float health; public string title; }

        [TearDown]
        public void TearDown() => TrackedAccessorRegistry.Clear();

        [Test]
        public void TryGet_Miss_ReturnsFalse()
        {
            Assert.IsFalse(TrackedAccessorRegistry.TryGet(typeof(StubComp), "health", typeof(float), out _));
        }

        [Test]
        public void Register_Hit_PushesValue()
        {
            TrackedAccessorRegistry.Register(typeof(StubComp), "health", typeof(float),
                (c, bb, slot) => bb.SetFloat(slot, ((StubComp)c).health));

            bool found = TrackedAccessorRegistry.TryGet(typeof(StubComp), "health", typeof(float),
                out TrackedAccessorRegistry.PushAccessor push);

            Assert.IsTrue(found);

            var def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<float>("health", 1, 0f));
            var compGo = new GameObject("comp");
            var bbGo = new GameObject("bb");
            try
            {
                var bb = bbGo.AddComponent<BlackBoard>();
                bb.Initialize(def);
                StubComp comp = compGo.AddComponent<StubComp>();
                comp.health = 55f;

                push(comp, bb, 0);
                Assert.AreEqual(55f, bb.GetFloat(0));
            }
            finally
            {
                Object.DestroyImmediate(compGo);
                Object.DestroyImmediate(bbGo);
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void StaleMemberType_Misses()
        {
            TrackedAccessorRegistry.Register(typeof(StubComp), "health", typeof(int),
                (c, bb, slot) => { });
            Assert.IsFalse(TrackedAccessorRegistry.TryGet(typeof(StubComp), "health", typeof(float), out _));
        }
    }
}
