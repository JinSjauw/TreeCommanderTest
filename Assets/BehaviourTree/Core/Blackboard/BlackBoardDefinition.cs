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

        /// <summary>
        /// Ensures a variable with the given name and type exists. If it already exists, returns it unchanged.
        /// If not, creates a new variable with the specified properties.
        /// Useful for auto-creating system variables when binding groups are set up.
        /// </summary>
        public BlackboardVariableBase EnsureVariable(string name, Type type, int stride = 1, bool isSquadData = false, bool isSystemVariable = false)
        {
            if (string.IsNullOrEmpty(name) || type == null) return null;

            BlackboardVariableBase existing = FindVariable(name);
            if (existing != null) return existing;

            Type genericType = typeof(BlackboardVariable<>).MakeGenericType(type);
            BlackboardVariableBase variable = (BlackboardVariableBase)Activator.CreateInstance(genericType);
            variable.Name = name;
            variable.Stride = stride;
            variable.isSquadData = isSquadData;
            variable.isSystemVariable = isSystemVariable;
            variable.IsArray = stride > 1;

            if (sharedVariables == null)
                sharedVariables = new List<BlackboardVariableBase>();
            sharedVariables.Add(variable);
            return variable;
        }

    }
}
