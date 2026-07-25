using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class BlackBoardTypedAccessTests
    {
        private GameObject go;
        private BlackBoard bb;
        private BlackboardDefinition def;

        [SetUp]
        public void SetUp()
        {
            def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<float>("health", 1, 50f));   // slot 0
            def.sharedVariables.Add(new BlackboardVariable<int>("agentRoles", 4, 0));     // slots 1-4
            def.sharedVariables.Add(new BlackboardVariable<Transform>("target", 1, null)); // slot 5

            go = new GameObject("bb-test");
            bb = go.AddComponent<BlackBoard>();
            bb.Initialize(def);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void TypedAccess_NoOffset_ReadsExactSlot()
        {
            Assert.AreEqual(50f, bb.GetFloat(0));
            bb.SetFloat(0, 75f);
            Assert.AreEqual(75f, bb.GetFloat(0));
        }

        [Test]
        public void TypedAccess_WithAgentOffset_ReadsOffsetSlot()
        {
            IBlackboardTypedAccess raw = (IBlackboardTypedAccess)bb.Storage;
            raw.SetInt(2, 42); // raw slot 2 = agentRoles element 1

            bb.currentAgentOffset = 1;
            Assert.AreEqual(42, bb.GetInt(1)); // agentRoles base + offset = raw 2

            bb.SetInt(1, 99);
            Assert.AreEqual(99, raw.GetInt(2)); // write also applies offset

            bb.currentAgentOffset = 0;
        }

        [Test]
        public void SetObject_RefSlot_Roundtrips()
        {
            var targetGo = new GameObject("target");
            try
            {
                bb.SetObject(5, targetGo.transform);
                Assert.AreEqual(targetGo.transform, bb.GetObject<Transform>(5));
                // generic + boxed paths see the same value
                Assert.AreEqual(targetGo.transform, bb.Get<Transform>(5));
                Assert.AreEqual(targetGo.transform, bb.GetBoxed(5));
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void GenericAccess_StillWorks_AlongsideTyped()
        {
            bb.SetFloat(0, 12.5f);
            Assert.AreEqual(12.5f, bb.Get<float>(0));

            bb.Set(0, 33f);
            Assert.AreEqual(33f, bb.GetFloat(0));
        }
    }
}
