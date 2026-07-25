using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BehaviourTree.Core;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// The single blackboard-variable dropdown used by every param row.
    /// Replaces the three former near-duplicate variants in CustomNodeEditor.
    /// </summary>
    public sealed class BlackboardVariablePicker
    {
        public const float SmallButtonWidth = 20f;

        public struct Options
        {
            /// <summary>Label text. Null draws no label column.</summary>
            public string label;
            public float labelWidth;
            public float dropdownWidth;
            /// <summary>Show the "S" (search by name) and "F" (change type filter) buttons.</summary>
            public bool showSearchFilterButtons;
            /// <summary>Read-only mode: also show the proxy parent mapping (legacy InspectorView rows).</summary>
            public bool showProxyMapping;
            /// <summary>Multi-type filter. Null/empty falls back to exact expectedType match.</summary>
            public Type[] allowedTypes;
            /// <summary>Called after the user picks a variable (dropdown or search). Host writes entry metadata + syncs.</summary>
            public Action<BlackboardVariableBase> onVariablePicked;
            /// <summary>Called after the user changes the type filter via the "F" button.</summary>
            public Action<Type, bool> onTypeFilterPicked; // (type, isArray)
        }

        private readonly List<string> matchingVars = new List<string>();
        private readonly List<string> matchingVarNames = new List<string>();

        public void Draw(SerializedProperty variableNameProp, Type expectedType, bool isArray, Options opts)
        {
            BlackboardDefinition blackboardDef = BehaviourTreeEditor.currentBlackboardDef;

            if (InspectorView.IsRenderingReadOnly)
            {
                if (opts.label != null)
                    EditorGUILayout.LabelField(opts.label, variableNameProp.stringValue);
                else
                    EditorGUILayout.LabelField(variableNameProp.stringValue);

                if (opts.showProxyMapping && InspectorView.CurrentProxyMappings != null &&
                    InspectorView.CurrentProxyMappings.TryGetValue(variableNameProp.stringValue, out string parentVar))
                    EditorGUILayout.LabelField("Mapped To: ", parentVar);
                return;
            }

            bool hasTypeFilter = opts.allowedTypes != null && opts.allowedTypes.Length > 0;
            if (expectedType == null && !hasTypeFilter)
            {
                EditorGUILayout.HelpBox("Select a variable -->", MessageType.Info);
                return;
            }
            if (blackboardDef == null)
            {
                EditorGUILayout.HelpBox("No Blackboard Definition assigned.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (opts.label != null)
            {
                EditorGUILayout.LabelField(opts.label, GUILayout.Width(opts.labelWidth));
                GUILayout.FlexibleSpace();
            }

            IReadOnlyList<BlackboardVariableBase> allVars = blackboardDef.GetAllVariables();
            if (allVars == null || allVars.Count == 0)
            {
                EditorGUILayout.HelpBox("No Blackboard variables added.", MessageType.Warning);
                EditorGUILayout.EndHorizontal();
                return;
            }

            BuildMatchingList(allVars, expectedType, isArray, opts.allowedTypes);

            if (matchingVars.Count == 0)
            {
                string typeName = expectedType != null
                    ? TypeDisplayRegistry.instance.GetDisplayName(expectedType) : "any";
                EditorGUILayout.HelpBox($"No matching variable of type '{typeName}' in Blackboard.", MessageType.Info);
                variableNameProp.stringValue = "";
                EditorGUILayout.EndHorizontal();
                return;
            }

            // Prepend placeholder so unassigned fields don't auto-pick the first variable
            matchingVars.Insert(0, "Select a variable...");
            matchingVarNames.Insert(0, string.Empty);

            string currentVal = variableNameProp.stringValue;
            int selectedIndex = matchingVarNames.IndexOf(currentVal);
            if (selectedIndex < 0) selectedIndex = 0;

            selectedIndex = EditorGUILayout.Popup(selectedIndex, matchingVars.ToArray(),
                GUILayout.Width(opts.dropdownWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            string newVal = matchingVarNames[selectedIndex];
            if (newVal != currentVal)
            {
                variableNameProp.stringValue = newVal;
                if (!string.IsNullOrEmpty(newVal))
                {
                    BlackboardVariableBase picked = FindVar(allVars, newVal);
                    if (picked != null) opts.onVariablePicked?.Invoke(picked);
                }
            }

            if (opts.showSearchFilterButtons)
                DrawSearchFilterButtons(variableNameProp, blackboardDef, opts);

            EditorGUILayout.EndHorizontal();
        }

        private void BuildMatchingList(IReadOnlyList<BlackboardVariableBase> allVars,
            Type expectedType, bool isArray, Type[] allowedTypes)
        {
            matchingVars.Clear();
            matchingVarNames.Clear();
            Type[] filterTypes = allowedTypes != null && allowedTypes.Length > 0
                ? allowedTypes
                : (expectedType != null ? new[] { expectedType } : null);

            for (int i = 0; i < allVars.Count; i++)
            {
                BlackboardVariableBase bv = allVars[i];
                Type bbType = bv.GetValueType();
                if (bbType == null) continue;
                if (filterTypes != null)
                {
                    bool isArrayLike = bv.Stride > 1 || bv.isSquadData;
                    if (!VariableSearchPopup.IsTypeAllowed(bbType, isArrayLike, filterTypes))
                        continue;
                }
                if (isArray)
                {
                    if (bv.Stride <= 1) continue;
                    matchingVars.Add($"{bv.Name} [{bv.Stride}]");
                }
                else
                {
                    if (bv.Stride > 1 && !bv.isSquadData) continue;
                    matchingVars.Add(bv.Name);
                }
                matchingVarNames.Add(bv.Name);
            }
        }

        private void DrawSearchFilterButtons(SerializedProperty variableNameProp,
            BlackboardDefinition blackboardDef, Options opts)
        {
            bool squadCtx = BehaviourTreeEditor.currentTree is CommanderTreeAsset;

            Rect searchRect = EditorGUILayout.GetControlRect(
                GUILayout.Width(SmallButtonWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUI.Button(searchRect, new GUIContent("S", "Search for a variable by name")))
            {
                GUI.FocusControl(null);
                var popup = new VariableSearchPopup(blackboardDef, opts.allowedTypes,
                    (BlackboardVariableBase chosen, bool chosenIsArray) =>
                    {
                        if (chosen == null) return;
                        variableNameProp.stringValue = chosen.Name;
                        variableNameProp.serializedObject.ApplyModifiedProperties();
                        opts.onVariablePicked?.Invoke(chosen);
                    }, isSquadContext: squadCtx);
                PopupWindow.Show(new Rect(searchRect.x, searchRect.yMax, 0, 0), popup);
            }

            Rect filterRect = EditorGUILayout.GetControlRect(
                GUILayout.Width(SmallButtonWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUI.Button(filterRect, new GUIContent("F", "Change the variable type filter")))
            {
                GUI.FocusControl(null);
                var popup = new VariableTypeSearchPopup(
                    (Type varType, bool varIsArray, int stride, bool isSquadData) =>
                    {
                        opts.onTypeFilterPicked?.Invoke(varType, varIsArray || isSquadData);
                    }, opts.allowedTypes, squadCtx);
                PopupWindow.Show(new Rect(filterRect.x, filterRect.yMax, 0, 0), popup);
            }
        }

        private static BlackboardVariableBase FindVar(IReadOnlyList<BlackboardVariableBase> allVars, string name)
        {
            for (int i = 0; i < allVars.Count; i++)
                if (allVars[i].Name == name) return allVars[i];
            return null;
        }
    }
}
