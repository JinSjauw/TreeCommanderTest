using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// How the squad handles leader death.
    /// </summary>
    public enum LeaderDeathBehavior
    {
        /// <summary>All agents scatter and act independently. Commander tree detects no leader.</summary>
        Scatter,
        /// <summary>Promote the next alive agent in the squad to leader.</summary>
        Promote
    }

    /// <summary>
    /// Manages squad lifecycle: spawning, runtime registration/deregistration,
    /// role assignment, and leader tracking with death-handling.
    /// 
    /// Replaces SquadSpawner with extended squad management capabilities.
    /// </summary>
    public class SquadManager : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField, Tooltip("The squad data schema.")]
        private SquadDefinition definition;

        [Header("Prefabs")]
        [SerializeField, Tooltip("Prefab with CommanderTreeRunner component.")]
        private GameObject commanderPrefab;

        [SerializeField, Tooltip("Fallback prefab used when a SquadRole has no prefab assigned.")]
        private GameObject agentPrefab;

        [Header("Spawn")]
        [SerializeField, Min(0), Tooltip("Number of agents to spawn on start.")]
        private int agentCount = 3;

        [SerializeField, Tooltip("Spawn automatically in Start().")]
        private bool spawnOnStart = true;

        [SerializeField, Tooltip("The patrolpoints collection")]
        private Transform patrolpointsParent;

        [Header("Leader")]
        [SerializeField, Tooltip("Agent index (in managed list) that starts as leader. 0 = first agent.")]
        private int initialLeaderIndex;

        [SerializeField, Tooltip("What happens when the leader is destroyed.")]
        private LeaderDeathBehavior leaderDeathBehavior = LeaderDeathBehavior.Promote;

        // ── Runtime state ──────────────────────────────────────────────

        private SquadInstance squadInstance;
        private CommanderTreeRunner commanderRunner;
        private readonly List<AgentTreeRunner> managedAgents = new List<AgentTreeRunner>();
        private int currentLeaderIndex = -1;
        private bool hasInitialized = false;

        /// <summary>Expanded role index per agent slot. Built once at init from SquadDefinition.availableRoles.
        /// agentRoleIndices[slotIndex] = roleIndex in availableRoles.</summary>
        private int[] agentRoleIndices;

        // ── BB slots (cached for fast write) ────────────────────

        private int leaderSlot = -1;
        private int squadMovePosSlot = -1;
        private int agentMoveSpeedSlot = -1;
        private int patrolpointsParentSlot = -1;
        private int agentCountSlot = -1;
        private int leaderTransformSlot = -1;

        // ── Public accessors ────────────────────────────────────────────

        public SquadInstance SquadInstance => squadInstance;
        public CommanderTreeRunner Commander => commanderRunner;
        public IReadOnlyList<AgentTreeRunner> Agents => managedAgents;
        public int LeaderIndex => currentLeaderIndex;
        public AgentTreeRunner Leader => (currentLeaderIndex >= 0 && currentLeaderIndex < managedAgents.Count)
            ? managedAgents[currentLeaderIndex]
            : null;
        public bool HasLeader => Leader != null;

        // ── Public API ─────────────────────────────────────────────────

        /// <summary>Prevents auto-spawn on Start. Call before activating the GameObject when spawning via object pool.</summary>
        public void DisableAutoSpawn()
        {
            spawnOnStart = false;
        }

        /// <summary>Spawns using serialized fields. Agent prefabs are read from the SquadDefinition's role composition.</summary>
        public void Spawn(Transform patrolPointsOverride = null, Vector3? squadMovePosition = null)
        {
            Spawn(commanderPrefab, agentCount, patrolPointsOverride, squadMovePosition);
        }

        /// <summary>Spawns with explicit commander prefab override.</summary>
        public void Spawn(GameObject commanderPrefabOverride, int count, Transform patrolPointsOverride = null, Vector3? squadMovePosition = null)
        {
            if (hasInitialized)
            {
                Debug.LogWarning("[SquadManager] Already initialized. Despawn first.");
                return;
            }

            if (definition == null)
            {
                Debug.LogError("[SquadManager] SquadDefinition is null.");
                return;
            }

            this.commanderPrefab = commanderPrefabOverride != null ? commanderPrefabOverride : commanderPrefab;
            this.agentCount = count;
            if (patrolPointsOverride != null)
                patrolpointsParent = patrolPointsOverride;

            SpawnSquad();

            if (squadMovePosition.HasValue)
                WriteSquadMovePosition(squadMovePosition.Value);
        }

        /// <summary>
        /// Spawns the squad and positions agents in a circle formation around the given center.
        /// Mirrors the CalculateFormation node logic: agent 0 (leader) at center,
        /// agents 1..N-1 evenly distributed around the circle.
        /// </summary>
        public void SpawnInFormation(Vector3 center, int count, float radius, Transform patrolPointsOverride = null, Vector3? squadMovePosition = null)
        {
            if (hasInitialized)
            {
                Debug.LogWarning("[SquadManager] Already initialized. Despawn first.");
                return;
            }

            if (definition == null)
            {
                Debug.LogError("[SquadManager] SquadDefinition is null.");
                return;
            }

            this.agentCount = count;
            if (patrolPointsOverride != null)
                patrolpointsParent = patrolPointsOverride;

            SpawnSquad();

            // Position agents in circle formation, matching CalculateFormation math
            for (int i = 0; i < managedAgents.Count; i++)
            {
                Vector3 position;
                if (i == 0)
                {
                    position = center;
                }
                else
                {
                    float angle = (i / (float)(managedAgents.Count - 1)) * 360f * Mathf.Deg2Rad;
                    position = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                }

                if (NavMesh.SamplePosition(position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    position = hit.position;
                }

                managedAgents[i].transform.position = position;

                NavMeshAgent navAgent = managedAgents[i].GetComponentInChildren<NavMeshAgent>();
                if (navAgent != null)
                {
                    navAgent.Warp(position);
                }
            }

            if (squadMovePosition.HasValue)
                WriteSquadMovePosition(squadMovePosition.Value);
        }

        /// <summary>Deregisters and destroys all agents and commander, then the squad instance.</summary>
        public void Despawn()
        {
            DespawnSquad();
        }

        /// <summary>
        /// Registers an already-existing AgentTreeRunner with this squad manager.
        /// Assigns a role and writes leader status to the squad BB.
        /// Returns the agent's index in the managed list, or -1 if registration failed.
        /// </summary>
        public int RegisterAgent(AgentTreeRunner agent)
        {
            if (agent == null || managedAgents.Contains(agent))
                return -1;

            if (commanderRunner == null || squadInstance == null)
            {
                Debug.LogError("[SquadManager] Cannot register agent: squad not initialized.");
                return -1;
            }

            // Check slot capacity against squad definition
            int maxSlots = definition.TotalAgentSlots;
            if (maxSlots > 0 && managedAgents.Count >= maxSlots)
            {
                Debug.LogError($"[SquadManager] Cannot register agent: slot capacity ({maxSlots}) exceeded.");
                return -1;
            }

            int agentIndex = managedAgents.Count;

            // Wire up references
            agent.commander = commanderRunner;
            agent.squadInstance = squadInstance;
            agent.Initialize();
            agent.RunIndependently = false;

            // Registration
            commanderRunner.RegisterAgent(agent);
            agent.RegisterSquad(squadInstance);

            // Assign role and leader status
            AssignRole(agent, agentIndex);
            WriteAgentMoveSpeed(agent, agentIndex);

            managedAgents.Add(agent);

            // If this is the first agent and no leader exists, make it leader
            if (currentLeaderIndex < 0)
            {
                SetLeader(agentIndex);
            }

            return agentIndex;
        }

        /// <summary>
        /// Unregisters an agent from the squad. Does NOT destroy the GameObject.
        /// If this was the leader, triggers leader death behavior.
        /// Squad BB slot compaction and invalidation are handled by
        /// CommanderTreeRunner.UnregisterAgent.
        /// </summary>
        public void UnregisterAgent(AgentTreeRunner agent)
        {
            if (agent == null || !managedAgents.Contains(agent))
                return;

            int removedIndex = managedAgents.IndexOf(agent);
            bool wasLeader = (removedIndex == currentLeaderIndex);

            agent.UnregisterSquad(squadInstance);
            commanderRunner.UnregisterAgent(agent);  // handles BB slot compaction & invalidation
            agent.commander = null;
            agent.squadInstance = null;

            managedAgents.RemoveAt(removedIndex);

            // Adjust leader index if agents shifted
            if (removedIndex < currentLeaderIndex)
                currentLeaderIndex--;

            if (wasLeader)
            {
                ClearLeader();
                HandleLeaderDeath();
            }
        }

        /// <summary>
        /// Manually promotes the next alive agent to leader.
        /// Public so commander trees or external scripts can trigger it.
        /// </summary>
        public void PromoteNewLeader()
        {
            // Clear old leader first
            if (currentLeaderIndex >= 0)
                ClearLeader();

            // Find next alive agent
            for (int i = 0; i < managedAgents.Count; i++)
            {
                if (managedAgents[i] != null && managedAgents[i].gameObject.activeInHierarchy)
                {
                    SetLeader(i);
                    return;
                }
            }

            // No agents left
            currentLeaderIndex = -1;
        }

        // ── Unity messages ─────────────────────────────────────────────

        private void Start()
        {
            if (spawnOnStart)
                Spawn();
        }

        private void Update()
        {
            if (!hasInitialized || currentLeaderIndex < 0)
                return;

            MonitorLeaderStatus();
        }

        private void OnDestroy()
        {
            DespawnSquad();
        }

        // ── Spawn / Despawn ────────────────────────────────────────────

        private void SpawnSquad()
        {
            // 1 — Commander
            if (commanderPrefab == null)
            {
                Debug.LogError("[SquadManager] Commander prefab is null.");
                return;
            }

            GameObject commanderGo = Instantiate(commanderPrefab, transform);
            commanderGo.name = $"[Commander] {commanderPrefab.name}";
            commanderRunner = commanderGo.GetComponent<CommanderTreeRunner>();
            if (commanderRunner == null)
            {
                Debug.LogError($"[SquadManager] Commander prefab '{commanderPrefab.name}' has no CommanderTreeRunner.");
                Destroy(commanderGo);
                return;
            }

            commanderRunner.Initialize();

            // 2 — Max squad size from squad definition's role composition
            int maxSize = definition.TotalAgentSlots;
            if (maxSize <= 0)
                maxSize = 1;

            // Build flat role-index array: one entry per slot, value = role index in availableRoles
            BuildRoleIndices(maxSize);

            // 3 — Squad instance
            GameObject squadGo = new GameObject($"[Squad] {definition.name}");
            squadInstance = squadGo.AddComponent<SquadInstance>();
            squadInstance.Initialize(definition, maxSize);

            // Cache squad BB slot info
            CacheSquadSlots();
            
            WritePatrolPoints();
            WriteAgentCount();
            WriteLeaderTransform();

            // 4 — Register commander with squad
            commanderRunner.RegisterSquad(squadInstance);

            // 5 — Agents
            int effectiveCount = Mathf.Min(agentCount, maxSize);
            if (agentCount > maxSize)
            {
                Debug.LogWarning($"[SquadManager] agentCount ({agentCount}) exceeds total agent slots ({maxSize}). Clamping to {effectiveCount}.");
            }
            for (int i = 0; i < effectiveCount; i++)
            {
                SpawnAgent(i);
            }

            // 6 — Set initial leader
            if (managedAgents.Count > 0)
            {
                int leaderIdx = Mathf.Clamp(initialLeaderIndex, 0, managedAgents.Count - 1);
                SetLeader(leaderIdx);
            }

            hasInitialized = true;
        }




        private void SpawnAgent(int agentIndex)
        {
            // Resolve the role for this slot index
            SquadRole role = definition.GetRoleForSlot(agentIndex);
            if (role == null)
            {
                Debug.LogError($"[SquadManager] No role defined for agent slot {agentIndex}.");
                return;
            }
            
            // Get prefab from the role, falling back to SquadManager default
            GameObject prefab = role.prefab != null ? role.prefab : agentPrefab;
            if (prefab == null)
            {
                Debug.LogError($"[SquadManager] No prefab for role '{role.name}' (slot {agentIndex}) and no fallback agentPrefab.");
                return;
            }

            GameObject agentGo = Instantiate(prefab, transform);
            agentGo.name = $"[Agent {agentIndex}] {role.name} | {prefab.name}";

            AgentTreeRunner agent = agentGo.GetComponent<AgentTreeRunner>();
            if (agent == null)
            {
                Debug.LogError($"[SquadManager] Prefab '{prefab.name}' (role '{role.name}') has no AgentTreeRunner.");
                Destroy(agentGo);
                return;
            }

            agent.commander = commanderRunner;
            agent.squadInstance = squadInstance;
            agent.Initialize();
            agent.RunIndependently = false;

            NavMeshAgent nav = agentGo.GetComponentInChildren<NavMeshAgent>();
            if (nav != null)
                nav.avoidancePriority += agentIndex;

            commanderRunner.RegisterAgent(agent);
            AssignRole(agent, agentIndex);
            WriteAgentMoveSpeed(agent, agentIndex);

            managedAgents.Add(agent);
        }

        private void DespawnSquad()
        {
            if (!hasInitialized) return;

            // Clear leader on BB before destroying
            if (currentLeaderIndex >= 0)
                ClearLeader();

            // Destroy agents
            for (int i = managedAgents.Count - 1; i >= 0; i--)
            {
                if (managedAgents[i] != null)
                    Destroy(managedAgents[i].gameObject);
            }
            managedAgents.Clear();

            // Destroy commander
            if (commanderRunner != null)
            {
                Destroy(commanderRunner.gameObject);
                commanderRunner = null;
            }

            // Destroy squad instance
            if (squadInstance != null)
            {
                Destroy(squadInstance.gameObject);
                squadInstance = null;
            }

            currentLeaderIndex = -1;
            leaderSlot = -1;
            squadMovePosSlot = -1;
            agentMoveSpeedSlot = -1;
            hasInitialized = false;
        }

        // ── Role assignment (flat array, built once at init) ──────────────

        /// <summary>
        /// Expands SquadDefinition.availableRoles into a flat int[] where
        /// each role is repeated maxAmount times. agentRoleIndices[slotIndex]
        /// directly gives the role index in availableRoles.
        /// </summary>
        private void BuildRoleIndices(int maxSize)
        {
            if (definition?.availableRoles == null) return;

            agentRoleIndices = new int[maxSize];
            int slot = 0;
            for (int roleIdx = 0; roleIdx < definition.availableRoles.Count; roleIdx++)
            {
                int amount = Mathf.Max(1, definition.availableRoles[roleIdx].maxAmount);
                for (int j = 0; j < amount && slot < maxSize; j++)
                    agentRoleIndices[slot++] = roleIdx;
            }
            // Fill remaining slots with fallback (last role) if any
            while (slot < maxSize)
                agentRoleIndices[slot++] = definition.availableRoles.Count - 1;
        }

        /// <summary>
        /// Assigns a role to an agent by direct lookup into the pre-built flat array.
        /// </summary>
        private void AssignRole(AgentTreeRunner agent, int agentIndex)
        {
            if (agentRoleIndices == null || agentIndex < 0 || agentIndex >= agentRoleIndices.Length)
                return;

            int roleIndex = agentRoleIndices[agentIndex];

            BlackBoard bb = agent.BlackBoard;
            BlackboardDefinition bbDef = bb?.Definition;
            if (bbDef == null) return;

            int agentRoleSlot = ComputeSlotForVariable(bbDef, "AgentAssignedRole");
            if (agentRoleSlot < 0) return;

            bb.SetBoxed(agentRoleSlot, roleIndex);
        }

        // ── Leader management ──────────────────────────────────────────

        /// <summary>
        /// Caches BB slots for LeaderIndex, SquadMovePosition, and AgentMoveSpeed.
        /// </summary>
        private void CacheSquadSlots()
        {
            leaderSlot = -1;
            squadMovePosSlot = -1;
            agentMoveSpeedSlot = -1;
            patrolpointsParentSlot = -1;

            if (squadInstance?.BlackBoard?.Definition == null) return;

            BlackboardDefinition squadDef = squadInstance.BlackBoard.Definition;
            leaderSlot = ComputeSlotForVariable(squadDef, "LeaderIndex");
            agentMoveSpeedSlot = ComputeSlotForVariable(squadDef, "AgentMoveSpeed");

            BlackboardDefinition commanderDef = commanderRunner.BlackBoard.Definition;
            squadMovePosSlot = ComputeSlotForVariable(commanderDef, "SquadMovePosition");
            patrolpointsParentSlot = ComputeSlotForVariable(commanderDef, "PatrolPoints");
            agentCountSlot = ComputeSlotForVariable(commanderDef, "AgentCount");
            leaderTransformSlot = ComputeSlotForVariable(commanderDef, "LeaderTransform");
            Debug.Log($"[SquadManager.CacheSquadSlots] agentCountSlot={agentCountSlot} | leaderTransformSlot={leaderTransformSlot}");
        }

        private static int ComputeSlotForVariable(BlackboardDefinition def, string varName)
        {
            if (def == null) return -1;
            int varIndex = def.GetVariableIndex(varName);
            if (varIndex < 0) return -1;

            Debug.Log($"[SquadManager.ComputeSlotForVariable] def={def} | varName={varName} | varIndex={varIndex}");

            IReadOnlyList<BlackboardVariableBase> vars = def.GetAllVariables();
            int slot = 0;
            for (int i = 0; i < varIndex && i < vars.Count; i++)
            {
                int stride = vars[i].Stride;
                slot += (stride > 1) ? stride : 1;
            }
            return slot;
        }

        /// <summary>Writes the leader index to LeaderIndex and the leader's position to SquadMovePosition.</summary>
        private void SetLeader(int agentIndex)
        {
            if (leaderSlot < 0 || squadInstance?.BlackBoard == null) return;
            squadInstance.BlackBoard.SetBoxed(leaderSlot, agentIndex);
            currentLeaderIndex = agentIndex;

            // Write leader position to SquadMovePosition on init
            WriteSquadMovePosition();
            WriteLeaderTransform();
        }

        /// <summary>Writes -1 (no leader) to the LeaderIndex slot.</summary>
        private void ClearLeader()
        {
            if (leaderSlot < 0 || squadInstance?.BlackBoard == null) return;
            squadInstance.BlackBoard.SetBoxed(leaderSlot, -1);
            currentLeaderIndex = -1;
        }

        /// <summary>Writes the current leader's transform position into SquadMovePosition on the commander BB.</summary>
        private void WriteSquadMovePosition()
        {
            if (squadMovePosSlot < 0 || commanderRunner?.BlackBoard == null) return;
            AgentTreeRunner leader = Leader;
            Debug.Log($"[SquadManager.WriteSquadMovePosition] leader={leader.name}");
            if (leader == null) return;
            commanderRunner.BlackBoard.SetBoxedRaw(squadMovePosSlot, leader.transform.position);
            Debug.Log($"[SquadManager.WriteSquadMovePosition] leader={leader.name} pos={leader.transform.position:F2}");
        }

        /// <summary>Writes an explicit position into SquadMovePosition on the commander BB.</summary>
        private void WriteSquadMovePosition(Vector3 position)
        {
            if (squadMovePosSlot < 0 || commanderRunner?.BlackBoard == null) return;
            commanderRunner.BlackBoard.SetBoxedRaw(squadMovePosSlot, position);
        }

        private void WritePatrolPoints()
        {
            if (patrolpointsParentSlot < 0 || commanderRunner?.BlackBoard == null) return;
            
            commanderRunner.BlackBoard.SetBoxedRaw(patrolpointsParentSlot, patrolpointsParent);
            Debug.Log($"[SquadManager.WritePatrolPoints]"); 
        }

        private void WriteAgentCount()
        {
            if (agentCountSlot < 0 || commanderRunner?.BlackBoard == null) return;
            
            commanderRunner.BlackBoard.SetBoxedRaw(agentCountSlot, agentCount);
            Debug.Log($"[SquadManager.WriteAgentCount] agentCount={agentCount}");
        }

        private void WriteLeaderTransform()
        {
            if (currentLeaderIndex < 0)
                return;

                       if (leaderTransformSlot < 0 || commanderRunner?.BlackBoard == null) return;
            
            commanderRunner.BlackBoard.SetBoxedRaw(leaderTransformSlot, managedAgents[currentLeaderIndex].transform);
            Debug.Log($"[SquadManager.WriteLeaderTransform]"); 
        }

        /// <summary>
        /// Reads the agent's NavMeshAgent.speed and writes it to AgentMoveSpeed[index] on the squad BB.
        /// </summary>
        private void WriteAgentMoveSpeed(AgentTreeRunner agent, int agentIndex)
        {
            if (agentMoveSpeedSlot < 0 || squadInstance?.BlackBoard == null) return;
            if (agentIndex < 0) return;

            NavMeshAgent navAgent = agent.GetComponent<NavMeshAgent>();
            float speed = navAgent != null ? navAgent.speed : 1f;
            squadInstance.BlackBoard.SetBoxedRaw(agentMoveSpeedSlot + agentIndex, speed);
        }

        /// <summary>Writes -1f to AgentMoveSpeed[index] to mark the slot as invalid.</summary>
        private void InvalidateAgentMoveSpeed(int agentIndex)
        {
            if (agentMoveSpeedSlot < 0 || squadInstance?.BlackBoard == null) return;
            if (agentIndex < 0) return;
            squadInstance.BlackBoard.SetBoxedRaw(agentMoveSpeedSlot + agentIndex, -1f);
        }

        /// <summary>Checks if the current leader is still alive. Handles death according to config.</summary>
        private void MonitorLeaderStatus()
        {
            AgentTreeRunner leader = Leader;
            if (leader != null && leader.gameObject.activeInHierarchy)
                return;

            // Leader is dead or inactive
            ClearLeader();
            HandleLeaderDeath();
        }

        private void HandleLeaderDeath()
        {
            switch (leaderDeathBehavior)
            {
                case LeaderDeathBehavior.Promote:
                    PromoteNewLeader();
                    break;

                case LeaderDeathBehavior.Scatter:
                    // LeaderIndex = -1 already set by ClearLeader.
                    // Commander tree detects -1 → no leader → scatter.
                    break;
            }
        }
    }
}
