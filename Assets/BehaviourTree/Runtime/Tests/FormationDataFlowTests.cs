using BehaviourTree.Core;
using BehaviourTree.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Runtime.Tests
{
    /// <summary>
    /// Verifies that formation data flows correctly through the system:
    /// base channel layout → slot computation → formation math.
    /// </summary>
    public class FormationDataFlowTests
    {
        private SquadDefinition squadDef;

        [SetUp]
        public void SetUp()
        {
            squadDef = ScriptableObject.CreateInstance<SquadDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            if (squadDef != null)
                Object.DestroyImmediate(squadDef);
        }

        // ═══════════════════════════════════════════════════════════════
        // Test 1: OnValidate produces the correct base-channel order
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void OnValidate_CreatesBaseChannels_InCorrectOrder()
        {
            // SquadDefinition.OnValidate is called by Unity automatically,
            // but we can simulate by calling EnsureBaseChannel directly.
            var bbDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            squadDef.blackboardDefinition = bbDef;

            try
            {
                // Replicate the OnValidate order:
                BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentRoles", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentOrders", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "LeaderIndex", isSquadData: false);
                BlackboardDefinition.EnsureBaseChannel<Vector3>(bbDef, "SquadMovePosition", isSquadData: false);
                BlackboardDefinition.EnsureBaseChannel<Vector3>(bbDef, "AgentMovePosition", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<float>(bbDef, "AgentMoveSpeed", isSquadData: true);

                // Verify variable order (index = position in list)
                Assert.That(bbDef.GetVariableIndex("AgentRoles"), Is.EqualTo(0), "AgentRoles should be index 0");
                Assert.That(bbDef.GetVariableIndex("AgentOrders"), Is.EqualTo(1), "AgentOrders should be index 1");
                Assert.That(bbDef.GetVariableIndex("LeaderIndex"), Is.EqualTo(2), "LeaderIndex should be index 2");
                Assert.That(bbDef.GetVariableIndex("SquadMovePosition"), Is.EqualTo(3), "SquadMovePosition should be index 3");
                Assert.That(bbDef.GetVariableIndex("AgentMovePosition"), Is.EqualTo(4), "AgentMovePosition should be index 4");
                Assert.That(bbDef.GetVariableIndex("AgentMoveSpeed"), Is.EqualTo(5), "AgentMoveSpeed should be index 5");
            }
            finally
            {
                Object.DestroyImmediate(bbDef);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Test 2: Variable types and isSquadData flags
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void BaseChannels_HaveCorrectTypesAndFlags()
        {
            var bbDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            squadDef.blackboardDefinition = bbDef;

            try
            {
                BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentRoles", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentOrders", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "LeaderIndex", isSquadData: false);
                BlackboardDefinition.EnsureBaseChannel<Vector3>(bbDef, "SquadMovePosition", isSquadData: false);
                BlackboardDefinition.EnsureBaseChannel<Vector3>(bbDef, "AgentMovePosition", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<float>(bbDef, "AgentMoveSpeed", isSquadData: true);

                VerifyVariable(bbDef, "AgentRoles", typeof(int), true);
                VerifyVariable(bbDef, "AgentOrders", typeof(int), true);
                VerifyVariable(bbDef, "LeaderIndex", typeof(int), false);
                VerifyVariable(bbDef, "SquadMovePosition", typeof(Vector3), false);
                VerifyVariable(bbDef, "AgentMovePosition", typeof(Vector3), true);
                VerifyVariable(bbDef, "AgentMoveSpeed", typeof(float), true);
            }
            finally
            {
                Object.DestroyImmediate(bbDef);
            }
        }

        private static void VerifyVariable(BlackboardDefinition bbDef, string name, System.Type expectedType, bool expectedSquadData)
        {
            BlackboardVariableBase v = bbDef.FindVariable(name);
            Assert.That(v, Is.Not.Null, $"Variable '{name}' should exist");
            Assert.That(v.GetValueType(), Is.EqualTo(expectedType), $"'{name}' should be {expectedType.Name}");
            Assert.That(v.isSquadData, Is.EqualTo(expectedSquadData), $"'{name}' isSquadData should be {expectedSquadData}");
        }

        // ═══════════════════════════════════════════════════════════════
        // Test 3: Slot computation (flat array offsets)
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void SlotOffsets_WithDefaultStride_AreCorrect()
        {
            var bbDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            squadDef.blackboardDefinition = bbDef;

            try
            {
                BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentRoles", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentOrders", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "LeaderIndex", isSquadData: false);
                BlackboardDefinition.EnsureBaseChannel<Vector3>(bbDef, "SquadMovePosition", isSquadData: false);
                BlackboardDefinition.EnsureBaseChannel<Vector3>(bbDef, "AgentMovePosition", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<float>(bbDef, "AgentMoveSpeed", isSquadData: true);

                // With default stride=1, each variable occupies 1 slot
                Assert.That(ComputeSlot(bbDef, "AgentRoles"), Is.EqualTo(0));
                Assert.That(ComputeSlot(bbDef, "AgentOrders"), Is.EqualTo(1));
                Assert.That(ComputeSlot(bbDef, "LeaderIndex"), Is.EqualTo(2));
                Assert.That(ComputeSlot(bbDef, "SquadMovePosition"), Is.EqualTo(3));
                Assert.That(ComputeSlot(bbDef, "AgentMovePosition"), Is.EqualTo(4));
                Assert.That(ComputeSlot(bbDef, "AgentMoveSpeed"), Is.EqualTo(5));
            }
            finally
            {
                Object.DestroyImmediate(bbDef);
            }
        }

        [Test]
        public void SlotOffsets_WithSquadDataStride_AreCorrect()
        {
            // Simulate what happens after SquadInstance applies stride for 4 agents.
            // Squad-data variables get stride=4, non-squad stay at stride=1.
            var bbDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            squadDef.blackboardDefinition = bbDef;

            try
            {
                BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentRoles", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentOrders", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "LeaderIndex", isSquadData: false);
                BlackboardDefinition.EnsureBaseChannel<Vector3>(bbDef, "SquadMovePosition", isSquadData: false);
                BlackboardDefinition.EnsureBaseChannel<Vector3>(bbDef, "AgentMovePosition", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<float>(bbDef, "AgentMoveSpeed", isSquadData: true);

                // Apply stride = 4 to all squad-data variables
                const int maxAgents = 4;
                foreach (var v in bbDef.GetAllVariables())
                {
                    if (v.isSquadData)
                        v.Stride = maxAgents;
                }

                // AgentRoles: index 0 → base slot 0, stride 4 → occupies slots 0-3
                // AgentOrders: index 1 → base slot 4, stride 4 → occupies slots 4-7
                // LeaderIndex: index 2 → stride 1 → base slot 8
                // SquadMovePosition: index 3 → stride 1 → base slot 9
                // AgentMovePosition: index 4 → stride 4 → base slot 10
                // AgentMoveSpeed: index 5 → stride 4 → base slot 14
                Assert.That(ComputeSlot(bbDef, "AgentRoles"), Is.EqualTo(0));
                Assert.That(ComputeSlot(bbDef, "AgentOrders"), Is.EqualTo(4));
                Assert.That(ComputeSlot(bbDef, "LeaderIndex"), Is.EqualTo(8));
                Assert.That(ComputeSlot(bbDef, "SquadMovePosition"), Is.EqualTo(9));
                Assert.That(ComputeSlot(bbDef, "AgentMovePosition"), Is.EqualTo(10));
                Assert.That(ComputeSlot(bbDef, "AgentMoveSpeed"), Is.EqualTo(14));
            }
            finally
            {
                Object.DestroyImmediate(bbDef);
            }
        }

        /// <summary>
        /// Replicates SquadManager.ComputeSlotForVariable logic.
        /// </summary>
        private static int ComputeSlot(BlackboardDefinition def, string varName)
        {
            int varIndex = def.GetVariableIndex(varName);
            if (varIndex < 0) return -1;

            var vars = def.GetAllVariables();
            int slot = 0;
            for (int i = 0; i < varIndex && i < vars.Count; i++)
            {
                int stride = vars[i].Stride;
                slot += (stride > 1) ? stride : 1;
            }
            return slot;
        }

        // ═══════════════════════════════════════════════════════════════
        // Test 4: CalculateFormation circle math
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void CircleFormation_Agent0_StraightAhead()
        {
            Vector3 center = new Vector3(10f, 0f, 20f);
            float radius = 5f;
            // Agent 0 of 4 → angle 0° → (sin 0, cos 0) = (0, 1) → offset (0, 0, radius)
            Vector3 pos = ComputeCirclePosition(center, radius, agentOffset: 0, agentCount: 4);
            Assert.That(pos.x, Is.EqualTo(10f).Within(0.001f));
            Assert.That(pos.y, Is.EqualTo(0f));
            Assert.That(pos.z, Is.EqualTo(25f).Within(0.001f)); // center.z + radius
        }

        [Test]
        public void CircleFormation_Agent1_RightSide()
        {
            Vector3 center = new Vector3(10f, 0f, 20f);
            float radius = 5f;
            // Agent 1 of 4 → angle 90° → (sin 90, cos 90) = (1, 0) → offset (radius, 0, 0)
            Vector3 pos = ComputeCirclePosition(center, radius, agentOffset: 1, agentCount: 4);
            Assert.That(pos.x, Is.EqualTo(15f).Within(0.001f)); // center.x + radius
            Assert.That(pos.y, Is.EqualTo(0f));
            Assert.That(pos.z, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void CircleFormation_Agent2_StraightBack()
        {
            Vector3 center = new Vector3(10f, 0f, 20f);
            float radius = 5f;
            // Agent 2 of 4 → angle 180° → (sin 180, cos 180) = (0, -1) → offset (0, 0, -radius)
            Vector3 pos = ComputeCirclePosition(center, radius, agentOffset: 2, agentCount: 4);
            Assert.That(pos.x, Is.EqualTo(10f).Within(0.001f));
            Assert.That(pos.y, Is.EqualTo(0f));
            Assert.That(pos.z, Is.EqualTo(15f).Within(0.001f)); // center.z - radius
        }

        [Test]
        public void CircleFormation_Agent3_LeftSide()
        {
            Vector3 center = new Vector3(10f, 0f, 20f);
            float radius = 5f;
            // Agent 3 of 4 → angle 270° → (sin 270, cos 270) = (-1, 0) → offset (-radius, 0, 0)
            Vector3 pos = ComputeCirclePosition(center, radius, agentOffset: 3, agentCount: 4);
            Assert.That(pos.x, Is.EqualTo(5f).Within(0.001f)); // center.x - radius
            Assert.That(pos.y, Is.EqualTo(0f));
            Assert.That(pos.z, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void CircleFormation_AllAgentsOnSameY()
        {
            // All agents should stay on the same Y level as center
            Vector3 center = new Vector3(0f, 3.5f, 0f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = ComputeCirclePosition(center, 10f, i, 3);
                Assert.That(pos.y, Is.EqualTo(3.5f).Within(0.001f), $"Agent {i} Y deviated");
            }
        }

        [Test]
        public void CircleFormation_EvenDistribution()
        {
            // 6 agents should be 60° apart. Check adjacent agents.
            Vector3 center = Vector3.zero;
            Vector3 prev = ComputeCirclePosition(center, 1f, 0, 6);
            for (int i = 1; i < 6; i++)
            {
                Vector3 current = ComputeCirclePosition(center, 1f, i, 6);
                float angleBetween = Vector3.Angle(prev, current);
                Assert.That(angleBetween, Is.EqualTo(60f).Within(0.01f), $"Angle between agent {i - 1} and {i} should be 60°");
                prev = current;
            }
        }

        /// <summary>
        /// Replicates the circle math from CalculateFormation.Execute.
        /// </summary>
        private static Vector3 ComputeCirclePosition(Vector3 center, float radius, int agentOffset, int agentCount)
        {
            float angle = (agentOffset / (float)agentCount) * 360f * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
        }

        // ═══════════════════════════════════════════════════════════════
        // Test 5: LeaderIndex to SquadMovePosition relationship
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void BaseChannels_LeaderIndex_IsNotSquadData()
        {
            // LeaderIndex is a single int, not per-agent. Commander reads it
            // with no agentOffset, writes to SquadMovePosition which is also single.
            var bbDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            squadDef.blackboardDefinition = bbDef;

            try
            {
                BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "LeaderIndex", isSquadData: false);
                BlackboardDefinition.EnsureBaseChannel<Vector3>(bbDef, "SquadMovePosition", isSquadData: false);

                Assert.That(bbDef.FindVariable("LeaderIndex").isSquadData, Is.False,
                    "LeaderIndex must NOT be squad data — it's a single value");
                Assert.That(bbDef.FindVariable("SquadMovePosition").isSquadData, Is.False,
                    "SquadMovePosition must NOT be squad data — it's a single value");
            }
            finally
            {
                Object.DestroyImmediate(bbDef);
            }
        }

        [Test]
        public void AgentMovePosition_IsSquadData()
        {
            var bbDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            squadDef.blackboardDefinition = bbDef;

            try
            {
                BlackboardDefinition.EnsureBaseChannel<Vector3>(bbDef, "AgentMovePosition", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<float>(bbDef, "AgentMoveSpeed", isSquadData: true);

                Assert.That(bbDef.FindVariable("AgentMovePosition").isSquadData, Is.True,
                    "AgentMovePosition MUST be squad data — per-agent positions");
                Assert.That(bbDef.FindVariable("AgentMoveSpeed").isSquadData, Is.True,
                    "AgentMoveSpeed MUST be squad data — per-agent speeds");
            }
            finally
            {
                Object.DestroyImmediate(bbDef);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Test 6: AgentMoveSpeed invalidation (unregister = -1)
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void AgentMoveSpeed_InvalidSlot_ReturnsNegativeOne()
        {
            // The SquadManager writes -1f to AgentMoveSpeed[removedIndex].
            // Commander reads it and should filter out negative values
            // when computing FormationSpeed = min(positive values).

            float[] speeds = { 3.5f, 2.0f, -1f, 4.0f };

            float minValid = float.MaxValue;
            for (int i = 0; i < speeds.Length; i++)
            {
                if (speeds[i] > 0f && speeds[i] < minValid)
                    minValid = speeds[i];
            }

            Assert.That(minValid, Is.EqualTo(2.0f), "Min speed should skip the -1 (removed agent)");
        }
    }
}
