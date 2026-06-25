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

        // ═══════════════════════════════════════════════════════════════
        // Test 7: Validate that CopyToBB/CopyFromBB use GetBoxedRaw /
        //         SetBoxedRaw — so currentAgentOffset can never corrupt a
        //         copy operation, even if a stray offset leaks from a tick.
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void SquadCopy_StrayCurrentAgentOffset_DoesNotCorruptCopy()
        {
            const int maxAgents = 4;

            var squadBBDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            var agentBBDef = ScriptableObject.CreateInstance<BlackboardDefinition>();

            squadDef.blackboardDefinition = squadBBDef;

            var agentTreeAsset = ScriptableObject.CreateInstance<BehaviourTreeAssetBase>();
            agentBBDef.sourceTreeAsset = agentTreeAsset;

            try
            {
                BlackboardDefinition.EnsureBaseChannel<Transform>(squadBBDef, "AgentTransform", isSquadData: true);
                squadDef.EnsureStrideApplied(maxAgents);

                agentBBDef.AddVariable<Transform>("AgentTransform", stride: 1);

                var agentGroup = squadDef.GetOrCreateBindingGroup(agentTreeAsset);
                agentGroup.bindings.Add(new VariableBinding
                {
                    treeVariableName = "AgentTransform",
                    squadVariableName = "AgentTransform",
                    direction = BindingDirection.ToSquad
                });

                var squadGO = new GameObject("SquadGO");
                var squadInstance = squadGO.AddComponent<SquadInstance>();
                squadInstance.Initialize(squadDef, maxAgents);

                var agentGO = new GameObject("AgentGO");
                var agentBB = agentGO.AddComponent<BlackBoard>();
                agentBB.Initialize(agentBBDef);

                squadInstance.EnsureResolved(agentBBDef);

                var transforms = new Transform[maxAgents];
                for (int i = 0; i < maxAgents; i++)
                {
                    var go = new GameObject($"Agent_{i}");
                    transforms[i] = go.transform;
                }

                try
                {
                    int agentIndex = 2;

                    // ── Phase 1: Write to agent BB with currentAgentOffset = 0 ──
                    agentBB.currentAgentOffset = 0;
                    agentBB.SetBoxed(0, transforms[agentIndex]);

                    object agentVal = agentBB.GetBoxed(0);
                    Assert.That(agentVal, Is.EqualTo(transforms[agentIndex]),
                        "Phase 1: Agent BB slot 0 should hold the transform");

                    // ── Phase 2: Confirm GetBoxed() shifts reads when offset is set ──
                    agentBB.currentAgentOffset = 5;
                    object strayRead = agentBB.GetBoxed(0);
                    Assert.That(strayRead, Is.Null,
                        "Phase 2: GetBoxed(0) with currentAgentOffset=5 reads OOB → null. " +
                        "This proves GetBoxed() still adds the offset for normal reads.");

                    // ── Phase 3: CopyFromBB uses GetBoxedRaw — stray offset ignored ──
                    squadInstance.CopyFromBB(agentBB, agentBBDef, agentIndex);

                    int squadBase = ComputeSlot(squadBBDef, "AgentTransform");
                    object squadVal = squadInstance.BlackBoard.GetBoxed(squadBase + agentIndex);
                    Assert.That(squadVal, Is.EqualTo(transforms[agentIndex]),
                        $"Phase 3: Squad slot {squadBase + agentIndex} has correct transform " +
                        "even with currentAgentOffset=5 on the agent BB — " +
                        "CopyFromBB uses GetBoxedRaw/SetBoxedRaw, bypassing currentAgentOffset.");
                }
                finally
                {
                    for (int i = 0; i < maxAgents; i++)
                        Object.DestroyImmediate(transforms[i].gameObject);
                    Object.DestroyImmediate(agentGO);
                    Object.DestroyImmediate(squadGO);
                }
            }
            finally
            {
                Object.DestroyImmediate(squadBBDef);
                Object.DestroyImmediate(agentBBDef);
                Object.DestroyImmediate(agentTreeAsset);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Test 8: Agent writes single Transform into squad's DetectedEnemies
        //         Transform[] array at agentIndex 3. Verify the exact slot
        //         position on squad BB. Then push to commander and verify
        //         commander's DetectedEnemies array matches slot-for-slot.
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void DetectedEnemies_Agent3Writes_SquadAndCommanderReceiveCorrectSlot()
        {
            const int maxAgents = 5;
            const int agentIndex = 3;

            var squadBBDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            var agentBBDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            var commanderBBDef = ScriptableObject.CreateInstance<BlackboardDefinition>();

            squadDef.blackboardDefinition = squadBBDef;

            var agentTreeAsset = ScriptableObject.CreateInstance<BehaviourTreeAssetBase>();
            var commanderTreeAsset = ScriptableObject.CreateInstance<BehaviourTreeAssetBase>();

            try
            {
                // ── Squad BB: DetectedEnemies Transform[] (squadData, stride=5, slots 0..4) ──
                BlackboardDefinition.EnsureBaseChannel<Transform>(squadBBDef, "DetectedEnemies", isSquadData: true);
                squadDef.EnsureStrideApplied(maxAgents);

                // ── Agent BB: single Transform "DetectedEnemy" (stride=1, slot 0) ──
                agentBBDef.AddVariable<Transform>("DetectedEnemy", stride: 1);

                // ── Commander BB: DetectedEnemies Transform[] (stride=5, slots 0..4) ──
                commanderBBDef.AddVariable<Transform>("DetectedEnemies", stride: maxAgents);

                // ── Bindings ────────────────────────────────────────────
                // Agent → Squad (ToSquad): agent "DetectedEnemy" → squad "DetectedEnemies"
                var agentGroup = squadDef.GetOrCreateBindingGroup(agentTreeAsset);
                agentGroup.bindings.Add(new VariableBinding
                {
                    treeVariableName = "DetectedEnemy",
                    squadVariableName = "DetectedEnemies",
                    direction = BindingDirection.ToSquad
                });

                // Squad → Commander (FromSquad): squad "DetectedEnemies" → commander "DetectedEnemies"
                var commanderGroup = squadDef.GetOrCreateBindingGroup(commanderTreeAsset);
                commanderGroup.bindings.Add(new VariableBinding
                {
                    treeVariableName = "DetectedEnemies",
                    squadVariableName = "DetectedEnemies",
                    direction = BindingDirection.FromSquad
                });

                agentBBDef.sourceTreeAsset = agentTreeAsset;
                commanderBBDef.sourceTreeAsset = commanderTreeAsset;

                var squadGO = new GameObject("SquadGO");
                var squadInstance = squadGO.AddComponent<SquadInstance>();
                squadInstance.Initialize(squadDef, maxAgents);

                var agentGO = new GameObject("AgentGO");
                var agentBB = agentGO.AddComponent<BlackBoard>();
                agentBB.Initialize(agentBBDef);

                var commanderGO = new GameObject("CommanderGO");
                var commanderBB = commanderGO.AddComponent<BlackBoard>();
                commanderBB.Initialize(commanderBBDef);

                squadInstance.EnsureResolved(agentBBDef);
                squadInstance.EnsureResolved(commanderBBDef);

                var testTransforms = new Transform[maxAgents];
                for (int i = 0; i < maxAgents; i++)
                {
                    var go = new GameObject($"Agent_{i}");
                    testTransforms[i] = go.transform;
                }

                try
                {
                    // ═══════════════════════════════════════════════════════
                    // STEP 1: Agent writes its Transform to its own BB
                    // ═══════════════════════════════════════════════════════
                    agentBB.SetBoxed(0, testTransforms[agentIndex]);

                    // ═══════════════════════════════════════════════════════
                    // STEP 2: Copy agent → squad (ToSquad, agentOffset=3)
                    // ═══════════════════════════════════════════════════════
                    squadInstance.CopyFromBB(agentBB, agentBBDef, agentIndex);

                    // Verify EACH squad slot individually
                    int squadBase = ComputeSlot(squadBBDef, "DetectedEnemies");
                    Assert.That(squadBase, Is.EqualTo(0),
                        "Squad DetectedEnemies base slot should be 0 (first variable)");

                    object[] squadAll = new object[maxAgents];
                    for (int i = 0; i < maxAgents; i++)
                    {
                        squadAll[i] = squadInstance.BlackBoard.GetBoxed(squadBase + i);
                        if (i == agentIndex)
                            Assert.That(squadAll[i], Is.EqualTo(testTransforms[agentIndex]),
                                $"Squad DetectedEnemies[{i}] should be agent 3's Transform");
                        else
                            Assert.That(squadAll[i], Is.Null,
                                $"Squad DetectedEnemies[{i}] should be null (no agent wrote)");
                    }

                    // ═══════════════════════════════════════════════════════
                    // STEP 3: Copy squad → commander (FromSquad, bulk copy)
                    // ═══════════════════════════════════════════════════════
                    squadInstance.CopyToBB(commanderBB, commanderBBDef, agentOffset: -1);

                    // Verify EACH commander slot individually
                    int cmdrBase = ComputeSlot(commanderBBDef, "DetectedEnemies");
                    Assert.That(cmdrBase, Is.EqualTo(0),
                        "Commander DetectedEnemies base slot should be 0 (first variable)");

                    for (int i = 0; i < maxAgents; i++)
                    {
                        object cmdrVal = commanderBB.GetBoxed(cmdrBase + i);
                        if (i == agentIndex)
                            Assert.That(cmdrVal, Is.EqualTo(testTransforms[agentIndex]),
                                $"Commander DetectedEnemies[{i}] should be agent 3's Transform");
                        else
                            Assert.That(cmdrVal, Is.Null,
                                $"Commander DetectedEnemies[{i}] should be null (no agent wrote)");
                    }

                    // ═══════════════════════════════════════════════════════
                    // STEP 4: Verify commander reading correctly when
                    //         currentAgentOffset is applied (ForEachAgent)
                    // ═══════════════════════════════════════════════════════
                    commanderBB.currentAgentOffset = agentIndex;
                    object viaOffset = commanderBB.GetBoxed(cmdrBase);
                    Assert.That(viaOffset, Is.EqualTo(testTransforms[agentIndex]),
                        $"currentAgentOffset={agentIndex}: GetBoxed({cmdrBase}) " +
                        $"→ storage[{cmdrBase}+{agentIndex}] = storage[{cmdrBase + agentIndex}] " +
                        $"should be agent 3's Transform");

                    // Also verify via BoxedVariableHandle (bypasses currentAgentOffset)
                    var handle = commanderBB.GetVariable("DetectedEnemies");
                    object viaHandle = handle[agentIndex];
                    Assert.That(viaHandle, Is.EqualTo(testTransforms[agentIndex]),
                        $"handle[3] should be agent 3's Transform (handle bypasses currentAgentOffset)");

                    commanderBB.currentAgentOffset = 0;
                }
                finally
                {
                    for (int i = 0; i < maxAgents; i++)
                        if (testTransforms[i] != null)
                            Object.DestroyImmediate(testTransforms[i].gameObject);
                    Object.DestroyImmediate(agentGO);
                    Object.DestroyImmediate(commanderGO);
                    Object.DestroyImmediate(squadGO);
                }
            }
            finally
            {
                Object.DestroyImmediate(squadBBDef);
                Object.DestroyImmediate(agentBBDef);
                Object.DestroyImmediate(commanderBBDef);
                Object.DestroyImmediate(agentTreeAsset);
                Object.DestroyImmediate(commanderTreeAsset);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Test 9: Same as Test 8 but with other variables on the BBs
        //         preceding DetectedEnemies — validates that
        //         ComputeBaseSlot handles non-zero base slots correctly.
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void DetectedEnemies_WithPrecedingVariables_SlotsAreCorrect()
        {
            const int maxAgents = 4;
            const int agentIndex = 2;

            var squadBBDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            var agentBBDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            var commanderBBDef = ScriptableObject.CreateInstance<BlackboardDefinition>();

            squadDef.blackboardDefinition = squadBBDef;

            var agentTreeAsset = ScriptableObject.CreateInstance<BehaviourTreeAssetBase>();
            var commanderTreeAsset = ScriptableObject.CreateInstance<BehaviourTreeAssetBase>();

            try
            {
                // ── Squad BB (in order): ─────────────────────────────────
                // AgentRoles    int[]     stride=4  slots 0..3
                // AgentOrders   int[]     stride=4  slots 4..7
                // DetectedEnemies Transform[] stride=4  slots 8..11
                BlackboardDefinition.EnsureBaseChannel<int>(squadBBDef, "AgentRoles", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<int>(squadBBDef, "AgentOrders", isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<Transform>(squadBBDef, "DetectedEnemies", isSquadData: true);
                squadDef.EnsureStrideApplied(maxAgents);

                // ── Agent BB: single Transform "DetectedEnemy" (stride=1, slot 0) ──
                agentBBDef.AddVariable<Transform>("DetectedEnemy", stride: 1);

                // ── Commander BB (in order): ────────────────────────────
                // SomeFlag       bool      stride=1  slot 0
                // DetectedEnemies Transform[] stride=4  slots 1..4
                commanderBBDef.AddVariable<bool>("SomeFlag", stride: 1);
                commanderBBDef.AddVariable<Transform>("DetectedEnemies", stride: maxAgents);

                // ── Bindings ────────────────────────────────────────────
                var agentGroup = squadDef.GetOrCreateBindingGroup(agentTreeAsset);
                agentGroup.bindings.Add(new VariableBinding
                {
                    treeVariableName = "DetectedEnemy",
                    squadVariableName = "DetectedEnemies",
                    direction = BindingDirection.ToSquad
                });

                var commanderGroup = squadDef.GetOrCreateBindingGroup(commanderTreeAsset);
                commanderGroup.bindings.Add(new VariableBinding
                {
                    treeVariableName = "DetectedEnemies",
                    squadVariableName = "DetectedEnemies",
                    direction = BindingDirection.FromSquad
                });

                agentBBDef.sourceTreeAsset = agentTreeAsset;
                commanderBBDef.sourceTreeAsset = commanderTreeAsset;

                var squadGO = new GameObject("SquadGO");
                var squadInstance = squadGO.AddComponent<SquadInstance>();
                squadInstance.Initialize(squadDef, maxAgents);

                var agentGO = new GameObject("AgentGO");
                var agentBB = agentGO.AddComponent<BlackBoard>();
                agentBB.Initialize(agentBBDef);

                var commanderGO = new GameObject("CommanderGO");
                var commanderBB = commanderGO.AddComponent<BlackBoard>();
                commanderBB.Initialize(commanderBBDef);

                squadInstance.EnsureResolved(agentBBDef);
                squadInstance.EnsureResolved(commanderBBDef);

                var testTransforms = new Transform[maxAgents];
                for (int i = 0; i < maxAgents; i++)
                {
                    var go = new GameObject($"Agent_{i}");
                    testTransforms[i] = go.transform;
                }

                try
                {
                    // Write
                    agentBB.SetBoxed(0, testTransforms[agentIndex]);

                    // ── Squad base slot check ───────────────────────────
                    int squadBase = ComputeSlot(squadBBDef, "DetectedEnemies");
                    Assert.That(squadBase, Is.EqualTo(8),
                        "Squad DetectedEnemies base slot: AgentRoles(0..3) + AgentOrders(4..7) = 8");

                    // Copy agent → squad
                    squadInstance.CopyFromBB(agentBB, agentBBDef, agentIndex);

                    object squadVal = squadInstance.BlackBoard.GetBoxed(squadBase + agentIndex);
                    Assert.That(squadVal, Is.EqualTo(testTransforms[agentIndex]),
                        $"Squad DetectedEnemies[{agentIndex}] at slot {squadBase + agentIndex} " +
                        "should be agent 2's Transform");

                    // ── Commander base slot check ───────────────────────
                    int cmdrBase = ComputeSlot(commanderBBDef, "DetectedEnemies");
                    Assert.That(cmdrBase, Is.EqualTo(1),
                        "Commander DetectedEnemies base slot: SomeFlag(0) + [0] = 1");

                    // Copy squad → commander
                    squadInstance.CopyToBB(commanderBB, commanderBBDef, agentOffset: -1);

                    object cmdrVal = commanderBB.GetBoxed(cmdrBase + agentIndex);
                    Assert.That(cmdrVal, Is.EqualTo(testTransforms[agentIndex]),
                        $"Commander DetectedEnemies[{agentIndex}] at slot {cmdrBase + agentIndex} " +
                        "should be agent 2's Transform");

                    // Verify via currentAgentOffset
                    commanderBB.currentAgentOffset = agentIndex;
                    object viaOffset = commanderBB.GetBoxed(cmdrBase);
                    Assert.That(viaOffset, Is.EqualTo(testTransforms[agentIndex]),
                        $"currentAgentOffset={agentIndex}: GetBoxed({cmdrBase}) " +
                        $"→ storage[{cmdrBase + agentIndex}] should be agent 2's Transform");

                    commanderBB.currentAgentOffset = 0;
                }
                finally
                {
                    for (int i = 0; i < maxAgents; i++)
                        if (testTransforms[i] != null)
                            Object.DestroyImmediate(testTransforms[i].gameObject);
                    Object.DestroyImmediate(agentGO);
                    Object.DestroyImmediate(commanderGO);
                    Object.DestroyImmediate(squadGO);
                }
            }
            finally
            {
                Object.DestroyImmediate(squadBBDef);
                Object.DestroyImmediate(agentBBDef);
                Object.DestroyImmediate(commanderBBDef);
                Object.DestroyImmediate(agentTreeAsset);
                Object.DestroyImmediate(commanderTreeAsset);
            }
        }
    }
}
