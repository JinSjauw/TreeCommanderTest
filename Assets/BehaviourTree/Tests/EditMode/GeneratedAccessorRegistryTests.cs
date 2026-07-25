using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class GeneratedAccessorRegistryTests
    {
        private class RegistryStubMethod : ActionMethod
        {
            public float speed;
            public override NodeState Execute(Runtime.TickContext ctx) => NodeState.SUCCESS;
        }

        [TearDown]
        public void TearDown() => GeneratedAccessorRegistry.Clear();

        [Test]
        public void TryGet_Miss_ReturnsFalse()
        {
            bool found = GeneratedAccessorRegistry.TryGet(
                typeof(RegistryStubMethod), typeof(RegistryStubMethod).GetField(nameof(RegistryStubMethod.speed)),
                out _, out _);
            Assert.IsFalse(found);
        }

        [Test]
        public void Register_And_Hit_ReturnsDelegates()
        {
            GeneratedAccessorRegistry.Register(typeof(RegistryStubMethod), nameof(RegistryStubMethod.speed), typeof(float),
                (m, bb, slot) => ((RegistryStubMethod)m).speed = bb.GetFloat(slot),
                (m, bb, slot) => bb.SetFloat(slot, ((RegistryStubMethod)m).speed));

            bool found = GeneratedAccessorRegistry.TryGet(
                typeof(RegistryStubMethod), typeof(RegistryStubMethod).GetField(nameof(RegistryStubMethod.speed)),
                out GeneratedAccessorRegistry.ReadAccessor read,
                out GeneratedAccessorRegistry.WriteAccessor write);

            Assert.IsTrue(found);
            Assert.IsNotNull(read);
            Assert.IsNotNull(write);
        }

        [Test]
        public void StaleFieldType_Misses()
        {
            // Registered as int; the actual field is float — stale generated code.
            GeneratedAccessorRegistry.Register(typeof(RegistryStubMethod), nameof(RegistryStubMethod.speed), typeof(int),
                (m, bb, slot) => { }, (m, bb, slot) => { });

            bool found = GeneratedAccessorRegistry.TryGet(
                typeof(RegistryStubMethod), typeof(RegistryStubMethod).GetField(nameof(RegistryStubMethod.speed)),
                out _, out _);

            Assert.IsFalse(found); // registered type != actual field type → stale
        }

        [Test]
        public void BindAccessors_PrefersGenerated_OverCompiled()
        {
            GeneratedAccessorRegistry.Register(typeof(RegistryStubMethod), nameof(RegistryStubMethod.speed), typeof(float),
                (m, bb, slot) => ((RegistryStubMethod)m).speed = bb.GetFloat(slot) + 1000f, // marker
                (m, bb, slot) => bb.SetFloat(slot, ((RegistryStubMethod)m).speed));

            var def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<float>("speed", 1, 3.5f));
            var go = new GameObject("bb");
            try
            {
                var bb = go.AddComponent<BlackBoard>();
                bb.Initialize(def);

                var binding = new FieldBinding
                {
                    fieldInfo = typeof(RegistryStubMethod).GetField(nameof(RegistryStubMethod.speed)),
                    bbSlotIndex = 0,
                    isOutput = true
                };
                binding.BindAccessors(typeof(RegistryStubMethod));

                var method = new RegistryStubMethod();
                binding.ReadFromBBGeneric(method, bb);
                Assert.AreEqual(1003.5f, method.speed); // generated accessor ran, not the compiled one
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void BindAccessors_FallsBack_WhenNoGeneratedEntry()
        {
            var def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<float>("speed", 1, 3.5f));
            var go = new GameObject("bb");
            try
            {
                var bb = go.AddComponent<BlackBoard>();
                bb.Initialize(def);

                var binding = new FieldBinding
                {
                    fieldInfo = typeof(RegistryStubMethod).GetField(nameof(RegistryStubMethod.speed)),
                    bbSlotIndex = 0,
                    isOutput = true
                };
                binding.BindAccessors(typeof(RegistryStubMethod)); // no registration → Expression fallback

                var method = new RegistryStubMethod();
                binding.ReadFromBBGeneric(method, bb);
                Assert.AreEqual(3.5f, method.speed); // compiled accessor behavior

                method.speed = 9f;
                binding.WriteToBBGeneric(method, bb);
                Assert.AreEqual(9f, bb.GetFloat(0));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(def);
            }
        }
    }
}
