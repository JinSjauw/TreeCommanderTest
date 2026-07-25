using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core 
{
    public enum BlackBoardType
    {
        SELF = 0,
        SQUAD = 1,
    }

    public class BlackBoard : MonoBehaviour, IBlackBoardAccess
    {        
        [SerializeField] private List<UnityEngine.Object> serializedReferences = new();

        [NonSerialized] public int currentAgentOffset;

        [SerializeField] private List<BlackboardValueOverride> valueOverrides = new();

        /// <summary>Flat slot indices of explicitly-overridden reference-type variables.
        /// Toggled by the editor Override/X buttons — independent of serializedReferences contents
        /// so that stale data in serializedReferences doesn't pollute the override state on domain reload. </summary>
        [SerializeField] private List<int> overriddenReferenceSlots = new();

        private BlackboardDefinition definition;
        private IBlackboardStorage storage;
        private IBlackboardTypedAccess typed; // same instance as storage, typed view

        private Dictionary<string, int> nameToBaseSlot = new();

        /// <summary>Snapshot of variable names from last BuildSerializedReferences call.
        /// Used to detect when the definition layout changes so we can remap reference values by name.</summary>
        [NonSerialized] private string[] lastBuiltVarNames;
        [NonSerialized] private int[] lastBuiltVarStrides;

        public BlackboardDefinition Definition => definition;
        public IBlackboardStorage Storage => storage;

        /// <summary>Initialize index-based storage from a definition.</summary>
        public void Initialize(BlackboardDefinition blackboardDefinition)
        {
            if(definition == null || definition != blackboardDefinition)
            {
                definition = blackboardDefinition;   
            }

            if (definition == null) return;

            int count = GetTotalSlotCount(definition);
            if (storage == null)
            {
                storage = new TypedBlackboardStorage();
                typed = (IBlackboardTypedAccess)storage;
            }
            storage.Initialize(definition);

            nameToBaseSlot.Clear();
            IReadOnlyList<BlackboardVariableBase> allVarsForCache = definition.GetAllVariables();
            int slot = 0;
            for (int i = 0; i < allVarsForCache.Count; i++)
            {
                if (!string.IsNullOrEmpty(allVarsForCache[i].Name))
                    nameToBaseSlot[allVarsForCache[i].Name] = slot;
                
                int stride = allVarsForCache[i].Stride;
                slot += (stride > 1) ? stride : 1;
            }
            
            IReadOnlyList<BlackboardVariableBase> allVars = definition.GetAllVariables();

            while (serializedReferences.Count < count)
            {
                serializedReferences.Add(null);
            }

            for (int i = 0; i < count; i++)
            {
                BlackboardSlotKind kind = storage.GetSlotKind(i);
                UnityEngine.Object refVal = serializedReferences[i];

                if (kind == BlackboardSlotKind.Reference && refVal != null)
                {
                    storage.SetBoxed(i, refVal);
                }
            }

            // Sync value-type overrides into storage.
            if (valueOverrides != null && valueOverrides.Count > 0)
            {
                for (int overrideIndex = 0; overrideIndex < valueOverrides.Count; overrideIndex++)
                {
                    BlackboardValueOverride vo = valueOverrides[overrideIndex];
                    if (vo == null) continue;

                    // Compute base slot offset by scanning variables before the matched one.
                    int baseSlot = 0;
                    bool found = false;
                    for (int varIndex = 0; varIndex < allVars.Count; varIndex++)
                    {
                        if (allVars[varIndex].Name == vo.variableName)
                        {
                            found = true;
                            break;
                        }
                        int stride = allVars[varIndex].Stride;
                        baseSlot += (stride > 1) ? stride : 1;
                    }

                    if (!found) continue;

                    int slotIndex = baseSlot + vo.elementIndex;
                    object overrideValue = vo.GetBoxedValue();
                    if (overrideValue != null && slotIndex < count)
                    {
                        storage.SetBoxed(slotIndex, overrideValue);
                    }
                }
            }
        }

        public void BuildSerializedReferences(BlackboardDefinition blackboardDefinition)
        {
            // 1. Detect whether the variable layout is changing.
            //    Compare the NEW definition's layout against the last-built snapshot.
            //    If different, save existing reference values using the SNAPSHOT layout
            //    (not the definition, which was already mutated by BlackBoardView).
            Dictionary<string, List<UnityEngine.Object>> savedByName = null;
            bool sameLayout = true;
            if (lastBuiltVarNames != null && blackboardDefinition != null)
            {
                IReadOnlyList<BlackboardVariableBase> newVars = blackboardDefinition.GetAllVariables();

                // Compare new definition's layout against the stored snapshot
                sameLayout = HasSameVariableLayout(newVars, lastBuiltVarNames, lastBuiltVarStrides);
                if (!sameLayout && definition != null && serializedReferences.Count > 0)
                {
#if UNITY_EDITOR
                    Debug.Log($"[BB.BuildSerializedRefs] Layout changed — remapping references by name. def='{(definition != null ? definition.name : "null")}' → '{(blackboardDefinition != null ? blackboardDefinition.name : "null")}'");
#endif
                    // Save from the SNAPSHOT (last-built layout), not from definition.GetAllVariables().
                    // The ScriptableObject was already mutated by BlackBoardView to the new layout,
                    // so reading its variables would compute wrong slot offsets and save refs under wrong names.
                    savedByName = SaveReferencesByNameFromSnapshot(lastBuiltVarNames, lastBuiltVarStrides);

                    // Remap saved references when variables were renamed.
                    // SaveReferencesByNameFromSnapshot keys by the OLD name (from snapshot),
                    // but RestoreReferencesByName looks up by the NEW name (from definition).
                    // Detect renames and remap the dictionary keys to match the new names.
                    RemapRenamedReferences(savedByName, newVars);
                }
            }

            definition = blackboardDefinition;

            if (definition == null)
            {
                serializedReferences.Clear();
                lastBuiltVarNames = null;
                lastBuiltVarStrides = null;
                return;
            }

            if (!sameLayout)
            {
                int count = GetTotalSlotCount(definition);
#if UNITY_EDITOR
                Debug.Log($"[BB.BuildSerializedRefs] def='{(definition != null ? definition.name : "null")}' totalSlots={count} serializedRefs.Count={serializedReferences.Count} → rebuilding to {count}");
#endif
                serializedReferences.Clear();
                for (int i = 0; i < count; i++)
                    serializedReferences.Add(null);

                if (savedByName != null)
                    RestoreReferencesByName(definition, savedByName);
            }
            else
            {
                int count = GetTotalSlotCount(definition);
                while (serializedReferences.Count < count)
                    serializedReferences.Add(null);
                while (serializedReferences.Count > count)
                    serializedReferences.RemoveAt(serializedReferences.Count - 1);
            }

            // 3. Store new layout snapshot
            IReadOnlyList<BlackboardVariableBase> allVars = definition.GetAllVariables();
            lastBuiltVarNames = new string[allVars.Count];
            lastBuiltVarStrides = new int[allVars.Count];
            for (int i = 0; i < allVars.Count; i++)
            {
                lastBuiltVarNames[i] = allVars[i].Name;
                lastBuiltVarStrides[i] = allVars[i].Stride;
            }
        }

        public void ClearSerializedReferences()
        {
            definition = null;
            storage = null;
            typed = null;
            nameToBaseSlot.Clear();
            serializedReferences.Clear();
            lastBuiltVarNames = null;
            lastBuiltVarStrides = null;
        }

        /// <summary>
        /// Compares current variable layout against a stored snapshot (names + strides).
        /// Returns true if the layout is identical — same variables in same order with same strides.
        /// </summary>
        private static bool HasSameVariableLayout(IReadOnlyList<BlackboardVariableBase> vars, string[] names, int[] strides)
        {
            if (vars == null || names == null) return false;
            if (vars.Count != names.Length) return false;
            for (int i = 0; i < vars.Count; i++)
            {
                if (vars[i].Name != names[i] || vars[i].Stride != strides[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Saves all non-null reference values from serializedReferences, keyed by variable name.
        /// Uses the given variable list to compute slot offsets (reflects the definition at save time).
        /// </summary>
        private Dictionary<string, List<UnityEngine.Object>> SaveReferencesByName(IReadOnlyList<BlackboardVariableBase> vars)
        {
            var result = new Dictionary<string, List<UnityEngine.Object>>();
            if (vars == null) return result;

            int slot = 0;
            for (int varIndex = 0; varIndex < vars.Count; varIndex++)
            {
                BlackboardVariableBase bv = vars[varIndex];
                int stride = bv.Stride;
                int effectiveStride = (stride > 1) ? stride : 1;
                Type type = bv.GetValueType();

                if (type != null && !type.IsValueType)
                {
                    var values = new List<UnityEngine.Object>(effectiveStride);
                    for (int elementIndex = 0; elementIndex < effectiveStride; elementIndex++)
                    {
                        int slotIndex = slot + elementIndex;
                        values.Add(slotIndex < serializedReferences.Count ? serializedReferences[slotIndex] : null);
                    }
                    if (!string.IsNullOrEmpty(bv.Name))
                        result[bv.Name] = values;
                }

                slot += effectiveStride;
            }
            return result;
        }

        /// <summary>
        /// Saves all non-null reference values from serializedReferences, keyed by variable name.
        /// Uses the snapshot (names + strides) to compute slot offsets — this reflects the layout
        /// at the time of the last BuildSerializedReferences call, before the ScriptableObject was mutated.
        /// </summary>
        private Dictionary<string, List<UnityEngine.Object>> SaveReferencesByNameFromSnapshot(string[] names, int[] strides)
        {
            var result = new Dictionary<string, List<UnityEngine.Object>>();
            if (names == null) return result;

            int slot = 0;
            for (int i = 0; i < names.Length; i++)
            {
                int effectiveStride = (strides[i] > 1) ? strides[i] : 1;
                var values = new List<UnityEngine.Object>(effectiveStride);
                for (int elementIndex = 0; elementIndex < effectiveStride; elementIndex++)
                {
                    int slotIndex = slot + elementIndex;
                    values.Add(slotIndex < serializedReferences.Count ? serializedReferences[slotIndex] : null);
                }

                if (!string.IsNullOrEmpty(names[i]))
                    result[names[i]] = values;

                slot += effectiveStride;
            }
            return result;
        }

        /// <summary>
        /// Detects variable renames between the saved snapshot names and the new definition,
        /// and remaps the dictionary keys in <paramref name="savedByName"/> from old names to new names.
        /// This ensures that per-component reference overrides survive variable renaming.
        /// </summary>
        private static void RemapRenamedReferences(
            Dictionary<string, List<UnityEngine.Object>> savedByName,
            IReadOnlyList<BlackboardVariableBase> newVars)
        {
            if (savedByName == null || savedByName.Count == 0) return;
            if (newVars == null || newVars.Count == 0) return;

            // Build sets of names with saved refs, and names in the new definition
            HashSet<string> savedNames = new HashSet<string>(savedByName.Keys);
            HashSet<string> newNames = new HashSet<string>();
            for (int i = 0; i < newVars.Count; i++)
            {
                if (!string.IsNullOrEmpty(newVars[i].Name))
                    newNames.Add(newVars[i].Name);
            }

            // Names that were saved but don't appear in the new definition → removed
            List<string> removed = new List<string>();
            foreach (string name in savedNames)
            {
                if (!newNames.Contains(name))
                    removed.Add(name);
            }

            // Names in the new definition that have no saved ref → added (renamed candidates)
            List<string> added = new List<string>();
            foreach (string name in newNames)
            {
                if (!savedNames.Contains(name))
                    added.Add(name);
            }

            // Pair up removals and additions as renames, remapping the dictionary keys.
            // This follows the same pattern as HandleRenames in BlackBoardView:
            // removals and additions are paired in list order.
            int pairCount = Math.Min(removed.Count, added.Count);
            for (int i = 0; i < pairCount; i++)
            {
                string oldName = removed[i];
                string newName = added[i];
                if (savedByName.TryGetValue(oldName, out List<UnityEngine.Object> savedList))
                {
                    savedByName.Remove(oldName);
                    savedByName[newName] = savedList;
#if UNITY_EDITOR
                    Debug.Log($"[BB.BuildSerializedRefs] Renamed reference mapping: '{oldName}' → '{newName}' ({savedList?.Count ?? 0} slots)");
#endif
                }
            }
        }

        /// <summary>
        /// Restores saved reference values into serializedReferences at new slot positions.
        /// Matches by variable name — values follow their variable even if the slot offset changed.
        /// </summary>
        private void RestoreReferencesByName(BlackboardDefinition def, Dictionary<string, List<UnityEngine.Object>> saved)
        {
            if (saved == null || saved.Count == 0) return;

            IReadOnlyList<BlackboardVariableBase> allVars = def.GetAllVariables();
            int slot = 0;
            for (int varIndex = 0; varIndex < allVars.Count; varIndex++)
            {
                BlackboardVariableBase bv = allVars[varIndex];
                int stride = bv.Stride;
                int effectiveStride = (stride > 1) ? stride : 1;

                if (!string.IsNullOrEmpty(bv.Name) && saved.TryGetValue(bv.Name, out var savedValues))
                {
                    int count = Math.Min(effectiveStride, savedValues.Count);
                    for (int elementIndex = 0; elementIndex < count; elementIndex++)
                    {
                        int slotIndex = slot + elementIndex;
                        if (slotIndex < serializedReferences.Count && savedValues[elementIndex] != null)
                            serializedReferences[slotIndex] = savedValues[elementIndex];
                    }
                }

                slot += effectiveStride;
            }
        }

        private static int GetTotalSlotCount(BlackboardDefinition definition)
        {
            if (definition == null)
                return 0;

            IReadOnlyList<BlackboardVariableBase> allVars = definition.GetAllVariables();
            int total = 0;
            for (int i = 0; i < allVars.Count; i++)
            {
                if (allVars[i] == null) continue;
                int stride = allVars[i].Stride;
                total += (stride > 1) ? stride : 1;
            }
            return total;
        }

        /// <summary>
        /// Returns the flat slot offset for a named variable, accounting for strides
        /// </summary>
        public int GetSlot(string variableName)
        {
            if (nameToBaseSlot != null && nameToBaseSlot.TryGetValue(variableName, out int slot))
                return slot;

            return -1;
        }

        /// <summary>
        /// Returns a boxed handle for efficient indexed access to a named variable.
        /// Resolve once, then use <c>handle.Value</c> or <c>handle[agentIndex]</c>.
        /// </summary>
        public BoxedVariableHandle GetVariable(string variableName)
        {
            int baseSlot = GetSlot(variableName);
            return new BoxedVariableHandle(storage, baseSlot);
        }

        public void Set<T>(string keyName, T value)
        {
            int slot = GetSlot(keyName);
            if (slot >= 0)
                Set(slot, value);
        }

        public T Get<T>(string keyName)
        {
            int slot = GetSlot(keyName);
            if (slot >= 0)
                return Get<T>(slot);
            return default;
        }

        /// <summary>Get a value by index in the blackboard array.</summary>
        public T Get<T>(int index)
        {
            if (storage == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Blackboard] Storage is NULL, returning default");
#endif
                return default;
            }
            return storage.Get<T>(index + currentAgentOffset);
        }

        /// <summary>Set a value by index in the blackboard array.</summary>
        public void Set<T>(int index, T value)
        {
            if (storage == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Blackboard] Storage is NULL");
#endif
                return;
            }

            int effectiveIndex = index + currentAgentOffset;
            storage.Set(effectiveIndex, value);

            // Keep serialized reference in sync for reference types.
            // Skip when offset is active — squad-data variables are value types.
            if (currentAgentOffset == 0 && definition != null && index >= 0 && index < serializedReferences.Count)
            {
                if (storage.GetSlotKind(index) == BlackboardSlotKind.Reference)
                {
                    if (value == null)
                    {
                        serializedReferences[index] = null;
                    }
                    else if (value is UnityEngine.Object unityObject)
                    {
                        serializedReferences[index] = unityObject;
                    }
                }
            }
        }

        /// <summary>Get a boxed value by slot index. Used by the bridge for type-agnostic copying.</summary>
        public object GetBoxed(int index)
        {
            if (storage == null)
                return null;
            return storage.GetBoxed(index + currentAgentOffset);
        }

        /// <summary>Get a boxed value WITHOUT applying currentAgentOffset.
        public object GetBoxedRaw(int index)
        {
            if (storage == null) return null;
            return storage.GetBoxed(index);
        }

        /// <summary>Set a boxed value WITHOUT applying currentAgentOffset.
        public void SetBoxedRaw(int index, object value)
        {
            if (storage == null) return;
            storage.SetBoxed(index, value);
        }

        /// <summary>Raw slot-range copy (no agent offset). Used by squad sync and agent compaction.</summary>
        public void CopySlotsRawFrom(BlackBoard source, int sourceSlot, int destSlot, int count)
        {
            if (storage == null || source == null || source.storage == null || count <= 0) return;
            storage.CopySlotsFrom(source.storage, sourceSlot, destSlot, count);
        }

        /// <summary>Set a boxed value by slot index. Used by the bridge for type-agnostic copying.</summary>
        public void SetBoxed(int index, object value)
        {
            if (storage == null)
                return;

            int effectiveIndex = index + currentAgentOffset;
            storage.SetBoxed(effectiveIndex, value);

            // Skip serializedReferences sync when offset is active — squad-data is value-typed.
            if (currentAgentOffset == 0 && definition != null && index >= 0 && index < serializedReferences.Count)
            {
                if (storage.GetSlotKind(index) == BlackboardSlotKind.Reference)
                {
                    if (value == null)
                    {
                        serializedReferences[index] = null;
                    }
                    else if (value is UnityEngine.Object unityObject)
                    {
                        serializedReferences[index] = unityObject;
                    }
                }
            }
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public float GetFloat(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetFloat(slot + currentAgentOffset);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public void SetFloat(int slot, float value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetFloat(slot + currentAgentOffset, value);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public int GetInt(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetInt(slot + currentAgentOffset);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public void SetInt(int slot, int value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetInt(slot + currentAgentOffset, value);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public bool GetBool(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetBool(slot + currentAgentOffset);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public void SetBool(int slot, bool value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetBool(slot + currentAgentOffset, value);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public Vector2 GetVector2(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetVector2(slot + currentAgentOffset);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public void SetVector2(int slot, Vector2 value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetVector2(slot + currentAgentOffset, value);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public Vector3 GetVector3(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetVector3(slot + currentAgentOffset);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public void SetVector3(int slot, Vector3 value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetVector3(slot + currentAgentOffset, value);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public Vector4 GetVector4(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetVector4(slot + currentAgentOffset);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public void SetVector4(int slot, Vector4 value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetVector4(slot + currentAgentOffset, value);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public Color GetColor(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetColor(slot + currentAgentOffset);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public void SetColor(int slot, Color value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetColor(slot + currentAgentOffset, value);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public Quaternion GetQuaternion(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetQuaternion(slot + currentAgentOffset);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public void SetQuaternion(int slot, Quaternion value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetQuaternion(slot + currentAgentOffset, value);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset, no boxing.</summary>
        public T GetObject<T>(int slot) where T : class
        {
            if (typed == null) { EditorWarnStorageNull(); return null; }
            return typed.GetObject<T>(slot + currentAgentOffset);
        }

        /// <summary>Typed hot-path accessor — applies currentAgentOffset. Syncs serializedReferences on ref slots.</summary>
        public void SetObject(int slot, object value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }

            typed.SetObject(slot + currentAgentOffset, value);

            // Keep serialized reference in sync, same rule as SetBoxed.
            if (currentAgentOffset == 0 && definition != null && slot >= 0 && slot < serializedReferences.Count
                && storage.GetSlotKind(slot) == BlackboardSlotKind.Reference)
            {
                serializedReferences[slot] = value as UnityEngine.Object;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void EditorWarnStorageNull()
        {
            Debug.LogWarning("[Blackboard] Storage is NULL");
        }

        T IBlackBoardAccess.Get<T>(int slot) => Get<T>(slot);
        void IBlackBoardAccess.Set<T>(int slot, T value) => Set(slot, value);
        object IBlackBoardAccess.GetBoxed(int slot) => GetBoxed(slot);
        void IBlackBoardAccess.SetBoxed(int slot, object value) => SetBoxed(slot, value);
        object IBlackBoardAccess.GetBoxedRaw(int slot) => GetBoxedRaw(slot);
        void IBlackBoardAccess.SetBoxedRaw(int slot, object value) => SetBoxedRaw(slot, value);

        /// <summary>Finds a value-type override by variable name and element index.</summary>
        public BlackboardValueOverride GetValueOverride(string variableName, int elementIndex)
        {
            for (int i = 0; i < valueOverrides.Count; i++)
            {
                BlackboardValueOverride vo = valueOverrides[i];
                if (vo.variableName == variableName && vo.elementIndex == elementIndex)
                    return vo;
            }
            return null;
        }

        /// <summary>Creates or updates a value-type override for the given variable/element.</summary>
        public void SetValueOverride(string variableName, int elementIndex, object value)
        {
            BlackboardValueOverride existing = GetValueOverride(variableName, elementIndex);
            if (existing != null)
            {
                existing.SetBoxedValue(value);
            }
            else
            {
                BlackboardValueOverride newOverride = new BlackboardValueOverride
                {
                    variableName = variableName,
                    elementIndex = elementIndex
                };
                newOverride.SetBoxedValue(value);
                valueOverrides.Add(newOverride);
            }
        }

        /// <summary>Removes a value-type override, reverting to the definition value.</summary>
        public void ClearValueOverride(string variableName, int elementIndex)
        {
            for (int i = valueOverrides.Count - 1; i >= 0; i--)
            {
                if (valueOverrides[i].variableName == variableName && valueOverrides[i].elementIndex == elementIndex)
                {
                    valueOverrides.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>Returns whether the given reference slot is explicitly overridden.</summary>
        public bool IsReferenceSlotOverridden(int slotIndex) => overriddenReferenceSlots.Contains(slotIndex);

        /// <summary>Marks a reference slot as overridden.</summary>
        public void SetReferenceSlotOverridden(int slotIndex)
        {
            if (!overriddenReferenceSlots.Contains(slotIndex))
                overriddenReferenceSlots.Add(slotIndex);
        }

        /// <summary>Clears the override flag for a reference slot.</summary>
        public void ClearReferenceSlotOverridden(int slotIndex)
        {
            overriddenReferenceSlots.Remove(slotIndex);
        }


    }
}
