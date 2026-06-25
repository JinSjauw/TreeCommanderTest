# Baking & Runtime Inconsistencies

This document describes inconsistencies found between the baking process, runtime blackboard access, `[SharedVar]` fields, and `DynamicParamDescriptor` nodes. Each issue includes a root-cause analysis, why it must be fixed, and a concrete example of what goes wrong if left unfixed.

---

## Issue 1: `PackFieldEntryWithArray` skips type checking for stride > 1 variables

### Location

`TreeBaker.cs` — `PackFieldEntryWithArray()`, lines 570–581.

### Root cause

```csharp
if (stride > 1)
{
    // Emits FromVariable(baseSlot) + FromStride(stride) directly.
    // PackFieldEntry() is NEVER called → type validation skipped.
}
else
{
    // PackFieldEntry() is called → validates field type vs BB variable type.
    fieldDataArray[offset++] = PackFieldEntry(entry, ...);
}
```

For stride > 1 variables, the baker computes the flat slot offset and emits a `FieldData` pair directly, bypassing `PackFieldEntry()` entirely. `PackFieldEntry()` is where the type-compatibility check lives (field type vs the blackboard variable's stored type).

For stride = 1 variables, `PackFieldEntry()` validates types and produces a `FromVariable(-1)` sentinel on mismatch, causing the runtime to skip the BB read gracefully.

### Why it needs fixing

The type guard should be uniform. A mismatch is a configuration error (user bound an `int` field to a `Vector3` variable) and should be caught at bake time with a clear warning, not at runtime with a silent failure.

### What goes wrong if unfixed

A behaviour tree with a squad-data `AgentRoles` variable of type `Vector3` (stride = 5 agents). A `ForEachRole` composite has:

```csharp
[SharedVar(IsHidden = true, AutoVariableName = "AgentRoles")]
public int agentRoleSlot;  // expects int
```

Baking: stride > 1 → type check skipped → `FieldData` stores `baseSlot = 12` (valid offset). No warning.

Runtime inside `ForEachRole.Execute()`:
```csharp
object roleBoxed = bb.GetBoxed(roleSlot + agentIndex);
int roleInt = roleBoxed is int roleVal ? roleVal : 0;
```

`roleBoxed` is a `Vector3`, not an `int`. The `is int` check fails → `roleInt` defaults to 0. Every agent gets role 0. The composite silently misbehaves with no error log. The user has no indication that the variable type doesn't match the field type.

---

## Issue 2: `Get<T>(string)` / `Set<T>(string, T)` conflate variable index with slot index

### Location

`BlackBoard.cs` — `Get<T>(string)`, line 436; `Set<T>(string, T)`, line 426; `FindVariableIndex()`, line 405.

### Root cause

```csharp
public void Set<T>(string keyName, T value)
{
    int index = FindVariableIndex(keyName);  // returns VARIABLE INDEX (position in sharedVariables list)
    if (index >= 0)
        Set(index, value);                   // treats it as SLOT INDEX (flat offset into object[] array)
}
```

`FindVariableIndex()` iterates `sharedVariables` and returns the position (0, 1, 2…). `Set(int, T)` adds `currentAgentOffset` and indexes directly into `storage[]`. These two concepts are only equal when every preceding variable has stride = 1.

For example, if variable 0 is squad-data with stride 5, variable 1 has flat slot offset 5. `Set("Var1", val)` → `FindVariableIndex` returns 1 → `Set(1, val)` → writes to slot 1 (element 1 of variable 0), not slot 5.

### Why it needs fixing

This is the only name-based public API on `BlackBoard`. External code will use it and get corrupted data without any error. It silently writes to the wrong slot.

### What goes wrong if unfixed

A squad blackboard has:

| Variable index | Name | Stride | Flat slot range |
|---|---|---|---|
| 0 | `AgentIDs` | 10 | 0–9 |
| 1 | `CommanderTarget` | 1 | 10 |
| 2 | `FormationType` | 1 | 11 |

External code (e.g., a UI script):
```csharp
var bb = commander.GetComponent<BlackBoard>();
bb.Set("CommanderTarget", someVector);   // FindVariableIndex → 1 → Set(1, someVector)
```

`Set(1, someVector)` writes to slot 1 — which is element 1 of `AgentIDs`, overwriting agent 1's ID. `CommanderTarget` at slot 10 is never touched. The commander tree reads a stale value. No error is logged because the slot exists, the type check is done against slot 1's type (int), not slot 10's type (Vector3) — so the type mismatch might also be swallowed or produce a misleading error about int/Vector3 mismatch on "AgentIDs".

---

## Issue 3: ~~`LogVariable` double-offsets when placed inside `ForEachAgent`~~ ✅ FIXED

### Location

`VariableMethods.cs` — `LogVariable.Execute()`, lines 150–163.

### Root cause

```csharp
for (int i = 0; i < stride; i++)
{
    values[i] = BB.GetBoxed(variableSlot + i);
    // GetBoxed internally: storage.GetBoxed(index + currentAgentOffset)
    // So actual read: storage[variableSlot + i + currentAgentOffset]
}
```

`BB.GetBoxed()` always adds `currentAgentOffset`. When `LogVariable` is placed as a child of `ForEachAgent`, `currentAgentOffset` is set to the agent index (e.g., 2). The iteration `variableSlot + i` for `i = 0..stride-1` then reads:

- `storage[variableSlot + 0 + 2]` — agent 2's data ✓
- `storage[variableSlot + 1 + 2]` — agent 3's data ✓
- …
- `storage[variableSlot + 4 + 2]` — agent 6's data, which is the NEXT variable's slot ✗

For a squad variable with stride 5, elements 3 and 4 read from the next variable's storage.

### Why it needs fixing

The method is documented as "useful for debugging inside ForEachAgent." It silently produces wrong log output when used in its documented use case, misleading the developer debugging their tree.

### What goes wrong if unfixed

A commander tree squad blackboard with `Health` (stride 5, slots 10–14) and `Stamina` (stride 5, slots 15–19). `LogVariable` bound to `Health`, placed inside a commander composite that iterates agents and sets `currentAgentOffset`. With `agentIndex = 3` at the time `LogVariable` executes:

Expected: logs all 5 agents' health: `[100, 80, 60, 40, 20]`

Actual: `BB.GetBoxed(variableSlot + i)` reads storage at `variableSlot + i + currentAgentOffset`:

| i | Expression | Slot | Contains |
|---|---|---|---|
| 0 | `10 + 0 + 3` | 13 | `Health[3]` = 60 ✓ |
| 1 | `10 + 1 + 3` | 14 | `Health[4]` = 40 ✓ |
| 2 | `10 + 2 + 3` | 15 | `Stamina[0]` = 90 ✗ |
| 3 | `10 + 3 + 3` | 16 | `Stamina[1]` = 80 ✗ |
| 4 | `10 + 4 + 3` | 17 | `Stamina[2]` = 70 ✗ |

Log output: `[60, 40, 90, 80, 70]` — a mix of Health and Stamina values from different agents. The developer investigating a bug now believes the squad blackboard is corrupt, when in fact only the log is wrong. Debugging time is wasted chasing phantom data corruption.

---

## Issue 4: ~~`ForEachRoleMethod` wastes a BB read on `agentRoleSlot` every tick~~ ✅ FIXED

### Location

`ForEachRoleMethod.cs`, lines 18–19 and 34.

### Root cause

```csharp
[SharedVar(IsHidden = true, AutoVariableName = "AgentRoles")]
public int agentRoleSlot;  // This is a slot offset, not a value

public override NodeState Execute(...)
{
    int roleSlot = GetSlotByName(nameof(agentRoleSlot));  // Uses binding.bbSlotIndex
    // The field agentRoleSlot was set to the BB VALUE by ResolveInputsGeneric, but is ignored.
}
```

`ResolveInputsGeneric` reads the BB value at `agentRoleSlot`'s baked slot offset and writes it into the C# field. Then `Execute()` ignores the field value and calls `GetSlotByName()`, which returns `binding.bbSlotIndex` — the raw slot offset, not the value.

So every tick: BB read → field write (wasted) → `GetSlotByName` lookup (binding array scan) → returns the same integer every frame.

### Why it needs fixing

Per-frame overhead: one unnecessary boxed BB read + one linear binding array scan. In a tree ticking 50 agents per frame, that's 50 wasted boxing allocations and 50 array scans. The composite author's intent is clear — `agentRoleSlot` should be a stored slot offset, not an auto-resolved value — but the framework forces the auto-resolve anyway.

### What goes wrong if unfixed

No correctness issue — the wasted read/write is harmless. But it creates GC pressure from boxing `int` values (the squad `AgentRoles` variable stores per-agent `int` roles). With 50 agents, 60 FPS: 3000 unnecessary boxed reads per second, each allocating a boxed `int` on the heap. This contributes to GC spikes in a hot path.

Also a maintenance hazard: a future developer might refactor the `Execute()` to use the field value directly (`int role = agentRoleSlot`) instead of `GetSlotByName()`, causing the composite to use whatever BB value happens to be at slot 0 of the stride (agent 0's role) instead of the slot offset. The naming "Slot" implies it holds an offset, but the field contains a value after auto-resolve.

---

## Issue 5: ~~No guard against mixing `[SharedVar]` fields with `DynamicParamDescriptor`~~ ✅ FIXED

### Location

`TreeEvaluator.cs`, lines 63–81. `MethodRegistry.cs`, `CreateBindings()`.

### Root cause

```csharp
FieldBinding[] bindings = MethodRegistry.GetBindings(name);
if (bindings != null && bindings.Length > 0)
{
    instance.DeserializeFields(fields, bindings, boxedConstants);   // SharedVar path
}
else if (instance.ParameterCount > 0)
{
    instance.DeserializeParameters(fields, nodeTypeNames, boxedConstants); // Descriptor path
}
```

If a class defines BOTH `[SharedVar]` public fields AND returns non-null from `GetDynamicParamDescriptors()`, the SharedVar path wins (bindings.Length > 0). `DeserializeFields` walks `FieldData[]` in lockstep with bindings. But the editor built `fieldEntries` from descriptors, not from the bindings. The counts and order may differ — causing `DeserializeFields` to read wrong `FieldData` entries for each binding.

### Why it needs fixing

This is a latent foot-gun. Nothing prevents a developer from adding `[SharedVar]` fields to a descriptor-based node or vice versa. The failure mode is silent misalignment — wrong values read from wrong slots — with no error.

### What goes wrong if unfixed

A developer extends `SetVariable`:
```csharp
public class SetVariable : ActionMethod
{
    public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
    {
        new DynamicParamDescriptor { kind = DynamicParamKind.Variable, index = 0 },  // target
        new DynamicParamDescriptor { kind = DynamicParamKind.Toggle,   index = 1 },  // value
    };

    // Developer adds a debug field:
    [SharedVar] public int debugSlot;  // 3rd field, but only 2 descriptors exist
}
```

`CreateBindings()` reflects `SetVariable`'s public instance fields. Only `debugSlot` is found → bindings.Length = 1.

`GetDynamicParamDescriptors()` returns 2 descriptors → editor creates 2 fieldEntries.

`CountFieldDataForNode` → 2 entries → 2 `FieldData` entries.

`TreeEvaluator`: `bindings.Length = 1 > 0` → calls `DeserializeFields`.

`DeserializeFields` walks 1 binding, reads `FieldData[0]` (which is the target variable). Sets `binding.bbSlotIndex` to the target variable's slot. The value parameter (FieldData[1]) is never consumed.

Result: `debugSlot` is bound to the wrong variable (the target, not whatever the developer intended). Worse: if the developer intended `debugSlot` to be a constant, it gets a `bbSlotIndex` pointing to a BB variable. `ResolveInputsGeneric` overwrites it every tick with whatever is in that BB slot. No error.

---

## Summary of fixes

| Issue | Status | Fix | Effort |
|---|---|---|---|
| 1. Missing type-check for stride > 1 | Open | Extract type-check from `PackFieldEntry` into a helper; call from both branches in `PackFieldEntryWithArray` | Small |
| 2. `Get<T>(string)` uses wrong index | Open | Add `GetSlot(string)` helper that walks definition summing strides; build a `Dictionary<string, int>` cache at `Initialize()`; use it in name-based Get/Set | Medium |
| 3. `LogVariable` double-offset | ✅ Fixed | Changed `BB.GetBoxed(variableSlot + i)` → `BB.GetBoxedRaw(variableSlot + i)` in `VariableMethods.cs:155` | Small |
| 4. `ForEachRole` wasted BB read | ✅ Fixed | Added `SkipAutoResolve = true` flag to `[SharedVar]`. Fields with this flag still receive `bbSlotIndex` during bake (GetSlotByName works) but ResolveInputsGeneric / WriteOutputsGeneric / CompileAccessors skip them. Applied to `ForEachRole`, `GetLowestAgent`, `GetHighestAgent`, `GetNearestAgent` — all 8 slot-offset fields. | Small |
| 5. No guard for mixed paths | ✅ Fixed | Added guard in `MethodRegistry` (registration time) and `TreeEvaluator` (deserialization time) that logs an error if a node method has both `[SharedVar]` fields and `DynamicParamDescriptor[]`. | Tiny |
