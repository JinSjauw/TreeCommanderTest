using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class FieldBindingTypedAccessTests
    {
        private enum TestState { Idle = 0, Running = 1 }

        private class FieldBindingStubMethod : ActionMethod
        {
            public float speed;
            public TestState state;
            public string label; // object-array path
            public override NodeState Execute(Runtime.TickContext ctx) => NodeState.SUCCESS;
        }

        private GameObject go;
        private BlackBoard bb;
        private BlackboardDefinition def;

        [SetUp]
        public void SetUp()
        {
            def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<float>("speed", 1, 0f));      // slot 0
            def.sharedVariables.Add(new BlackboardVariable<TestState>("state", 1, TestState.Idle)); // slot 1
            def.sharedVariables.Add(new BlackboardVariable<string>("label", 1, ""));      // slot 2

            go = new GameObject("fb-test");
            bb = go.AddComponent<BlackBoard>();
            bb.Initialize(def);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(def);
        }

        private static FieldBinding MakeBinding(string fieldName, int slot)
        {
            return new FieldBinding
            {
                fieldInfo = typeof(FieldBindingStubMethod).GetField(fieldName),
                bbSlotIndex = slot,
                isOutput = true
            };
        }

        [Test]
        public void FloatField_ReadWrite_NoBoxingPath()
        {
            var method = new FieldBindingStubMethod();
            FieldBinding binding = MakeBinding(nameof(FieldBindingStubMethod.speed), 0);
            binding.CompileAccessors(typeof(FieldBindingStubMethod));
            Assert.IsTrue(binding.IsCompiled);

            bb.SetFloat(0, 3.5f);
            binding.ReadFromBBGeneric(method, bb);
            Assert.AreEqual(3.5f, method.speed);

            method.speed = 9f;
            binding.WriteToBBGeneric(method, bb);
            Assert.AreEqual(9f, bb.GetFloat(0));
        }

        [Test]
        public void EnumField_ReadWrite_ViaIntArray()
        {
            var method = new FieldBindingStubMethod();
            FieldBinding binding = MakeBinding(nameof(FieldBindingStubMethod.state), 1);
            binding.CompileAccessors(typeof(FieldBindingStubMethod));

            bb.Set(1, TestState.Running);
            binding.ReadFromBBGeneric(method, bb);
            Assert.AreEqual(TestState.Running, method.state);

            method.state = TestState.Idle;
            binding.WriteToBBGeneric(method, bb);

            // Written as int into the int array, but boxed reads still return the enum.
            Assert.AreEqual(0, ((IBlackboardTypedAccess)bb.Storage).GetInt(1));
            Assert.IsInstanceOf<TestState>(bb.Storage.GetBoxed(1));
            Assert.AreEqual(TestState.Idle, bb.Get<TestState>(1));
        }

        [Test]
        public void RefField_ReadWrite_ViaObjectArray()
        {
            var method = new FieldBindingStubMethod();
            FieldBinding binding = MakeBinding(nameof(FieldBindingStubMethod.label), 2);
            binding.CompileAccessors(typeof(FieldBindingStubMethod));

            bb.SetObject(2, "hello");
            binding.ReadFromBBGeneric(method, bb);
            Assert.AreEqual("hello", method.label);

            method.label = "world";
            binding.WriteToBBGeneric(method, bb);
            Assert.AreEqual("world", bb.GetObject<string>(2));
        }
    }
}
