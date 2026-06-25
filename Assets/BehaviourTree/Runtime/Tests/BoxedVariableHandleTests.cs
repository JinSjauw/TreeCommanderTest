using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Runtime.Tests
{
    /// <summary>
    /// Validates BoxedVariableHandle, name→slot cache, and GetVariable/GetSlot
    /// correctness — especially with strided squad-data variables (Issue 2 fix).
    /// </summary>
    public class BoxedVariableHandleTests
    {
        private GameObject bbObject;
        private BlackBoard bb;
        private BlackboardDefinition bbDef;

        [SetUp]
        public void SetUp()
        {
            bbObject = new GameObject("TestBB");
            bb = bbObject.AddComponent<BlackBoard>();
            bbDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            if (bbDef != null) Object.DestroyImmediate(bbDef);
            if (bbObject != null) Object.DestroyImmediate(bbObject);
        }

        // ═══════════════════════════════════════════════════════════════
        // Variable setup helper
        // ═══════════════════════════════════════════════════════════════

        private void AddVariable<T>(string name, int stride)
        {
            bbDef.AddVariable<T>(name, stride);
        }

        private void BuildBB()
        {
            bb.Initialize(bbDef);
        }

        // ═══════════════════════════════════════════════════════════════
        // GetSlot — correct flat slot offsets with strided variables
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void GetSlot_AllStride1_MatchesVariableIndex()
        {
            AddVariable<float>("Health", stride: 1);
            AddVariable<float>("Stamina", stride: 1);
            AddVariable<float>("Mana", stride: 1);
            BuildBB();

            Assert.That(bb.GetSlot("Health"), Is.EqualTo(0));
            Assert.That(bb.GetSlot("Stamina"), Is.EqualTo(1));
            Assert.That(bb.GetSlot("Mana"), Is.EqualTo(2));
        }

        [Test]
        public void GetSlot_WithStridedPrecedingVars_FlatOffsetsShift()
        {
            // AgentIDs[0..4], Health=5, Stamina=6
            AddVariable<int>("AgentIDs", stride: 5);
            AddVariable<float>("Health", stride: 1);
            AddVariable<float>("Stamina", stride: 1);
            BuildBB();

            Assert.That(bb.GetSlot("AgentIDs"), Is.EqualTo(0));
            Assert.That(bb.GetSlot("Health"), Is.EqualTo(5),
                "Health should be at slot 5 (after 5 AgentIDs slots)");
            Assert.That(bb.GetSlot("Stamina"), Is.EqualTo(6),
                "Stamina should be at slot 6 (after Health)");
        }

        [Test]
        public void GetSlot_MultipleStridedVars_CorrectCumulativeOffsets()
        {
            // Roles[0..9]=10, Positions[10..19]=10, Flags[20..24]=5
            AddVariable<int>("Roles", stride: 10);
            AddVariable<Vector3>("Positions", stride: 10);
            AddVariable<bool>("Flags", stride: 5);
            BuildBB();

            Assert.That(bb.GetSlot("Roles"), Is.EqualTo(0));
            Assert.That(bb.GetSlot("Positions"), Is.EqualTo(10));
            Assert.That(bb.GetSlot("Flags"), Is.EqualTo(20));
        }

        [Test]
        public void GetSlot_MixedStridedAndNonStrided_OffsetsCorrect()
        {
            AddVariable<int>("AgentRoles", stride: 5);        // 0-4
            AddVariable<Vector3>("Positions", stride: 5);     // 5-9
            AddVariable<float>("CommanderSpeed", stride: 1);  // 10
            AddVariable<int>("LeaderIndex", stride: 1);       // 11
            BuildBB();

            Assert.That(bb.GetSlot("AgentRoles"), Is.EqualTo(0));
            Assert.That(bb.GetSlot("Positions"), Is.EqualTo(5));
            Assert.That(bb.GetSlot("CommanderSpeed"), Is.EqualTo(10));
            Assert.That(bb.GetSlot("LeaderIndex"), Is.EqualTo(11));
        }

        [Test]
        public void GetSlot_UnknownVariable_ReturnsMinusOne()
        {
            AddVariable<int>("Health", stride: 1);
            BuildBB();
            Assert.That(bb.GetSlot("GhostVar"), Is.EqualTo(-1));
        }

        // ═══════════════════════════════════════════════════════════════
        // GetVariable — returns valid/invalid handles
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void GetVariable_KnownVariable_ReturnsValidHandle()
        {
            AddVariable<float>("Health", stride: 1);
            BuildBB();

            var handle = bb.GetVariable("Health");
            Assert.That(handle.IsValid, Is.True);
            Assert.That(handle.BaseSlot, Is.EqualTo(bb.GetSlot("Health")));
        }

        [Test]
        public void GetVariable_UnknownVariable_ReturnsInvalidHandle()
        {
            BuildBB();
            var handle = bb.GetVariable("Ghost");
            Assert.That(handle.IsValid, Is.False);
            Assert.That(handle.BaseSlot, Is.EqualTo(-1));
        }

        [Test]
        public void GetVariable_AfterStridedVars_HasCorrectBaseSlot()
        {
            AddVariable<int>("AgentIDs", stride: 5);
            AddVariable<float>("Health", stride: 1);
            BuildBB();

            var handle = bb.GetVariable("Health");
            Assert.That(handle.BaseSlot, Is.EqualTo(5));
        }

        // ═══════════════════════════════════════════════════════════════
        // BoxedVariableHandle — Value and indexed access
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void Handle_Value_ReadsElementZero()
        {
            AddVariable<int>("Score", stride: 1);
            BuildBB();
            bb.Set(0, 42); // Set at slot 0 (value-type via Set<T>(int))

            var handle = bb.GetVariable("Score");
            Assert.That(handle.GetValue<int>(), Is.EqualTo(42));
            Assert.That(handle.Value, Is.EqualTo(42));
        }

        [Test]
        public void Handle_Value_WritesElementZero()
        {
            AddVariable<int>("Score", stride: 1);
            BuildBB();

            var handle = bb.GetVariable("Score");
            handle.SetValue(99);

            Assert.That(bb.Get<int>(0), Is.EqualTo(99));
        }

        [Test]
        public void Handle_Indexer_ReadsPerAgentElement()
        {
            AddVariable<int>("AgentHealth", stride: 3);
            BuildBB();

            // Manually write per-agent values
            bb.Set(0, 100); // Health slot 0 = agent 0
            bb.Set(1, 80);  // Health slot 1 = agent 1
            bb.Set(2, 60);  // Health slot 2 = agent 2

            var handle = bb.GetVariable("AgentHealth");
            Assert.That(handle[0], Is.EqualTo(100));
            Assert.That(handle[1], Is.EqualTo(80));
            Assert.That(handle[2], Is.EqualTo(60));
        }

        [Test]
        public void Handle_Indexer_WritesPerAgentElement()
        {
            AddVariable<int>("AgentHealth", stride: 3);
            BuildBB();

            var handle = bb.GetVariable("AgentHealth");
            handle[1] = 55;

            Assert.That(bb.GetBoxed(1), Is.EqualTo(55));
            // Slot 0 and 2 unchanged
            Assert.That(bb.GetBoxed(0), Is.Null.Or.EqualTo(0)); // default int
        }

        [Test]
        public void Handle_WithStridedPrecedingVars_IndexerOffsetsCorrectly()
        {
            // AgentIDs[0..4], Positions[5..9]
            AddVariable<int>("AgentIDs", stride: 5);
            AddVariable<Vector3>("Positions", stride: 5);
            BuildBB();

            bb.SetBoxed(5 + 0, new Vector3(1, 0, 0)); // agent 0 position
            bb.SetBoxed(5 + 1, new Vector3(2, 0, 0)); // agent 1 position
            bb.SetBoxed(5 + 2, new Vector3(3, 0, 0)); // agent 2 position

            var posHandle = bb.GetVariable("Positions");
            Assert.That(posHandle.BaseSlot, Is.EqualTo(5));
            Assert.That(posHandle[0], Is.EqualTo(new Vector3(1, 0, 0)));
            Assert.That(posHandle[1], Is.EqualTo(new Vector3(2, 0, 0)));
            Assert.That(posHandle[2], Is.EqualTo(new Vector3(3, 0, 0)));
        }

        [Test]
        public void Handle_BoxedValue_ReadsObject()
        {
            AddVariable<Vector3>("Position", stride: 1);
            BuildBB();
            bb.SetBoxed(0, new Vector3(7, 8, 9));

            var handle = bb.GetVariable("Position");
            Assert.That(handle.Value, Is.EqualTo(new Vector3(7, 8, 9)));
        }

        [Test]
        public void Handle_BoxedValue_WritesObject()
        {
            AddVariable<Vector3>("Position", stride: 1);
            BuildBB();

            var handle = bb.GetVariable("Position");
            handle.Value = new Vector3(4, 5, 6);

            Assert.That(bb.GetBoxed(0), Is.EqualTo(new Vector3(4, 5, 6)));
        }

        // ═══════════════════════════════════════════════════════════════
        // Issue 2 fix: Get<T>(string) and Set<T>(string) use correct slot
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void GetString_WithStridedPrecedingVars_ReadsCorrectSlot()
        {
            AddVariable<int>("AgentIDs", stride: 5);
            AddVariable<float>("CommanderTarget", stride: 1);
            BuildBB();

            // Write a known value to CommanderTarget's correct slot (5)
            bb.Set(5, 3.14f);

            // Get<T>(string) should resolve to slot 5, not variable index 1
            float result = bb.Get<float>("CommanderTarget");
            Assert.That(result, Is.EqualTo(3.14f),
                "Get<T>(string) must use flat slot offset (5), not variable index (1)");
        }

        [Test]
        public void SetString_WithStridedPrecedingVars_WritesToCorrectSlot()
        {
            AddVariable<int>("AgentIDs", stride: 5);
            AddVariable<float>("CommanderTarget", stride: 1);
            BuildBB();

            // Set<T>(string) should write to slot 5, not variable index 1 (which is AgentIDs[1])
            bb.Set("CommanderTarget", 2.71f);

            Assert.That(bb.Get<float>(5), Is.EqualTo(2.71f),
                "Set<T>(string) must write to flat slot 5");
            // AgentIDs[1] at slot 1 should be untouched
            Assert.That(bb.Get<float>(1), Is.Not.EqualTo(2.71f),
                "AgentIDs[1] at slot 1 must not be overwritten");
        }

        [Test]
        public void GetSetString_AllStride1_StillCorrect()
        {
            AddVariable<float>("A", stride: 1);
            AddVariable<float>("B", stride: 1);
            BuildBB();

            bb.Set("A", 1.0f);
            bb.Set("B", 2.0f);

            Assert.That(bb.Get<float>("A"), Is.EqualTo(1.0f));
            Assert.That(bb.Get<float>("B"), Is.EqualTo(2.0f));
        }

        // ═══════════════════════════════════════════════════════════════
        // Handle typed accessors
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void Handle_GetValueT_NoBoxing()
        {
            AddVariable<float>("Speed", stride: 1);
            BuildBB();
            bb.Set(0, 12.5f);

            var handle = bb.GetVariable("Speed");
            float val = handle.GetValue<float>();
            Assert.That(val, Is.EqualTo(12.5f));
        }

        [Test]
        public void Handle_GetElementT_PerAgentNoBoxing()
        {
            AddVariable<Vector3>("Waypoints", stride: 4);
            BuildBB();
            bb.SetBoxed(0, new Vector3(0, 1, 2));
            bb.SetBoxed(1, new Vector3(3, 4, 5));

            var handle = bb.GetVariable("Waypoints");
            Assert.That(handle.GetElement<Vector3>(0), Is.EqualTo(new Vector3(0, 1, 2)));
            Assert.That(handle.GetElement<Vector3>(1), Is.EqualTo(new Vector3(3, 4, 5)));
        }

        [Test]
        public void Handle_SetElementT_WritesWithoutBoxing()
        {
            AddVariable<int>("Counts", stride: 3);
            BuildBB();

            var handle = bb.GetVariable("Counts");
            handle.SetElement(2, 999);

            Assert.That(bb.GetBoxed(2), Is.EqualTo(999));
        }
    }
}
