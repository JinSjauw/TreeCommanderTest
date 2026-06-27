using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Orchestrates a group of agents under a commander behaviour tree.
    /// Per-frame flow:
    ///   1. EvaluateCommander:
    ///      a. Squads → commander BB (agent status from last frame)
    ///      b. Commander evaluates (reads state, writes orders)
    ///      c. Commander → squads (orders)
    ///   2. TickAgents: for each agent
    ///      a. Push tracked bindings → agent self BB
    ///      b. Squad → agent BB (fresh orders + status)
    ///      c. Agent evaluates (reacts to orders)
    ///      d. Agent → squad BB (reports new status)
    /// Communication between agents and commander happens exclusively through
    /// the squad blackboard channel.
    /// </summary>
    [RequireComponent(typeof(BlackBoard))]
    public class CommanderTreeRunner : BehaviourTreeRunnerBase
    {
        [SerializeField] private List<AgentTreeRunner> registeredAgents = new List<AgentTreeRunner>();

        /// <summary>Squad instances this commander has registered with.</summary>
        [System.NonSerialized] public List<SquadInstance> registeredSquads = new List<SquadInstance>();

        private void Start()
        {
            Initialize();
        }

        protected override void OnDisable()
        {
            // Deregister all agents — they may outlive the commander.
            // Remove from the end so each removal doesn't require shifting
            // subsequent slots (compaction is still called for invalidation).
            for (int i = registeredAgents.Count - 1; i >= 0; i--)
            {
                AgentTreeRunner agent = registeredAgents[i];
                if (agent != null)
                {
                    agent.commander = null;
                }
                CompactAndInvalidateSquadSlots(i);
                registeredAgents.RemoveAt(i);
            }
        }

        private void Update()
        {
            if (evaluator == null || blackBoard == null) return;

            PushTrackedBindings();
            EvaluateCommander();
            TickAgents();
        }

        /// <summary>
        /// Agents tick after commander: squad BB has fresh orders from commander.
        /// Each agent reads squad data, pushes own data providers, evaluates,
        /// then writes results back to squad BB.
        /// The agent index is used as the offset into per-agent squad data slots.
        /// </summary>
        private void TickAgents()
        {
            for (int i = 0; i < registeredAgents.Count; i++)
            {
                AgentTreeRunner agent = registeredAgents[i];
                if (agent == null)
                {
                    continue;
                }
                agent.PushTrackedBindings();
                CopySquadsToTree(agent, i);
                agent.Evaluate();
                // Skip write-back if the agent died during Evaluate — compaction
                // already invalidated and shifted its squad BB slots, so writing
                // with the old offset would corrupt the next agent's data.
                if (agent.commander != null)
                    CopySquadsFromTree(agent, i);
            }
        }

        /// <summary>
        /// Copies squad data into a tree runner's BB (agent or commander).
        /// When agentOffset is >= 0, per-agent squad variables use it as
        /// the slot offset to read the correct agent's data.
        /// </summary>
        private static void CopySquadsToTree(BehaviourTreeRunnerBase runner, int agentOffset = -1)
        {
            List<SquadInstance> squads = GetRunnerSquads(runner);
            if (squads == null || squads.Count == 0) return;

            BlackboardDefinition treeDef = runner.BlackBoard?.Definition;
            if (treeDef == null) return;

            for (int i = 0; i < squads.Count; i++)
            {
                if (squads[i] != null)
                {
                    squads[i].CopyToBB(runner.BlackBoard, treeDef, agentOffset);
                }
            }
        }

        /// <summary>
        /// Copies tree runner's BB data back to all registered squads.
        /// When agentOffset is >= 0, per-agent squad variables use it as
        /// the slot offset to write to the correct agent's slot.
        /// </summary>
        private static void CopySquadsFromTree(BehaviourTreeRunnerBase runner, int agentOffset = -1)
        {
            List<SquadInstance> squads = GetRunnerSquads(runner);
            if (squads == null || squads.Count == 0) return;

            BlackboardDefinition treeDef = runner.BlackBoard?.Definition;
            if (treeDef == null) return;

            for (int i = 0; i < squads.Count; i++)
            {
                if (squads[i] != null)
                {
                    squads[i].CopyFromBB(runner.BlackBoard, treeDef, agentOffset);
                }
            }
        }

        /// <summary>
        /// Gets the registered squads for a runner. Handles both AgentTreeRunner
        /// and CommanderTreeRunner types.
        /// </summary>
        private static List<SquadInstance> GetRunnerSquads(BehaviourTreeRunnerBase runner)
        {
            if (runner is AgentTreeRunner agentRunner)
                return agentRunner.registeredSquads;
            if (runner is CommanderTreeRunner commanderRunner)
                return commanderRunner.registeredSquads;
            return null;
        }

        /// <summary>
        /// Commander evaluates first: reads agent status from squad BB (last frame),
        /// runs the commander behaviour tree, writes orders back to squad BB.
        /// </summary>
        private void EvaluateCommander()
        {
            CopySquadsToTree(this);

            evaluator.agentCount = registeredAgents.Count;
            evaluator.Evaluate(blackBoard);

            CopySquadsFromTree(this);

            if (debugProvider != null)
            {
                debugProvider.currentNodeStates = evaluator.nodeStates;
                debugProvider.activeNodeIndex = evaluator.currentNodeIndex;
                debugProvider.currentNodeGuids = runtimeAsset != null ? runtimeAsset.runtimeNodeGuids : null;
            }
        }

        protected override void OnPostInitialize()
        {
            ResolveTrackedBindings();

            for (int i = 0; i < registeredAgents.Count; i++)
            {
                AgentTreeRunner agent = registeredAgents[i];
                if (agent != null)
                {
                    agent.Initialize();
                    agent.RunIndependently = false;
                }
            }
        }

        /// <summary>
        /// Registers an agent with this commander. The stride is fixed at bake time
        /// (from the CommanderBlackboardDefinition asset), so no runtime resize occurs.
        /// Logs an error if the agent count would exceed the available stride capacity.
        /// </summary>
        public void RegisterAgent(AgentTreeRunner agent)
        {
            if (agent == null || registeredAgents.Contains(agent))
                return;

            // Verify capacity against the fixed stride set in the asset
            int maxStride = GetMaxSquadDataStride();
            if (maxStride > 0 && registeredAgents.Count >= maxStride)
            {
                Debug.LogError($"[CommanderTreeRunner] Cannot register agent: squad-data stride ({maxStride}) exceeded. " +
                               "Increase the stride on squad-data variables in the Commander Blackboard Definition to support more agents.");
                return;
            }

            registeredAgents.Add(agent);
        }

        /// <summary>
        /// Unregisters an agent. Before removing from the list, invalidates the
        /// agent's squad-data slots (-1 for ints, zero for floats/vectors) and
        /// compacts all subsequent slots down by one to keep indices contiguous
        /// with the remaining agent list.
        /// </summary>
        public void UnregisterAgent(AgentTreeRunner agent)
        {
            if (agent == null) return;

            int removedIndex = registeredAgents.IndexOf(agent);
            if (removedIndex >= 0)
            {
                CompactAndInvalidateSquadSlots(removedIndex);
                registeredAgents.RemoveAt(removedIndex);
            }
        }

        /// <summary>
        /// Returns the stride of the first squad-data variable in the commander BB,
        /// or 0 if no squad-data variables exist.
        /// </summary>
        public int GetMaxSquadDataStride()
        {
            if (blackBoard?.Definition == null) return 0;
            IReadOnlyList<BlackboardVariableBase> vars = blackBoard.Definition.GetAllVariables();
            for (int i = 0; i < vars.Count; i++)
            {
                if (vars[i].isSquadData)
                    return vars[i].Stride;
            }
            return 0;
        }

        /// <summary>
        /// Invalidates the squad-data slots for the agent at removedIndex by writing
        /// sentinel values (-1 for ints, zero for floats/Vector3), then shifts all
        /// subsequent active per-agent slots down by one to keep indices contiguous
        /// with the compacted agent list.
        /// Must be called BEFORE the agent is removed from registeredAgents.
        /// 
        /// Active range is [0, registeredAgents.Count). Slots beyond that (up to the
        /// configured stride) are uninitialized garbage and are neither read nor shifted.
        /// After the shift, the ex-last slot in the active range becomes unreferenced
        /// — harmless since agentCount will drop by one on the next frame.
        /// </summary>
        private void CompactAndInvalidateSquadSlots(int removedIndex)
        {
            int activeCount = registeredAgents.Count; // snapshot before removal

            for (int s = 0; s < registeredSquads.Count; s++)
            {
                SquadInstance squad = registeredSquads[s];
                if (squad?.BlackBoard?.Definition == null) continue;

                BlackboardDefinition squadDef = squad.BlackBoard.Definition;
                IReadOnlyList<BlackboardVariableBase> vars = squadDef.GetAllVariables();

                int currentSlot = 0;
                for (int v = 0; v < vars.Count; v++)
                {
                    int varStride = vars[v].Stride > 1 ? vars[v].Stride : 1;

                    if (vars[v].isSquadData && varStride > 1)
                    {
                        Type varType = vars[v].GetValueType();

                        // 1) Invalidate the removed agent's slot
                        if (varType == typeof(int))
                            squad.BlackBoard.SetBoxedRaw(currentSlot + removedIndex, -1);
                        else if (varType == typeof(float))
                            squad.BlackBoard.SetBoxedRaw(currentSlot + removedIndex, 0f);
                        else if (varType == typeof(Vector3))
                            squad.BlackBoard.SetBoxedRaw(currentSlot + removedIndex, Vector3.zero);
                        else if (varType == typeof(bool))
                            squad.BlackBoard.SetBoxedRaw(currentSlot + removedIndex, false);
                        else
                            squad.BlackBoard.SetBoxedRaw(currentSlot + removedIndex, -1);

                        // 2) Shift subsequent active slots down by one.
                        //    Stops at activeCount-1 so garbage beyond the active range
                        //    is never shifted into the active range.
                        for (int slot = removedIndex; slot < activeCount - 1; slot++)
                        {
                            object val = squad.BlackBoard.GetBoxedRaw(currentSlot + slot + 1);
                            squad.BlackBoard.SetBoxedRaw(currentSlot + slot, val);
                        }
                    }

                    currentSlot += varStride;
                }
            }

            // Adjust runningAgentIndex in the evaluator so ForEachRole/ForEachAgent
            // resume at the correct agent after compaction shifts indices.
            evaluator?.OnAgentCompacted(removedIndex);
        }

        /// <summary>
        /// Registers this commander with a squad instance.
        /// </summary>
        public void RegisterSquad(SquadInstance squad)
        {
            if (squad == null || registeredSquads.Contains(squad))
                return;

            registeredSquads.Add(squad);

            if (blackBoard != null && blackBoard.Definition != null)
                squad.EnsureResolved(blackBoard.Definition);
        }

        /// <summary>
        /// Unregisters this commander from a squad instance.
        /// </summary>
        public void UnregisterSquad(SquadInstance squad)
        {
            registeredSquads.Remove(squad);
        }
    }
}
