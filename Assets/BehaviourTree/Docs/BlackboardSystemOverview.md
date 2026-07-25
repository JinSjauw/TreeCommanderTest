# Blackboard System — Complete Architecture Overview

## Layer Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│  EDITOR LAYER                                                   │
│  BlackBoardView (UI Toolkit list of variables)                  │
│  BlackBoardEditor (IMGUI per-component override inspector)      │
│  TrackedVariablesView (component field → BB variable binding UI) │
├─────────────────────────────────────────────────────────────────┤
│  CORE LAYER (shared data types, no Unity runtime deps)          │
│  BlackboardDefinition  →  BlackboardVariableBase  →  BlackboardVariable<T> │
│  BlackBoard (MonoBehaviour)  →  IBlackboardStorage              │
│  BlackboardValueOverride │ FieldData │ IBlackBoardAccess        │
├─────────────────────────────────────────────────────────────────┤
│  RUNTIME LAYER                                                  │
│  FieldReader  │  TrackedBinding  │  SquadInstance (bidirectional copy) │
│  VariableMethods (SetVariable, CompareVariable, CheckVariable…)  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 1. Core Layer — Data Model

### 1.1 `BlackboardVariableBase` (abstract base)

File: `Assets/BehaviourTree/Core/BlackboardVariableBase.cs`

Every variable in the blackboard is a **named, typed, slotted** entry. Key properties:

| Property | Description |
|---|---|
| `Name` | Editor-facing identifier. Unique within a definition. |
| `Stride` | Number of slots this variable occupies. Stride=1 is a scalar; stride>1 is an array. |
| `TypeName` | Assembly-qualified .NET type name. Lazy-resolved via `FieldTypeHelper`. |
| `isSquadData` | If true, stride is managed dynamically at runtime (CommanderTreeRunner). |
| `isSystemVariable` | If true, cannot be deleted/renamed/re-typed by user (base channels like `AgentRoles`, `AgentOrders`). |
| `GetBoxedValue(elementIndex)` | Returns the boxed value for a given element. |
| `SetBoxedValue(object, elementIndex)` | Sets the boxed value. |

The **stride** concept is central: a `Vector3[10]` variable stores 10 `Vector3` values in 10 contiguous slots. Stride controls how many slots a variable consumes in the flat storage array.

### 1.2 `BlackboardVariable<T>` (typed generic)

File: `Assets/BehaviourTree/Core/BlackboardVariable.cs`

Concrete implementation. Stores:
- `singleValue` — used when stride=1
- `arrayValues` — used when stride>1 (the raw T[])
- Auto-populates `TypeName` from `typeof(T).AssemblyQualifiedName`

Provides `Value`, `GetValue(index)`, `SetValue(value, index)`, `EnsureArraySize()`, and `Clone()`.

### 1.3 `BlackboardDefinition` (ScriptableObject)

File: `Assets/BehaviourTree/Core/BlackBoardDefinition.cs`

The **schema** for a blackboard — an asset that defines what variables exist and their types/strides. Lives as a `[SerializeReference]` list of `BlackboardVariableBase` instances (polymorphic).

Key members:
- `sharedVariables` — `List<BlackboardVariableBase>` (SerializeReference allows mixed T types)
- `sourceTreeAsset` / `sourceTreeGuid` — back-references to the tree this definition was baked from
- `FindVariable(name)` / `GetVariableIndex(name)` — lookups
- `AddVariable<T>(name, stride, initialValue)` — typed factory
- `CopyVariable(source)` — clone a variable definition (without values)
- `EnsureBaseChannel<T>(bbDef, name, isSquadData)` — auto-creates system variables (`AgentRoles`, `AgentOrders`)

### 1.4 `BlackBoard` (MonoBehaviour)

File: `Assets/BehaviourTree/Core/BlackBoard.cs`

The **runtime component** that holds the actual data. Implements `IBlackBoardAccess`. Key design:

**Storage**: Delegates to `IBlackboardStorage` (`TypedBlackboardStorage`) — one array per supported value type plus an `object[]` fallback, with a `SlotLocation[]` map translating virtual slots. Each slot also has a declared `Type` and `BlackboardSlotKind` (Value or Reference).

**SerializedReferences** (`List<UnityEngine.Object>`): Unity can only serialize `UnityEngine.Object` references in MonoBehaviours, not arbitrary C# objects. For reference-type variables (GameObject, Transform, custom ScriptableObjects, etc.), the references are stored in this parallel list. They sync to/from the flat storage on `Initialize()`.

**Value Overrides** (`List<BlackboardValueOverride>`): Per-component overrides for value-type variables. Keyed by `variableName + elementIndex` so they survive definition reorders. Stored as typed fields (int, float, bool, Vector2/3/4, Color, enum) for Unity serialization.

**Slot offset indexing**: `Get<T>(int index)` and `Set<T>(int index, T value)` operate on flat slot indices. The **variable→slot mapping** is positional: variable 0 starts at slot 0, variable N starts at `sum(strides_of_vars_0..N-1)`.

**`currentAgentOffset`**: A transient offset applied to all slot reads/writes by commander composites (`ForEachAgent`, `SelectAgent`). Makes each agent see a different slice of per-agent squad data. Cleared after subtree evaluation.

### 1.5 `TypedBlackboardStorage`

File: `Assets/BehaviourTree/Core/Blackboard/TypedBlackboardStorage.cs`

Typed-array storage implementation (replaced the legacy flat `object[]` storage):

```
map[]       → SlotLocation[N]  (virtual slot → typed array + local index)
slotTypes[] → Type[N]          (declared type per slot)
slotKinds[] → BlackboardSlotKind[N]  (Value or Reference)
versions[]  → int[N]           (per-slot write counters for change detection)
floats/ints/bools/vec2s/vec3s/vec4s/colors/quats[] → typed value arrays
objects[]   → object[]         (reference types, custom value types, exotic enums)
```

- Virtual slot numbering matches the legacy flat layout exactly (variable order, stride expansion) — baked `FieldData` slot indices are unaffected. `BlackboardStorageLayout` builds the map at `Initialize`; the supported type set is `float/int/bool/Vector2/3/4/Color/Quaternion` + int32-backed enums (stored in the int array). Everything else lands in `objects[]`.
- `GetFloat/SetFloat` etc. (`IBlackboardTypedAccess`) — allocation-free typed access; the hot path used by compiled field bindings and tracked-binding pushes. Enums are read/written as int.
- `Get<T>`/`Set<T>` and `GetBoxed`/`SetBoxed` — compatibility paths (may box); used by editor tooling, JSON, and dynamic-type nodes. Boxed reads of enum slots reconstruct the enum instance.
- `CopySlotsFrom(source, src, dst, count)` — typed region copies (`Array.Copy` runs) when layouts match, boxed fallback per slot otherwise. Used by squad sync and agent compaction.
- `GetSlotVersion(slot)` — monotonic write counter per slot (zero after seeding; bumped by every write path).
- `Initialize(definition)` / `Initialize(variables)` — builds layout + arrays, seeds typed initial values without boxing.
- `GetVariableSlotRange(varIndex, out baseSlot, out stride)` — converts variable index to flat slot range

### 1.6 `BlackboardValueOverride`

File: `Assets/BehaviourTree/Core/BlackboardValueOverride.cs`

Keyed by `variableName + elementIndex`. Stores a per-component override for a single value-type slot element. Uses typed serialized fields:

```csharp
int intValue; float floatValue; bool boolValue;
Vector2 vector2Value; Vector3 vector3Value; Vector4 vector4Value;
Color colorValue; int enumUnderlyingValue;
string typeName; // Assembly-qualified type for deserialization
```

This avoids the "Unity can't serialize `object`" problem for value-type overrides.

### 1.7 `FieldData` (5 bytes packed)

File: `Assets/BehaviourTree/Core/FieldData.cs`

A 5-byte `[StructLayout(Explicit)]` struct that is the **unit of parameter passing** at runtime:

| Mode | Meaning |
|---|---|
| `0` | Packed constant: value is int/bool bits or float-bits-reinterpreted-as-int |
| `1` | Blackboard variable: value is the flat slot index |
| `2` | Boxed constant: value is index into a parallel `object[] boxedConstants` array |
| `3` | Stride marker: emitted after a variable entry when stride>1 |

All node method parameters are stored as `FieldData[]` — constant or variable. The `TreeBaker` resolves variable names to slot offsets at bake time.

### 1.8 `IBlackBoardAccess` (interface)

File: `Assets/BehaviourTree/Core/IBlackBoardAccess.cs`

```
T Get<T>(int slot);
void Set<T>(int slot, T value);
object GetBoxed(int slot);
void SetBoxed(int slot, object value);
```

Minimal contract. Allows `FieldReader` and node methods to read/write without coupling to `BlackBoard` directly.

### 1.9 `IBlackboardStorage` (interface)

File: `Assets/BehaviourTree/Core/IBlackboardStorage.cs`

Lower-level storage contract with `Initialize`, `GetSlotKind`, `GetVariableSlotRange`, plus the get/set methods.

### 1.10 `BlackboardVariableJsonUtility`

File: `Assets/BehaviourTree/Core/BlackboardVariableJsonUtility.cs`

JSON-based serialization for `BlackboardVariableBase` instances of arbitrary types. Used for custom types that can't be stored in the typed variable lists. Supports arrays (stride > 1) via `ArrayWrapper`.

---

## 2. Runtime Layer — How Data Flows

### 2.1 `FieldReader` (ref struct)

File: `Assets/BehaviourTree/Runtime/FieldReader.cs`

A lightweight ref struct that wraps `ReadOnlySpan<FieldData>` + `BlackBoard`. Provides typed accessors:

```csharp
reader.Get<int>(fieldIndex)      // reads constant or BB variable
reader.Set<int>(fieldIndex, 42)  // writes to BB (variable fields only)
```

Supports `int`, `float`, `bool`, `enum`, `Vector2`, `Vector3`, `GameObject`, `Transform`, plus generic `Get<T>`/`Set<T>`. Constants are read from packed `FieldData.value` directly; variables hit the blackboard.

### 2.2 Tracked Bindings

File: `Assets/BehaviourTree/Runtime/TrackedBinding.cs`

Maps a **component's field/property** → **blackboard variable**. Each frame, the value is pushed into the BB before tree evaluation.

```csharp
class TrackedBinding {
    Component targetComponent;
    string memberName;          // field or property name
    string blackboardVariableName;
    bool isProperty;
    string memberTypeName;
    // Resolved at runtime:
    FieldInfo cachedFieldInfo / PropertyInfo cachedPropertyInfo;
    int variableIndex;         // flat slot offset
}
```

Grouped by tree asset via `TrackedBindingGroup` (GUID-matched so builds work). Only the group matching the active tree is activated. In `BehaviourTreeRunnerBase`:
- `ResolveTrackedBindings()` — resolves `FieldInfo`/`PropertyInfo` and computes slot offsets
- `PushTrackedBindings()` — called each frame, reads component values → `blackBoard.SetBoxed(slot, value)`

### 2.3 `AgentTreeRunner` (new-style, simpler)

File: `Assets/BehaviourTree/Runtime/TreeRunner.cs`

Extends `BehaviourTreeRunnerBase`. Can run **independently** (its own `Update()` loop) or be **driven by a CommanderTreeRunner**.

```csharp
class AgentTreeRunner : BehaviourTreeRunnerBase {
    bool runIndependently;
    List<SquadInstance> registeredSquads;
    CommanderTreeRunner commander;
    SquadInstance squadInstance;
}
```

Per-frame flow when independent:
1. `PushTrackedBindings()` — component values → BB
2. `evaluator.Evaluate(blackBoard)` — runs the tree

When commander-driven, the commander handles squad copy before/after evaluation.

### 2.4 `CommanderTreeRunner`

File: `Assets/BehaviourTree/Runtime/CommanderTreeRunner.cs`

Orchestrates squads + agents. Per-frame flow:

```
1. Commander: squad BB → commander BB (CopySquadsToTree)
2. Commander.Evaluate() — reads agent status, writes orders
3. Commander: commander BB → squad BB (CopySquadsFromTree)
4. For each agent:
   a. PushTrackedBindings() — agent component values → agent BB
   b. Squad BB → agent BB (CopySquadsToTree with agentOffset=i)
   c. Agent.Evaluate()
   d. Agent BB → squad BB (CopySquadsFromTree with agentOffset=i)
```

The **`currentAgentOffset`** on BlackBoard is set by ForEachAgent/SelectAgent composites to make each agent see its own slice of per-agent squad data.

### 2.5 `SquadInstance` — Bidirectional Data Bridge

File: `Assets/BehaviourTree/Runtime/SquadInstance.cs`

A MonoBehaviour that owns a `BlackBoard` (squad data). Holds a `SquadDefinition` which defines:
- Its own `BlackboardDefinition` (squad schema)
- `SquadBindingGroup[]` — per-tree variable bindings

On registration (`EnsureResolved(treeDef)`):
1. Finds the `SquadBindingGroup` whose tree matches (by GUID)
2. For each `VariableBinding`, resolves squad-side and tree-side slot offsets
3. Builds flat `int[]` copy triplets: `[srcSlot, dstSlot, stride]`

Copy operations:
- `CopyToBB(treeBB, treeDef, agentOffset)` — squad → tree (FromSquad/Both bindings)
- `CopyFromBB(treeBB, treeDef, agentOffset)` — tree → squad (ToSquad/Both bindings)

When `agentOffset >= 0` (agent case): squad-side slot is offset by the agent index. When `agentOffset == -1` (commander case): all stride slots are copied.

### 2.6 Variable Methods (runtime nodes)

File: `Assets/BehaviourTree/Runtime/Methods/VariableMethods.cs`

Node methods that operate on blackboard variables:

| Method | Type | Description |
|---|---|---|
| `SetVariable` | Action | Writes a value (constant or variable) to a BB slot |
| `ClearVariable` | Action | Resets a variable to its type default |
| `LogVariable` | Action | Logs a variable's value(s) to console |
| `CompareVariable` | Condition | Compares a variable against a value (Equal, NotEqual, Less, Greater, MagnitudeLess…) |
| `CheckVariable` | Condition | Checks boolean/null/zero/active states (IsTrue, IsNull, IsZero, IsActive…) |
| `HasChanged` | Condition | Detects value change since last tick |
| `EdgeDetect` | Condition | Detects rising/falling edges on booleans |
| `Toggle` | Action | Toggles a boolean variable |
| `SetFromTransform` | Action | Writes a Transform's position into a Vector2/3 variable |
| `MoveTo` | Action | Moves a NavMeshAgent toward a position stored in a variable |

All use `BB.GetBoxed()`/`BB.SetBoxed()` with slot indices resolved at bake time by `TreeBaker`.

### 2.7 `TickContext`

File: `Assets/BehaviourTree/Runtime/TickContext.cs`

Per-frame snapshot passed through the tree during evaluation. Contains:
- `nodeDatas[]`, `methodInstances[]` — baked node data
- `nodeStates[]`, `activeChildIndex[]` — mutable state arrays
- `runningAgentIndex[]` — per-node running agent index for ForEachAgent composites
- `agentCount` — current agent count (set by CommanderTreeRunner)
- `blackBoard` — the `BlackBoard` instance for the current evaluation

### 2.8 `TreeEvaluator`

File: `Assets/BehaviourTree/Runtime/TreeEvaluator.cs`

Tick-based behaviour tree evaluator. Replaces stack-based approach with fixed-size arrays (`activeChildIndex`) and recursive `TickNode` dispatch. Each `Evaluate(blackBoard)` performs a single tick from the root, resuming RUNNING branches via `activeChildIndex`.

### 2.9 `BehaviourTreeRunnerBase`

File: `Assets/BehaviourTree/Runtime/BehaviourTreeRunnerBase.cs`

Shared base class for `AgentTreeRunner` and `CommanderTreeRunner`. Handles:
- Resolving the runtime asset (`RuntimeAssetHelper.Resolve` — editor in-memory autobake, or `Resources/BakedTrees/{guid}` in builds)
- BB initialization (`blackBoard.Initialize(runtimeAsset.blackboardDefinition)`)
- Evaluator creation
- Debug provider setup
- Tracked binding resolution and pushing

---

## 3. Editor Layer — Authoring Tools

### 3.1 `BlackBoardView` (UI Toolkit)

File: `Assets/BehaviourTree/Editor/BlackBoardView.cs`

A `VisualElement` that renders the blackboard variable list inside the behaviour tree editor. Features:
- **ListView** with reorderable, animated drag-and-drop
- **Variable creator**: type search popup → creates `BlackboardVariable<T>` by reflection
- **Inline editing**: name (via `TextField`), type (via `DropdownField` with type registry), stride (via `IntegerField`)
- **Value cell**: typed field editors for common types; foldout for arrays
- **Rename detection**: periodic polling detects name changes and propagates them via `VariableChangePropagator`
- **Type change detection**: similar polling for type changes
- **System variable protection**: disabled name/type/stride fields, hidden delete button
- **SquadData row styling**: visual distinction for dynamic-stride variables

### 3.2 `BlackBoardEditor` (IMGUI Inspector)

File: `Assets/BehaviourTree/Editor/BlackBoardEditor.cs`

Custom inspector for the `BlackBoard` component. Shows **per-component overrides**:

**Reference types** (GameObject, Transform, etc.):
- Shows the definition's value read-only with an "Override" button
- When overridden: editable `ObjectField` + "X" button to revert
- Tracks override state in `overriddenReferenceSlots` on the `BlackBoard`

**Value types** (int, float, bool, Vector2/3/4, Color, enum):
- Same override/X pattern
- Override values stored as `BlackboardValueOverride` on the `BlackBoard` component
- Keyed by `variableName + elementIndex` — survives definition reorders

**Layout change detection**: computes a hash of `(Name, Stride)` entries. On mismatch, remaps serialized references and rebuilds override state from persisted data.

### 3.3 `TrackedVariablesView` (UI Toolkit)

File: `Assets/BehaviourTree/Editor/TrackedVariablesView.cs`

Editor UI for creating tracked bindings (component field → BB variable). Shows a `ListView` of `TrackedBinding` entries. Each row:
- **Member button**: opens `ComponentMemberSearchProvider` to pick a component.field
- **Variable button**: opens `VariableSearchPopup` filtered by member type
- **Remove button**
- **Validation**: highlights incomplete bindings in yellow

Bindings are grouped by tree asset (`TrackedBindingGroup`) with GUID matching.

### 3.4 `VariableChangePropagator`

File: `Assets/BehaviourTree/Editor/Propagation/VariableChangePropagator.cs`

When variables are renamed, deleted, or type-changed, this propagator notifies all registered handlers:
- `TreeNodesPropagationHandler` — updates node field bindings across all trees
- `TrackedBindingsPropagationHandler` — updates tracked binding references on runners
- `SquadBindingsPropagationHandler` — updates squad variable binding references

### 3.5 `BlackboardEnumGenerator`

File: `Assets/BehaviourTree/Editor/BlackboardEnumGenerator.cs`

Auto-generates `BlackboardEnums.cs` at `Assets/BehaviourTree/Runtime/Execution/Generated/` containing typed enum keys for each tree asset's blackboard variables. Regenerated via Tools → BehaviourTree → Generate Blackboard Enums.

---

## 4. Supporting Core Types

### 4.1 `SquadDefinition` (ScriptableObject)

File: `Assets/BehaviourTree/Core/SquadDefinition.cs`

Defines a squad's shared data schema, roles, and per-tree variable bindings. Contains:
- `BlackboardDefinition blackboardDefinition` — the squad's own variable schema
- `List<SquadRole> availableRoles` — role definitions
- `List<SquadBindingGroup> bindingGroups` — per-tree variable mappings

Auto-binds base channels (`AgentRoles`, `AgentOrders`) on `OnValidate()`:
- **Commander trees**: receives roles FROM squad, pushes orders TO squad
- **Agent trees**: pushes role TO squad, receives orders FROM squad

`EnsureStrideApplied(maxAgents)` applies the commander's agent count as stride to all squad-data variables before BB initialization.

### 4.2 `SharedVarAttribute` / `SharedArrayAttribute`

File: `Assets/BehaviourTree/Core/BTreeVarAttribute.cs`

Custom attributes for node fields:
- `[SharedVar]` — marks a field as a blackboard variable reference. Supports `IsToggleVariable`, `IsRoleDropdown`, `IsHidden` (auto-bind), `IsOrderDropdown`.
- `[SharedArray]` — marks a field as requiring a strided (array) variable.

### 4.3 `BlackboardEnums` (auto-generated)

File: `Assets/BehaviourTree/Runtime/Execution/Generated/BlackboardEnums.cs`

Auto-generated enum types mapping tree→blackboard→variable names to integer indices. Example:
```csharp
public enum BehaviourTreeAsset_BB_Keys : int {
    IdleTimer = 0
}
public enum TreeTestB_BB_Keys : int {
    TIMER = 0, POSITION = 1, HEALTHTHRESHHOLD = 2,
    HEALTH = 3, TESTRUNNER = 4, TESTRUNNER2 = 5
}
```

---

## 5. Key Data Flow Patterns

### Pattern A: Simple AgentTree

```
[Component.field] → TrackedBinding.Push → BlackBoard.SetBoxed(slot, value)
                                                       ↓
[Behaviour Tree] → FieldReader.Get<T>(fieldIndex) → BlackBoard.Get<T>(slot)
                     (via TreeEvaluator/TickContext)
```

### Pattern B: Commander + Agent + Squad

```
         ┌─────────────┐    ┌──────────────────┐    ┌─────────────┐
         │ Squad BB    │←──→│ Commander BB     │    │ Agent BB    │
         │ (SquadInst) │    │ (CommanderRunner) │    │ (AgentRun)  │
         └─────────────┘    └──────────────────┘    └─────────────┘
                ↑                                          ↑
                └──────────── SquadInstance.Copy ──────────┘
                      (with agentOffset=i per agent)
```

### Pattern C: Method → BB read/write

```
Node method (e.g. SetVariable) 
  → DeserializeParameters: reads slot indices from FieldData[] (baked at build time)
  → Execute: BB.GetBoxed(slot) / BB.SetBoxed(slot, value)
```

---

## 6. For AgentTree Reimplementation (Enemy as Agent)

When implementing the enemy as an AgentTree, you'll need:

1. **`AgentTreeRunner`** component on the enemy GameObject (already requires `BlackBoard`)
2. **`BlackBoard`** component (auto-added via `[RequireComponent]`)
3. **Tree asset** assigned to the runner (`.asset` file with a `BlackboardDefinition`)
4. **Variables** defined on the tree's `BlackboardDefinition` (e.g., `Health` float, `Target` Transform, `PatrolPoints` Vector3[])
5. **Tracked bindings** (optional) to push component values (e.g., `Health.health → Health`) into the BB each frame
6. If working with squads: **`SquadInstance`** for inter-agent/commander communication

Key APIs you'll use in code:
- `BlackBoard.Set<T>(name, value)` — write to a named variable (finds index by name)
- `BlackBoard.Get<T>(name)` — read from a named variable
- `AgentTreeRunner.Initialize()` — bake & init the runner
- `AgentTreeRunner.Evaluate()` — tick the tree once

The existing code pattern:
```csharp
spawnedObj.GetComponent<BlackBoard>().Set("PatrolPoints", patrolPoints);
```
This is the pattern for setting initial values on spawned agents — write to the BlackBoard by variable name after initialization.

---

## 7. Complete File Index

### Core (`Assets/BehaviourTree/Core/`)
| File | Role |
|---|---|
| `BlackBoard.cs` | MonoBehaviour data holder, IBlackBoardAccess |
| `BlackboardVariableBase.cs` | Abstract base for all variable types |
| `BlackboardVariable.cs` | Typed generic variable (BlackboardVariable\<T\>) |
| `BlackBoardDefinition.cs` | ScriptableObject schema (variable list) |
| `TypedBlackboardStorage.cs` | Typed-array storage backend (+ typed accessors, slot versions) |
| `BlackboardStorageLayout.cs` | Virtual-slot → typed-array mapping (SlotLocation) |
| `IBlackboardTypedAccess.cs` | Allocation-free typed accessor contract |
| `IBlackboardStorage.cs` | Storage interface |
| `IBlackBoardAccess.cs` | Read/write interface |
| `BlackboardValueOverride.cs` | Per-component typed override storage |
| `BlackboardVariableJsonUtility.cs` | JSON serialization for variables |
| `FieldData.cs` | Packed 5-byte parameter unit |
| `BTreeVarAttribute.cs` | `[SharedVar]` and `[SharedArray]` attributes |
| `SquadDefinition.cs` | Squad schema, roles, bindings |
| `FieldTypeHelper.cs` | Type resolution and display names |
| `NodeData.cs` | Baked node data structure |
| `NodeFieldEntry.cs` | Editor-side field entry |
| `NodeMethod.cs` | Base class for node methods |
| `NodeMethodAttribute.cs` | `[NodeMethod]` attribute |
| `NodeState.cs` | SUCCESS/FAILURE/RUNNING enum |
| `OrderRegistry.cs` | Order definitions |
| `SquadConnection.cs` | Squad connection data |
| `SquadRole.cs` | Role definition |
| `AbortType.cs` | Abort type enum |
| `BehaviourTreeAssetBase.cs` | Base tree asset class |

### Runtime (`Assets/BehaviourTree/Runtime/`)
| File | Role |
|---|---|
| `FieldReader.cs` | ref struct for reading FieldData + BB |
| `TrackedBinding.cs` | Component field → BB variable binding |
| `BehaviourTreeRunnerBase.cs` | Shared runner base class |
| `TreeRunner.cs` | AgentTreeRunner implementation |
| `CommanderTreeRunner.cs` | Commander orchestration runner |
| `SquadInstance.cs` | Squad BB with bidirectional copy |
| `TreeEvaluator.cs` | Tick-based tree evaluator |
| `TickContext.cs` | Per-frame evaluation context |
| `TreeBaker.cs` | Bakes authoring assets to runtime data |
| `Methods/VariableMethods.cs` | SetVariable, CompareVariable, CheckVariable, etc. |

### Editor (`Assets/BehaviourTree/Editor/`)
| File | Role |
|---|---|
| `BlackBoardView.cs` | UI Toolkit variable list editor |
| `BlackBoardEditor.cs` | IMGUI per-component override inspector |
| `TrackedVariablesView.cs` | UI Toolkit tracked binding editor |
| `Propagation/VariableChangePropagator.cs` | Variable rename/delete/type-change propagation |
| `Propagation/TreeNodesPropagationHandler.cs` | Updates node bindings on variable changes |
| `Propagation/TrackedBindingsPropagationHandler.cs` | Updates tracked bindings on variable changes |
| `Propagation/SquadBindingsPropagationHandler.cs` | Updates squad bindings on variable changes |
| `BlackboardEnumGenerator.cs` | Auto-generates enum keys per tree |
