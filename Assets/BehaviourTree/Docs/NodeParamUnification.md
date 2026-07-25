# Node Parameter System Unification

**Branch:** `refactor/NodeInspectorUI` · **Date:** 2026-07-25 · **Plan:** `docs/superpowers/plans/2026-07-25-node-param-unification.md`

This document explains what changed in the node parameter system, why, and how to author node parameters going forward.

---

## 1. Why the change was made

### The old problems

**Two competing parameter schemas.** Node methods declared their inspector parameters in two completely different ways:

| Path | Declaration | Type knowledge |
|---|---|---|
| `[SharedVar]` attributes on public fields | Reflected into `ParamInfo` by `MethodMetadataCache` | Compile-time, fixed |
| `GetDynamicParamDescriptors()` override | Hand-written `DynamicParamDescriptor[]` | Edit-time, flexible |

Both paths ended up writing the *same* `NodeFieldEntry[] fieldEntries` — but the editor maintained **two separate renderers** (`BuildFieldEntries` + `BuildDynamicFieldEntries`), **three near-identical variable dropdowns**, and **two near-identical constant-value type switches**. Adding a feature (e.g. the S/F search buttons) meant implementing it in one path and not the other, and the UI looked different depending on which path a node used.

**Boilerplate.** The descriptor path (the flexible one) required verbose struct initializers — including an `index` field that was set by every method but consumed by nothing.

**Editor size.** `CustomNodeEditor.cs` had grown to 1.343 lines mixing 8 unrelated responsibilities (inspector frame, two schema renderers, three dropdown implementations, role/order dropdowns, SO config picker, type syncing).

### The solution in one sentence

> **One canonical schema (`DynamicParamDescriptor`), projected from attributes when no override exists, rendered by one renderer — so both authoring styles produce identical UI from identical data.**

`NodeMethod.GetDynamicParamDescriptors()` still returns `null` by default (runtime `ParameterCount` behavior is unchanged); the projection is applied **editor-side only** via `NodeParamSchema`:

```
method.GetDynamicParamDescriptors()  →  override?  ──yes──→ descriptors
        │no
        ▼
ParamSchemaReflection (reflect [SharedVar]/plain fields) → descriptors
        │
        ▼
NodeParamSectionRenderer (ONE renderer) → NodeFieldEntry[] → TreeBaker (untouched)
```

---

## 2. Old → New reference

### Schema & declaration

| Old | New | Why |
|---|---|---|
| `MethodMetadataCache` + `ParamInfo` (reflection, editor-only, second schema model) | `ParamSchemaReflection` (Runtime asmdef, cached) projects fields directly into `DynamicParamDescriptor` | One schema model. The attribute path became a *projection* of the descriptor model instead of a competitor. File deleted. |
| `DynamicParamDescriptor` with 9 fields | Same + 6 parity fields: `fieldName`, `isArray`, `isHidden`, `autoVariableName`, `constantEditor`, `visibilityDependsOnIndex` | Descriptors can now express everything `[SharedVar]` could (hidden auto-bind, role/order dropdowns, array-ness) — prerequisite for unification. |
| Verbose object initializers + manual `index` | Fluent `Params` builder (`Params.Variable(...)`, `.SyncTypeFrom(0)`, `Params.Build(...)`) | Less boilerplate; index auto-assigned (it was dead weight — set everywhere, read nowhere). |
| Hardcoded `customTickValue` rule in the editor (hide when previous entry's bool is false) | Declared in schema: `visibilityDependsOnIndex` (projection sets it by the same convention; fluent API: `.VisibleWhen(i)`) | Cross-field visibility is schema data, not editor hardcode. |
| `TooltipRegistry` → `MethodMetadataCache` | `TooltipRegistry` → `NodeParamSchema.GetForMethod(...)` | Last `ParamInfo` consumer migrated, enabling deletion of the legacy schema. |

### Editor UI

| Old | New | Why |
|---|---|---|
| 3 variable dropdowns: `DrawVariableDropdown` (legacy), `DrawVariableDropdownWithSquadFilter` (toggle rows), `DrawDynamicVariableField` (S/F buttons) — ~250 lines of copy-pasted filtering | `BlackboardVariablePicker` (one class, `Options` struct: label, S/F buttons, allowedTypes, proxy mapping, change callbacks) | One filter implementation; S/F buttons and type auto-config now available uniformly; read-only (`InspectorView`) handling in one place. |
| 2 constant switches: `DrawConstantField(ParamInfo)` and `DrawConstantFieldForType(Type)` — same 8-branch type switch twice | `ConstantValueFieldDrawer.Draw(entryProp, type, label, showLabel, fieldWidth)` | One type switch to maintain (adding a new constant type = 1 edit, was 2). |
| `MethodRegistry.CreateInstance` twice per repaint | Once per repaint | Less per-frame allocation in the inspector. |

### File organization

| Old | New |
|---|---|
| `CustomNodeEditor.cs` — **1.343 lines**, 8 responsibilities | `CustomNodeEditor.cs` — **120 lines** (inspector frame only) |
| | `NodeParamSectionRenderer.cs` — 705 lines (schema → rows, syncing, role/order dropdowns) |
| | `ConfigFieldPicker.cs` — 122 lines (ScriptableObject constant field picker) |
| | `BlackboardVariablePicker.cs` — variable dropdown |
| | `ConstantValueFieldDrawer.cs` — constant type switch |
| `MethodMetadataCache.cs` | **deleted** |

### Deliberate behavior changes (small, intentional)

1. **Uniform layout:** attribute-path nodes now render in the same single-box style as dynamic nodes (previously each param had its own box).
2. **Uniform copy:** help-box messages are identical across rows.
3. **Scalar filtering:** blackboard variables marked `isSquadData` are eligible in scalar mode everywhere (previously only in dynamic Variable rows).
4. **Proxy-mapping display** in read-only sub-tree views is available on all variable rows (was legacy-only).

### What did NOT change (guarantees)

- `NodeFieldEntry` serialization layout — **existing assets deserialize identically**.
- `TreeBaker` — untouched; consumes `fieldEntries` as before.
- Runtime binding (`ResolveInputsGeneric`/`WriteOutputsGeneric`, `DeserializeParameters`, `FieldBinding`s) — untouched.
- `NodeMethod.GetDynamicParamDescriptors()` default still returns `null` → `ParameterCount` semantics in `TreeEvaluator`/`MethodRegistry` unchanged.
- `entry.fieldName` for attribute nodes is still the exact reflected field name (kept via `desc.fieldName`), so `GetSlotByName()` keeps working.

---

## 3. How to use the new system

### Style A — attributes (unchanged, zero boilerplate)

Use when parameter types are known at compile time. **Nothing about this changed** — existing nodes work as-is:

```csharp
[NodeMethod("SendOrder", allowedTreeType = AllowedTreeType.Commander)]
public sealed class SendOrderMethod : ActionMethod
{
    [SharedVar(IsHidden = true, AutoVariableName = "AgentOrders")]
    public int ordersSlot;                                   // hidden, auto-bound — no UI

    [SharedVar(isToggleVariable: true, IsOrderDropdown = true)]
    public int orderValue;                                   // C/V toggle, order search in C mode

    public float speedFactor;                                // plain public field = constant
}
```

Under the hood, `ParamSchemaReflection` now projects each field into a descriptor:

| Field | Projected descriptor |
|---|---|
| `[SharedVar]` | `Params.Variable(title, fieldType)` |
| `[SharedVar(isToggleVariable: true)]` | `Params.Toggle(title, fieldType)` |
| `[SharedArray]` | Variable + `isArray = true` |
| plain public field | `Params.Constant(title, fieldType)` |
| `IsHidden`/`AutoVariableName` | `isHidden` + `autoVariableName` (auto-bound, row skipped) |
| `IsRoleDropdown`/`IsOrderDropdown` | `constantEditor = RoleDropdown`/`OrderDropdown` |
| `customTickValue` after a bool field | `visibilityDependsOnIndex = previous index` |

### Style B — fluent descriptors (less boilerplate than before)

Use when the type is chosen at edit time (multi-type params, synced types, per-type operations).

**Before:**

```csharp
public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
{
    new DynamicParamDescriptor { titleLabel = "Target", label = "Target", kind = DynamicParamKind.Variable, index = 0 },
    new DynamicParamDescriptor { titleLabel = "Value",  label = "Value",  kind = DynamicParamKind.Toggle,   index = 1, syncTypeFromIndex = 0 },
};
```

**After:**

```csharp
public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => Params.Build(
    Params.Variable("Target"),
    Params.Toggle("Value").SyncTypeFrom(0));
```

**Builder reference:**

| Factory | Row kind |
|---|---|
| `Params.Variable(title, params Type[] allowed)` | Blackboard variable picker (+ S/F buttons) |
| `Params.Toggle(title, params Type[] allowed)` | Constant ⇄ variable with C/V button |
| `Params.Constant(title, Type)` | Constant value field |
| `Params.Operation<TOpEnum>(title, Func<Type,int[]> filter = null)` | Enum dropdown, optionally filtered per resolved type |
| `Params.SOConstant(title, params Type[] allowed)` | Three-way C/V/SO toggle (ScriptableObject field constant) |
| `Params.Hidden(autoVariableName)` | Hidden, auto-bound variable |

| Fluent modifier | Effect |
|---|---|
| `.SyncTypeFrom(i)` | Mirror the resolved type of param `i` |
| `.SyncElementTypeFrom(i)` | Mirror the *element* type of array param `i` (output is scalar) |
| `.VisibleWhen(i)` | Row only drawn while entry `i`'s bool value is true |
| `.NoTitle()` | No bold title header; the row shows a typed label instead (e.g. `MoveTo`) |

`Params.Build(...)` assigns sequential indices. The old object-initializer style still compiles and behaves identically — migrating existing methods is optional (Phase 5).

### Extending the editor

- **New constant type support** (e.g. `Color`): add one branch in `ConstantValueFieldDrawer.Draw`.
- **New custom constant editor** (like the role dropdown): add a member to `ConstantEditorHint`, handle it in the `Constant` case of `NodeParamSectionRenderer.Render` and in `DrawToggleParamRow`.
- **New `DynamicParamKind`**: add the enum member + one `case` in `Render` + a row drawer method in `NodeParamSectionRenderer`.

---

## 4. Where things live

| File | Responsibility |
|---|---|
| `Core/DynamicParamDescriptor.cs` | Canonical param schema + `ConstantEditorHint` |
| `Core/Params.cs` | Fluent descriptor factory + modifiers |
| `Runtime/ParamSchemaReflection.cs` | `[SharedVar]`/field → descriptor projection (cached) |
| `Editor/Registries/NodeParamSchema.cs` | Single lookup: `override ?? projection` |
| `Editor/NodeInspectorViewEditors/CustomNodeEditor.cs` | Inspector frame (name, comment, abort type, children) |
| `Editor/NodeInspectorViewEditors/NodeParamSectionRenderer.cs` | Entry resize/sync + all param rows |
| `Editor/NodeInspectorViewEditors/BlackboardVariablePicker.cs` | The variable dropdown (+ S/F buttons) |
| `Editor/NodeInspectorViewEditors/ConstantValueFieldDrawer.cs` | The constant-value type switch |
| `Editor/NodeInspectorViewEditors/ConfigFieldPicker.cs` | ScriptableObject constant field picker |
| `Tests/Editor/` | EditMode tests (Params builder, projection) |

## 5. Verification

- Language-server diagnostics clean after every step; 5 commits on `refactor/NodeInspectorUI`.
- EditMode tests: 8 (builder + projection + sanity) — run via Test Runner → EditMode.
- Manual: fixture matrix in the plan doc (`WaitSeconds`, `Cooldown`, `SendOrderMethod`, `ForEachRoleMethod`, `SetVariable`, `SquadReduceMethod`, `SetNavAgentSpeed`, `Enemy_Detected`) + bake & Play smoke test.
