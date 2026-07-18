using System;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// A ScriptableObject template that holds a set of blackboard variables.
    /// Created via CreateAssetMenu and edited in a dedicated editor window.
    /// Apply to a BlackboardDefinition to populate variables in bulk.
    /// </summary>
    [CreateAssetMenu(menuName = "BehaviourTree/Blackboard Template")]
    public class BlackboardTemplate : ScriptableObject
    {
        /// <summary>
        /// Embedded BlackboardDefinition that stores the template's variables.
        /// Uses BlackboardDefinition's serialization infrastructure so the
        /// same BlackBoardView UI can be reused for editing.
        /// </summary>
        public BlackboardDefinition templateDefinition;

        /// <summary>
        /// Fired after a template has been successfully applied to a target definition.
        /// Views subscribed to this can refresh themselves (e.g. BlackBoardView, SquadDefinitionEditor).
        /// </summary>
        public static event Action<BlackboardDefinition> Applied;

        public enum ApplyMode
        {
            AddMissing,
            Overwrite
        }

        /// <summary>
        /// Applies this template's variables to the given <paramref name="target"/> definition.
        /// </summary>
        /// <param name="target">The BlackboardDefinition to apply variables to.</param>
        /// <param name="mode">
        /// AddMissing: Only adds variables not already present (matched by name).
        /// Overwrite: Adds missing variables AND replaces existing ones with template versions.
        /// </param>
        /// <param name="skipSquadData">
        /// When true, variables with isSquadData == true are skipped during application.
        /// Use when applying to agent-tree blackboards that shouldn't receive squad data.
        /// </param>
        public void ApplyTo(BlackboardDefinition target, ApplyMode mode, bool skipSquadData = false)
        {
            if (target == null)
            {
                Debug.LogWarning($"[BlackboardTemplate] Cannot apply template '{name}': target is null.");
                return;
            }

            if (templateDefinition?.sharedVariables == null)
            {
                Debug.LogWarning($"[BlackboardTemplate] Template '{name}' has no variables to apply.");
                return;
            }

            // Filter null holes from [SerializeReference]
            templateDefinition.sharedVariables.RemoveAll(v => v == null);

            foreach (BlackboardVariableBase sourceVar in templateDefinition.sharedVariables)
            {
                if (sourceVar == null) continue;

                // Skip squad-data variables when applying to agent trees
                if (skipSquadData && sourceVar.isSquadData)
                    continue;

                int existingIdx = target.GetVariableIndex(sourceVar.Name);

                if (existingIdx >= 0)
                {
                    if (mode == ApplyMode.Overwrite)
                    {
                        BlackboardVariableBase clone = sourceVar.Clone();
                        if (clone != null)
                        {
                            target.sharedVariables[existingIdx] = clone;
                        }
                    }
                    // AddMissing: skip existing variables
                }
                else
                {
                    // Add new variable from template (cloned so values are preserved)
                    BlackboardVariableBase clone = sourceVar.Clone();
                    if (clone != null)
                    {
                        if (target.sharedVariables == null)
                            target.sharedVariables = new System.Collections.Generic.List<BlackboardVariableBase>();
                        target.sharedVariables.Add(clone);
                    }
                }
            }

            // Notify subscribers that this definition was modified
            Applied?.Invoke(target);
        }
    }
}
