using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Centralized helper that defines which base channel variables
    /// get auto-created on squad and tree blackboard definitions.
    /// Called from SquadDefinition, CommanderTreeAsset, and AgentTreeAsset.
    /// </summary>
    public static class SquadChannelHelper
    {
        /// <summary>
        /// System channels required on the squad blackboard definition.
        /// Returns true if any variable was added or repaired.
        /// </summary>
        public static bool EnsureSquadSystemChannels(BlackboardDefinition bbDef)
        {
            if (bbDef == null) return false;

            bool changed = false;
            changed |= BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentRoles", isSquadData: true);
            changed |= BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentOrders", isSquadData: true);
            changed |= BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "LeaderIndex", isSquadData: false);
            changed |= BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentStatus", isSquadData: true);
            // changed |= BlackboardDefinition.EnsureBaseChannel<Vector3>(bbDef, "SquadMovePosition", isSquadData: false);
            // changed |= BlackboardDefinition.EnsureBaseChannel<float>(bbDef, "AgentMoveSpeed", isSquadData: true);
            return changed;
        }

        /// <summary>
        /// System channels required on the commander tree blackboard definition.
        /// Returns true if any variable was added or repaired.
        /// </summary>
        public static bool EnsureCommanderSystemChannels(BlackboardDefinition bbDef)
        {
            if (bbDef == null) return false;

            bool changed = false;
            changed |= BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentRoles", isSquadData: true);
            changed |= BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentOrders", isSquadData: true);
            changed |= BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "LeaderIndex", isSquadData: false);
            changed |= BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentStatus", isSquadData: true);
            changed |= BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentCount", isSquadData: false);
            return changed;
        }

        /// <summary>
        /// System channels required on the agent tree blackboard definition.
        /// AgentMovePosition is intentionally NOT auto-created — it is optional,
        /// added by the user only when using formation behaviors.
        /// Returns true if any variable was added or repaired.
        /// </summary>
        public static bool EnsureAgentSystemChannels(BlackboardDefinition bbDef)
        {
            if (bbDef == null) return false;

            bool changed = false;
            changed |= BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentAssignedRole", isSquadData: false);
            changed |= BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentReceivedOrder", isSquadData: false);
            changed |= BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentStatus", isSquadData: false);
            return changed;
        }
    }
}
