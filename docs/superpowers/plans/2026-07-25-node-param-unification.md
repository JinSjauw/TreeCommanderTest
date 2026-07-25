# Node Parameter System Unification — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Unify the `[SharedVar]` attribute path and the `DynamicParamDescriptor` path into one schema + one renderer, eliminating UI duplication and shrinking `CustomNodeEditor.cs` (1.3k LOC) into focused classes.

**Architecture:** The descriptor becomes the canonical param schema. `[SharedVar]` fields are *projected* into descriptors by reflection (zero boilerplate retained); dynamic methods keep overriding `GetDynamicParamDescriptors()` but get a fluent `Params` builder (less boilerplate). The editor gets exactly one render path. Serialization (`NodeFieldEntry[] fieldEntries`) and baking (`TreeBaker`) are untouched.

**Tech Stack:** Unity (C#), IMGUI editor, NUnit EditMode tests via Unity Test Runner.

---

## Context for the Engineer (read first)

Key facts that constrain the design — learned from auditing the codebase:

1. **Entry storage is already unified.** Both paths write the same `NodeFieldEntry[]` (`Assets/BehaviourTree/Core/Fields/NodeFieldEntry.cs`). `TreeBaker` only reads `fieldEntries` — no baker changes needed.
2. **`NodeMethod.GetDynamicParamDescriptors()` default MUST keep returning `null`.** `TreeEvaluator.cs:73,82` and `MethodRegistry.cs:69` call `ParameterCount` at runtime to decide whether to invoke `DeserializeParameters`. If the default started returning projected descriptors, legacy nodes would suddenly report `ParameterCount > 0` — a runtime behavior change. Therefore the projection is applied **editor-side only**, via a helper: `override ?? projection`.
3. **`DynamicParamDescriptor.index` is dead weight.** Set by every dynamic method, consumed nowhere. The builder auto-assigns it.
4. **`entry.fieldName` parity matters.** The legacy path stores the exact reflected field name (e.g. `"orderValue"`); `InitNewEntryFromDescriptor` currently derives it from the label (`"ordervalue"`). Field names feed `GetSlotByName()` at runtime. The projection must carry the real field name — hence a new `desc.fieldName`.
5. **`[SharedArray]` is currently unused** by any method (docs only). Projection supports it, no fixture exists.
6. **`customTickValue` visibility** is a hardcoded editor hack (`CustomNodeEditor.cs:223-231`): hidden when the *previous* entry's `boolValue` is false. Only `Cooldown` (`TimeMethods.cs`) uses it. Projection converts it to a declared `visibilityDependsOnIndex`.
7. **No test infrastructure exists.** Phase 2 creates the EditMode test assembly. Verification otherwise = Unity console compile + manual inspector checks against the fixture matrix below.
8. `MethodMetadataCache` is also consumed by `TooltipRegistry.cs:857` (needs `fieldName` per param only).

### Fixture matrix (used for manual verification in every phase)

| Fixture | File | Exercises |
|---|---|---|
| `WaitSeconds` | `Assets/BehaviourTree/Runtime/Methods/TimeMethods.cs` | Plain constant |
| `Cooldown` | same | `[SharedVar]` + `customTickValue` visibility |
| `SendOrderMethod` | `Assets/BehaviourTree/Runtime/Methods/SendOrderMethod.cs` | Hidden auto-bind + order dropdown toggle |
| `ForEachRoleMethod` | `Assets/BehaviourTree/Runtime/Methods/ForEachRoleMethod.cs` | Role dropdown |
| `SetVariable` | `Assets/BehaviourTree/Runtime/Methods/VariableMethods.cs` | Variable + Toggle + type sync |
| `CompareVariable` | same file (~line 180-210) | Operation + per-type op filter |
| `SquadReduceMethod` | `Assets/BehaviourTree/Runtime/Methods/SquadReduceMethod.cs` | Operation + element-type sync |
| `SetNavAgentSpeed` | `Assets/BehaviourTree/Runtime/Methods/SetNavAgentSpeed.cs` | SOConstant (C/V/SO three-way) |
| `Enemy_Detected` | `Assets/Scripts/Enemy/EnemyBTExtensions/Enemy_Detected.cs` | SOConstant + Operation |

**Manual check per fixture:** select a node using that method in the BT editor → rows render, types/colors correct, toggle buttons work, variable dropdown filters correctly, values persist after selecting a different node and coming back.

---

## File Structure

**Create (Phase 1):**
- `Assets/BehaviourTree/Editor/NodeInspectorViewEditors/BlackboardVariablePicker.cs` — the ONE variable dropdown (filter + popup + S/F buttons + read-only mode)
- `Assets/BehaviourTree/Editor/NodeInspectorViewEditors/ConstantValueFieldDrawer.cs` — the ONE constant-value type switch

**Create (Phase 2):**
- `Assets/BehaviourTree/Core/Params.cs` — fluent descriptor factory
- `Assets/BehaviourTree/Tests/Editor/BehaviourTree.Tests.Editor.asmdef`
- `Assets/BehaviourTree/Tests/Editor/ParamsTests.cs`

**Modify (Phase 2):**
- `Assets/BehaviourTree/Core/DynamicParamDescriptor.cs` — parity fields + `ConstantEditorHint` enum

**Create (Phase 3):**
- `Assets/BehaviourTree/Runtime/ParamSchemaReflection.cs` — cached attribute→descriptor projection (namespace `BehaviourTree.Core`, matching `NodeMethod.cs`)
- `Assets/BehaviourTree/Editor/Registries/NodeParamSchema.cs` — editor helper: `override ?? projection`
- `Assets/BehaviourTree/Tests/Editor/ParamSchemaReflectionTests.cs`

**Modify (Phase 3):**
- `Assets/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs` — single render entry; legacy behaviors ported into descriptor path

**Modify (Phase 4):**
- `Assets/BehaviourTree/Editor/Registries/TooltipRegistry.cs` — migrate off `ParamInfo`
- `Assets/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs` — delete legacy path; split into shell + `NodeParamSectionRenderer.cs` + `ConfigFieldPicker.cs` (both new, same folder)

**Delete (Phase 4):**
- `Assets/BehaviourTree/Editor/Registries/MethodMetadataCache.cs` (contains `ParamInfo`)

---

## Phase 1 — Unified UI primitives (pure refactor, no behavior change)

Collapses 3 variable dropdowns → 1 and 2 constant switches → 1. ~400 lines removed from `CustomNodeEditor.cs`.

### Task 1.1: Create `BlackboardVariablePicker`

**Files:**
- Create: `Assets/BehaviourTree/Editor/NodeInspectorViewEditors/BlackboardVariablePicker.cs`

- [ ] **Step 1: Write the picker**

Consolidates the logic of `DrawVariableDropdown` (CustomNodeEditor.cs:348-431), `DrawVariableDropdownWithSquadFilter` (:1034-1108) and `DrawDynamicVariableField` (:1114-1266). Deliberate unifications: help-box copy becomes uniform; scalar-mode squad-data filtering follows the dynamic-row rule (`Stride > 1 && !isSquadData` excluded).

```csharp
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
```

- [ ] **Step 2: Verify compile** (Unity → Console clean)

- [ ] **Step 3: Commit**

```bash
git add Assets/BehaviourTree/Editor/NodeInspectorViewEditors/BlackboardVariablePicker.cs
git commit -m "refactor(editor): add unified BlackboardVariablePicker"
```

### Task 1.2: Rewire the three variable dropdowns to the picker

**Files:**
- Modify: `Assets/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs`

- [ ] **Step 1: Add picker field; remove `matchingVars`/`matchingVarNames` fields and their `OnEnable` init (lines 33-34, 51-52)**

```csharp
private readonly BlackboardVariablePicker variablePicker = new BlackboardVariablePicker();
```

- [ ] **Step 2: Replace `DrawVariableDropdown` body (lines 348-431)**

```csharp
private void DrawVariableDropdown(SerializedProperty variableNameProp, Type expectedType, bool isArray = false)
{
    variablePicker.Draw(variableNameProp, expectedType, isArray, new BlackboardVariablePicker.Options
    {
        label = "Shared Variable",
        labelWidth = FieldLabelWidth,
        dropdownWidth = FieldLabelWidth + InputFieldWidth,
        showProxyMapping = true,
    });
}
```

Note: the legacy variant drew the popup *with* a prefix label inside `EditorGUILayout.Popup("Shared Variable", ...)`; the picker draws label + popup as separate controls — visually equivalent.

- [ ] **Step 3: Replace `DrawVariableDropdownWithSquadFilter` body (lines 1034-1108)**

```csharp
private void DrawVariableDropdownWithSquadFilter(SerializedProperty variableNameProp, Type expectedType, bool isArray)
{
    variablePicker.Draw(variableNameProp, expectedType, isArray, new BlackboardVariablePicker.Options
    {
        dropdownWidth = InputFieldWidth,
        onVariablePicked = _ => nodeVisualsChangedThisFrame = true,
    });
}
```

- [ ] **Step 4: Replace `DrawDynamicVariableField` body (lines 1114-1266)**

```csharp
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
    EditorUtility.SetDirty(target);
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
    EditorUtility.SetDirty(target);
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
```

- [ ] **Step 5: Verify compile + spot-checks:** `SendOrderMethod` (legacy labeled dropdown), `SetVariable` (S/F buttons + auto type config on pick), `SetNavAgentSpeed` (V-mode dropdown). Behavior must match pre-refactor.

- [ ] **Step 6: Commit**

```bash
git add Assets/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs
git commit -m "refactor(editor): route all variable dropdowns through BlackboardVariablePicker"
```

### Task 1.3: Create `ConstantValueFieldDrawer` and rewire both constant switches

**Files:**
- Create: `Assets/BehaviourTree/Editor/NodeInspectorViewEditors/ConstantValueFieldDrawer.cs`
- Modify: `Assets/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs`

- [ ] **Step 1: Write the drawer**

Merges `DrawConstantField` (:286-346) and `DrawConstantFieldForType` (:1272-1342). Role/order dropdown branches stay in the caller for now (Phase 3 routes them via descriptor hint).

```csharp
using System;
using UnityEditor;
using UnityEngine;
using BehaviourTree.Core;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// The single constant-value field renderer. Writes into a NodeFieldEntry's
    /// typed value properties (intValue/floatValue/boolValue/vector*/object refs).
    /// </summary>
    public static class ConstantValueFieldDrawer
    {
        public static void Draw(SerializedProperty entryProp, Type fieldType,
            string label, bool showLabel, float fieldWidth)
        {
            if (fieldType == null)
            {
                EditorGUILayout.HelpBox("No type selected.", MessageType.Warning);
                return;
            }

            if (showLabel)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(label);
            }

            if (fieldType.IsEnum)
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
                Enum current = (Enum)Enum.ToObject(fieldType, prop.intValue);
                Enum next = EditorGUILayout.EnumPopup(current, GUILayout.Width(fieldWidth));
                prop.intValue = Convert.ToInt32(next);
            }
            else if (fieldType == typeof(int) || fieldType == typeof(uint))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
                prop.intValue = EditorGUILayout.IntField(prop.intValue, GUILayout.Width(fieldWidth));
            }
            else if (fieldType == typeof(float))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("floatValue");
                prop.floatValue = EditorGUILayout.FloatField(prop.floatValue, GUILayout.Width(fieldWidth));
            }
            else if (fieldType == typeof(bool))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("boolValue");
                prop.boolValue = GUILayout.Toggle(prop.boolValue, prop.boolValue ? "True" : "False",
                    "Button", GUILayout.Width(fieldWidth));
            }
            else if (fieldType == typeof(Vector2))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("vector2Value");
                prop.vector2Value = EditorGUILayout.Vector2Field(GUIContent.none, prop.vector2Value,
                    GUILayout.Width(fieldWidth));
            }
            else if (fieldType == typeof(Vector3))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("vector3Value");
                prop.vector3Value = EditorGUILayout.Vector3Field(GUIContent.none, prop.vector3Value,
                    GUILayout.Width(fieldWidth));
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                SerializedProperty prop = fieldType == typeof(GameObject)
                    ? entryProp.FindPropertyRelative("gameObjectValue")
                    : entryProp.FindPropertyRelative("transformValue");
                prop.objectReferenceValue = EditorGUILayout.ObjectField(
                    prop.objectReferenceValue, fieldType, true, GUILayout.Width(fieldWidth));
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Type '{TypeDisplayRegistry.instance.GetDisplayName(fieldType)}' is not supported " +
                    "for constant values. Use a variable source instead.",
                    MessageType.Warning);
            }

            if (showLabel)
                EditorGUILayout.EndHorizontal();
        }
    }
}
```

- [ ] **Step 2: Rewire `DrawConstantField` (CustomNodeEditor.cs:286-346)** — keep role/order early-returns, replace the rest:

```csharp
private void DrawConstantField(SerializedProperty entryProp, ParamInfo info)
{
    if (info.isRoleDropdown) { DrawRoleDropdown(entryProp); return; }
    if (info.isOrderDropdown) { DrawOrderDropdown(entryProp); return; }
    ConstantValueFieldDrawer.Draw(entryProp, info.fieldType, "Value",
        showLabel: true, fieldWidth: FieldLabelWidth + InputFieldWidth);
}
```

- [ ] **Step 3: Rewire `DrawConstantFieldForType` (:1272-1342)**

```csharp
private void DrawConstantFieldForType(SerializedProperty entryProp, Type fieldType, string label, bool showLabel = true)
{
    float savedWidth = EditorGUIUtility.labelWidth;
    EditorGUIUtility.labelWidth = 0;
    ConstantValueFieldDrawer.Draw(entryProp, fieldType, label, showLabel, InputFieldWidth);
    if (showLabel)
        GUILayout.Space(SmallButtonWidth * 2f);
    EditorGUIUtility.labelWidth = savedWidth;
}
```

- [ ] **Step 4: Verify compile + spot-checks:** `Cooldown` (float constant + bool), `SendOrderMethod` (order dropdown unchanged), `SetVariable` Value row in C mode.

- [ ] **Step 5: Commit**

```bash
git add Assets/BehaviourTree/Editor/NodeInspectorViewEditors/ConstantValueFieldDrawer.cs Assets/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs
git commit -m "refactor(editor): merge constant value switches into ConstantValueFieldDrawer"
```

### Task 1.4: Fetch the `NodeMethod` instance once per repaint

**Files:**
- Modify: `Assets/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs:142-151` and `:544-548`

`MethodRegistry.CreateInstance` currently runs twice per repaint (lines 146 and 546).

- [ ] **Step 1: Hoist instance + descriptors into `BuildFieldEntries`**

Change the opening of `BuildFieldEntries` (lines 142-151) to:

```csharp
private void BuildFieldEntries(string selectedMethodName, bool methodChanged)
{
    NodeMethod temp = MethodRegistry.CreateInstance(selectedMethodName);
    DynamicParamDescriptor[] descriptors = temp?.GetDynamicParamDescriptors();
    if (descriptors != null)
    {
        BuildDynamicFieldEntries(descriptors, methodChanged);
        return;
    }
    // ... legacy ParamInfo path continues unchanged below (deleted in Phase 3)
```

Change the signature and opening of `BuildDynamicFieldEntries` (lines 544-548) to:

```csharp
private void BuildDynamicFieldEntries(DynamicParamDescriptor[] descriptors, bool methodChanged)
{
    currentDescriptors = descriptors;
    if (descriptors == null || descriptors.Length == 0)
    {
        if (methodChanged) fieldEntriesProp.ClearArray();
        return;
    }
```

- [ ] **Step 2: Verify compile + `SetVariable` renders**

- [ ] **Step 3: Commit**

```bash
git add Assets/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs
git commit -m "perf(editor): create NodeMethod instance once per repaint"
```

**Phase 1 gate:** full fixture matrix spot-check passes; `CustomNodeEditor.cs` down ~400 lines.

---

## Phase 2 — Descriptor parity + fluent `Params` builder (Core changes, test-first)

### Task 2.1: EditMode test assembly

**Files:**
- Create: `Assets/BehaviourTree/Tests/Editor/BehaviourTree.Tests.Editor.asmdef`
- Create: `Assets/BehaviourTree/Tests/Editor/SanityTests.cs`

- [ ] **Step 1: Verify Test Framework availability** — Unity menu: Window → Test Runner. If missing: Package Manager → Unity Registry → install "Test Framework".

- [ ] **Step 2: Write the asmdef**

```json
{
    "name": "BehaviourTree.Tests.Editor",
    "rootNamespace": "BehaviourTree.Tests",
    "references": [
        "BehaviourTree.Core",
        "BehaviourTree.Runtime"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "precompiledReferences": ["nunit.framework.dll"],
    "overrideReferences": true,
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

- [ ] **Step 3: Write a sanity test**

```csharp
using NUnit.Framework;

namespace BehaviourTree.Tests
{
    public class SanityTests
    {
        [Test]
        public void TestAssembly_Runs()
        {
            Assert.Pass();
        }
    }
}
```

- [ ] **Step 4: Run** — Test Runner → EditMode → Run All. Expected: 1 passed.

- [ ] **Step 5: Commit**

```bash
git add Assets/BehaviourTree/Tests
git commit -m "test: add EditMode test assembly"
```

### Task 2.2: Extend `DynamicParamDescriptor` with attribute-parity fields

**Files:**
- Modify: `Assets/BehaviourTree/Core/DynamicParamDescriptor.cs`

- [ ] **Step 1: Add fields + hint enum** (replace file contents)

```csharp
using System;

namespace BehaviourTree.Core
{
    public enum DynamicParamKind
    {
        Variable,
        Toggle,
        Constant,
        Operation,
        /// <summary>
        /// Constant value sourced from a field on a ScriptableObject in the tree's config sources list.
        /// Renders as a three-way C/V/SO toggle: constant / variable / SO-field constant.
        /// Resolved at bake time and packed as a regular FieldData constant — zero runtime overhead.
        /// </summary>
        ScriptableObjectConstant,
    }

    /// <summary>Which custom editor a constant-mode param uses instead of the raw value field.</summary>
    public enum ConstantEditorHint
    {
        None,
        RoleDropdown,
        OrderDropdown,
    }

    public struct DynamicParamDescriptor
    {
        public string titleLabel;
        public string label;
        public DynamicParamKind kind;
        public int index;
        public Type[] allowedTypes;
        public int? syncTypeFromIndex;
        public bool syncElementType;
        public Type operationEnumType;
        public Func<Type, int[]> getAvailableOpIndices;

        // ── Attribute-projection parity (set by ParamSchemaReflection; dynamic
        // methods may also set these via the Params builder) ──

        /// <summary>Exact reflected field name for attribute-projected params.
        /// Written to entry.fieldName so runtime GetSlotByName() keeps working.
        /// Null for hand-authored dynamic params (label-derived fallback).</summary>
        public string fieldName;

        /// <summary>Default array-ness ([SharedArray]). Mutable afterwards via the type filter button.</summary>
        public bool isArray;

        /// <summary>Hidden from inspector; variableName auto-bound from autoVariableName.</summary>
        public bool isHidden;
        public string autoVariableName;

        /// <summary>Custom constant editor (role/order dropdown) instead of the raw value field.</summary>
        public ConstantEditorHint constantEditor;

        /// <summary>When set, the row is only drawn while that entry's boolValue is true
        /// (replaces the hardcoded customTickValue rule).</summary>
        public int? visibilityDependsOnIndex;
    }
}
```

- [ ] **Step 2: Verify compile** (purely additive)

- [ ] **Step 3: Commit**

```bash
git add Assets/BehaviourTree/Core/DynamicParamDescriptor.cs
git commit -m "feat(core): add attribute-parity fields to DynamicParamDescriptor"
```

### Task 2.3: Fluent `Params` builder

**Files:**
- Create: `Assets/BehaviourTree/Core/Params.cs`
- Test: `Assets/BehaviourTree/Tests/Editor/ParamsTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using BehaviourTree.Core;
using NUnit.Framework;

namespace BehaviourTree.Tests
{
    public class ParamsTests
    {
        private enum TestOp { A, B }

        [Test]
        public void Variable_SetsKindLabelsAndTypes()
        {
            var d = Params.Variable("Target", typeof(int), typeof(float));
            Assert.AreEqual(DynamicParamKind.Variable, d.kind);
            Assert.AreEqual("Target", d.titleLabel);
            Assert.AreEqual("Target", d.label);
            Assert.AreEqual(2, d.allowedTypes.Length);
        }

        [Test]
        public void Build_AssignsSequentialIndices()
        {
            var ds = Params.Build(
                Params.Variable("Source"),
                Params.Operation<TestOp>("Operation"),
                Params.Variable("Output").SyncElementTypeFrom(0));
            Assert.AreEqual(0, ds[0].index);
            Assert.AreEqual(1, ds[1].index);
            Assert.AreEqual(2, ds[2].index);
            Assert.AreEqual(0, ds[2].syncTypeFromIndex);
            Assert.IsTrue(ds[2].syncElementType);
        }

        [Test]
        public void Operation_SetsEnumTypeAndFilter()
        {
            Func<Type, int[]> filter = _ => new[] { 0 };
            var d = Params.Operation<TestOp>("Op", filter);
            Assert.AreEqual(DynamicParamKind.Operation, d.kind);
            Assert.AreEqual(typeof(TOpPlaceholder), d.operationEnumType); // see note below
            Assert.AreSame(filter, d.getAvailableOpIndices);
        }

        [Test]
        public void Hidden_SetsAutoBind()
        {
            var d = Params.Hidden("AgentOrders");
            Assert.AreEqual(DynamicParamKind.Variable, d.kind);
            Assert.IsTrue(d.isHidden);
            Assert.AreEqual("AgentOrders", d.autoVariableName);
        }
    }
}
```

Note: in the actual test file, replace `typeof(TOpPlaceholder)` with `typeof(TestOp)` — the generic parameter is the enum type.

- [ ] **Step 2: Run — verify FAIL** (`Params` type doesn't exist → compile error in Test Runner)

- [ ] **Step 3: Write the builder**

```csharp
using System;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Fluent factory for DynamicParamDescriptor. Removes object-initializer
    /// boilerplate from GetDynamicParamDescriptors() overrides.
    /// </summary>
    public static class Params
    {
        /// <summary>Blackboard variable picker row.</summary>
        public static DynamicParamDescriptor Variable(string title, params Type[] allowedTypes) =>
            Base(title, DynamicParamKind.Variable, allowedTypes);

        /// <summary>Constant-or-variable row with C/V toggle button.</summary>
        public static DynamicParamDescriptor Toggle(string title, params Type[] allowedTypes) =>
            Base(title, DynamicParamKind.Toggle, allowedTypes);

        /// <summary>Constant value row.</summary>
        public static DynamicParamDescriptor Constant(string title, Type type) =>
            Base(title, DynamicParamKind.Constant, new[] { type });

        /// <summary>Enum operation dropdown, optionally filtered per resolved param type.</summary>
        public static DynamicParamDescriptor Operation<TOp>(string title, Func<Type, int[]> filter = null)
            where TOp : Enum
        {
            DynamicParamDescriptor d = Base(title, DynamicParamKind.Operation, null);
            d.operationEnumType = typeof(TOp);
            d.getAvailableOpIndices = filter;
            return d;
        }

        /// <summary>Three-way C/V/SO row (constant / variable / ScriptableObject field).</summary>
        public static DynamicParamDescriptor SOConstant(string title, params Type[] allowedTypes) =>
            Base(title, DynamicParamKind.ScriptableObjectConstant, allowedTypes);

        /// <summary>Hidden variable, auto-bound to a blackboard variable by name.</summary>
        public static DynamicParamDescriptor Hidden(string autoVariableName)
        {
            DynamicParamDescriptor d = Base(autoVariableName, DynamicParamKind.Variable, null);
            d.isHidden = true;
            d.autoVariableName = autoVariableName;
            return d;
        }

        /// <summary>Assigns sequential indices (the field is positional metadata only).</summary>
        public static DynamicParamDescriptor[] Build(params DynamicParamDescriptor[] descriptors)
        {
            for (int i = 0; i < descriptors.Length; i++)
                descriptors[i].index = i;
            return descriptors;
        }

        private static DynamicParamDescriptor Base(string title, DynamicParamKind kind, Type[] allowedTypes) =>
            new DynamicParamDescriptor
            {
                titleLabel = title,
                label = title,
                kind = kind,
                allowedTypes = allowedTypes,
            };
    }

    /// <summary>Fluent modifiers. Structs are copied — each returns the modified copy.</summary>
    public static class DynamicParamDescriptorExtensions
    {
        /// <summary>Mirror the resolved type of another param index.</summary>
        public static DynamicParamDescriptor SyncTypeFrom(this DynamicParamDescriptor d, int sourceIndex)
        {
            d.syncTypeFromIndex = sourceIndex;
            return d;
        }

        /// <summary>Mirror the element type of an array-typed source param (output is scalar).</summary>
        public static DynamicParamDescriptor SyncElementTypeFrom(this DynamicParamDescriptor d, int sourceIndex)
        {
            d.syncTypeFromIndex = sourceIndex;
            d.syncElementType = true;
            return d;
        }

        /// <summary>Row only visible while the referenced entry's boolValue is true.</summary>
        public static DynamicParamDescriptor VisibleWhen(this DynamicParamDescriptor d, int entryIndex)
        {
            d.visibilityDependsOnIndex = entryIndex;
            return d;
        }
    }
}
```

- [ ] **Step 4: Run tests — verify PASS** (4 passed)

- [ ] **Step 5: Commit**

```bash
git add Assets/BehaviourTree/Core/Params.cs Assets/BehaviourTree/Tests/Editor/ParamsTests.cs
git commit -m "feat(core): add fluent Params descriptor builder"
```

**Phase 2 gate:** all EditMode tests green; no editor behavior changed.

---

## Phase 3 — Attribute→descriptor projection + single render path

### Task 3.1: `ParamSchemaReflection` projection

**Files:**
- Create: `Assets/BehaviourTree/Runtime/ParamSchemaReflection.cs`
- Test: `Assets/BehaviourTree/Tests/Editor/ParamSchemaReflectionTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using NUnit.Framework;

namespace BehaviourTree.Tests
{
    public class ParamSchemaReflectionTests
    {
        private sealed class FixtureMethod : ActionMethod
        {
            [SharedVar(IsHidden = true, AutoVariableName = "AgentOrders")]
            public int ordersSlot;

            [SharedVar(isToggleVariable: true, IsOrderDropdown = true)]
            public int orderValue;

            public float plainConstant;
            public bool useCustomTick;
            public float customTickValue;

            public override NodeState Execute(TickContext ctx) => NodeState.SUCCESS;
        }

        private sealed class ParameterlessMethod : ActionMethod
        {
            public override NodeState Execute(TickContext ctx) => NodeState.SUCCESS;
        }

        [Test]
        public void Projection_MapsAttributes()
        {
            var d = ParamSchemaReflection.GetDescriptors(typeof(FixtureMethod));
            Assert.AreEqual(5, d.Length);

            Assert.AreEqual(DynamicParamKind.Variable, d[0].kind);
            Assert.IsTrue(d[0].isHidden);
            Assert.AreEqual("AgentOrders", d[0].autoVariableName);
            Assert.AreEqual("ordersSlot", d[0].fieldName);

            Assert.AreEqual(DynamicParamKind.Toggle, d[1].kind);
            Assert.AreEqual(ConstantEditorHint.OrderDropdown, d[1].constantEditor);
            Assert.AreEqual(typeof(int), d[1].allowedTypes[0]);

            Assert.AreEqual(DynamicParamKind.Constant, d[2].kind);
            Assert.AreEqual("plainConstant", d[2].fieldName);
        }

        [Test]
        public void Projection_CustomTickValueDependsOnPreviousBool()
        {
            var d = ParamSchemaReflection.GetDescriptors(typeof(FixtureMethod));
            Assert.AreEqual("customTickValue", d[4].fieldName);
            Assert.AreEqual(3, d[4].visibilityDependsOnIndex);
        }

        [Test]
        public void Projection_NoPublicFields_ReturnsNull()
        {
            Assert.IsNull(ParamSchemaReflection.GetDescriptors(typeof(ParameterlessMethod)));
        }
    }
}
```

- [ ] **Step 2: Run — verify FAIL** (type doesn't exist)

- [ ] **Step 3: Write the projection**

Must use the exact same reflection query as the legacy `MethodMetadataCache.BuildParamListFromFields` (`GetFields(BindingFlags.Public | BindingFlags.Instance)`), so results are identical by construction.

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Projects [SharedVar]/[SharedArray]/plain public fields of a NodeMethod
    /// subclass into DynamicParamDescriptors. Cached per type; invalidated when
    /// the method registry rebuilds. Editor-side use via NodeParamSchema —
    /// NodeMethod.GetDynamicParamDescriptors() intentionally stays null by
    /// default so runtime ParameterCount behavior is unchanged.
    /// </summary>
    public static class ParamSchemaReflection
    {
        private static readonly Dictionary<Type, DynamicParamDescriptor[]> cache =
            new Dictionary<Type, DynamicParamDescriptor[]>();

        public static DynamicParamDescriptor[] GetDescriptors(Type nodeMethodType)
        {
            if (cache.TryGetValue(nodeMethodType, out DynamicParamDescriptor[] d)) return d;
            d = Build(nodeMethodType);
            cache[nodeMethodType] = d;
            return d;
        }

        public static void Invalidate() => cache.Clear();

        private static DynamicParamDescriptor[] Build(Type type)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            if (fields.Length == 0) return null;

            var list = new List<DynamicParamDescriptor>(fields.Length);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                SharedVarAttribute sv = f.GetCustomAttribute<SharedVarAttribute>();
                bool isArray = f.GetCustomAttribute<SharedArrayAttribute>() != null;
                string title = char.ToUpper(f.Name[0]) + f.Name.Substring(1);

                DynamicParamDescriptor d;
                if (sv != null && sv.IsToggleVariable)
                    d = Params.Toggle(title, f.FieldType);
                else if (sv != null || isArray)
                    d = Params.Variable(title, f.FieldType);
                else
                    d = Params.Constant(title, f.FieldType);

                d.index = i;
                d.fieldName = f.Name;
                d.isArray = isArray;
                d.isHidden = sv?.IsHidden ?? false;
                d.autoVariableName = sv?.AutoVariableName;
                if (sv?.IsRoleDropdown == true) d.constantEditor = ConstantEditorHint.RoleDropdown;
                if (sv?.IsOrderDropdown == true) d.constantEditor = ConstantEditorHint.OrderDropdown;

                // Port of the former hardcoded editor rule: a field literally
                // named "customTickValue" is only visible when the preceding
                // bool entry is true.
                if (f.Name == "customTickValue" && i > 0 && fields[i - 1].FieldType == typeof(bool))
                    d.visibilityDependsOnIndex = i - 1;

                list.Add(d);
            }
            return list.ToArray();
        }
    }
}
```

- [ ] **Step 4: Run tests — verify PASS** (3 passed)

- [ ] **Step 5: Commit**

```bash
git add Assets/BehaviourTree/Runtime/ParamSchemaReflection.cs Assets/BehaviourTree/Tests/Editor/ParamSchemaReflectionTests.cs
git commit -m "feat(core): project SharedVar fields into param descriptors"
```

### Task 3.2: `NodeParamSchema` editor helper

**Files:**
- Create: `Assets/BehaviourTree/Editor/Registries/NodeParamSchema.cs`

- [ ] **Step 1: Write the helper**

```csharp
using BehaviourTree.Core;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Single schema lookup for the inspector: a method's own
    /// GetDynamicParamDescriptors() override wins; otherwise its
    /// [SharedVar]/plain fields are projected into descriptors.
    /// </summary>
    public static class NodeParamSchema
    {
        static NodeParamSchema()
        {
            MethodRegistry.OnRegistryRebuilt += ParamSchemaReflection.Invalidate;
        }

        public static DynamicParamDescriptor[] GetForMethod(NodeMethod method)
        {
            if (method == null) return null;
            return method.GetDynamicParamDescriptors()
                   ?? ParamSchemaReflection.GetDescriptors(method.GetType());
        }
    }
}
```

- [ ] **Step 2: Verify compile**

- [ ] **Step 3: Commit**

```bash
git add Assets/BehaviourTree/Editor/Registries/NodeParamSchema.cs
git commit -m "feat(editor): add unified NodeParamSchema lookup"
```

### Task 3.3: Single render path in `CustomNodeEditor`

**Files:**
- Modify: `Assets/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs`

The legacy `ParamInfo` branch of `BuildFieldEntries` becomes dead — the descriptor renderer now handles all methods. Legacy-only behaviors are ported into the descriptor path first.

- [ ] **Step 1: Port hidden auto-bind into the resize logic**

In `BuildDynamicFieldEntries`, inside the `if (methodChanged)` block, after the existing isVariable sync loop, add:

```csharp
// Hidden params: auto-bind by convention, no UI rendered
for (int i = 0; i < syncCount; i++)
{
    DynamicParamDescriptor desc = descriptors[i];
    if (!desc.isHidden) continue;
    SerializedProperty entry = fieldEntriesProp.GetArrayElementAtIndex(i);
    entry.FindPropertyRelative("isVariable").boolValue = true;
    entry.FindPropertyRelative("variableName").stringValue = desc.autoVariableName;
}
```

- [ ] **Step 2: Port per-repaint metadata enforcement**

Legacy enforced `fieldName`/`fieldTypeName`/`isArray` from reflection every repaint. Add this method and call it right after `SyncLinkedEntryTypes();` in `BuildDynamicFieldEntries`:

```csharp
/// <summary>
/// Attribute-projected descriptors (fieldName != null) carry static metadata that
/// overrides the serialized entry every repaint — same guarantee the legacy
/// ParamInfo path gave. Hand-authored dynamic descriptors skip this (user edits win).
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

        if (desc.isArray)
            entry.FindPropertyRelative("isArray").boolValue = true;
    }
}
```

- [ ] **Step 3: Port visibility dependency + hidden rows into the row loop**

In `BuildDynamicFieldEntries`'s `for` loop over descriptors, immediately after `bool entryIsArray = ...`, add:

```csharp
// Hidden params render nothing (auto-bound in resize step)
if (desc.isHidden) continue;

// Visibility gated on another entry's bool (e.g. customTickValue)
if (desc.visibilityDependsOnIndex is int depIndex &&
    depIndex >= 0 && depIndex < fieldEntriesProp.arraySize &&
    !fieldEntriesProp.GetArrayElementAtIndex(depIndex).FindPropertyRelative("boolValue").boolValue)
{
    continue;
}
```

- [ ] **Step 4: Port role/order constant editors**

In the row dispatch `switch`, `DynamicParamKind.Constant` case, before the default layout:

```csharp
case DynamicParamKind.Constant:
    if (desc.constantEditor == ConstantEditorHint.RoleDropdown) { DrawRoleDropdown(entry); break; }
    if (desc.constantEditor == ConstantEditorHint.OrderDropdown) { DrawOrderDropdown(entry); break; }
    // existing label + constant layout unchanged
```

In `DrawToggleParamRow`, replace the constant-mode `else` branch with:

```csharp
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
```

- [ ] **Step 5: Switch `BuildFieldEntries` to the schema helper**

Replace the whole method body (the `descriptors != null` dispatch AND the entire legacy `ParamInfo` branch) with:

```csharp
private void BuildFieldEntries(string selectedMethodName, bool methodChanged)
{
    NodeMethod temp = MethodRegistry.CreateInstance(selectedMethodName);
    DynamicParamDescriptor[] descriptors = NodeParamSchema.GetForMethod(temp);

    if (descriptors == null || descriptors.Length == 0)
    {
        fieldEntriesProp.ClearArray();
        if (target is CompositeNode) return;
        string methodDesc = !string.IsNullOrEmpty(selectedMethodName) ? selectedMethodName : "(none)";
        EditorGUILayout.HelpBox($"No schema found for method '{methodDesc}'.", MessageType.Info);
        return;
    }

    BuildDynamicFieldEntries(descriptors, methodChanged);
}
```

Also update `InitNewEntryFromDescriptor` to honor `desc.fieldName` (parity point 4):

```csharp
entry.FindPropertyRelative("fieldName").stringValue =
    desc.fieldName ?? desc.label.ToLowerInvariant().Replace(" ", "");
```

Do NOT yet delete the now-unused legacy private methods — they stay compilable until Phase 4. (If unused-method warnings appear, delete them now instead.)

- [ ] **Step 6: Verify compile + FULL fixture matrix.** Every fixture must render as before (aside from the intended single-box layout unification). Pay special attention to: `SendOrderMethod` (hidden `ordersSlot` auto-bound; order search dropdown in C mode), `Cooldown` (`customTickValue` only when `useCustomTick` true), `ForEachRoleMethod` (role dropdown).

- [ ] **Step 7: Bake + runtime smoke test.** Bake a tree containing `SendOrderMethod` + `Cooldown` + `SetVariable`; Play Mode; no bake errors; orders/cooldowns behave as before.

- [ ] **Step 8: Run EditMode tests — all green**

- [ ] **Step 9: Commit**

```bash
git add Assets/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs
git commit -m "feat(editor): render all node params through the unified descriptor path"
```

**Phase 3 gate:** one render path serves both authoring styles; matrix + bake + tests pass.

---

## Phase 4 — Delete the legacy path + decompose `CustomNodeEditor`

### Task 4.1: Migrate `TooltipRegistry` off `MethodMetadataCache`

**Files:**
- Modify: `Assets/BehaviourTree/Editor/Registries/TooltipRegistry.cs:857-875`

- [ ] **Step 1: Replace the `AutoDeriveFieldDescriptions` lookup (lines 857-875)**

```csharp
NodeMethod temp = MethodRegistry.CreateInstance(entry.methodName);
var descriptors = NodeParamSchema.GetForMethod(temp);
if (descriptors == null || descriptors.Length == 0) continue;

var existingDescriptions = entry.data.fieldDescriptions;
var newDescriptions = new NodeFieldDescription[descriptors.Length];

for (int f = 0; f < descriptors.Length; f++)
{
    string fieldName = descriptors[f].fieldName
                       ?? descriptors[f].label.ToLowerInvariant().Replace(" ", "");
    string existingDesc = FindExistingDescription(existingDescriptions, fieldName);

    newDescriptions[f] = new NodeFieldDescription
    {
        fieldName = fieldName,
        description = existingDesc ?? ""
    };
}

entry.data.fieldDescriptions = newDescriptions;
```

- [ ] **Step 2: Verify compile + run the TooltipRegistry auto-derive action; descriptions preserved**

- [ ] **Step 3: Commit**

```bash
git add Assets/BehaviourTree/Editor/Registries/TooltipRegistry.cs
git commit -m "refactor(editor): migrate TooltipRegistry to unified param schema"
```

### Task 4.2: Delete the legacy path

**Files:**
- Delete: `Assets/BehaviourTree/Editor/Registries/MethodMetadataCache.cs`
- Modify: `Assets/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs`

- [ ] **Step 1: Delete `MethodMetadataCache.cs`** (contains `ParamInfo`; last consumer migrated in 4.1)

- [ ] **Step 2: Delete from `CustomNodeEditor.cs`:**
  - `ResizeFieldEntries(List<ParamInfo>)` overload
  - `DrawConstantField(SerializedProperty, ParamInfo)` (the ParamInfo overload)
  - any remaining `ParamInfo`/`MethodMetadataCache` references (compile-driven: delete until clean)

- [ ] **Step 3: Verify compile + fixture matrix spot-check**

- [ ] **Step 4: Commit**

```bash
git add -A Assets/BehaviourTree/Editor
git commit -m "refactor(editor): delete legacy ParamInfo schema path"
```

### Task 4.3: Decompose `CustomNodeEditor`

**Files:**
- Create: `Assets/BehaviourTree/Editor/NodeInspectorViewEditors/NodeParamSectionRenderer.cs`
- Create: `Assets/BehaviourTree/Editor/NodeInspectorViewEditors/ConfigFieldPicker.cs`
- Modify: `Assets/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs`

- [ ] **Step 1: Extract `ConfigFieldPicker` (static class)**

Move these methods verbatim (locate by name): `ShowConfigFieldPicker`, `ApplySOFieldSelection`, `IsTypeCompatible`, `GetConfigSourceByGuid`. `ApplySOFieldSelection` sets `nodeVisualsChangedThisFrame` — replace with an `Action onChanged` callback parameter; update the `DrawSOConstantParamRow` call site.

- [ ] **Step 2: Extract `NodeParamSectionRenderer` (instance class)**

Move the entire params region: `BuildDynamicFieldEntries`, `EnforceProjectedMetadata`, `SyncLinkedEntryTypes`, `InitNewEntryFromDescriptor`, `ResolveEntryType`, all row drawers (`DrawVariableParamRow`, `DrawToggleParamRow`, `DrawOperationParamRow`, `DrawSOConstantParamRow`, `BuildTypedLabel`), `DrawRoleDropdown`, `DrawOrderDropdown`, `DrawDynamicVariableField`, `ApplyPickedVariable`, `ApplyPickedTypeFilter`, `DrawConstantFieldForType`, plus the `currentDescriptors` field, `variablePicker`, and layout constants.

```csharp
public NodeParamSectionRenderer(UnityEditor.Editor host, SerializedProperty fieldEntriesProp)

public bool nodeVisualsChangedThisFrame;
public void Render(DynamicParamDescriptor[] descriptors, bool methodChanged);
```

- [ ] **Step 3: Slim `CustomNodeEditor` to a shell** (~150 lines)

Remaining: serialized prop caching (`OnEnable`), `OnInspectorGUI` frame (node name, comment, abort type, children debug), one `NodeParamSectionRenderer` instance created in `OnEnable`, and a single `Render(NodeParamSchema.GetForMethod(temp), methodChanged)` call.

- [ ] **Step 4: Verify compile + FULL fixture matrix + bake + Play Mode smoke (as in 3.3 steps 6-7)**

- [ ] **Step 5: Run EditMode tests — all green**

- [ ] **Step 6: Commit**

```bash
git add Assets/BehaviourTree/Editor/NodeInspectorViewEditors
git commit -m "refactor(editor): split param rendering out of CustomNodeEditor"
```

**Phase 4 gate:** legacy code gone; `CustomNodeEditor.cs` ~150 lines; everything green.

---

## Phase 5 (optional, incremental) — Migrate dynamic methods to the `Params` builder

One commit per method or per file. No behavior change — produced descriptors must be field-identical to the hand-written ones.

- [ ] **Step 1: Pilot — `SetVariable` (`VariableMethods.cs:37-41`)**

```csharp
public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => Params.Build(
    Params.Variable("Target"),
    Params.Toggle("Value").SyncTypeFrom(0));
```

Verify: both rows render; Target→Value type sync works; bake passes.

- [ ] **Step 2: Pilot — `ClearVariable` and `LogVariable`** (single `Params.Variable(...)` inside `Params.Build`)

- [ ] **Step 3: Migrate remaining dynamic methods** (`SquadReduceMethod`, `CheckSquadData`, `PollAgentStatus`, `ReportStatusMethod`, `SetNavAgentSpeed`, `CalculateFormation`, `CheckVariableArray`, `ExtractPosition`, `Enemy_*` methods) — per-file commits; re-check each node in the inspector + bake after each file.

---

## Phase 6 (future, only if needed) — Attribute mid-ground extensions

Only pursue if methods emerge whose shape is static but types must be edit-time-flexible *without* writing an override:

- `AllowedTypes` / `SyncTypeFrom` named args on `SharedVarAttribute` → mapped in `ParamSchemaReflection.Build`.
- Boxed auto-resolve for multi-type attribute fields (`ResolveInputsGeneric` extension writing `object` fields via `BB.GetBoxed`).

Deferred per YAGNI — the fluent override already covers this with acceptable boilerplate after Phase 5.

---

## Phase 7 — Runtime auto-binding for dynamic nodes (`DeserializeParameters`)

**Goal:** eliminate the hand-written `DeserializeParameters` cursor walk from every dynamic node. The `FieldData` stream is self-describing (mode byte per entry) and the schema declares the param count — so the walk is mechanical and belongs in one shared implementation. This mirrors what `DeserializeFields` already does for `[SharedVar]` nodes and completes the runtime-side symmetry with the editor unification.

**Design decisions (locked during discussion):**
- Naming convention is **Option A**: `private const int Target = 0;` above the descriptor list. No string lookups, no reflection.
- `NodeMethod.DeserializeParameters` gets a real default implementation: decode the whole stream into positional arrays, then call a new `OnParametersReady()` virtual (one-time hook for resolving typed getters/comparers by name or index).
- Overrides remain the escape hatch for **exotic streams** (variadic entry counts, baker trailers, mode reinterpretation) — currently zero such methods exist; `CompareVariable`/`Enemy_FireSequence` only *look* exotic but are standard positional reads.
- Typed-access consumers (typed handles, typed comparers, `CopySlot`, slot versions) are gated on the typed-storage plan (Task 7.5 below) — the auto-binding layer itself is storage-agnostic and lands first, acting as the isolation layer so the later boxed→typed swap touches one file, not fifteen.

### Task 7.1: `ParamReader` + `ParamValues`

**Files:**
- Create: `Assets/BehaviourTree/Runtime/ParamReader.cs` (namespace `BehaviourTree.Core`, matching `NodeMethod.cs`/`ParamSchemaReflection.cs`)
- Test: `Assets/BehaviourTree/Tests/Editor/ParamReaderTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class ParamReaderTests
    {
        [Test]
        public void SingleVariable_ReadsSlot_ScalarStride()
        {
            var stream = new[] { FieldData.FromVariable(5) };
            ParamValues v = ParamReader.Read(stream, null, null);
            Assert.AreEqual(1, v.Count);
            Assert.IsTrue(v.IsVariable(0));
            Assert.AreEqual(5, v.slots[0]);
            Assert.AreEqual(1, v.strides[0]);
        }

        [Test]
        public void StrideMarker_AttachesToPrecedingVariable()
        {
            var stream = new[] { FieldData.FromVariable(2), FieldData.FromStride(3) };
            ParamValues v = ParamReader.Read(stream, null, null);
            Assert.AreEqual(1, v.Count);                 // marker is not a param
            Assert.AreEqual(2, v.slots[0]);
            Assert.AreEqual(3, v.strides[0]);
        }

        [Test]
        public void PackedConstant_DecodedViaFieldTypeNames()
        {
            var stream = new[] { FieldData.FromConstant(42) };
            var names = new[] { "System.Int32" };
            ParamValues v = ParamReader.Read(stream, names, null);
            Assert.IsFalse(v.IsVariable(0));
            Assert.AreEqual(-1, v.slots[0]);
            Assert.AreEqual(42, v.constants[0]);
        }

        [Test]
        public void BoxedConstant_ReadFromBoxedArray()
        {
            var stream = new[] { FieldData.FromBoxedConstant(0) };
            var boxed = new object[] { Vector3.one };
            ParamValues v = ParamReader.Read(stream, null, boxed);
            Assert.AreEqual(Vector3.one, v.constants[0]);
        }

        [Test]
        public void MultiParam_MixedStream_PositionallyAligned()
        {
            var stream = new[]
            {
                FieldData.FromVariable(1), FieldData.FromStride(2),  // param 0: array var
                FieldData.FromConstant(2.5f),                        // param 1: float const
                FieldData.FromVariable(4),                           // param 2: scalar var
            };
            var names = new[] { null, null, "System.Single", null }; // aligns with RAW stream indices
            ParamValues v = ParamReader.Read(stream, names, null);
            Assert.AreEqual(3, v.Count);
            Assert.AreEqual(1, v.slots[0]);  Assert.AreEqual(2, v.strides[0]);
            Assert.AreEqual(-1, v.slots[1]); Assert.AreEqual(2.5f, v.constants[1]);
            Assert.AreEqual(4, v.slots[2]);  Assert.AreEqual(1, v.strides[2]);
        }
    }
}
```

- [ ] **Step 2: Run — verify FAIL**

- [ ] **Step 3: Implement**

```csharp
using System;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Positional decode of a node's FieldData stream: one entry per param,
    /// stride markers attached to their owning variable. Aligned by index with
    /// the method's DynamicParamDescriptors.
    /// </summary>
    public readonly struct ParamValues
    {
        public readonly int[] slots;       // BB slot per param; -1 when constant
        public readonly int[] strides;     // 1 for scalars
        public readonly object[] constants; // decoded constant per param; null when variable

        public ParamValues(int[] slots, int[] strides, object[] constants)
        {
            this.slots = slots;
            this.strides = strides;
            this.constants = constants;
        }

        public int Count => slots.Length;
        public bool IsVariable(int i) => slots[i] >= 0;

        public static readonly ParamValues Empty =
            new ParamValues(new int[0], new int[0], new object[0]);
    }

    public static class ParamReader
    {
        public static ParamValues Read(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            if (fields.Length == 0) return ParamValues.Empty;

            int paramCount = 0;
            for (int i = 0; i < fields.Length; i++)
                if (!fields[i].IsStrideMarker) paramCount++;

            int[] slots = new int[paramCount];
            int[] strides = new int[paramCount];
            object[] constants = new object[paramCount];

            int p = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                FieldData fd = fields[i];
                if (fd.IsStrideMarker)
                {
                    if (p > 0) strides[p - 1] = fd.value;
                    continue;
                }

                strides[p] = 1;
                if (fd.IsVariable)
                {
                    slots[p] = fd.value;
                }
                else
                {
                    slots[p] = -1;
                    constants[p] = DecodeConstant(fd, fieldTypeNames, boxedConstants, i);
                }
                p++;
            }
            return new ParamValues(slots, strides, constants);
        }

        // Same decode as VariableMethodHelper.ReadConstantValue; fieldTypeNames
        // aligns with RAW stream indices (TreeEvaluator slices it like fields).
        private static object DecodeConstant(FieldData fd, string[] fieldTypeNames, object[] boxedConstants, int streamIndex)
        {
            if (fd.IsBoxedConstant)
                return fd.GetBoxedConstant<object>(boxedConstants);
            if (!fd.IsConstant) return null;

            string typeName = (fieldTypeNames != null && streamIndex < fieldTypeNames.Length)
                ? fieldTypeNames[streamIndex] : null;
            Type type = string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);
            if (type == typeof(int)) return fd.value;
            if (type == typeof(uint)) return (uint)fd.value;
            if (type == typeof(float)) return fd.GetFloat();
            if (type == typeof(bool)) return fd.value != 0;
            if (type != null && type.IsEnum) return Enum.ToObject(type, fd.value);
            return fd.value;
        }
    }
}
```

- [ ] **Step 4: Run — verify PASS (5 tests)**

- [ ] **Step 5: Commit**

```powershell
git add Assets/BehaviourTree/Runtime/ParamReader.cs Assets/BehaviourTree/Tests/Editor/ParamReaderTests.cs
git commit -m "feat(runtime): add ParamReader — deterministic positional FieldData decode"
```

### Task 7.2: Base-class auto-populate + `OnParametersReady`

**Files:**
- Modify: `Assets/BehaviourTree/Runtime/NodeMethod.cs`
- Test: `Assets/BehaviourTree/Tests/Editor/NodeMethodAutoBindTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using NUnit.Framework;

namespace BehaviourTree.Tests
{
    public class NodeMethodAutoBindTests
    {
        private sealed class AutoMethod : ActionMethod
        {
            public bool readyCalled;
            public int SlotA => Slot(0);
            public bool IsVarB => IsVariableParam(1);
            public float ConstB => Const<float>(1);

            public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => Params.Build(
                Params.Variable("A"),
                Params.Toggle("B"));

            public override NodeState Execute(TickContext ctx) => NodeState.SUCCESS;
            protected override void OnParametersReady() => readyCalled = true;
        }

        [Test]
        public void BaseDeserialize_PopulatesAccessors_AndCallsHook()
        {
            var m = new AutoMethod();
            var stream = new[] { FieldData.FromVariable(3), FieldData.FromConstant(2.5f) };
            var names = new[] { null, "System.Single" };
            m.DeserializeParameters(stream, names, null);

            Assert.IsTrue(m.readyCalled);
            Assert.AreEqual(3, m.SlotA);
            Assert.IsFalse(m.IsVarB);
            Assert.AreEqual(2.5f, m.ConstB);
        }
    }
}
```

- [ ] **Step 2: Run — verify FAIL**

- [ ] **Step 3: Modify `NodeMethod.cs`** — replace the empty `DeserializeParameters` and add:

```csharp
        private ParamValues paramValues = ParamValues.Empty;

        protected bool IsVariableParam(int i) => paramValues.IsVariable(i);
        protected int Slot(int i) => paramValues.slots[i];
        protected int Stride(int i) => paramValues.strides[i];
        protected T Const<T>(int i) => (T)paramValues.constants[i];

        /// <summary>
        /// Default: decodes the whole stream positionally into the Slot/Const/Stride
        /// accessors, then calls OnParametersReady. Override ONLY for exotic streams
        /// (variadic entry counts, baker trailers, mode reinterpretation) — and call
        /// base first if you still want the accessors populated.
        /// Attribute ([SharedVar]) methods are unaffected: they bind via DeserializeFields.
        /// </summary>
        public virtual void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            paramValues = ParamReader.Read(fields, fieldTypeNames, boxedConstants);
            OnParametersReady();
        }

        /// <summary>
        /// Called once after parameters are deserialized. Resolve one-time work here:
        /// named slot caching, typed getter/comparer dispatch (per-instance type is
        /// fixed for the session even for multi-type params).
        /// </summary>
        protected virtual void OnParametersReady() { }
```

- [ ] **Step 4: Run — verify PASS**

- [ ] **Step 5: Commit**

```powershell
git add Assets/BehaviourTree/Runtime/NodeMethod.cs Assets/BehaviourTree/Tests/Editor/NodeMethodAutoBindTests.cs
git commit -m "feat(runtime): auto-bind DeserializeParameters via ParamReader + OnParametersReady hook"
```

### Task 7.3: Pilot migrations

Delete the override + private slot fields; use `Slot(i)`/`Const<T>(i)`/`Stride(i)` with named const indices. Per-method commits; verify each node behaves identically in Play Mode.

- [ ] **Step 1: `MoveTo`** (`VariableMethods.cs`) — delete `targetSlot` + override:

```csharp
        private const int Target = 0;

        public override NodeState Execute(TickContext ctx)
        {
            if (!IsVariableParam(Target)) return NodeState.FAILURE;
            object target = BB.GetBoxed(Slot(Target));
            // ...rest unchanged
```

- [ ] **Step 2: `ClearVariable` + `LogVariable`** — same shape; `LogVariable`'s manual stride read becomes `Stride(0)`.
- [ ] **Step 3: `SetVariable`** — `IsVariableParam(Value) ? BB.GetBoxed(Slot(Value)) : Const<object>(Value)`.
- [ ] **Step 4: `PollAgentStatus`** — `statusSlot + i` arithmetic becomes `Slot(0) + i` (slot arithmetic is method logic, not deserialization).
- [ ] **Step 5: Commit(s)**

### Task 7.4: Remaining migrations + delete `VariableMethodHelper`

- [ ] **Step 1: Migrate** `CompareVariable` (`Const<int>(Op)` for the operation), `SquadReduceMethod`, `CheckSquadData`, `CheckVariableArray`, `ReportStatusMethod`, `SetNavAgentSpeed`, `CalculateFormation`, `ExtractPosition`, all `Enemy_*` methods. Per-file commits.
- [ ] **Step 2: Delete `VariableMethodHelper`** once no callers remain (grep to confirm zero references).
- [ ] **Step 3: Run all EditMode tests + Play Mode smoke per migrated scene.**

### Task 7.5 (gated) — Typed consumers of the typed storage

**Prerequisite:** typed-storage plan Phases 3–5 (`IBlackboardTypedAccess`, `BlackBoard` typed methods, `TypedAccessorMap`) **and its Phase 7.5** (`GetSlotVersion`, `BlackBoard.CopySlot`). Until then the helpers read via `GetBoxed` — that is the point of the isolation layer.

- [ ] **Step 1: Typed handles** — `Var<T>`/`ConstParam<T>` structs resolved in `OnParametersReady` via `TypedAccessorMap` classification of the entry's runtime type; init-time validation against `fieldTypeName`. Eliminates per-tick boxing for single-type value params; multi-type params resolve a typed getter per instance.
- [ ] **Step 2: `CompareVariable`** — resolve typed comparer in `OnParametersReady` (typed-array reads for the 9 supported types; `object.Equals` for reference types, which never boxed).
- [ ] **Step 3: `SetVariable`** — replace `GetBoxed`/`SetBoxed` with `BB.CopySlot(Slot(Value), Slot(Target))`; value never materializes in managed code.
- [ ] **Step 4: `HasChanged`** — replace boxed `Equals` with `bb.GetSlotVersion(Slot(Variable))` int comparison; delete `previousValue`/`hasPrevious` instance state (also fixes the Architecture-Problems #5 smell for this node).
- [ ] **Step 5:** `LogVariable` stays boxed by design (`Debug.Log` formats strings).

---

## Self-Review

**Spec coverage:**
- Boilerplate vs flexibility → Phases 2 (fluent builder + parity fields), 3 (projection = attribute path keeps zero boilerplate), 5 (adoption), 6 (future mid-ground).
- Unified UI representation → Phase 1 (shared primitives), Phase 3 (single render path; legacy per-param boxes converge to the one-box descriptor style).
- CustomNodeEditor refactor → Phases 1 (~400 lines), 3 (legacy branch removal), 4.3 (decomposition to ~150-line shell).
- `DeserializeParameters` boilerplate → Phase 7 (ParamReader + base-class auto-bind + OnParametersReady; typed consumers gated on typed-storage Phases 3–5 + 7.5).

**Placeholder scan:** All code steps contain complete code; code-motion steps (4.2, 4.3) name exact methods to move. Task 2.3 Step 1 contains one deliberate inline note (`TestOp` substitution) — resolved in the note itself.

**Type/name consistency:** `BlackboardVariablePicker.Options` fields used consistently across 1.1/1.2; `Params.*` signatures match their tests; `ParamSchemaReflection.GetDescriptors/Invalidate` match NodeParamSchema wiring; `desc.fieldName`, `desc.constantEditor`, `desc.visibilityDependsOnIndex` defined in 2.2 before use in 3.x; `BuildDynamicFieldEntries(descriptors, methodChanged)` signature established in 1.4 and reused in 3.3.

**Known deliberate behavior changes (all intended, all verified at phase gates):**
1. Help-box copy unified across rows (Phase 1).
2. Legacy per-param boxes → single box layout (Phase 3).
3. Scalar-mode variable filtering treats `isSquadData` vars as scalar-eligible everywhere (Phase 1).
4. Read-only proxy-mapping display available on all rows, not just legacy (Phase 1 option, enabled per call site).
