using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace BehaviourTree.Core
{
    public class BlackboardDefinition : ScriptableObject
    {
        [NonSerialized] public BehaviourTreeAssetBase sourceTreeAsset;
        [NonSerialized] public string sourceTreeGuid;

        [SerializeReference] public List<BlackboardVariableBase> sharedVariables = new();

        public int VariableCount => sharedVariables?.Count ?? 0;

        public IReadOnlyList<BlackboardVariableBase> GetAllVariables()
        {
            if (sharedVariables == null) return Array.Empty<BlackboardVariableBase>();
            return sharedVariables;
        }

        public BlackboardVariableBase FindVariable(string variableName)
        {
            if (string.IsNullOrEmpty(variableName) || sharedVariables == null)
                return null;

            foreach (BlackboardVariableBase v in sharedVariables)
            {
                if (v.Name == variableName)
                    return v;
            }
            return null;
        }

        public int GetVariableIndex(string variableName)
        {
            if (string.IsNullOrEmpty(variableName) || sharedVariables == null)
                return -1;

            for (int i = 0; i < sharedVariables.Count; i++)
            {
                if (sharedVariables[i].Name == variableName)
                    return i;
            }
            return -1;
        }

        public BlackboardVariable<T> AddVariable<T>(string name, int stride = 1, T initialValue = default)
        {
            BlackboardVariable<T> variable = new BlackboardVariable<T>(name, stride, initialValue);
            if (sharedVariables == null)
                sharedVariables = new List<BlackboardVariableBase>();
            sharedVariables.Add(variable);
            return variable;
        }

        public BlackboardVariableBase CopyVariable(BlackboardVariableBase source)
        {
            if (source == null) return null;

            if (FindVariable(source.Name) != null) return null;

            Type valueType = source.GetValueType();
            if (valueType == null) return null;

            Type genericType = typeof(BlackboardVariable<>).MakeGenericType(valueType);
            BlackboardVariableBase clone = (BlackboardVariableBase)Activator.CreateInstance(genericType);
            clone.Name = source.Name;
            clone.Stride = source.Stride;

            if (sharedVariables == null)
                sharedVariables = new List<BlackboardVariableBase>();
            sharedVariables.Add(clone);
            return clone;
        }

        public static bool EnsureBaseChannel<T>(
            BlackboardDefinition bbDef, string name, bool isSquadData, bool isSystemVariable = true)
        {
            if (bbDef == null || bbDef.sharedVariables == null)
                return false;

            BlackboardVariableBase existing = bbDef.FindVariable(name);
            if (existing != null)
            {
                bool changed = false;

                if (existing.GetValueType() != typeof(T))
                {
                    Debug.LogWarning(
                        $"[BaseChannel] '{name}' has wrong type " +
                        $"(expected {typeof(T).Name}, got {existing.GetValueType()?.Name ?? "null"}).");
                }

                if (!existing.isSystemVariable && isSystemVariable)
                {
                    existing.isSystemVariable = true;
                    changed = true;
                }

                if (!existing.isSquadData && isSquadData)
                {
                    existing.isSquadData = isSquadData;
                    changed = true;
                }

                return changed;
            }

            Debug.LogWarning($"[BaseChannel] Missing '{name}' on '{bbDef.name}' — auto-creating.");

            bbDef.AddVariable<T>(name, stride: 1, initialValue: default);
            BlackboardVariableBase created = bbDef.FindVariable(name);
            if (created != null)
            {
                created.isSquadData = isSquadData;
                created.isSystemVariable = isSystemVariable;
            }

            return true;
        }
    }
}
