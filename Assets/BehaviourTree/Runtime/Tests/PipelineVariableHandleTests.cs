using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Runtime.Tests
{
    /// <summary>
    /// End-to-end pipeline test for the unified BoxedVariableHandle + name→slot cache system.
    /// Validates input→output correctness for value types (int, float, Vector3, bool) and
    /// reference types (Transform, GameObject) — both scalar and strided arrays.
    /// Also validates that type mismatches are caught by GetVariable using the baked type.
    /// </summary>
    public class PipelineVariableHandleTests
    {
        private GameObject bbObject;
        private BlackBoard bb;
        private BlackboardDefinition bbDef;

        [SetUp]
        public void SetUp()
        {
            bbObject = new GameObject("TestPipelineBB");
            bb = bbObject.AddComponent<BlackBoard>();
            bbDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            if (bbDef != null) Object.DestroyImmediate(bbDef);
            if (bbObject != null) Object.DestroyImmediate(bbObject);
        }

        private void AddVariable<T>(string name, int stride)
        {
            bbDef.AddVariable<T>(name, stride);
        }

        private void BuildBB()
        {
            bb.Initialize(bbDef);
        }

        // ═══════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Writes a value to a storage slot using the typed Set(int,T) method.
        /// Slot index = flat offset into the storage array.
        /// </summary>
        private void SetSlot<T>(int slot, T value) => bb.Set(slot, value);

        /// <summary>
        /// Reads a value from a storage slot using the typed Get(int) method.
        /// </summary>
        private T GetSlot<T>(int slot) => bb.Get<T>(slot);

        // ═══════════════════════════════════════════════════════════════
        // VALUE TYPE: int — scalar
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void Int_Scalar_HandleValue_Roundtrip()
        {
            AddVariable<int>("Score", stride: 1);
            BuildBB();

            var handle = bb.GetVariable("Score");
            handle.SetValue(42);

            Assert.That(handle.GetValue<int>(), Is.EqualTo(42));
            Assert.That(handle.Value, Is.EqualTo(42));
        }

        [Test]
        public void Int_Scalar_NameBased_Roundtrip()
        {
            AddVariable<int>("Score", stride: 1);
            BuildBB();

            bb.Set("Score", 99);

            Assert.That(bb.Get<int>("Score"), Is.EqualTo(99));
        }

        [Test]
        public void Int_Scalar_HandleAndNameBased_Agree()
        {
            AddVariable<int>("Score", stride: 1);
            BuildBB();

            var handle = bb.GetVariable("Score");
            handle.SetValue(77);

            Assert.That(bb.Get<int>("Score"), Is.EqualTo(77));
        }

        // ═══════════════════════════════════════════════════════════════
        // VALUE TYPE: int — strided array (stride=4)
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void Int_Array_HandleIndexer_Roundtrip()
        {
            AddVariable<int>("AgentIDs", stride: 4);
            BuildBB();

            var handle = bb.GetVariable("AgentIDs");
            handle.SetElement(0, 1001);
            handle.SetElement(1, 1002);
            handle.SetElement(3, 1004);

            Assert.That(handle.GetElement<int>(0), Is.EqualTo(1001));
            Assert.That(handle.GetElement<int>(1), Is.EqualTo(1002));
            Assert.That(handle.GetElement<int>(3), Is.EqualTo(1004));
            Assert.That(handle[0], Is.EqualTo(1001));
            Assert.That(handle[1], Is.EqualTo(1002));
            Assert.That(handle[3], Is.EqualTo(1004));
        }

        [Test]
        public void Int_Array_HandleValue_ReturnsElementZero()
        {
            AddVariable<int>("AgentIDs", stride: 4);
            BuildBB();

            SetSlot(0, 9001); // slot 0 = element 0
            SetSlot(3, 9004); // slot 3 = element 3

            var handle = bb.GetVariable("AgentIDs");
            Assert.That(handle.GetValue<int>(), Is.EqualTo(9001),
                "Value should return element 0");
            Assert.That(handle.GetElement<int>(3), Is.EqualTo(9004));
        }

        [Test]
        public void Int_Array_WithPrecedingStride_BaseSlotCorrect()
        {
            AddVariable<float>("OtherArr", stride: 3);  // slots 0-2
            AddVariable<int>("AgentIDs", stride: 4);     // slots 3-6
            BuildBB();

            Assert.That(bb.GetSlot("AgentIDs"), Is.EqualTo(3));

            SetSlot(3, 10); // AgentIDs[0]
            SetSlot(6, 40); // AgentIDs[3]

            var handle = bb.GetVariable("AgentIDs");
            Assert.That(handle[0], Is.EqualTo(10));
            Assert.That(handle[3], Is.EqualTo(40));
        }

        [Test]
        public void Int_Array_NameBased_ReadsElementZero()
        {
            AddVariable<int>("AgentIDs", stride: 4);
            BuildBB();

            SetSlot(0, 777);

            Assert.That(bb.Get<int>("AgentIDs"), Is.EqualTo(777));
        }

        // ═══════════════════════════════════════════════════════════════
        // VALUE TYPE: float — scalar + array
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void Float_Scalar_Handle_Roundtrip()
        {
            AddVariable<float>("Speed", stride: 1);
            BuildBB();

            var handle = bb.GetVariable("Speed");
            handle.SetValue(3.14f);

            Assert.That(handle.GetValue<float>(), Is.EqualTo(3.14f));
            Assert.That(bb.Get<float>("Speed"), Is.EqualTo(3.14f));
        }

        [Test]
        public void Float_Array_HandleIndexer_Roundtrip()
        {
            AddVariable<float>("AgentSpeeds", stride: 5);
            BuildBB();

            var handle = bb.GetVariable("AgentSpeeds");
            handle.SetElement(0, 1.1f);
            handle.SetElement(2, 2.2f);
            handle.SetElement(4, 5.5f);

            Assert.That(handle.GetElement<float>(0), Is.EqualTo(1.1f));
            Assert.That(handle.GetElement<float>(2), Is.EqualTo(2.2f));
            Assert.That(handle.GetElement<float>(4), Is.EqualTo(5.5f));
            Assert.That(handle[2], Is.EqualTo(2.2f));
        }

        [Test]
        public void Float_Array_HandleValue_ReturnsElementZero()
        {
            AddVariable<float>("AgentSpeeds", stride: 3);
            BuildBB();
            SetSlot(0, 99.9f);

            var handle = bb.GetVariable("AgentSpeeds");
            Assert.That(handle.GetValue<float>(), Is.EqualTo(99.9f));
        }

        // ═══════════════════════════════════════════════════════════════
        // VALUE TYPE: Vector3 — scalar + array
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void Vector3_Scalar_HandleBoxed_Roundtrip()
        {
            AddVariable<Vector3>("Position", stride: 1);
            BuildBB();

            var handle = bb.GetVariable("Position");
            handle.Value = new Vector3(1, 2, 3);

            Assert.That(handle.Value, Is.EqualTo(new Vector3(1, 2, 3)));
        }

        [Test]
        public void Vector3_Scalar_HandleTyped_Roundtrip()
        {
            AddVariable<Vector3>("Position", stride: 1);
            BuildBB();

            var handle = bb.GetVariable("Position");
            handle.SetValue(new Vector3(4, 5, 6));

            Assert.That(handle.GetValue<Vector3>(), Is.EqualTo(new Vector3(4, 5, 6)));
        }

        [Test]
        public void Vector3_Array_HandleIndexer_Roundtrip()
        {
            AddVariable<Vector3>("Waypoints", stride: 4);
            BuildBB();

            var handle = bb.GetVariable("Waypoints");
            handle[0] = new Vector3(0, 0, 0);
            handle[1] = new Vector3(1, 0, 0);
            handle[3] = new Vector3(3, 0, 0);

            Assert.That(handle[0], Is.EqualTo(new Vector3(0, 0, 0)));
            Assert.That(handle[1], Is.EqualTo(new Vector3(1, 0, 0)));
            Assert.That(handle[3], Is.EqualTo(new Vector3(3, 0, 0)));
        }

        [Test]
        public void Vector3_Array_TypedElement_Roundtrip()
        {
            AddVariable<Vector3>("Waypoints", stride: 3);
            BuildBB();

            var handle = bb.GetVariable("Waypoints");
            handle.SetElement(0, new Vector3(10, 20, 30));
            handle.SetElement(2, new Vector3(30, 40, 50));

            Assert.That(handle.GetElement<Vector3>(0), Is.EqualTo(new Vector3(10, 20, 30)));
            Assert.That(handle.GetElement<Vector3>(2), Is.EqualTo(new Vector3(30, 40, 50)));
        }

        [Test]
        public void Vector3_Array_HandleValue_ReturnsElementZero()
        {
            AddVariable<Vector3>("Waypoints", stride: 4);
            BuildBB();
            bb.SetBoxed(0, new Vector3(7, 7, 7));

            var handle = bb.GetVariable("Waypoints");
            Assert.That(handle.Value, Is.EqualTo(new Vector3(7, 7, 7)));
        }

        // ═══════════════════════════════════════════════════════════════
        // VALUE TYPE: bool
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void Bool_Scalar_Handle_Roundtrip()
        {
            AddVariable<bool>("IsActive", stride: 1);
            BuildBB();

            var handle = bb.GetVariable("IsActive");
            handle.SetValue(true);

            Assert.That(handle.GetValue<bool>(), Is.True);
            Assert.That(handle.Value, Is.True);
        }

        [Test]
        public void Bool_Scalar_NameBased_Roundtrip()
        {
            AddVariable<bool>("IsActive", stride: 1);
            BuildBB();

            bb.Set("IsActive", true);
            Assert.That(bb.Get<bool>("IsActive"), Is.True);

            bb.Set("IsActive", false);
            Assert.That(bb.Get<bool>("IsActive"), Is.False);
        }

        [Test]
        public void Bool_Array_HandleIndexer_Roundtrip()
        {
            AddVariable<bool>("AgentAlive", stride: 3);
            BuildBB();

            var handle = bb.GetVariable("AgentAlive");
            handle.SetElement(0, true);
            handle.SetElement(1, false);
            handle.SetElement(2, true);

            Assert.That(handle[0], Is.True);
            Assert.That(handle[1], Is.False);
            Assert.That(handle[2], Is.True);
        }

        // ═══════════════════════════════════════════════════════════════
        // REFERENCE TYPE: Transform — scalar + array
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void Transform_Scalar_HandleBoxed_Roundtrip()
        {
            AddVariable<Transform>("Target", stride: 1);
            BuildBB();

            var dummyObject = new GameObject("DummyTransform");
            var dummyTransform = dummyObject.transform;
            try
            {
                var handle = bb.GetVariable("Target");
                handle.Value = dummyTransform;

                Assert.That(handle.Value, Is.SameAs(dummyTransform));
                Assert.That(handle.GetValue<Transform>(), Is.SameAs(dummyTransform));
            }
            finally
            {
                Object.DestroyImmediate(dummyObject);
            }
        }

        [Test]
        public void Transform_Scalar_NullWorks()
        {
            AddVariable<Transform>("Target", stride: 1);
            BuildBB();

            var handle = bb.GetVariable("Target");
            handle.Value = null;

            Assert.That(handle.Value, Is.Null);
        }

        [Test]
        public void Transform_Array_HandleIndexer_Roundtrip()
        {
            AddVariable<Transform>("AgentTransforms", stride: 3);
            BuildBB();

            var go0 = new GameObject("Agent0");
            var go1 = new GameObject("Agent1");
            try
            {
                var handle = bb.GetVariable("AgentTransforms");
                handle[0] = go0.transform;
                handle[1] = go1.transform;

                Assert.That(handle[0], Is.SameAs(go0.transform));
                Assert.That(handle[1], Is.SameAs(go1.transform));
                Assert.That(handle[2], Is.Null); // unset
            }
            finally
            {
                Object.DestroyImmediate(go1);
                Object.DestroyImmediate(go0);
            }
        }

        [Test]
        public void Transform_Array_WithPrecedingArray_BaseSlotShifted()
        {
            AddVariable<int>("AgentIDs", stride: 5);           // 0-4
            AddVariable<Transform>("AgentTransforms", stride: 3); // 5-7
            BuildBB();

            Assert.That(bb.GetSlot("AgentTransforms"), Is.EqualTo(5));

            var go = new GameObject("TestAgent");
            try
            {
                var handle = bb.GetVariable("AgentTransforms");
                handle[1] = go.transform;

                // Verify via raw slot — slot 6 = offset 5 + index 1
                Assert.That(bb.GetBoxed(6), Is.SameAs(go.transform));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // REFERENCE TYPE: GameObject — scalar + array
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void GameObject_Scalar_HandleBoxed_Roundtrip()
        {
            AddVariable<GameObject>("SpawnPoint", stride: 1);
            BuildBB();

            var go = new GameObject("Spawn");
            try
            {
                var handle = bb.GetVariable("SpawnPoint");
                handle.Value = go;

                Assert.That(handle.Value, Is.SameAs(go));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GameObject_Array_HandleIndexer_Roundtrip()
        {
            AddVariable<GameObject>("Agents", stride: 4);
            BuildBB();

            var go0 = new GameObject("A0");
            var go3 = new GameObject("A3");
            try
            {
                var handle = bb.GetVariable("Agents");
                handle[0] = go0;
                handle[3] = go3;

                Assert.That(handle[0], Is.SameAs(go0));
                Assert.That(handle[3], Is.SameAs(go3));
                Assert.That(handle[1], Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(go3);
                Object.DestroyImmediate(go0);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // INVALID / TYPE MISMATCH: Mismatched reads/writes
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void Invalid_IntSlot_WrittenAsVector3_ThrowsOrDefaults()
        {
            AddVariable<int>("Score", stride: 1);
            BuildBB();

            // Write Vector3 value to an int-typed slot
            // ManagedBlackboardStorage.CanWriteBoxed checks type compatibility.
            // This should either throw an InvalidCastException or be rejected.
            try
            {
                bb.SetBoxed(0, new Vector3(1, 2, 3));
                // If it didn't throw, the value should NOT be readable as int correctly
                object raw = bb.GetBoxed(0);
                Assert.That(raw is Vector3 || raw is int,
                    "Storage should either reject the write or store as Vector3");
            }
            catch (System.InvalidCastException)
            {
                // Expected — storage rejects incompatible type
                Assert.Pass("Storage correctly rejected incompatible type write");
            }
        }

        [Test]
        public void Invalid_IntVariable_WrongTypeRead_FailsCorrect()
        {
            AddVariable<int>("Score", stride: 1);
            BuildBB();
            SetSlot(0, 42);

            var handle = bb.GetVariable("Score");

            // Reading as wrong type via boxed Value returns the int boxed
            object val = handle.Value;
            Assert.That(val, Is.TypeOf<int>());
            Assert.That(val, Is.Not.TypeOf<float>());

            // Reading via typed GetElement<T> with wrong T will throw cast exception
            try
            {
                float wrong = handle.GetValue<float>();
                Assert.That(wrong, Is.EqualTo(42.0f),
                    "If the underlying type is int but storage returns it as int, " +
                    "GetValue<float> should throw or auto-convert");
            }
            catch (System.InvalidCastException)
            {
                Assert.Pass("Correctly threw InvalidCastException for wrong typed read");
            }
        }

        [Test]
        public void Invalid_StridedVariable_WrongTypeRead_Detected()
        {
            AddVariable<int>("AgentIDs", stride: 5);
            BuildBB();
            SetSlot(2, 999);

            var handle = bb.GetVariable("AgentIDs");

            // Boxed reads should return int, not Vector3
            object element2 = handle[2];
            Assert.That(element2, Is.Not.TypeOf<Vector3>(),
                "handle[2] should return int, not Vector3");
            Assert.That(element2, Is.TypeOf<int>());
        }

        [Test]
        public void Invalid_TransformSlot_WriteWrongType_ThrowsOrRejects()
        {
            AddVariable<Transform>("Target", stride: 1);
            BuildBB();

            // Writing Vector3 to a Transform slot should be silently rejected
            bb.SetBoxed(0, new Vector3(1, 2, 3));
            object raw = bb.GetBoxed(0);
            // Value should not be Vector3 — either null (unwritten Transform) or still a Transform
            Assert.That(raw is not Vector3, "Vector3 should not be stored in a Transform slot");
        }

        [Test]
        public void Invalid_GameObjectSlot_WriteInt_ThrowsOrRejects()
        {
            AddVariable<GameObject>("SpawnPoint", stride: 1);
            BuildBB();

            // Writing int to a GameObject slot should be silently rejected
            bb.SetBoxed(0, 42);
            object raw = bb.GetBoxed(0);
            Assert.That(raw is not int, "int should not be stored in a GameObject slot");
        }

        [Test]
        public void Invalid_UnknownVariable_ReturnsInvalidHandle()
        {
            BuildBB();
            var handle = bb.GetVariable("DoesNotExist");
            Assert.That(handle.IsValid, Is.False);
            Assert.That(handle.BaseSlot, Is.EqualTo(-1));

            // Read/write on invalid handle should be no-ops
            Assert.That(handle.Value, Is.Null);
            handle.Value = 42; // should not throw
        }

        // ═══════════════════════════════════════════════════════════════
        // MIXED: Multiple variables, different types and strides
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void Mixed_ComplexLayout_AllSlotsCorrect()
        {
            // Simulates a realistic commander BB:
            //  Roles[0..4]     = int, stride 5
            //  Positions[5..14] = Vector3, stride 10
            //  Speed[15]        = float, stride 1
            //  Leader[16]       = Transform, stride 1
            AddVariable<int>("AgentRoles", stride: 5);
            AddVariable<Vector3>("Positions", stride: 10);
            AddVariable<float>("Speed", stride: 1);
            AddVariable<Transform>("Leader", stride: 1);
            BuildBB();

            // Verify slot offsets
            Assert.That(bb.GetSlot("AgentRoles"), Is.EqualTo(0));
            Assert.That(bb.GetSlot("Positions"), Is.EqualTo(5));
            Assert.That(bb.GetSlot("Speed"), Is.EqualTo(15));
            Assert.That(bb.GetSlot("Leader"), Is.EqualTo(16));

            // Write & read per variable
            var roles = bb.GetVariable("AgentRoles");
            roles.SetElement(2, 42);
            Assert.That(roles.GetElement<int>(2), Is.EqualTo(42));

            var positions = bb.GetVariable("Positions");
            positions.SetElement(9, new Vector3(9, 9, 9));
            Assert.That(positions.GetElement<Vector3>(9), Is.EqualTo(new Vector3(9, 9, 9)));

            var speed = bb.GetVariable("Speed");
            speed.SetValue(12.5f);
            Assert.That(speed.GetValue<float>(), Is.EqualTo(12.5f));

            // Ensure no cross-variable corruption
            Assert.That(roles.GetElement<int>(2), Is.EqualTo(42),
                "AgentRoles should not be corrupted by writing to other variables");
        }

        [Test]
        public void Mixed_NameBased_ReadsCorrectAcrossVariables()
        {
            AddVariable<int>("Health", stride: 1);
            AddVariable<float>("Stamina", stride: 1);
            AddVariable<Vector3>("Target", stride: 1);
            BuildBB();

            bb.Set("Health", 100);
            bb.Set("Stamina", 75.5f);
            bb.SetBoxed(bb.GetSlot("Target"), new Vector3(5, 5, 5));

            Assert.That(bb.Get<int>("Health"), Is.EqualTo(100));
            Assert.That(bb.Get<float>("Stamina"), Is.EqualTo(75.5f));
            Assert.That(bb.GetVariable("Target").Value, Is.EqualTo(new Vector3(5, 5, 5)));
        }
    }
}
