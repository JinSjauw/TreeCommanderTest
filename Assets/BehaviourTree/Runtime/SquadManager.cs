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

        [SerializeField, Tooltip("Prefab with AgentTreeRunner component.")]
        private GameObject agentPrefab;

        [Header("Spawn")]
        [SerializeField, Min(0), Tooltip("Number of agents to spawn on start.")]
        private int agentCount = 3;

        [SerializeField, Tooltip("Spawn automatically in Start().")]
        private bool spawnOnStart = true;

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
        private bool hasInitialized;

        // ── Squad BB slots (cached for fast write) ────────────────────

        private int leaderSlot = -1;
        private int squadMovePosSlot = -1;
        private int agentMoveSpeedSlot = -1;

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

        /// <summary>Spawns using serialized fields.</summary>
        public void Spawn()
        {
            Spawn(commanderPrefab, agentPrefab, agentCount);
        }

        /// <summary>Spawns with explicit parameters.</summary>
        public void Spawn(GameObject commanderPrefabOverride, GameObject agentPrefabOverride, int count)
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
            this.agentPrefab = agentPrefabOverride != null ? agentPrefabOverride : agentPrefab;
            this.agentCount = count;

            SpawnSquad();
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

            // Check stride capacity
            int maxStride = commanderRunner.GetMaxSquadDataStride();
            if (maxStride > 0 && managedAgents.Count >= maxStride)
            {
                Debug.LogError($"[SquadManager] Cannot register agent: stride ({maxStride}) exceeded.");
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
        /// </summary>
        public void UnregisterAgent(AgentTreeRunner agent)
        {
            if (agent == null || !managedAgents.Contains(agent))
                return;

            int removedIndex = managedAgents.IndexOf(agent);
            bool wasLeader = (removedIndex == currentLeaderIndex);

            agent.UnregisterSquad(squadInstance);
            commanderRunner.UnregisterAgent(agent);
            agent.commander = null;
            agent.squadInstance = null;

            InvalidateAgentMoveSpeed(removedIndex);

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

            // 2 — Max squad size
            int maxSize = commanderRunner.GetMaxSquadDataStride();
            if (maxSize <= 0)
                maxSize = 8;

            // 3 — Squad instance
            GameObject squadGo = new GameObject($"[Squad] {definition.name}");
            squadInstance = squadGo.AddComponent<SquadInstance>();
            squadInstance.Initialize(definition, maxSize);

            // Cache squad BB slot info
            CacheSquadSlots();

            // 4 — Register commander with squad
            commanderRunner.RegisterSquad(squadInstance);

            // 5 — Agents
            int effectiveCount = Mathf.Min(agentCount, maxSize);
            if (agentCount > maxSize)
            {
                Debug.LogWarning($"[SquadManager] agentCount ({agentCount}) exceeds max stride ({maxSize}). Clamping to {effectiveCount}.");
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
            if (agentPrefab == null)
            {
                Debug.LogError("[SquadManager] Agent prefab is null.");
                return;
            }

            GameObject agentGo = Instantiate(agentPrefab, transform);
            agentGo.name = $"[Agent {agentIndex}] {agentPrefab.name}";

            AgentTreeRunner agent = agentGo.GetComponent<AgentTreeRunner>();
            if (agent == null)
            {
                Debug.LogError($"[SquadManager] Agent prefab '{agentPrefab.name}' has no AgentTreeRunner.");
                Destroy(agentGo);
                return;
            }

            agent.commander = commanderRunner;
            agent.squadInstance = squadInstance;
            agent.Initialize();
            agent.RunIndependently = false;

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

        // ── Role assignment ────────────────────────────────────────────

        /// <summary>
        /// Assigns a role to an agent based on SquadDefinition.availableRoles.
        /// Respects maxAmount per role. Roles are filled in definition order;
        /// when all non-fallback roles are full, falls back to isFallback roles.
        /// </summary>
        private void AssignRole(AgentTreeRunner agent, int agentIndex)
        {
            if (definition == null || definition.availableRoles == null || definition.availableRoles.Count == 0)
                return;

            BlackBoard bb = agent.BlackBoard;
            BlackboardDefinition bbDef = bb?.Definition;
            if (bbDef == null) return;

            int agentRoleSlot = ComputeSlotForVariable(bbDef, "AgentAssignedRole");
            if (agentRoleSlot < 0) return;

            // Count current role assignments across all managed agents
            int[] roleCounts = new int[definition.availableRoles.Count];

            // Also count roles on squad BB for agents registered externally
            CountRoleAssignmentsOnSquad(roleCounts);

            // Pick best role: fill non-fallback first, then fallback
            int roleIndex = FindAvailableRole(roleCounts, agentIndex);
            bb.SetBoxed(agentRoleSlot, roleIndex);
        }

        private void CountRoleAssignmentsOnSquad(int[] roleCounts)
        {
            if (squadInstance?.BlackBoard?.Definition == null) return;

            BlackboardDefinition squadDef = squadInstance.BlackBoard.Definition;
            int rolesVarIndex = squadDef.GetVariableIndex("AgentRoles");
            if (rolesVarIndex < 0) return;

            int rolesBaseSlot = ComputeSlotForVariable(squadDef, "AgentRoles");
            if (rolesBaseSlot < 0) return;

            int stride = squadDef.GetAllVariables()[rolesVarIndex].Stride;
            for (int i = 0; i < stride && i < roleCounts.Length; i++)
            {
                object val = squadInstance.BlackBoard.GetBoxed(rolesBaseSlot + i);
                int role = val is int intVal ? intVal : -1;
                if (role >= 0 && role < roleCounts.Length)
                    roleCounts[role]++;
            }
        }

        private int FindAvailableRole(int[] roleCounts, int agentIndex)
        {
            // First pass: non-fallback roles that aren't full
            for (int i = 0; i < definition.availableRoles.Count; i++)
            {
                SquadRole role = definition.availableRoles[i];
                if (role.isFallback) continue;
                if (roleCounts[i] < role.maxAmount)
                    return i;
            }

            // Second pass: fallback roles
            for (int i = 0; i < definition.availableRoles.Count; i++)
            {
                SquadRole role = definition.availableRoles[i];
                if (!role.isFallback) continue;
                if (roleCounts[i] < role.maxAmount)
                    return i;
            }

            // All full — cycle as last resort
            return agentIndex % definition.availableRoles.Count;
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

            if (squadInstance?.BlackBoard?.Definition == null) return;

            BlackboardDefinition squadDef = squadInstance.BlackBoard.Definition;

            leaderSlot = ComputeSlotForVariable(squadDef, "LeaderIndex");
            squadMovePosSlot = ComputeSlotForVariable(squadDef, "SquadMovePosition");
            agentMoveSpeedSlot = ComputeSlotForVariable(squadDef, "AgentMoveSpeed");
        }

        private static int ComputeSlotForVariable(BlackboardDefinition def, string varName)
        {
            if (def == null) return -1;
            int varIndex = def.GetVariableIndex(varName);
            if (varIndex < 0) return -1;

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
        }

        /// <summary>Writes -1 (no leader) to the LeaderIndex slot.</summary>
        private void ClearLeader()
        {
            if (leaderSlot < 0 || squadInstance?.BlackBoard == null) return;
            squadInstance.BlackBoard.SetBoxed(leaderSlot, -1);
            currentLeaderIndex = -1;
        }

        /// <summary>Writes the current leader's transform position into SquadMovePosition on the squad BB.</summary>
        private void WriteSquadMovePosition()
        {
            if (squadMovePosSlot < 0 || squadInstance?.BlackBoard == null) return;
            AgentTreeRunner leader = Leader;
            if (leader == null) return;
            squadInstance.BlackBoard.SetBoxed(squadMovePosSlot, leader.transform.position);
        }

        /// <summary>
        /// Reads the agent's NavMeshAgent.speed and writes it to AgentMoveSpeed[index] on the squad BB.
        /// </summary>
        private void WriteAgentMoveSpeed(AgentTreeRunner agent, int agentIndex)
        {
            if (agentMoveSpeedSlot < 0 || squadInstance?.BlackBoard == null) return;
            if (agentIndex < 0) return;

            NavMeshAgent navAgent = agent.GetComponent<NavMeshAgent>();
            float speed = navAgent != null ? navAgent.speed : 3.5f;
            squadInstance.BlackBoard.SetBoxed(agentMoveSpeedSlot + agentIndex, speed);
        }

        /// <summary>Writes -1f to AgentMoveSpeed[index] to mark the slot as invalid.</summary>
        private void InvalidateAgentMoveSpeed(int agentIndex)
        {
            if (agentMoveSpeedSlot < 0 || squadInstance?.BlackBoard == null) return;
            if (agentIndex < 0) return;
            squadInstance.BlackBoard.SetBoxed(agentMoveSpeedSlot + agentIndex, -1f);
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
