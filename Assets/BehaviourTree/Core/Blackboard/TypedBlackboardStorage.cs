using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Blackboard storage backed by one array per supported value type, plus an
    /// object[] fallback for reference types and custom value types.
    /// Virtual slot numbering matches the legacy flat object[] layout exactly;
    /// a SlotLocation map translates virtual slots to (typed array, local index).
    ///
    /// The generic Get&lt;T&gt;/Set&lt;T&gt; and boxed paths may allocate (compatibility).
    /// Hot paths must use the IBlackboardTypedAccess accessors (Phase 3).
    /// </summary>
    public sealed class TypedBlackboardStorage : IBlackboardStorage
    {
        private BlackboardDefinition definition;
        private IReadOnlyList<BlackboardVariableBase> runtimeVariables;

        private object[] objects;
        private int[] ints;
        private float[] floats;
        private bool[] bools;
        private Vector2[] vec2s;
        private Vector3[] vec3s;
        private Vector4[] vec4s;
        private Color[] colors;
        private Quaternion[] quats;

        private SlotLocation[] map;
        private Type[] slotTypes;
        private BlackboardSlotKind[] slotKinds;

        public BlackboardDefinition Definition => definition;
        public int Count => map?.Length ?? 0;

        public void Initialize(BlackboardDefinition definition)
        {
            this.definition = definition;
            if (definition == null)
            {
                runtimeVariables = null;
                ClearArrays();
                return;
            }

            runtimeVariables = definition.GetAllVariables();
            InitializeFromVariables(runtimeVariables);
        }

        public void Initialize(IReadOnlyList<BlackboardVariableBase> variables)
        {
            definition = null;
            runtimeVariables = variables;
            InitializeFromVariables(variables);
        }

        private void ClearArrays()
        {
            map = null;
            slotTypes = null;
            slotKinds = null;
            objects = null;
            ints = null;
            floats = null;
            bools = null;
            vec2s = null;
            vec3s = null;
            vec4s = null;
            colors = null;
            quats = null;
        }

        private void InitializeFromVariables(IReadOnlyList<BlackboardVariableBase> variables)
        {
            if (variables == null || variables.Count == 0)
            {
                ClearArrays();
                return;
            }

            BlackboardStorageLayout.Build(variables, out map, out slotTypes, out slotKinds, out int[] sizes);

            objects = new object[sizes[(int)BlackboardArrayId.Object]];
            ints = new int[sizes[(int)BlackboardArrayId.Int]];
            floats = new float[sizes[(int)BlackboardArrayId.Float]];
            bools = new bool[sizes[(int)BlackboardArrayId.Bool]];
            vec2s = new Vector2[sizes[(int)BlackboardArrayId.Vector2]];
            vec3s = new Vector3[sizes[(int)BlackboardArrayId.Vector3]];
            vec4s = new Vector4[sizes[(int)BlackboardArrayId.Vector4]];
            colors = new Color[sizes[(int)BlackboardArrayId.Color]];
            quats = new Quaternion[sizes[(int)BlackboardArrayId.Quaternion]];

            SeedFromVariables(variables);
        }

        private void SeedFromVariables(IReadOnlyList<BlackboardVariableBase> variables)
        {
            // Phase 8 replaces this boxed seeding with typed seeding.
            // Boxing here happens once per Initialize, not per tick.
            int slot = 0;
            for (int varIndex = 0; varIndex < variables.Count; varIndex++)
            {
                BlackboardVariableBase variable = variables[varIndex];
                int stride = variable.Stride;
                int actualStride = (stride > 1) ? stride : 1;
                for (int element = 0; element < actualStride; element++)
                    WriteBoxedUnchecked(slot + element, variable.GetBoxedValue(element));
                slot += actualStride;
            }
        }

        public void GetVariableSlotRange(int variableIndex, out int baseSlot, out int stride)
        {
            if (runtimeVariables == null || variableIndex < 0 || variableIndex >= runtimeVariables.Count)
            {
                baseSlot = -1;
                stride = -1;
                Debug.LogError($"[TypedBlackboardStorage] GetVariableSlotRange: variableIndex {variableIndex} is out of bounds ({(runtimeVariables?.Count ?? 0)} variables). Returning sentinel (-1, -1).");
                return;
            }

            baseSlot = 0;
            stride = 1;

            for (int prevIndex = 0; prevIndex < variableIndex; prevIndex++)
            {
                int prevStride = runtimeVariables[prevIndex].Stride;
                baseSlot += (prevStride > 1) ? prevStride : 1;
            }

            stride = runtimeVariables[variableIndex].Stride;
            if (stride <= 1) stride = 1;
        }

        public BlackboardSlotKind GetSlotKind(int index)
        {
            if (slotKinds == null || index < 0 || index >= slotKinds.Length) return BlackboardSlotKind.Value;
            return slotKinds[index];
        }

        public T Get<T>(int index)
        {
            if (map == null || index < 0 || index >= map.Length)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Blackboard] Invalid index or map[] is NULL: {index} : {typeof(T).Name}");
#endif
                return default;
            }

            SlotLocation loc = map[index];
            switch (loc.ArrayId)
            {
                case BlackboardArrayId.Float:
                {
                    float f = floats[loc.LocalIndex];
                    if (typeof(T) == typeof(float)) return (T)(object)f;
                    try { return (T)Convert.ChangeType(f, typeof(T)); }
                    catch { return default; }
                }
                case BlackboardArrayId.Int:
                {
                    int i = ints[loc.LocalIndex];
                    if (typeof(T) == typeof(int)) return (T)(object)i;
                    if (typeof(T).IsEnum) return (T)Enum.ToObject(typeof(T), i);
                    try { return (T)Convert.ChangeType(i, typeof(T)); }
                    catch { return default; }
                }
                case BlackboardArrayId.Bool:
                {
                    bool b = bools[loc.LocalIndex];
                    if (typeof(T) == typeof(bool)) return (T)(object)b;
                    try { return (T)Convert.ChangeType(b, typeof(T)); }
                    catch { return default; }
                }
                case BlackboardArrayId.Vector2:
                    return typeof(T) == typeof(Vector2) ? (T)(object)vec2s[loc.LocalIndex] : default;
                case BlackboardArrayId.Vector3:
                    return typeof(T) == typeof(Vector3) ? (T)(object)vec3s[loc.LocalIndex] : default;
                case BlackboardArrayId.Vector4:
                    return typeof(T) == typeof(Vector4) ? (T)(object)vec4s[loc.LocalIndex] : default;
                case BlackboardArrayId.Color:
                    return typeof(T) == typeof(Color) ? (T)(object)colors[loc.LocalIndex] : default;
                case BlackboardArrayId.Quaternion:
                    return typeof(T) == typeof(Quaternion) ? (T)(object)quats[loc.LocalIndex] : default;
                default:
                {
                    object value = objects[loc.LocalIndex];
                    if (value is T typed) return typed;
                    if (value == null) return default;
                    try { return (T)Convert.ChangeType(value, typeof(T)); }
                    catch
                    {
#if UNITY_EDITOR
                        Debug.LogWarning($"[Blackboard] Unexpected type in BB: index {index} expected {typeof(T).Name} but found {value.GetType().Name}.");
#endif
                        return default;
                    }
                }
            }
        }

        public void Set<T>(int index, T value)
        {
            if (!CanWrite<T>(index, value))
            {
                Debug.LogWarning($"[Blackboard.Set] Type mismatch at index {index}: expected {(slotTypes != null && index < slotTypes.Length ? slotTypes[index]?.Name : "unknown")}, got {typeof(T).Name}");
                return;
            }

            SlotLocation loc = map[index];
            switch (loc.ArrayId)
            {
                case BlackboardArrayId.Float: floats[loc.LocalIndex] = (float)(object)value; break;
                case BlackboardArrayId.Int:
                    // CanWrite guarantees exact type match; Convert handles boxed enums.
                    ints[loc.LocalIndex] = typeof(T).IsEnum ? Convert.ToInt32(value) : (int)(object)value;
                    break;
                case BlackboardArrayId.Bool: bools[loc.LocalIndex] = (bool)(object)value; break;
                case BlackboardArrayId.Vector2: vec2s[loc.LocalIndex] = (Vector2)(object)value; break;
                case BlackboardArrayId.Vector3: vec3s[loc.LocalIndex] = (Vector3)(object)value; break;
                case BlackboardArrayId.Vector4: vec4s[loc.LocalIndex] = (Vector4)(object)value; break;
                case BlackboardArrayId.Color: colors[loc.LocalIndex] = (Color)(object)value; break;
                case BlackboardArrayId.Quaternion: quats[loc.LocalIndex] = (Quaternion)(object)value; break;
                default: objects[loc.LocalIndex] = value; break;
            }
        }

        public object GetBoxed(int index)
        {
            if (map == null || index < 0 || index >= map.Length) return null;

            SlotLocation loc = map[index];
            switch (loc.ArrayId)
            {
                case BlackboardArrayId.Float: return floats[loc.LocalIndex];
                case BlackboardArrayId.Int:
                {
                    // Boxed reads of enum slots return the enum instance (design invariant).
                    Type slotType = slotTypes[index];
                    return (slotType != null && slotType.IsEnum)
                        ? Enum.ToObject(slotType, ints[loc.LocalIndex])
                        : ints[loc.LocalIndex];
                }
                case BlackboardArrayId.Bool: return bools[loc.LocalIndex];
                case BlackboardArrayId.Vector2: return vec2s[loc.LocalIndex];
                case BlackboardArrayId.Vector3: return vec3s[loc.LocalIndex];
                case BlackboardArrayId.Vector4: return vec4s[loc.LocalIndex];
                case BlackboardArrayId.Color: return colors[loc.LocalIndex];
                case BlackboardArrayId.Quaternion: return quats[loc.LocalIndex];
                default: return objects[loc.LocalIndex];
            }
        }

        public void SetBoxed(int index, object value)
        {
            if (!CanWriteBoxed(index, value)) return;
            WriteBoxedUnchecked(index, value);
        }

        private void WriteBoxedUnchecked(int index, object value)
        {
            if (map == null || index < 0 || index >= map.Length) return;

            SlotLocation loc = map[index];
            if (value == null)
            {
                // Null only has a representation in the object array. Typed slots
                // keep their current value (CanWrite checks reject null for value
                // slots; seeding leaves default(T)).
                if (loc.ArrayId == BlackboardArrayId.Object)
                    objects[loc.LocalIndex] = null;
                return;
            }

            switch (loc.ArrayId)
            {
                case BlackboardArrayId.Float: floats[loc.LocalIndex] = Convert.ToSingle(value); break;
                case BlackboardArrayId.Int: ints[loc.LocalIndex] = Convert.ToInt32(value); break; // handles boxed enums
                case BlackboardArrayId.Bool: bools[loc.LocalIndex] = Convert.ToBoolean(value); break;
                case BlackboardArrayId.Vector2: vec2s[loc.LocalIndex] = (Vector2)value; break;
                case BlackboardArrayId.Vector3: vec3s[loc.LocalIndex] = (Vector3)value; break;
                case BlackboardArrayId.Vector4: vec4s[loc.LocalIndex] = (Vector4)value; break;
                case BlackboardArrayId.Color: colors[loc.LocalIndex] = (Color)value; break;
                case BlackboardArrayId.Quaternion: quats[loc.LocalIndex] = (Quaternion)value; break;
                default: objects[loc.LocalIndex] = value; break;
            }
        }

        private bool CanWrite<T>(int index, T value)
        {
            if (map == null || index < 0 || index >= map.Length) return false;

            Type slotType = slotTypes?[index];
            if (slotType == null) return true;

            if (!slotType.IsValueType)
            {
                if (value == null) return true;
                return slotType.IsAssignableFrom(typeof(T));
            }

            if (value == null) return false;
            return slotType == typeof(T);
        }

        private bool CanWriteBoxed(int index, object value)
        {
            if (map == null || index < 0 || index >= map.Length) return false;

            Type slotType = slotTypes?[index];
            if (slotType == null) return true;

            if (!slotType.IsValueType)
            {
                if (value == null) return true;
                return slotType.IsAssignableFrom(value.GetType());
            }

            if (value == null) return false;
            return slotType == value.GetType();
        }
    }
}
