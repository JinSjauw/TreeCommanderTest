using System;
using System.Collections.Generic;

namespace BehaviourTree.Core
{
    public enum BlackboardSlotKind
    {
        Value = 0,
        Reference = 1,
    }

    public interface IBlackboardStorage
    {
        BlackboardDefinition Definition { get; }
        int Count { get; }
        BlackboardSlotKind GetSlotKind(int index);
        void Initialize(BlackboardDefinition definition);
        void Initialize(IReadOnlyList<BlackboardVariableBase> variables);
        T Get<T>(int index);
        void Set<T>(int index, T value);
        object GetBoxed(int index);
        void SetBoxed(int index, object value);

        /// <summary>
        /// Copies a range of virtual slots from another storage into this one.
        /// Implementations use fast typed region copies when both sides share a
        /// layout; otherwise falls back to per-slot boxed copies.
        /// Overlapping ranges within the same storage are supported (memmove semantics).
        /// </summary>
        void CopySlotsFrom(IBlackboardStorage source, int sourceSlot, int destSlot, int count);

        /// <summary>
        /// Monotonic per-slot write counter. Zero after Initialize (seeding does
        /// not count). Bumped on every write to the slot — typed setters, generic
        /// Set, boxed writes, and CopySlotsFrom destinations. Reads never bump.
        /// Used by change-detection nodes (e.g. HasChanged) to avoid value reads.
        /// </summary>
        int GetSlotVersion(int slot);

        /// <summary>
        /// Given a variable index into the unified variable list,
        /// returns the base slot index and stride in the flat values array.
        /// </summary>
        void GetVariableSlotRange(int variableIndex, out int baseSlot, out int stride);
    }
}
