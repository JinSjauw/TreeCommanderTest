using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
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

        private void CompactAndInvalidateSquadSlots(int removedIndex)
        {
            int activeCount = registeredAgents.Count; // snapshot before removal

            for (int i = 0; i < registeredSquads.Count; i++)
            {
                SquadInstance squad = registeredSquads[i];
                if (squad?.BlackBoard?.Definition == null) continue;

                BlackboardDefinition squadDef = squad.BlackBoard.Definition;
                IReadOnlyList<BlackboardVariableBase> vars = squadDef.GetAllVariables();

                int currentSlot = 0;
                for (int j = 0; j < vars.Count; j++)
                {
                    int varStride = vars[j].Stride > 1 ? vars[j].Stride : 1;

                    if (vars[j].isSquadData && varStride > 1)
                    {
                        Type varType = vars[j].GetValueType();

                        if (varType == typeof(int))
                            squad.BlackBoard.SetBoxedRaw(currentSlot + removedIndex, -1);
                        else if (varType == typeof(float))
                            squad.BlackBoard.SetBoxedRaw(currentSlot + removedIndex, -1f);
                        else if (varType == typeof(Vector3))
                            squad.BlackBoard.SetBoxedRaw(currentSlot + removedIndex, Vector3.zero);
                        else if (varType == typeof(bool))
                            squad.BlackBoard.SetBoxedRaw(currentSlot + removedIndex, false);
                        else
                            squad.BlackBoard.SetBoxedRaw(currentSlot + removedIndex, -1);

                        // Shift per-agent slots down by one within the stride region (typed memmove).
                        squad.BlackBoard.CopySlotsRawFrom(squad.BlackBoard,
                            currentSlot + removedIndex + 1, currentSlot + removedIndex,
                            activeCount - 1 - removedIndex);
                    }

                    currentSlot += varStride;
                }
            }

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
