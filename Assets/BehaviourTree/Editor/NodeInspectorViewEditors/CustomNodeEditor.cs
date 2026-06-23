using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using BehaviourTree;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using System;
using UnityEditor.Experimental.GraphView;

namespace BehaviourTree.Editor
{
    [CustomEditor(typeof(BehaviourNode), true)]
    public class CustomNodeEditor : UnityEditor.Editor
    {
        private const float SmallButtonWidth = 20f;
        private const float SmallButtonsMargin = SmallButtonWidth * 2f + 6f;
        private const float FieldLabelWidth = 140f;
        private const float InputFieldWidth = 130f;

        public bool nodeNameChangedThisFrame;
        public bool nodeVisualsChangedThisFrame;

        private string lastMethodName;
        private SerializedProperty nodeNameProp;
        private SerializedProperty methodNameProp;
        private SerializedProperty fieldEntriesProp;
        private DynamicParamDescriptor[] currentDescriptors;
        private SerializedProperty childrenProp;
        private SerializedProperty commentProp;
        private SerializedProperty abortTypeProp;
        private AbortType lastAbortType;
        private GUIStyle style;
        private List<string> matchingVars;
        private List<string> matchingVarNames;
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

        private void OnEnable()
        {
            if (target == null) return;
            
            matchingVars = new List<string>();
            matchingVarNames = new List<string>();
            nodeNameProp = serializedObject.FindProperty("nodeName");
            methodNameProp = serializedObject.FindProperty("methodName");
            fieldEntriesProp = serializedObject.FindProperty("fieldEntries");
            childrenProp = serializedObject.FindProperty("children");
            commentProp = serializedObject.FindProperty("comment");
            if (target is CompositeNode) abortTypeProp = serializedObject.FindProperty("abortType");
            if (target is CompositeNode composite) lastAbortType = composite.abortType;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUIUtility.labelWidth = FieldLabelWidth;
            
            if(target is RootNode) return;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(nodeNameProp, new GUIContent("Node Name"));
            nodeNameChangedThisFrame = EditorGUI.EndChangeCheck();            
            
            if(!(target is LeafNode || target is DecoratorNode || target is CompositeNode)) 
            {
                DrawDefaultInspector();
                DrawChildrenDebug();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            // Check if method changed and rebuild field entries
            string selectedMethodName = methodNameProp != null ? methodNameProp.stringValue : null;
            bool methodChanged = selectedMethodName != lastMethodName;
            lastMethodName = selectedMethodName;
            EditorGUI.BeginChangeCheck();

            if (commentProp != null)
            {
                EditorGUILayout.LabelField("Comment", EditorStyles.boldLabel);
                commentProp.stringValue = EditorGUILayout.TextArea(commentProp.stringValue, GUILayout.Height(60));
                EditorGUILayout.Space();
            }

            BuildFieldEntries(selectedMethodName, methodChanged);

            if (abortTypeProp != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Conditional Abort", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(abortTypeProp, new GUIContent("Abort Type"));

                AbortType currentAbort = (AbortType)abortTypeProp.enumValueIndex;
                if (currentAbort != lastAbortType)
                {
                    lastAbortType = currentAbort;
                    nodeVisualsChangedThisFrame = true;
                }

                if (currentAbort != AbortType.None)
                {
                    CompositeNode composite = (CompositeNode)target;
                    if (!NodeWarningEvaluator.HasValidConditionForAbort(composite, currentAbort))
                    {
                        EditorGUILayout.HelpBox(
                            "No reachable Condition node found. Add a Condition node as a " +
                            "descendant for this abort type to take effect.",
                            MessageType.Warning);
                    }
                }
            }

            if(target is CompositeNode)
            {
                DrawChildrenDebug();
            }

            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawChildrenDebug()
        {
            if (childrenProp == null) return;

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(childrenProp, true);
            EditorGUI.EndDisabledGroup();
        }

        private void BuildFieldEntries(string selectedMethodName, bool methodChanged)
        {
            // Dynamic-type nodes: descriptors drive the inspector layout.
            // If GetDynamicParamDescriptors() returns non-null, defer to generic dynamic renderer.
            NodeMethod temp = MethodRegistry.CreateInstance(selectedMethodName);
            if (temp?.GetDynamicParamDescriptors() != null)
            {
                BuildDynamicFieldEntries(selectedMethodName, methodChanged);
                return;
            }

            List<ParamInfo> paramInfoList = null;
            if (!string.IsNullOrEmpty(selectedMethodName))
                paramInfoList = MethodMetadataCache.GetParamsForMethod(selectedMethodName);

            if (paramInfoList != null && paramInfoList.Count > 0)
            {
                if(methodChanged)
                {
                    ResizeFieldEntries(paramInfoList);
                }

                for (int i = 0; i < paramInfoList.Count; i++)
                {
                    ParamInfo info = paramInfoList[i];
                    SerializedProperty entryProp = fieldEntriesProp.GetArrayElementAtIndex(i);
                    SerializedProperty fieldNameProp = entryProp.FindPropertyRelative("fieldName");
                    SerializedProperty isVariableProp = entryProp.FindPropertyRelative("isVariable");
                    SerializedProperty isArrayProp = entryProp.FindPropertyRelative("isArray");
                    SerializedProperty isToggleVariableProp = entryProp.FindPropertyRelative("isToggleVariable");
                    SerializedProperty variableNameProp = entryProp.FindPropertyRelative("variableName");
                    SerializedProperty fieldTypeNameProp = entryProp.FindPropertyRelative("fieldTypeName");

                    // Set static metadata
                    fieldNameProp.stringValue = info.fieldName;
                    fieldTypeNameProp.stringValue = info.fieldType?.AssemblyQualifiedName ?? string.Empty;
                    isArrayProp.boolValue = info.isArray;

                    if (methodChanged && info.isOrderDropdown)
                    {
                        SerializedProperty isOrderProp = entryProp.FindPropertyRelative("isOrderConstant");
                        if (isOrderProp != null) isOrderProp.boolValue = true;
                    }

                    if (info.isHidden)
                    {
                        // Auto-fill variable binding by convention — no UI rendered
                        if (methodChanged)
                        {
                            isVariableProp.boolValue = true;
                            variableNameProp.stringValue = info.autoVariableName;
                        }
                        continue;
                    }

                    EditorGUILayout.BeginVertical("box");

                    string typeLabel;
                    if(info.fieldType != null && info.fieldType.IsEnum)
                    {
                        typeLabel = $"Enum( {info.fieldType.Name} )";
                    }
                    else
                    {
                        typeLabel = info.fieldType != null ? info.fieldType.Name : "Unknown";
                        if (info.isArray) typeLabel += "[]";
                    }
                    
                    string displayName = char.ToUpper(info.fieldName[0]) + info.fieldName.Substring(1);
                    EditorGUILayout.LabelField($"<b>{displayName}</b> : <color=lightblue>{typeLabel}</color>", RichTextLabelStyle);

                    if (!info.isToggleVariable)
                        isVariableProp.boolValue = info.isVariable || info.isArray;

                    // Hide customTickValue when useCustomTick is false (works for both legacy and new)
                    if (info.fieldName == "customTickValue" && i > 0)
                    {
                        SerializedProperty useCustomEntry = fieldEntriesProp.GetArrayElementAtIndex(i - 1);
                        if (!useCustomEntry.FindPropertyRelative("boolValue").boolValue)
                        {
                            EditorGUILayout.EndVertical();
                            continue;
                        }
                    }

                    EditorGUILayout.BeginHorizontal();

                    bool drawVariableField = info.isToggleVariable ? isToggleVariableProp.boolValue : isVariableProp.boolValue;

                    if (drawVariableField)
                    {
                        DrawVariableDropdown(variableNameProp, info.fieldType, info.isArray);
                    }
                    else
                    {
                        DrawConstantField(entryProp, info);
                    }

                    GUILayout.FlexibleSpace();

                    if (info.isToggleVariable)
                    {
                        string toggleLabel = drawVariableField ? "C" : "V";
                        string toggleTooltip = drawVariableField ? "Switch to constant" : "Switch to variable";
                        if (GUILayout.Button(new GUIContent(toggleLabel, toggleTooltip), GUILayout.Width(SmallButtonWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                        {
                            bool newValue = !drawVariableField;
                            entryProp.FindPropertyRelative("isVariable").boolValue = newValue;
                            entryProp.FindPropertyRelative("isToggleVariable").boolValue = newValue;
                            nodeVisualsChangedThisFrame = true;
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();
                }
            }
            else
            {
                // No metadata; clear entries
                fieldEntriesProp.ClearArray();

                if(target is CompositeNode) return;

                string methodDesc = !string.IsNullOrEmpty(selectedMethodName) ? selectedMethodName : "(none)";
                EditorGUILayout.HelpBox($"No schema found for method '{methodDesc}'.", MessageType.Info);
            }
        }

        private void ResizeFieldEntries(List<ParamInfo> paramInfoList)
        {
            while (fieldEntriesProp.arraySize < paramInfoList.Count)
                fieldEntriesProp.InsertArrayElementAtIndex(fieldEntriesProp.arraySize);
            while (fieldEntriesProp.arraySize > paramInfoList.Count)
                fieldEntriesProp.DeleteArrayElementAtIndex(fieldEntriesProp.arraySize - 1);
        }

        private void DrawConstantField(SerializedProperty entryProp, ParamInfo info)
        {
            Type fieldType = info.fieldType;

            if (info.isRoleDropdown)
            {
                DrawRoleDropdown(entryProp);
                return;
            }

            if (info.isOrderDropdown)
            {
                DrawOrderDropdown(entryProp);
                return;
            }

            if (fieldType != null && fieldType.IsEnum)
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
                int currentRaw = prop.intValue;
                Enum current = (Enum)Enum.ToObject(fieldType, currentRaw);
                Enum next = EditorGUILayout.EnumPopup("Value", current, GUILayout.Width(FieldLabelWidth + InputFieldWidth));
                prop.intValue = Convert.ToInt32(next);
            }
            else if (fieldType == typeof(int) || fieldType == typeof(uint))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
                prop.intValue = EditorGUILayout.IntField("Value", prop.intValue, GUILayout.Width(FieldLabelWidth + InputFieldWidth));
            }
            else if (fieldType == typeof(float))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("floatValue");
                prop.floatValue = EditorGUILayout.FloatField("Value", prop.floatValue, GUILayout.Width(FieldLabelWidth + InputFieldWidth));
            }
            else if (fieldType == typeof(bool))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("boolValue");
                prop.boolValue = GUILayout.Toggle(prop.boolValue, prop.boolValue ? "True" : "False", "Button", GUILayout.Width(InputFieldWidth));
            }
            else if (fieldType == typeof(Vector2))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("vector2Value");
                prop.vector2Value = EditorGUILayout.Vector2Field("Value", prop.vector2Value, GUILayout.Width(FieldLabelWidth + InputFieldWidth));
            }
            else if (fieldType == typeof(Vector3))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("vector3Value");
                prop.vector3Value = EditorGUILayout.Vector3Field("Value", prop.vector3Value, GUILayout.Width(FieldLabelWidth + InputFieldWidth));
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                SerializedProperty prop = fieldType == typeof(GameObject)
                    ? entryProp.FindPropertyRelative("gameObjectValue")
                    : entryProp.FindPropertyRelative("transformValue");
                prop.objectReferenceValue = EditorGUILayout.ObjectField("Value", prop.objectReferenceValue, fieldType, true, GUILayout.Width(FieldLabelWidth + InputFieldWidth));
            }
            else
            {
                EditorGUILayout.HelpBox($"Type '{fieldType.Name}' requires a [SharedVar] — use a blackboard variable instead of a constant.", MessageType.Warning);
            }
        }

        private void DrawVariableDropdown(SerializedProperty variableNameProp, Type expectedType, bool isArray = false)
        {
            if (expectedType == null)
            {
                EditorGUILayout.HelpBox("[SharedVar] field type could not be resolved.", MessageType.Warning);
                return;
            }

            // All types are supported as blackboard variables (including enums)
            BlackboardDefinition blackBoardDef = BehaviourTreeEditor.currentBlackboardDef;

            if (blackBoardDef == null)
            {
                EditorGUILayout.HelpBox("No Blackboard Definition assigned.", MessageType.Warning);
                return;
            }

            IReadOnlyList<BlackboardVariableBase> allVars = blackBoardDef.GetAllVariables();
            if (allVars == null || allVars.Count == 0)
            {
                EditorGUILayout.HelpBox("No Blackboard variables added.", MessageType.Warning);
                return;
            }

            // Filter variables whose type matches and stride matches the param kind
            matchingVars.Clear();
            matchingVarNames.Clear();
            for (int variableIndex = 0; variableIndex < allVars.Count; variableIndex++)
            {
                BlackboardVariableBase bv = allVars[variableIndex];
                Type bbType = bv.GetValueType();
                if (bbType == null) continue;
                if (bbType != expectedType) continue;

                if (isArray)
                {
                    if (bv.Stride <= 1) continue;
                    matchingVarNames.Add(bv.Name);
                    matchingVars.Add($"{bv.Name} [{bv.Stride}]");
                }
                else
                {
                    if (bv.Stride > 1) continue;
                    matchingVarNames.Add(bv.Name);
                    matchingVars.Add(bv.Name);
                }
            }

            if (matchingVars.Count == 0)
            {
                EditorGUILayout.HelpBox($"No matching variable of type '{expectedType.Name}' in Blackboard.", MessageType.Info);
                variableNameProp.stringValue = "";
                return;
            }

            // Prepend placeholder so unassigned fields don't auto-pick the first variable
            matchingVars.Insert(0, "Select a variable...");
            matchingVarNames.Insert(0, string.Empty);

            string currentVal = variableNameProp.stringValue;
            int selectedIndex = matchingVarNames.IndexOf(currentVal);
            if (selectedIndex < 0) selectedIndex = 0;

            if (InspectorView.IsRenderingReadOnly)
            {
                string display = currentVal;
                EditorGUILayout.LabelField("Shared Variable", display);

                if (InspectorView.CurrentProxyMappings != null &&
                    InspectorView.CurrentProxyMappings.TryGetValue(currentVal, out string parentVar))
                {
                    EditorGUILayout.LabelField("Mapped To: ", parentVar);
                }

            }
            else
            {
                string previousVal = variableNameProp.stringValue;
                selectedIndex = EditorGUILayout.Popup("Shared Variable", selectedIndex, matchingVars.ToArray(), GUILayout.Width(FieldLabelWidth + InputFieldWidth));
                variableNameProp.stringValue = matchingVarNames[selectedIndex];
                if (variableNameProp.stringValue != previousVal)
                    nodeVisualsChangedThisFrame = true;
            }
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

        private void BuildDynamicFieldEntries(string methodName, bool methodChanged)
        {
            NodeMethod temp = MethodRegistry.CreateInstance(methodName);
            DynamicParamDescriptor[] descriptors = temp?.GetDynamicParamDescriptors();
            currentDescriptors = descriptors;
            if (descriptors == null || descriptors.Length == 0)
            {
                // No descriptors — this is a legacy [SharedVar]-based node, not dynamic
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
            }

            // Sync linked entry types from their source indices
            SyncLinkedEntryTypes();

            if (fieldEntriesProp.arraySize < realCount)
                return;

            // isArray tracks whether the first entry is array-mode (stride > 1)
            bool isArray = fieldEntriesProp.arraySize > 0
                ? fieldEntriesProp.GetArrayElementAtIndex(0).FindPropertyRelative("isArray").boolValue : false;

            EditorGUILayout.BeginVertical("box");

            // ── Generic parameter rows ──
            for (int i = 0; i < realCount; i++)
            {
                DynamicParamDescriptor desc = descriptors[i];
                SerializedProperty entry = fieldEntriesProp.GetArrayElementAtIndex(i);
                Type paramType = ResolveEntryType(i);

                bool hasTitle = !string.IsNullOrEmpty(desc.titleLabel);

                // ── Title header ──
                if (hasTitle)
                {
                    string typeName = paramType != null ? paramType.Name
                        : (desc.allowedTypes != null && desc.allowedTypes.Length == 1 ? desc.allowedTypes[0].Name : null);
                    string header = typeName != null ? $"<b>{desc.titleLabel}</b> : {typeName}" : $"<b>{desc.titleLabel}</b>";
                    EditorGUILayout.LabelField(header, RichTextLabelStyle);
                }

                switch (desc.kind)
                {
                    case DynamicParamKind.Variable:
                        DrawVariableParamRow(entry, desc, paramType, isArray, i, hasTitle);
                        break;
                    case DynamicParamKind.Toggle:
                        DrawToggleParamRow(entry, desc, paramType, isArray, hasTitle);
                        break;
                    case DynamicParamKind.Constant:
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
                        DrawSOConstantParamRow(entry, desc, paramType, isArray, hasTitle);
                        break;
                }
                EditorGUILayout.Space(4f);
            }
            EditorGUILayout.EndVertical();
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
            entry.FindPropertyRelative("fieldName").stringValue = desc.label.ToLowerInvariant().Replace(" ", "");
            entry.FindPropertyRelative("isVariable").boolValue =
                desc.kind == DynamicParamKind.Variable || desc.kind == DynamicParamKind.Toggle;
            entry.FindPropertyRelative("isConfigConstant").boolValue =
                desc.kind == DynamicParamKind.ScriptableObjectConstant;
            // SO constants start with empty selection — user picks a field via the "..." button
            entry.FindPropertyRelative("configSourceGuid").stringValue = "";
            entry.FindPropertyRelative("configFieldName").stringValue = "";
            if (desc.kind == DynamicParamKind.Operation && desc.operationEnumType != null)
                entry.FindPropertyRelative("fieldTypeName").stringValue = typeof(int).AssemblyQualifiedName;
            // Set fieldTypeName from allowedTypes for Constant, Variable, and Toggle kinds
            // so each field carries its own type independently.
            if (desc.allowedTypes != null && desc.allowedTypes.Length > 0)
                entry.FindPropertyRelative("fieldTypeName").stringValue = desc.allowedTypes[0].AssemblyQualifiedName;
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
                if (ftProp != null && ftProp.stringValue != srcTypeName)
                    ftProp.stringValue = srcTypeName;
            }
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
                return $"{baseLabel} ({resolvedType.Name})";
            if (allowedTypes != null && allowedTypes.Length == 1)
                return $"{baseLabel} ({allowedTypes[0].Name})";
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
                    EditorUtility.SetDirty(target);
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
                    ScriptableObject so = GetConfigSourceByGuid(currentConfigGuid);
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
                    ShowConfigFieldPicker(entry, paramType, configGuidProp, configFieldProp);
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
                    EditorUtility.SetDirty(target);
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
        /// Opens a GenericMenu listing fields from every SO in the tree's
        /// <see cref="BehaviourTreeAssetBase.availableConfigs"/> whose type
        /// is compatible with the expected parameter type.
        /// Selecting a field stores the SO reference (GUID + field name) as metadata;
        /// the actual value is resolved at bake time by TreeBaker.
        /// </summary>
        private void ShowConfigFieldPicker(SerializedProperty entry, Type paramType,
            SerializedProperty configGuidProp, SerializedProperty configFieldProp)
        {
            var treeAsset = BehaviourTreeEditor.currentTree;
            if (treeAsset == null) return;

            GenericMenu menu = new GenericMenu();

            foreach (ScriptableObject so in treeAsset.availableConfigs)
            {
                if (so == null) continue;

                string soPath = AssetDatabase.GetAssetPath(so);
                string soGuid = AssetDatabase.AssetPathToGUID(soPath);
                if (string.IsNullOrEmpty(soGuid)) continue;

                System.Reflection.FieldInfo[] fields = so.GetType()
                    .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                System.Reflection.PropertyInfo[] properties = so.GetType()
                    .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                foreach (var f in fields)
                {
                    if (IsTypeCompatible(f.FieldType, paramType))
                    {
                        string itemPath = $"{so.name}/{f.Name}  ({f.FieldType.Name})";
                        string capturedGuid = soGuid;
                        string capturedField = f.Name;
                        ScriptableObject capturedSO = so;
                        menu.AddItem(new GUIContent(itemPath), false, () =>
                        {
                            ApplySOFieldSelection(entry, configGuidProp, configFieldProp,
                                capturedSO, capturedGuid, capturedField);
                        });
                    }
                }

                foreach (var p in properties)
                {
                    if (p.CanRead && IsTypeCompatible(p.PropertyType, paramType))
                    {
                        string itemPath = $"{so.name}/{p.Name}  ({p.PropertyType.Name})";
                        string capturedGuid = soGuid;
                        string capturedField = p.Name;
                        ScriptableObject capturedSO = so;
                        menu.AddItem(new GUIContent(itemPath), false, () =>
                        {
                            ApplySOFieldSelection(entry, configGuidProp, configFieldProp,
                                capturedSO, capturedGuid, capturedField);
                        });
                    }
                }
            }

            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No compatible fields in config sources"));

            menu.ShowAsContext();
        }

        /// <summary>
        /// Stores the SO reference metadata (GUID + field name) on the entry so the
        /// popup button can display which field is selected. The actual value is
        /// resolved at bake time by <see cref="TreeBaker.ResolveSOConstantEntry"/>
        /// which re-reads the current SO field value — so SO changes are always picked up.
        /// </summary>
        private void ApplySOFieldSelection(SerializedProperty entry,
            SerializedProperty configGuidProp, SerializedProperty configFieldProp,
            ScriptableObject so, string guid, string fieldName)
        {
            SerializedProperty isConfig = entry.FindPropertyRelative("isConfigConstant");
            if (isConfig != null) isConfig.boolValue = true;
            configGuidProp.stringValue = guid;
            configFieldProp.stringValue = fieldName;

            entry.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            nodeVisualsChangedThisFrame = true;
        }

        private static bool IsTypeCompatible(Type sourceType, Type targetType)
        {
            if (targetType == null) return true;
            return targetType.IsAssignableFrom(sourceType);
        }

        private ScriptableObject GetConfigSourceByGuid(string guid)
        {
            var treeAsset = BehaviourTreeEditor.currentTree;
            if (treeAsset == null) return null;
            foreach (var so in treeAsset.availableConfigs)
            {
                if (so == null) continue;
                string soPath = AssetDatabase.GetAssetPath(so);
                string soGuid = AssetDatabase.AssetPathToGUID(soPath);
                if (soGuid == guid) return so;
            }
            return null;
        }

        /// <summary>
        /// Variable dropdown filtered by type and stride mode (array vs singular).
        /// When isArray is true, only shows stride > 1 variables (squad data).
        /// When isArray is false, only shows stride <= 1 variables (singular).
        /// </summary>
        private void DrawVariableDropdownWithSquadFilter(SerializedProperty variableNameProp, Type expectedType, bool isArray)
        {
            if (expectedType == null)
            {
                EditorGUILayout.HelpBox("Select a variable -->", MessageType.Info);
                return;
            }

            BlackboardDefinition blackBoardDef = BehaviourTreeEditor.currentBlackboardDef;
            if (blackBoardDef == null)
            {
                EditorGUILayout.HelpBox("No Blackboard Definition assigned.", MessageType.Warning);
                return;
            }

            IReadOnlyList<BlackboardVariableBase> allVars = blackBoardDef.GetAllVariables();
            if (allVars == null || allVars.Count == 0)
            {
                EditorGUILayout.HelpBox("No Blackboard variables added.", MessageType.Warning);
                return;
            }

            matchingVars.Clear();
            matchingVarNames.Clear();
            for (int variableIndex = 0; variableIndex < allVars.Count; variableIndex++)
            {
                BlackboardVariableBase bv = allVars[variableIndex];
                Type bbType = bv.GetValueType();
                if (bbType == null) continue;
                if (bbType != expectedType) continue;

                // Filter by stride: array mode shows stride > 1, singular shows stride <= 1
                if (isArray)
                {
                    if (bv.Stride <= 1) continue;
                    matchingVars.Add($"{bv.Name} [{bv.Stride}]");
                }
                else
                {
                    if (bv.Stride > 1) continue;
                    matchingVars.Add(bv.Name);
                }
                matchingVarNames.Add(bv.Name);
            }

            if (matchingVars.Count == 0)
            {
                string modeLabel = isArray ? "array" : "singular";
                EditorGUILayout.HelpBox(
                    $"No {modeLabel} variable of type '{expectedType.Name}' in Blackboard. ",
                    MessageType.Info);
                variableNameProp.stringValue = "";
                return;
            }

            matchingVars.Insert(0, "Select a variable...");
            matchingVarNames.Insert(0, string.Empty);

            string currentVal = variableNameProp.stringValue;
            int selectedIndex = matchingVarNames.IndexOf(currentVal);
            if (selectedIndex < 0) selectedIndex = 0;

            if (InspectorView.IsRenderingReadOnly)
            {
                EditorGUILayout.LabelField(currentVal);
            }
            else
            {
                string previousVal = variableNameProp.stringValue;
                selectedIndex = EditorGUILayout.Popup(selectedIndex, matchingVars.ToArray(), GUILayout.Width(InputFieldWidth));
                variableNameProp.stringValue = matchingVarNames[selectedIndex];
                if (variableNameProp.stringValue != previousVal)
                    nodeVisualsChangedThisFrame = true;
            }
        }

        /// <summary>
        /// Draws the primary variable field for dynamic-type nodes with two inline
        /// buttons: a search button (variable by name) and a filter button (change type).
        /// </summary>
        private void DrawDynamicVariableField(SerializedProperty variableNameProp, Type selectedType, bool isArray, string label, Type[] allowedTypes = null, int entryIndex = 0)
        {
            if (InspectorView.IsRenderingReadOnly)
            {
                EditorGUILayout.LabelField(label, variableNameProp.stringValue);
                return;
            }

            BlackboardDefinition blackboardDef = BehaviourTreeEditor.currentBlackboardDef;
            bool squadCtx = BehaviourTreeEditor.currentTree is CommanderTreeAsset;

            float savedLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 0;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(FieldLabelWidth));
            GUILayout.FlexibleSpace();

            if (blackboardDef != null)
            {
                // ── Compact dropdown (no label) ──
                IReadOnlyList<BlackboardVariableBase> allVars = blackboardDef.GetAllVariables();
                Type[] filterTypes = allowedTypes != null && allowedTypes.Length > 0
                    ? allowedTypes
                    : (selectedType != null ? new[] { selectedType } : null);

                matchingVars.Clear();
                matchingVarNames.Clear();
                for (int variableIndex = 0; variableIndex < allVars.Count; variableIndex++)
                {
                    BlackboardVariableBase bv = allVars[variableIndex];
                    Type bbType = bv.GetValueType();
                    if (bbType == null) continue;
                    if (filterTypes != null)
                    {
                        bool typeMatch = false;
                        for (int ft = 0; ft < filterTypes.Length; ft++)
                        {
                            if (filterTypes[ft] == bbType) { typeMatch = true; break; }
                        }
                        if (!typeMatch) continue;
                    }
                    else if (selectedType != null && bbType != selectedType)
                    {
                        continue;
                    }
                    if (isArray) { if (bv.Stride <= 1) continue; matchingVars.Add($"{bv.Name} [{bv.Stride}]"); }
                    else { if (bv.Stride > 1) continue; matchingVars.Add(bv.Name); }
                    matchingVarNames.Add(bv.Name);
                }

                if (matchingVars.Count > 0)
                {
                    matchingVars.Insert(0, "Select a variable...");
                    matchingVarNames.Insert(0, string.Empty);
                    string currentVal = variableNameProp.stringValue;
                    int selectedIndex = matchingVarNames.IndexOf(currentVal);
                    if (selectedIndex < 0) selectedIndex = 0;
                    string previousVal = currentVal;
                    selectedIndex = EditorGUILayout.Popup(selectedIndex, matchingVars.ToArray(), GUILayout.Width(InputFieldWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    variableNameProp.stringValue = matchingVarNames[selectedIndex];
                    if (variableNameProp.stringValue != previousVal)
                        nodeVisualsChangedThisFrame = true;
                }
                else
                {
                    EditorGUILayout.LabelField("(no matches)", GUILayout.Width(InputFieldWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
            }
            else
            {
                EditorGUILayout.LabelField("[ Pick a variable/type --> ]", GUILayout.Width(InputFieldWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            // ── S + F buttons ──
            Rect searchButtonRect = EditorGUILayout.GetControlRect(GUILayout.Width(SmallButtonWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUI.Button(searchButtonRect, new GUIContent("S", "Search for a variable by name")))
            {
                GUI.FocusControl(null);
                VariableSearchPopup popup = new VariableSearchPopup(blackboardDef, allowedTypes,
                    (BlackboardVariableBase chosen, bool chosenIsArray) =>
                    {
                        if (chosen == null) return;
                        EditorUtility.SetDirty(target);
                        // Auto-configure type and array mode from chosen variable
                        Type varType = chosen.GetValueType();
                        string newTypeName = varType?.AssemblyQualifiedName ?? string.Empty;
                        SerializedProperty ftProp = fieldEntriesProp.GetArrayElementAtIndex(entryIndex)
                            .FindPropertyRelative("fieldTypeName");
                        if (ftProp != null) ftProp.stringValue = newTypeName;
                        fieldEntriesProp.GetArrayElementAtIndex(entryIndex)
                            .FindPropertyRelative("isArray").boolValue = chosen.Stride > 1;
                        variableNameProp.stringValue = chosen.Name;
                        fieldEntriesProp.serializedObject.ApplyModifiedProperties();
                        SyncLinkedEntryTypes();
                        nodeVisualsChangedThisFrame = true;
                    }, isSquadContext: squadCtx);
                PopupWindow.Show(
                    new Rect(searchButtonRect.x, searchButtonRect.yMax, 0, 0),
                    popup);
            }

            // ── Filter (type) button ──
            Rect filterButtonRect = EditorGUILayout.GetControlRect(GUILayout.Width(SmallButtonWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUI.Button(filterButtonRect, new GUIContent("F", "Change the variable type filter")))
            {
                GUI.FocusControl(null);
                VariableTypeSearchPopup popup = new VariableTypeSearchPopup(
                    (Type varType, bool varIsArray, int stride, bool isSquadData) =>
                    {
                        EditorUtility.SetDirty(target);
                        string newTypeName = varType?.AssemblyQualifiedName ?? string.Empty;
                        SerializedProperty ftProp = fieldEntriesProp.GetArrayElementAtIndex(entryIndex)
                            .FindPropertyRelative("fieldTypeName");
                        if (ftProp != null) ftProp.stringValue = newTypeName;
                        fieldEntriesProp.GetArrayElementAtIndex(entryIndex)
                            .FindPropertyRelative("isArray").boolValue = varIsArray || isSquadData;
                        SerializedProperty varProp = fieldEntriesProp.GetArrayElementAtIndex(entryIndex)
                            .FindPropertyRelative("variableName");
                        if (varProp != null) varProp.stringValue = string.Empty;
                        fieldEntriesProp.serializedObject.ApplyModifiedProperties();
                        SyncLinkedEntryTypes();
                        nodeVisualsChangedThisFrame = true;
                    }, allowedTypes, squadCtx);
                PopupWindow.Show(
                    new Rect(filterButtonRect.x, filterButtonRect.yMax, 0, 0),
                    popup);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUIUtility.labelWidth = savedLabelWidth;
        }

        /// <summary>
        /// Renders a constant field appropriate for the given type,
        /// reading/writing to the NodeFieldEntry's typed value properties.
        /// </summary>
        private void DrawConstantFieldForType(SerializedProperty entryProp, Type fieldType, string label, bool showLabel = true)
        {
            if (fieldType == null)
            {
                EditorGUILayout.HelpBox("No type selected.", MessageType.Warning);
                return;
            }

            float savedWidth = EditorGUIUtility.labelWidth;
            if (showLabel)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUIUtility.labelWidth = 0;
                EditorGUILayout.LabelField(label, GUILayout.Width(FieldLabelWidth));
                GUILayout.FlexibleSpace();
            }

            if (fieldType.IsEnum)
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
                int currentRaw = prop.intValue;
                Enum current = (Enum)Enum.ToObject(fieldType, currentRaw);
                Enum next = EditorGUILayout.EnumPopup(current, GUILayout.Width(InputFieldWidth));
                prop.intValue = Convert.ToInt32(next);
            }
            else if (fieldType == typeof(int) || fieldType == typeof(uint))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
                prop.intValue = EditorGUILayout.IntField(prop.intValue, GUILayout.Width(InputFieldWidth));
            }
            else if (fieldType == typeof(float))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("floatValue");
                prop.floatValue = EditorGUILayout.FloatField(prop.floatValue, GUILayout.Width(InputFieldWidth));
            }
            else if (fieldType == typeof(bool))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("boolValue");
                prop.boolValue = GUILayout.Toggle(prop.boolValue, prop.boolValue ? "True" : "False", "Button", GUILayout.Width(InputFieldWidth));
            }
            else if (fieldType == typeof(Vector2))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("vector2Value");
                prop.vector2Value = EditorGUILayout.Vector2Field(GUIContent.none, prop.vector2Value, GUILayout.Width(InputFieldWidth));
            }
            else if (fieldType == typeof(Vector3))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("vector3Value");
                prop.vector3Value = EditorGUILayout.Vector3Field(GUIContent.none, prop.vector3Value, GUILayout.Width(InputFieldWidth));
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                SerializedProperty prop = fieldType == typeof(GameObject)
                    ? entryProp.FindPropertyRelative("gameObjectValue")
                    : entryProp.FindPropertyRelative("transformValue");
                prop.objectReferenceValue = EditorGUILayout.ObjectField(prop.objectReferenceValue, fieldType, true, GUILayout.Width(InputFieldWidth));
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Type '{fieldType.Name}' is not supported for constant values. Use a variable source instead.",
                    MessageType.Warning);
            }

            if (showLabel)
            {
                GUILayout.Space(SmallButtonWidth * 2f);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUIUtility.labelWidth = savedWidth;
        }

        /// <summary>
        /// Returns the subset of VariableCompareOp names applicable to the given type.
        /// </summary>
        private static string[] GetCompareOpNames(Type fieldType)
        {
            if (fieldType == null)
                return new[] { "Equal", "NotEqual" };

            if (fieldType == typeof(Vector2) || fieldType == typeof(Vector3))
                return new[] { "Equal", "NotEqual", "Mag <", "Mag <=", "Mag >", "Mag >=" };

            if (fieldType == typeof(int) || fieldType == typeof(float))
                return new[] { "Equal", "NotEqual", "Less", "LessOrEqual", "Greater", "GreaterOrEqual" };

            // bool, enum, GameObject, Transform, etc.
            return new[] { "Equal", "NotEqual" };
        }

        private static bool HasValidConditionForAbort(CompositeNode composite, AbortType abortType)
        {
            return NodeWarningEvaluator.HasValidConditionForAbort(composite, abortType);
        }
    }
}
