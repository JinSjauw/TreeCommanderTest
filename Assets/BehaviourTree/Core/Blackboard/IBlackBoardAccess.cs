using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Minimal blackboard read/write contract used by FieldBinding to resolve
    /// [SharedVar] fields without coupling to the concrete BlackBoard implementation.
    /// OOP BlackBoard and DOTS DotsBlackboard both satisfy this interface.
    /// </summary>
    public interface IBlackBoardAccess : IBlackboardTypedAccess
    {
        T Get<T>(int slot);
        void Set<T>(int slot, T value);
        object GetBoxed(int slot);
        void SetBoxed(int slot, object value);

        /// <summary>Get a boxed value WITHOUT applying currentAgentOffset.
        /// Use for shared/commander-level variables that are not per-agent squad data.</summary>
        object GetBoxedRaw(int slot);

        /// <summary>Set a boxed value WITHOUT applying currentAgentOffset.
        /// Use for internal copy operations that handle offsets themselves.</summary>
        void SetBoxedRaw(int slot, object value);
    }
}
