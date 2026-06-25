using System;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Lightweight struct that provides name→slot resolution once and then
    /// zero-allocation indexed access to a variable in the blackboard.
    /// Replaces raw slot integer arithmetic and String-based lookups.
    /// </summary>
    public readonly struct BoxedVariableHandle
    {
        private readonly IBlackboardStorage storage;
        private readonly int baseSlot;

        /// <summary>
        /// The flat storage offset of element 0. Use for debugging or
        /// composite-level per-agent iteration (handle[agentIndex] = baseSlot + agentIndex).
        /// </summary>
        public int BaseSlot => baseSlot;

        /// <summary>
        /// True when the handle was successfully resolved to a valid slot.
        /// </summary>
        public bool IsValid => baseSlot >= 0 && storage != null;

        internal BoxedVariableHandle(IBlackboardStorage storage, int baseSlot)
        {
            this.storage = storage;
            this.baseSlot = baseSlot;
        }

        /// <summary>
        /// Boxed value at element 0 — equivalent to GetValue(0).
        /// </summary>
        public object Value
        {
            get => storage != null ? storage.GetBoxed(baseSlot) : null;
            set
            {
                if (storage != null)
                    storage.SetBoxed(baseSlot, value);
            }
        }

        /// <summary>
        /// Indexed boxed access for per-agent squad data.
        /// <code>handle[agentIndex]</code> is <c>storage.GetBoxed(baseSlot + agentIndex)</c>.
        /// </summary>
        public object this[int index]
        {
            get => storage != null ? storage.GetBoxed(baseSlot + index) : null;
            set
            {
                if (storage != null)
                    storage.SetBoxed(baseSlot + index, value);
            }
        }

        /// <summary>
        /// Typed value at element 0. No boxing for value types.
        /// </summary>
        public T GetValue<T>() => storage != null ? storage.Get<T>(baseSlot) : default;

        /// <summary>
        /// Typed write at element 0. No boxing for value types.
        /// </summary>
        public void SetValue<T>(T value)
        {
            if (storage != null)
                storage.Set(baseSlot, value);
        }

        /// <summary>
        /// Typed indexed read for per-agent access.
        /// </summary>
        public T GetElement<T>(int index) => storage != null ? storage.Get<T>(baseSlot + index) : default;

        /// <summary>
        /// Typed indexed write for per-agent access.
        /// </summary>
        public void SetElement<T>(int index, T value)
        {
            if (storage != null)
                storage.Set(baseSlot + index, value);
        }
    }
}
