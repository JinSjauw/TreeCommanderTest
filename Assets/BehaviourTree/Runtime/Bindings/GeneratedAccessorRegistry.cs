using System;
using System.Collections.Generic;
using System.Reflection;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Registry for build-time-generated binding accessors. The generated assembly
    /// populates this via [RuntimeInitializeOnLoadMethod]; FieldBinding.BindAccessors
    /// consults it before falling back to Expression.Compile.
    ///
    /// Accessors are slot-agnostic: the slot is supplied per call, so one accessor
    /// serves every instance of a method type.
    /// </summary>
    public static class GeneratedAccessorRegistry
    {
        public delegate void ReadAccessor(NodeMethod method, IBlackBoardAccess bb, int slot);
        public delegate void WriteAccessor(NodeMethod method, IBlackBoardAccess bb, int slot);

        private struct Entry
        {
            public Type fieldType;
            public ReadAccessor read;
            public WriteAccessor write;
        }

        private static readonly Dictionary<(Type methodType, string fieldName), Entry> entries = new();

        public static int Count => entries.Count;

        public static void Clear() => entries.Clear();

        public static void Register(Type methodType, string fieldName, Type fieldType,
            ReadAccessor read, WriteAccessor write)
        {
            entries[(methodType, fieldName)] = new Entry { fieldType = fieldType, read = read, write = write };
        }

        /// <summary>
        /// Looks up accessors for (methodType, field). Returns false when no entry
        /// exists OR when the entry was generated for a different field type
        /// (stale generated code — caller should fall back + warn).
        /// </summary>
        public static bool TryGet(Type methodType, FieldInfo field,
            out ReadAccessor read, out WriteAccessor write)
        {
            if (field != null && entries.TryGetValue((methodType, field.Name), out Entry e)
                && e.fieldType == field.FieldType)
            {
                read = e.read;
                write = e.write;
                return true;
            }

            read = null;
            write = null;
            return false;
        }
    }
}
