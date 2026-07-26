using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using BehaviourTree.Core;
using BehaviourTree.Runtime;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Renders the dynamic parameter section of the node inspector.
    /// Extracted from CustomNodeEditor (pure code motion): owns the descriptor-driven
    /// field-entry rebuild, all param row drawers, and the role/order dropdowns.
    /// </summary>
    public sealed class NodeParamSectionRenderer
    {
        public const float SmallButtonWidth = 20f;
        public const float SmallButtonsMargin = SmallButtonWidth * 2f + 6f;
        public const float FieldLabelWidth = 140f;
        public const float InputFieldWidth = 130f;

        private readonly UnityEditor.Editor host;
        private readonly SerializedProperty fieldEntriesProp;
        private DynamicParamDescriptor[] currentDescriptors;
        private readonly BlackboardVariablePicker variablePicker = new BlackboardVariablePicker();
        private GUIStyle style;

        public bool nodeVisualsChangedThisFrame;

        private GUIStyle RichTextLabelStyle
        {
            get
            {
                if (style == null)
                {
                    style = new GUIStyle(EditorStyles.label) { richText = true };
                }
                return style;
            }
        }

        public NodeParamSectionRenderer(UnityEditor.Editor host, SerializedProperty fieldEntriesProp)
        {
            this.host = host;
            this.fieldEntriesProp = fieldEntriesProp;
        }

        public void BuildFieldEntries(string selectedMethodName, bool methodChanged)
        {
            NodeMethod temp = MethodRegistry.CreateInstance(selectedMethodName);
            DynamicParamDescriptor[] descriptors = NodeParamSchema.GetForMethod(temp);

            if (descriptors == null || descriptors.Length == 0)
            {
                fieldEntriesProp.ClearArray();
                if (host.target is CompositeNode) return;
                string methodDesc = !string.IsNullOrEmpty(selectedMethodName) ? selectedMethodName : "(none)";
                EditorGUILayout.HelpBox($"No schema found for method '{methodDesc}'.", MessageType.Info);
                return;
            }

            Render(descriptors, methodChanged);
        }

        // ── Conditional Abort Validation ──

        private void DrawRoleDropdown(SerializedProperty entryProp)
        {
            SerializedProperty intValueProp = entryProp.FindPropertyRelative("intValue");
            if (intValueProp == null) return;

            BaseEditorTreeAsset tree = BehaviourTreeEditor.currentTree;

            CommanderTreeAsset commanderTree = tree as CommanderTreeAsset;
            if (commanderTree == null)
            {
                EditorGUILayout.HelpBox("Role dropdown only available on commander trees.", MessageType.Warning);
                return;
            }

            SquadDefinition squad = commanderTree.commanderSquad;
            if (squad == null)
            {
                EditorGUILayout.HelpBox("No commander squad assigned. Configure it in the Commander tab.", MessageType.Warning);
                return;
            }

            if (squad.availableRoles == null || squad.availableRoles.Count == 0)
            {
                EditorGUILayout.HelpBox("No roles defined in the commander squad.", MessageType.Warning);
                return;
            }

            List<SquadRole> roles = squad.availableRoles;
            string[] roleNames = new string[roles.Count];
            for (int roleIndex = 0; roleIndex < roles.Count; roleIndex++)
                roleNames[roleIndex] = roles[roleIndex].name;

            int currentIndex = intValueProp.intValue;
            if (currentIndex < 0 || currentIndex >= roles.Count) currentIndex = 0;

            if (InspectorView.IsRenderingReadOnly)
            {
                EditorGUILayout.LabelField("Role", roleNames[currentIndex]);
            }
            else
            {
                int newIndex = EditorGUILayout.Popup("Role", currentIndex, roleNames, GUILayout.Width(FieldLabelWidth + InputFieldWidth));
                intValueProp.intValue = newIndex;
            }
        }

        private void DrawOrderDropdown(SerializedProperty entryProp)
        {
            SerializedProperty stringValueProp = entryProp.FindPropertyRelative("stringValue");
            SerializedProperty intValueProp = entryProp.FindPropertyRelative("intValue");
            SerializedProperty isOrderProp = entryProp.FindPropertyRelative("isOrderConstant");
            if (stringValueProp == null || intValueProp == null) return;

            if (isOrderProp != null)
                isOrderProp.boolValue = true;

            OrderRegistry registry = OrderRegistry.FindInstance();
            if (registry == null || registry.orderNames == null || registry.orderNames.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No OrderRegistry asset found. Create one via Assets > Create > BehaviourTree > Order Registry.",
                    MessageType.Warning);
                return;
            }

            string currentName = stringValueProp.stringValue;
            int currentIndex = 0;
            if (!string.IsNullOrEmpty(currentName))
            {
                currentIndex = registry.orderNames.IndexOf(currentName);
                if (currentIndex < 0) currentIndex = 0;
            }

            if (InspectorView.IsRenderingReadOnly)
            {
                string displayName = currentIndex < registry.orderNames.Count
                    ? registry.orderNames[currentIndex]
                    : "(unknown)";
                EditorGUILayout.LabelField("Value", displayName);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Value");
                string buttonLabel = !string.IsNullOrEmpty(currentName) ? currentName : "Select Order...";
                if (GUILayout.Button(buttonLabel, EditorStyles.popup, GUILayout.Width(InputFieldWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                {
                    OrderSearchProvider provider = ScriptableObject.CreateInstance<OrderSearchProvider>();
                    provider.registry = registry;
                    provider.onOrderSelected = name =>
                    {
                        stringValueProp.stringValue = name;
                        intValueProp.intValue = registry.orderNames.IndexOf(name);
                        stringValueProp.serializedObject.ApplyModifiedProperties();
                    };
                    SearchWindow.Open(
                        new SearchWindowContext(GUIUtility.GUIToScreenPoint(
                            Event.current.mousePosition)), provider);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Dynamic-Type Node Methods — driven by GetDynamicParamDescriptors
        // Adding a new dynamic node: override GetDynamicParamDescriptors()
        // on the NodeMethod subclass — NO editor changes required.
        // ═══════════════════════════════════════════════════════════════

        public void Render(DynamicParamDescriptor[] descriptors, bool methodChanged)
        {
            currentDescriptors = descriptors;
            if (descriptors == null || descriptors.Length == 0)
            {
                if (methodChanged) fieldEntriesProp.ClearArray();
                return;
            }

            int realCount = descriptors.Length;

            // Resize entries on method change
            if (methodChanged)
            {
                while (fieldEntriesProp.arraySize < realCount)
                {
                    int insertIndex = fieldEntriesProp.arraySize;
                    fieldEntriesProp.InsertArrayElementAtIndex(insertIndex);
                    InitNewEntryFromDescriptor(fieldEntriesProp.GetArrayElementAtIndex(insertIndex), descriptors[insertIndex]);
                }
                while (fieldEntriesProp.arraySize > realCount)
                    fieldEntriesProp.DeleteArrayElementAtIndex(fieldEntriesProp.arraySize - 1);

                // Sync isVariable from descriptors for non-Toggle, non-SO-Constant kinds.
                // Existing entries may carry stale isVariable after descriptor layout changes
                // (e.g. a Variable param replacing a Constant at the same index).
                // Toggle and ScriptableObjectConstant kinds preserve user choice via C/V/SO buttons.
                int syncCount = Mathf.Min(fieldEntriesProp.arraySize, realCount);
                for (int i = 0; i < syncCount; i++)
                {
                    DynamicParamDescriptor desc = descriptors[i];
                    if (desc.kind == DynamicParamKind.Toggle || desc.kind == DynamicParamKind.ScriptableObjectConstant)
                        continue;
                    SerializedProperty entry = fieldEntriesProp.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("isVariable").boolValue =
                        desc.kind == DynamicParamKind.Variable;
                }

                // Hidden params: auto-bind by convention, no UI rendered
                for (int i = 0; i < syncCount; i++)
                {
                    DynamicParamDescriptor desc = descriptors[i];
                    if (!desc.isHidden) continue;
                    SerializedProperty entry = fieldEntriesProp.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("isVariable").boolValue = true;
                    entry.FindPropertyRelative("variableName").stringValue = desc.autoVariableName;
                }
            }

            // Sync linked entry types from their source indices
            SyncLinkedEntryTypes();
            EnforceProjectedMetadata(descriptors);

            if (fieldEntriesProp.arraySize < realCount)
                return;

            EditorGUILayout.BeginVertical("box");

            // ── Generic parameter rows ──
            for (int i = 0; i < realCount; i++)
            {
                DynamicParamDescriptor desc = descriptors[i];
                SerializedProperty entry = fieldEntriesProp.GetArrayElementAtIndex(i);
                Type paramType = ResolveEntryType(i);
                bool entryIsArray = entry.FindPropertyRelative("isArray").boolValue;

                // Hidden params render nothing (auto-bound in resize step)
                if (desc.isHidden) continue;

                // Visibility gated on another entry's bool (e.g. customTickValue)
                if (desc.visibilityDependsOnIndex is int depIndex &&
                    depIndex >= 0 && depIndex < fieldEntriesProp.arraySize &&
                    !fieldEntriesProp.GetArrayElementAtIndex(depIndex).FindPropertyRelative("boolValue").boolValue)
                {
                    continue;
                }

                bool hasTitle = !string.IsNullOrEmpty(desc.titleLabel);

                // ── Title header ──
                if (hasTitle)
                {
                    Type displayType = paramType ?? (desc.allowedTypes != null && desc.allowedTypes.Length == 1 ? desc.allowedTypes[0] : null);
                    string header;
                    if (displayType != null)
                    {
                        string typeDisplay = TypeDisplayRegistry.instance.GetDisplayName(displayType);
                        if (entryIsArray) typeDisplay += "[]";
                        string colorHex = TypeDisplayRegistry.instance.GetRichColorHex(displayType);
                        header = $"<b>{desc.titleLabel}</b> : <color=#{colorHex}>{typeDisplay}</color>";
                    }
                    else
                    {
                        header = $"<b>{desc.titleLabel}</b>";
                    }
                    EditorGUILayout.LabelField(header, RichTextLabelStyle);
                }

                switch (desc.kind)
                {
                    case DynamicParamKind.Variable:
                        DrawVariableParamRow(entry, desc, paramType, entryIsArray, i, hasTitle);
                        break;
                    case DynamicParamKind.Toggle:
                        DrawToggleParamRow(entry, desc, paramType, entryIsArray, hasTitle);
                        break;
                    case DynamicParamKind.Constant:
                        if (desc.constantEditor == ConstantEditorHint.RoleDropdown) { DrawRoleDropdown(entry); break; }
                        if (desc.constantEditor == ConstantEditorHint.OrderDropdown) { DrawOrderDropdown(entry); break; }
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(desc.label, GUILayout.Width(FieldLabelWidth));
                        GUILayout.FlexibleSpace();
                        DrawConstantFieldForType(entry, paramType, desc.label, showLabel: false);
                        GUILayout.Space(SmallButtonsMargin);
                        EditorGUILayout.EndHorizontal();
                        break;
                    case DynamicParamKind.Operation:
                        DrawOperationParamRow(entry, desc, paramType);
                        break;
                    case DynamicParamKind.ScriptableObjectConstant:
                        DrawSOConstantParamRow(entry, desc, paramType, entryIsArray, hasTitle);
                        break;
                }
                EditorGUILayout.Space(4f);
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Attribute-projected descriptors (fieldName != null) carry static metadata that
        /// overrides the serialized entry every repaint — same guarantee the legacy
        /// reflection path gave. Hand-authored dynamic descriptors skip this (user edits win).
        /// </summary>
        private void EnforceProjectedMetadata(DynamicParamDescriptor[] descriptors)
        {
            for (int i = 0; i < descriptors.Length && i < fieldEntriesProp.arraySize; i++)
            {
                DynamicParamDescriptor desc = descriptors[i];
                SerializedProperty entry = fieldEntriesProp.GetArrayElementAtIndex(i);

                if (desc.fieldName != null)
                {
                    entry.FindPropertyRelative("fieldName").stringValue = desc.fieldName;
                    entry.FindPropertyRelative("fieldTypeName").stringValue =
                        desc.allowedTypes != null && desc.allowedTypes.Length > 0
                            ? desc.allowedTypes[0].AssemblyQualifiedName : string.Empty;
                }

                // Unified isVariable rule: Variable => always variable, Constant => never,
                // Toggle/SOConstant preserve the user's C/V/SO choice.
                switch (desc.kind)
                {
                    case DynamicParamKind.Variable:
                        entry.FindPropertyRelative("isVariable").boolValue = true;
                        break;
                    case DynamicParamKind.Constant:
                        entry.FindPropertyRelative("isVariable").boolValue = false;
                        break;
                }

                // SOConstant rows have no type-filter button, so for single-type
                // descriptors the entry type is unambiguous — repair stale values
                // (e.g. entries serialized before the descriptor declared allowedTypes).
                if (desc.kind == DynamicParamKind.ScriptableObjectConstant &&
                    desc.allowedTypes != null && desc.allowedTypes.Length == 1)
                {
                    entry.FindPropertyRelative("fieldTypeName").stringValue =
                        desc.allowedTypes[0].AssemblyQualifiedName;
                }

                if (desc.isArray)
                    entry.FindPropertyRelative("isArray").boolValue = true;
            }
        }

        private Type ResolveEntryType(int entryIndex)
        {
            if (entryIndex >= fieldEntriesProp.arraySize) return null;
            string typeName = fieldEntriesProp.GetArrayElementAtIndex(entryIndex)
                .FindPropertyRelative("fieldTypeName")?.stringValue;
            if (string.IsNullOrEmpty(typeName)) return null;
            return FieldTypeHelper.TryGetSystemTypeFromName(typeName, out Type t) ? t : null;
        }

        /// <summary>Sets initial defaults on a newly created entry from its descriptor.</summary>
        private static void InitNewEntryFromDescriptor(SerializedProperty entry, DynamicParamDescriptor desc)
        {
            entry.FindPropertyRelative("fieldName").stringValue =
                desc.fieldName ?? desc.label.ToLowerInvariant().Replace(" ", "");
            entry.FindPropertyRelative("isVariable").boolValue =
                desc.kind == DynamicParamKind.Variable || desc.kind == DynamicParamKind.Toggle;
            entry.FindPropertyRelative("isConfigConstant").boolValue =
                desc.kind == DynamicParamKind.ScriptableObjectConstant;
            // SO constants start with empty selection — user picks a field via the "..." button
            entry.FindPropertyRelative("configSourceGuid").stringValue = "";
            entry.FindPropertyRelative("configFieldName").stringValue = "";
            if (desc.kind == DynamicParamKind.Operation && desc.operationEnumType != null)
                entry.FindPropertyRelative("fieldTypeName").stringValue = typeof(int).AssemblyQualifiedName;
            // Set fieldTypeName from allowedTypes for Variable and Toggle kinds.
            // If no allowedTypes specified, default to int.
            Type firstType = desc.allowedTypes != null && desc.allowedTypes.Length > 0
                ? desc.allowedTypes[0]
                : typeof(int);
            entry.FindPropertyRelative("fieldTypeName").stringValue = firstType.AssemblyQualifiedName;
        }

        /// <summary>
        /// Propagates types from source entries to any entry whose
        /// <see cref="DynamicParamDescriptor.syncTypeFromIndex"/> points to a source.
        /// Called after initialisation and after any type-change callback (S/F buttons).
        /// </summary>
        private void SyncLinkedEntryTypes()
        {
            if (currentDescriptors == null) return;
            for (int i = 0; i < currentDescriptors.Length; i++)
            {
                int? srcIndex = currentDescriptors[i].syncTypeFromIndex;
                if (srcIndex == null || srcIndex.Value >= fieldEntriesProp.arraySize) continue;

                string srcTypeName = fieldEntriesProp.GetArrayElementAtIndex(srcIndex.Value)
                    .FindPropertyRelative("fieldTypeName")?.stringValue;
                if (string.IsNullOrEmpty(srcTypeName)) continue;

                SerializedProperty ftProp = fieldEntriesProp.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("fieldTypeName");
                if (ftProp != null)
                {
                    string targetTypeName = srcTypeName;
                    if (currentDescriptors[i].syncElementType)
                    {
                        Type srcType = FieldTypeHelper.GetSystemTypeFromName(srcTypeName);
                        // fieldTypeName stores the element type for blackboard variables.
                        // Array-ness is tracked via the isArray serialized property.
                        bool sourceIsArray = srcType != null && srcType.IsArray;
                        if (!sourceIsArray && srcIndex >= 0 && srcIndex.Value < fieldEntriesProp.arraySize)
                        {
                            SerializedProperty srcIsArrayProp = fieldEntriesProp
                                .GetArrayElementAtIndex(srcIndex.Value)
                                .FindPropertyRelative("isArray");
                            sourceIsArray = srcIsArrayProp != null && srcIsArrayProp.boolValue;
                        }

                        if (sourceIsArray && srcType != null)
                        {
                            Type elemType = srcType.IsArray ? srcType.GetElementType() : srcType;
                            if (elemType != null)
                                targetTypeName = elemType.AssemblyQualifiedName;
                        }

                        // Output is always scalar when syncing element type
                        SerializedProperty isArrayProp = fieldEntriesProp.GetArrayElementAtIndex(i)
                            .FindPropertyRelative("isArray");
                        if (isArrayProp != null)
                            isArrayProp.boolValue = false;
                    }
                    if (ftProp.stringValue != targetTypeName)
                        ftProp.stringValue = targetTypeName;
                }
            }
            fieldEntriesProp.serializedObject.ApplyModifiedProperties();
        }

        // ── Row helpers — one per DynamicParamKind ──

        /// <summary>Variable picker row: dropdown + S (search) + F (filter) buttons inline.</summary>
        private void DrawVariableParamRow(SerializedProperty entry, DynamicParamDescriptor desc, Type paramType, bool isArray, int entryIndex, bool hasTitle)
        {
            SerializedProperty variableNameProp = entry.FindPropertyRelative("variableName");
            string displayLabel = hasTitle ? desc.label : BuildTypedLabel(desc.label, paramType, desc.allowedTypes);
            DrawDynamicVariableField(variableNameProp, paramType, isArray, displayLabel, desc.allowedTypes, entryIndex);
        }

        private static string BuildTypedLabel(string baseLabel, Type resolvedType, Type[] allowedTypes)
        {
            if (resolvedType != null)
                return $"{baseLabel} ({TypeDisplayRegistry.instance.GetDisplayName(resolvedType)})";
            if (allowedTypes != null && allowedTypes.Length == 1)
                return $"{baseLabel} ({TypeDisplayRegistry.instance.GetDisplayName(allowedTypes[0])})";
            return baseLabel;
        }

        /// <summary>Toggle row: variable dropdown OR constant field + C/V toggle button.</summary>
        private void DrawToggleParamRow(SerializedProperty entry, DynamicParamDescriptor desc, Type paramType, bool isArray, bool hasTitle)
        {
            SerializedProperty isVarProp = entry.FindPropertyRelative("isVariable");
            SerializedProperty variableNameProp = entry.FindPropertyRelative("variableName");

            string typedLabel = hasTitle ? desc.label : BuildTypedLabel(desc.label, paramType, desc.allowedTypes);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(typedLabel, GUILayout.Width(FieldLabelWidth));
            GUILayout.FlexibleSpace();

            if (isVarProp.boolValue)
            {
                DrawVariableDropdownWithSquadFilter(variableNameProp, paramType, isArray);
            }
            else if (desc.constantEditor == ConstantEditorHint.RoleDropdown)
            {
                DrawRoleDropdown(entry);
            }
            else if (desc.constantEditor == ConstantEditorHint.OrderDropdown)
            {
                DrawOrderDropdown(entry);
            }
            else
            {
                DrawConstantFieldForType(entry, paramType, desc.label, showLabel: false);
            }

            // C/V toggle button
            {
                string toggleLabel = isVarProp.boolValue ? "C" : "V";
                string toggleTooltip = isVarProp.boolValue ? "Switch to constant" : "Switch to variable";
                if (GUILayout.Button(new GUIContent(toggleLabel, toggleTooltip), GUILayout.Width(SmallButtonWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                {
                    EditorUtility.SetDirty(host.target);
                    isVarProp.boolValue = !isVarProp.boolValue;
                    nodeVisualsChangedThisFrame = true;
                }
            }

            GUILayout.Space(SmallButtonsMargin / 2); // pad to 2×SmallButtonWidth margin

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Operation row: enum dropdown from descriptor.operationEnumType, with optional per-type filtering.</summary>
        private void DrawOperationParamRow(SerializedProperty entry, DynamicParamDescriptor desc, Type paramType)
        {
            if (desc.operationEnumType == null || !desc.operationEnumType.IsEnum) return;

            int[] availableIndices = desc.getAvailableOpIndices?.Invoke(paramType);
            bool hasFilter = availableIndices != null && availableIndices.Length > 0;

            string[] displayNames = hasFilter
                ? Array.ConvertAll(availableIndices, i => Enum.GetName(desc.operationEnumType, i))
                : Enum.GetNames(desc.operationEnumType);

            SerializedProperty intValueProp = entry.FindPropertyRelative("intValue");
            int currentVal = intValueProp.intValue;

            // Map current value to a display index
            int displayIndex = hasFilter
                ? Array.IndexOf(availableIndices, currentVal)
                : currentVal;
            if (displayIndex < 0 || displayIndex >= displayNames.Length) displayIndex = 0;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(desc.label, GUILayout.Width(FieldLabelWidth));
            GUILayout.FlexibleSpace();
            int newDisplayIndex = EditorGUILayout.Popup(displayIndex, displayNames, GUILayout.Width(InputFieldWidth));
            GUILayout.Space(SmallButtonsMargin);
            EditorGUILayout.EndHorizontal();

            // Map display index back to enum value
            if (newDisplayIndex >= 0 && newDisplayIndex < displayNames.Length)
                intValueProp.intValue = hasFilter ? availableIndices[newDisplayIndex] : newDisplayIndex;
        }

        // ── ScriptableObject constant row (C/V/SO three-way toggle) ──────────────────

        private void DrawSOConstantParamRow(SerializedProperty entry, DynamicParamDescriptor desc,
            Type paramType, bool isArray, bool hasTitle)
        {
            SerializedProperty isVarProp = entry.FindPropertyRelative("isVariable");
            SerializedProperty isConfigProp = entry.FindPropertyRelative("isConfigConstant");
            SerializedProperty configGuidProp = entry.FindPropertyRelative("configSourceGuid");
            SerializedProperty configFieldProp = entry.FindPropertyRelative("configFieldName");

            string currentConfigGuid = configGuidProp.stringValue;
            bool isVar = isVarProp.boolValue;
            bool isConfig = isConfigProp.boolValue || !string.IsNullOrEmpty(currentConfigGuid);

            // Determine the next mode on button click: C → V → SO → C
            string nextLabel;
            if (!isVar && !isConfig)      nextLabel = "V";
            else if (isVar)               nextLabel = "SO";
            else                          nextLabel = "C";

            // ── Row: label + value + toggle button ────────────────────────────────────
            EditorGUILayout.BeginHorizontal();

            string typedLabel = hasTitle ? desc.titleLabel : BuildTypedLabel(desc.label, paramType, desc.allowedTypes);
            EditorGUILayout.LabelField(typedLabel, GUILayout.Width(FieldLabelWidth));
            GUILayout.FlexibleSpace();

            if (isConfig)
            {
                string configName;
                if (!string.IsNullOrEmpty(configFieldProp.stringValue))
                {
                    ScriptableObject so = ConfigFieldPicker.GetConfigSourceByGuid(currentConfigGuid);
                    configName = so != null
                        ? $"{so.name}.{configFieldProp.stringValue}"
                        : $"(missing).{configFieldProp.stringValue}";
                }
                else
                {
                    configName = "(no field selected)";
                }

                // Popup button — shows selected field, click to re-pick.
                // Toggle back to C or V to clear the selection.
                if (GUILayout.Button(configName, EditorStyles.popup, GUILayout.Width(InputFieldWidth)))
                    ConfigFieldPicker.ShowConfigFieldPicker(entry, paramType, configGuidProp, configFieldProp,
                        host.target, () => nodeVisualsChangedThisFrame = true);
            }
            else if (isVar)
            {
                DrawVariableDropdownWithSquadFilter(
                    entry.FindPropertyRelative("variableName"), paramType, isArray);
            }
            else
            {
                DrawConstantFieldForType(entry, paramType, null, showLabel: false);
            }

            // Three-way toggle button
            {
                string toggleTooltip = nextLabel == "V" ? "Switch to variable" :
                                       nextLabel == "SO" ? "Switch to SO constant" : "Switch to constant";
                if (GUILayout.Button(new GUIContent(nextLabel, toggleTooltip),
                    GUILayout.Width(SmallButtonWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                {
                    EditorUtility.SetDirty(host.target);
                    nodeVisualsChangedThisFrame = true;

                    if (!isVar && !isConfig)
                    {
                        isVarProp.boolValue = true;
                        isConfigProp.boolValue = false;
                        configGuidProp.stringValue = "";
                        configFieldProp.stringValue = "";
                    }
                    else if (isVar)
                    {
                        isVarProp.boolValue = false;
                        isConfigProp.boolValue = true;
                        configGuidProp.stringValue = "";
                        configFieldProp.stringValue = "";
                    }
                    else
                    {
                        isVarProp.boolValue = false;
                        isConfigProp.boolValue = false;
                        configGuidProp.stringValue = "";
                        configFieldProp.stringValue = "";
                    }
                }
            }

            GUILayout.Space(SmallButtonsMargin / 2); // pad to 2×SmallButtonWidth margin

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Variable dropdown filtered by type and stride mode (array vs singular).
        /// When isArray is true, only shows stride > 1 variables (squad data).
        /// When isArray is false, only shows stride <= 1 variables (singular).
        /// </summary>
        private void DrawVariableDropdownWithSquadFilter(SerializedProperty variableNameProp, Type expectedType, bool isArray)
        {
            variablePicker.Draw(variableNameProp, expectedType, isArray, new BlackboardVariablePicker.Options
            {
                dropdownWidth = InputFieldWidth,
                onVariablePicked = _ => nodeVisualsChangedThisFrame = true,
            });
        }

        /// <summary>
        /// Draws the primary variable field for dynamic-type nodes with two inline
        /// buttons: a search button (variable by name) and a filter button (change type).
        /// </summary>
        private void DrawDynamicVariableField(SerializedProperty variableNameProp, Type selectedType, bool isArray, string label, Type[] allowedTypes = null, int entryIndex = 0)
        {
            float savedLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 0;

            variablePicker.Draw(variableNameProp, selectedType, isArray, new BlackboardVariablePicker.Options
            {
                label = label,
                labelWidth = FieldLabelWidth,
                dropdownWidth = InputFieldWidth,
                showSearchFilterButtons = true,
                allowedTypes = allowedTypes,
                onVariablePicked = picked => ApplyPickedVariable(entryIndex, picked),
                onTypeFilterPicked = (t, arr) => ApplyPickedTypeFilter(entryIndex, t, arr),
            });

            EditorGUIUtility.labelWidth = savedLabelWidth;
        }

        private void ApplyPickedVariable(int entryIndex, BlackboardVariableBase picked)
        {
            EditorUtility.SetDirty(host.target);
            Type varType = picked.GetValueType();
            SerializedProperty ftProp = fieldEntriesProp.GetArrayElementAtIndex(entryIndex)
                .FindPropertyRelative("fieldTypeName");
            if (ftProp != null && varType != null)
                ftProp.stringValue = varType.AssemblyQualifiedName;
            fieldEntriesProp.GetArrayElementAtIndex(entryIndex)
                .FindPropertyRelative("isArray").boolValue = picked.Stride > 1;
            fieldEntriesProp.serializedObject.ApplyModifiedProperties();
            SyncLinkedEntryTypes();
            nodeVisualsChangedThisFrame = true;
        }

        private void ApplyPickedTypeFilter(int entryIndex, Type varType, bool isArray)
        {
            EditorUtility.SetDirty(host.target);
            SerializedProperty entry = fieldEntriesProp.GetArrayElementAtIndex(entryIndex);
            SerializedProperty ftProp = entry.FindPropertyRelative("fieldTypeName");
            if (ftProp != null)
                ftProp.stringValue = varType?.AssemblyQualifiedName ?? string.Empty;
            entry.FindPropertyRelative("isArray").boolValue = isArray;
            SerializedProperty varProp = entry.FindPropertyRelative("variableName");
            if (varProp != null) varProp.stringValue = string.Empty;
            fieldEntriesProp.serializedObject.ApplyModifiedProperties();
            SyncLinkedEntryTypes();
            nodeVisualsChangedThisFrame = true;
        }

        /// <summary>
        /// Renders a constant field appropriate for the given type,
        /// reading/writing to the NodeFieldEntry's typed value properties.
        /// </summary>
        private void DrawConstantFieldForType(SerializedProperty entryProp, Type fieldType, string label, bool showLabel = true)
        {
            float savedWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 0;
            ConstantValueFieldDrawer.Draw(entryProp, fieldType, label, showLabel, InputFieldWidth);
            if (showLabel)
                GUILayout.Space(SmallButtonWidth * 2f);
            EditorGUIUtility.labelWidth = savedWidth;
        }
    }
}
