using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Direction of data flow for a variable binding between a tree and a squad.
    /// </summary>
    public enum BindingDirection
    {
        ToSquad,    // tree → squad
        FromSquad,  // squad → tree
        Both        // bidirectional
    }

    /// <summary>
    /// A single variable mapping between a tree's blackboard and a squad's blackboard.
    /// Lives on SquadDefinition inside a SquadBindingGroup.
    /// </summary>
    [Serializable]
    public class VariableBinding
    {
        /// <summary>Variable name in the tree's BlackboardDefinition.</summary>
        public string treeVariableName;

        /// <summary>Variable name in the squad's BlackboardDefinition.</summary>
        public string squadVariableName;

        /// <summary>Direction of data flow.</summary>
        public BindingDirection direction = BindingDirection.Both;
    }

    /// <summary>
    /// Groups all variable bindings between one specific tree asset and this squad.
    /// One group exists per tree that connects to the squad.
    /// </summary>
    [Serializable]
    public class SquadBindingGroup
    {
        /// <summary>The tree asset this binding group maps to.</summary>
        public BehaviourTreeAssetBase treeAsset;

        /// <summary>GUID of the tree asset. Populated in OnValidate for build-safe matching.</summary>
        public string treeAssetGuid;

        /// <summary>Variable bindings between the tree and the squad.</summary>
        public List<VariableBinding> bindings = new List<VariableBinding>();
    }

    /// <summary>
    /// ScriptableObject that defines a squad: its shared data schema (blackboard),
    /// available role names, and per-tree variable bindings.
    /// </summary>
    public class SquadDefinition : ScriptableObject
    {
        /// <summary>The squad's own blackboard schema. Defines all shared squad data variables.</summary>
        public BlackboardDefinition blackboardDefinition;

        /// <summary>
        /// Roles available in this squad, with colour, max amount, and fallback settings.
        /// Agents pick one role. Commander trees iterate over roles.
        /// </summary>
        public List<SquadRole> availableRoles = new List<SquadRole>();

        /// <summary>
        /// Total number of agent slots in this squad's role composition.
        /// Sum of all role.maxAmount values. Drives commander tree stride and BB allocation.
        /// </summary>
        public int TotalAgentSlots => availableRoles != null ? availableRoles.Sum(r => r.maxAmount) : 0;

        /// <summary>
        /// Resolves a slot index (0..TotalAgentSlots-1) to the SquadRole that covers it.
        /// Roles are iterated in list order; maxAmount determines how many consecutive slots each covers.
        /// Returns null if slotIndex is out of range or no roles are defined.
        /// </summary>
        public SquadRole GetRoleForSlot(int slotIndex)
        {
            if (availableRoles == null || slotIndex < 0) return null;

            int cursor = 0;
            for (int i = 0; i < availableRoles.Count; i++)
            {
                int amount = Mathf.Max(1, availableRoles[i].maxAmount);
                if (slotIndex < cursor + amount)
                    return availableRoles[i];
                cursor += amount;
            }
            return null;
        }

        /// <summary>
        /// One binding group per tree that connects to this squad.
        /// Keyed by treeAsset — both the squad inspector and the tree's squads tab
        /// edit the same data through this list.
        /// </summary>
        public List<SquadBindingGroup> bindingGroups = new List<SquadBindingGroup>();

        /// <summary>
        /// Finds or creates a binding group for the given tree asset.
        /// </summary>
        public SquadBindingGroup GetOrCreateBindingGroup(BehaviourTreeAssetBase treeAsset)
        {
            for (int i = 0; i < bindingGroups.Count; i++)
            {
                if (bindingGroups[i].treeAsset == treeAsset)
                    return bindingGroups[i];
            }

            SquadBindingGroup newGroup = new SquadBindingGroup
            {
                treeAsset = treeAsset
            };
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GetAssetPath(treeAsset);
            newGroup.treeAssetGuid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
#endif
            bindingGroups.Add(newGroup);
            return newGroup;
        }

        /// <summary>
        /// Returns the binding group for a tree, or null if not connected.
        /// </summary>
        public SquadBindingGroup GetBindingGroup(BehaviourTreeAssetBase treeAsset)
        {
            for (int i = 0; i < bindingGroups.Count; i++)
            {
                if (bindingGroups[i].treeAsset == treeAsset)
                    return bindingGroups[i];
            }
            return null;
        }

        /// <summary>
        /// Applies the given maxAgents as the stride to all squad-data variables
        /// in the blackboard definition. Called before BB initialization to ensure
        /// per-agent storage is correctly sized.
        /// The YAML stays at stride=1 intentionally — definitions are editor schemas;
        /// stride is a runtime sizing concern applied before BB initialization.
        /// </summary>
        public void EnsureStrideApplied(int maxAgents)
        {
            if (blackboardDefinition == null) return;

            IReadOnlyList<BlackboardVariableBase> vars = blackboardDefinition.GetAllVariables();
            for (int i = 0; i < vars.Count; i++)
            {
                if (vars[i].isSquadData)
                    vars[i].Stride = maxAgents;
            }
        }

        /// <summary>
        /// Ensures the squad-side system variables exist in the squad blackboard:
        /// AgentRoles, AgentOrders and AgentStatus (per-agent squad data).
        /// Generated on squad creation so every squad always carries its required schema.
        /// </summary>
        public void EnsureSystemVariables()
        {
            if (blackboardDefinition == null) return;
            blackboardDefinition.EnsureVariable("AgentRoles", typeof(int), stride: 1, isSquadData: true, isSystemVariable: true);
            blackboardDefinition.EnsureVariable("AgentOrders", typeof(int), stride: 1, isSquadData: true, isSystemVariable: true);
            blackboardDefinition.EnsureVariable("AgentStatus", typeof(int), stride: 1, isSquadData: true, isSystemVariable: true);
        }

        /// <summary>
        /// Removes the agent-side system variables (AgentAssignedRole, AgentReceivedOrder,
        /// AgentStatus) from the given tree blackboard. Called when an agent tree's last
        /// squad connection is removed, so the auto-spawned variables don't linger.
        /// Only variables flagged as system variables are removed.
        /// </summary>
        public static void RemoveAgentSystemVariables(BlackboardDefinition treeDef)
        {
            if (treeDef?.sharedVariables == null) return;
            treeDef.sharedVariables.RemoveAll(v =>
                v != null && v.isSystemVariable &&
                (v.Name == "AgentAssignedRole" || v.Name == "AgentReceivedOrder" || v.Name == "AgentStatus"));
        }

        /// <summary>
        /// Ensures system variables exist and bindings are set for a tree binding group.
        /// Creates the following if they don't exist:
        ///   - Squad BB: AgentRoles, AgentOrders, AgentStatus
        ///   - Commander BB: AgentRoles, AgentOrders, AgentStatus
        ///   - Agent BB: AgentAssignedRole, AgentReceivedOrder, AgentStatus
        /// Also creates the corresponding variable bindings.
        /// Called from OnValidate and from editor UIs when a new group is created.
        /// </summary>
        public void EnsureAutoBindings(SquadBindingGroup group)
        {
            if (group?.treeAsset == null) return;

            BlackboardDefinition squadDef = blackboardDefinition;
            if (squadDef == null) return;

            bool isCommander = group.treeAsset.CommanderBlackboardDefinition != null;
            BlackboardDefinition treeDef = isCommander
                ? group.treeAsset.CommanderBlackboardDefinition
                : group.treeAsset.BlackboardDefinition;
            if (treeDef == null) return;

            // ── Ensure squad-side system variables ──
            EnsureSystemVariables();

            // ── Ensure tree-side system variables ──
            if (isCommander)
            {
                treeDef.EnsureVariable("AgentRoles", typeof(int), stride: 1, isSquadData: true, isSystemVariable: true);
                treeDef.EnsureVariable("AgentOrders", typeof(int), stride: 1, isSquadData: true, isSystemVariable: true);
                treeDef.EnsureVariable("AgentStatus", typeof(int), stride: 1, isSquadData: true, isSystemVariable: true);
            }
            else
            {
                treeDef.EnsureVariable("AgentAssignedRole", typeof(int), stride: 1, isSquadData: false, isSystemVariable: true);
                treeDef.EnsureVariable("AgentReceivedOrder", typeof(int), stride: 1, isSquadData: false, isSystemVariable: true);
                treeDef.EnsureVariable("AgentStatus", typeof(int), stride: 1, isSquadData: false, isSystemVariable: true);
            }

            // ── Create bindings ──
            string roleTreeVar = isCommander ? "AgentRoles" : "AgentAssignedRole";
            string orderTreeVar = isCommander ? "AgentOrders" : "AgentReceivedOrder";

            BindingDirection roleDir = isCommander ? BindingDirection.FromSquad : BindingDirection.ToSquad;
            BindingDirection orderDir = isCommander ? BindingDirection.ToSquad : BindingDirection.FromSquad;

            EnsureBinding(group, "AgentRoles", roleTreeVar, roleDir);
            EnsureBinding(group, "AgentOrders", orderTreeVar, orderDir);

            BindingDirection statusDir = isCommander ? BindingDirection.FromSquad : BindingDirection.ToSquad;
            EnsureBinding(group, "AgentStatus", "AgentStatus", statusDir);
        }

        private void OnValidate()
        {
            // Ensure auto-bindings for each connected tree
            if (bindingGroups != null)
            {
                for (int groupIndex = 0; groupIndex < bindingGroups.Count; groupIndex++)
                    EnsureAutoBindings(bindingGroups[groupIndex]);
            }

#if UNITY_EDITOR
            // Backfill GUID on binding groups for build-safe matching
            if (bindingGroups != null)
            {
                for (int groupIndex = 0; groupIndex < bindingGroups.Count; groupIndex++)
                {
                    SquadBindingGroup group = bindingGroups[groupIndex];
                    if (group?.treeAsset != null && string.IsNullOrEmpty(group.treeAssetGuid))
                    {
                        string path = UnityEditor.AssetDatabase.GetAssetPath(group.treeAsset);
                        group.treeAssetGuid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                    }
                }
            }
#endif
        }

        private static void EnsureBinding(
            SquadBindingGroup group, string squadVar, string treeVar, BindingDirection direction)
        {
            if (group.bindings == null)
                group.bindings = new List<VariableBinding>();

            for (int bindingIndex = 0; bindingIndex < group.bindings.Count; bindingIndex++)
            {
                VariableBinding binding = group.bindings[bindingIndex];
                if (binding != null && binding.squadVariableName == squadVar)
                {
                    // Update existing binding if values differ
                    if (binding.treeVariableName != treeVar || binding.direction != direction)
                    {
                        binding.treeVariableName = treeVar;
                        binding.direction = direction;
                        Debug.LogWarning(
                            $"[BaseChannel] Repaired binding: squad.{squadVar} → tree.{treeVar} ({direction}).");
                    }
                    return;
                }
            }

            group.bindings.Add(new VariableBinding
            {
                squadVariableName = squadVar,
                treeVariableName = treeVar,
                direction = direction
            });
            Debug.LogWarning(
                $"[BaseChannel] Auto-created binding: squad.{squadVar} → tree.{treeVar} ({direction}).");
        }
    }
}
