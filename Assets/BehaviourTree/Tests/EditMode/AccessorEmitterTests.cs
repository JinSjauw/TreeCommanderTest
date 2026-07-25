using BehaviourTree.Core;
using BehaviourTree.EditorTools.Codegen;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class AccessorEmitterTests
    {
        private enum TestState { Idle = 0, Running = 1 }

        private class EmitterStubMethod : ActionMethod
        {
            public float speed;
            public TestState state;
            public Transform target;
            public string label;
            public override NodeState Execute(Runtime.TickContext ctx) => NodeState.SUCCESS;
        }

        private static string Emit(string fieldName)
        {
            var field = typeof(EmitterStubMethod).GetField(fieldName);
            return AccessorEmitter.EmitRegistration(typeof(EmitterStubMethod), field);
        }

        [Test]
        public void FloatField_UsesTypedAccessor()
        {
            string code = Emit(nameof(EmitterStubMethod.speed));
            StringAssert.Contains("bb.GetFloat(slot)", code);
            StringAssert.Contains("bb.SetFloat(slot,", code);
            StringAssert.Contains(".EmitterStubMethod)m).speed", code); // global:: full name
            StringAssert.Contains("typeof(float)", code);
        }

        [Test]
        public void EnumField_ReadsInt_WithCastBack()
        {
            string code = Emit(nameof(EmitterStubMethod.state));
            StringAssert.Contains("TestState)bb.GetInt(slot)", code); // cast back, full name prefix
            StringAssert.Contains("bb.SetInt(slot, (int)", code);
        }

        [Test]
        public void RefField_UsesGenericAccessors()
        {
            string code = Emit(nameof(EmitterStubMethod.target));
            StringAssert.Contains("bb.Get<global::UnityEngine.Transform>(slot)", code);
            StringAssert.Contains("bb.Set(slot,", code);
            StringAssert.Contains(".EmitterStubMethod)m).target", code);
        }

        [Test]
        public void StringField_UsesGenericAccessors()
        {
            string code = Emit(nameof(EmitterStubMethod.label));
            StringAssert.Contains("bb.Get<string>(slot)", code); // string is special-cased to the keyword
            StringAssert.Contains("bb.Set(slot,", code);
            StringAssert.Contains(".EmitterStubMethod)m).label", code);
        }

        [Test]
        public void FloatField_ExactOutput_IsValidArgumentList()
        {
            // Pins the exact 3-line shape: expression lambdas WITHOUT trailing
            // semicolons, comma-separated inside the R(...) argument list.
            string typeName = AccessorEmitter.FormatTypeName(typeof(EmitterStubMethod));
            string expected =
                $"            R(typeof({typeName}), \"speed\", typeof(float),\n" +
                $"                (m, bb, slot) => (({typeName})m).speed = bb.GetFloat(slot),\n" +
                $"                (m, bb, slot) => bb.SetFloat(slot, (({typeName})m).speed));\n";

            Assert.AreEqual(expected, Emit(nameof(EmitterStubMethod.speed)));
        }

        private class TrackedStubComp : MonoBehaviour { public float health; }

        [Test]
        public void TrackedMember_ExactOutput_IsValidArgumentList()
        {
            string compName = AccessorEmitter.FormatTypeName(typeof(TrackedStubComp));
            string expected =
                $"            TR(typeof({compName}), \"health\", typeof(float),\n" +
                $"                (c, bb, slot) => bb.SetFloat(slot, (({compName})c).health));\n";

            Assert.AreEqual(expected,
                AccessorEmitter.EmitTrackedRegistration(typeof(TrackedStubComp),
                    typeof(TrackedStubComp).GetField(nameof(TrackedStubComp.health))));
        }
    }
}
