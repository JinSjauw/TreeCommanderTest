using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class CopySlotTests
    {
        [Test]
        public void CopySlot_SameType_CopiesValue()
        {
            var def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<float>("a", 1, 1.5f)); // slot 0
            def.sharedVariables.Add(new BlackboardVariable<float>("b", 1, 0f));   // slot 1
            var go = new GameObject("bb");
            try
            {
                var bb = go.AddComponent<BlackBoard>();
                bb.Initialize(def);
                bb.CopySlot(0, 1);
                Assert.AreEqual(1.5f, bb.GetFloat(1));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(def); }
        }

        [Test]
        public void CopySlot_MismatchedTypes_ConvertsThroughBoxedFallback()
        {
            var def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<int>("i", 1, 7));   // slot 0
            def.sharedVariables.Add(new BlackboardVariable<float>("f", 1, 0f)); // slot 1
            var go = new GameObject("bb");
            try
            {
                var bb = go.AddComponent<BlackBoard>();
                bb.Initialize(def);
                bb.CopySlot(0, 1); // int slot -> float slot: boxed Convert.ToSingle path
                Assert.AreEqual(7f, bb.GetFloat(1));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(def); }
        }
    }
}
