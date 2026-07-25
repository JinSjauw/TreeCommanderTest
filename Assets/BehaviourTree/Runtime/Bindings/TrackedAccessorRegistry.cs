using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Registry for generated tracked-binding push accessors, keyed by
    /// (component type, member name). Same staleness contract as
    /// GeneratedAccessorRegistry: member-type mismatch → miss → Expression fallback.
    /// </summary>
    public static class TrackedAccessorRegistry
    {
        public delegate void PushAccessor(Component component, IBlackBoardAccess bb, int slot);

        private struct Entry
        {
            public Type memberType;
            public PushAccessor push;
        }

        private static readonly Dictionary<(Type compType, string memberName), Entry> entries = new();

        public static int Count => entries.Count;

        public static void Clear() => entries.Clear();

        public static void Register(Type compType, string memberName, Type memberType, PushAccessor push)
        {
            entries[(compType, memberName)] = new Entry { memberType = memberType, push = push };
        }

        public static bool TryGet(Type compType, string memberName, Type memberType, out PushAccessor push)
        {
            if (entries.TryGetValue((compType, memberName), out Entry e) && e.memberType == memberType)
            {
                push = e.push;
                return true;
            }

            push = null;
            return false;
        }
    }
}
