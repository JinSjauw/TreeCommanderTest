# Behaviour Tree Cheat Sheet

## Node Colours

| Colour | Node Type | Return Values |
|--------|-----------|---------------|
| 🟢 Green | **Root** | Passes through child result. |
| 🔵 Blue | **Selector** | SUCCESS / FAILURE / RUNNING (from children) |
| 🟣 Purple | **Sequence** | SUCCESS / FAILURE / RUNNING (from children) |
| 🟣 [Magenta] | **Parallel** | SUCCESS / FAILURE / RUNNING (from children) |
| 🔵 [Cyan] | **Priority** | SUCCESS / FAILURE / RUNNING (from children) |
| 🔴 Red | **Action** | SUCCESS, FAILURE, or **RUNNING** |
| 🟡 Yellow | **Condition** | **Only** SUCCESS or FAILURE (never RUNNING) |
| 🟤 Brown | **Decorator** | Modifies child result (Inverter, Repeater) |
| 🟢 YellowGreen | **Subtree** | Passes through child tree result. |

---

## Node Return Rules

### 🔴 Actions (Red)

Actions **can** take multiple frames. When an action returns **RUNNING**, the tree pauses on that branch and resumes it next frame.

```
Frame 1: MoveTo → RUNNING (still moving)
Frame 2: MoveTo → RUNNING (still moving)
Frame 3: MoveTo → SUCCESS  (arrived!)
```

### 🟡 Conditions (Yellow)

Conditions are **instant**. They check something right now and answer yes/no. They **never** return RUNNING.

```
Frame 1: Enemy_Detected → SUCCESS (target found!)
Frame 1: Enemy_Detected → FAILURE (no targets)
```

---

## Composite Nodes

All composite descriptions below are sourced from the [TooltipRegistry](file:///d:/Dev/TreeCommanderTest/Assets/Scripts/BehaviourTree/TooltipRegistry.asset).

---

### 🔵 SELECTOR — "OR" / Fallback

> *"Executes children in order until one returns SUCCESS. Returns FAILURE only if all children fail."*

```
      [🔵 SELECTOR]
      ╱       ╲
 [🔴 Do A]   [🔴 Do B]
```

| What happens | Result |
|-------------|--------|
| Child A → SUCCESS | Selector → **SUCCESS** (stops, B never runs) |
| Child A → FAILURE | Try child B |
| Child A → FAILURE, B → SUCCESS | Selector → **SUCCESS** |
| A → FAILURE, B → FAILURE | Selector → **FAILURE** |
| Child A → RUNNING | Selector → **RUNNING** (resumes A next frame) |

> **Use for**: "Try this, else try that." Only falls back when earlier children fail.

---

### 🟣 SEQUENCE — "AND" / Chain

> *"Executes children in order until one returns FAILURE. Returns SUCCESS only if all children succeed."*

```
      [🟣 SEQUENCE]
      ╱       ╲
 [🟡 Check]   [🔴 Do A]
```

| What happens | Result |
|-------------|--------|
| Check → SUCCESS, Do A → SUCCESS | Sequence → **SUCCESS** |
| Check → **FAILURE** | Sequence → **FAILURE** (stops, Do A never runs) |
| Check → SUCCESS, Do A → **FAILURE** | Sequence → **FAILURE** |
| Do A → RUNNING | Sequence → **RUNNING** (resumes Do A next frame) |

> **Use for**: "Do this, then that." All steps must succeed. First failure aborts the chain.

---

### 🟣 [MAGENTA] PARALLEL — "All At Once"

> *"Executes all incomplete children each tick in parallel. Returns FAILURE immediately if any child fails. Stays RUNNING while any child is still running. Returns SUCCESS only when all children have succeeded."*

```
      [🟣 PARALLEL]
      ╱           ╲
 [🟡 Check]     [🔴 Action]
```

| What happens | Result |
|-------------|--------|
| Both → SUCCESS | Parallel → **SUCCESS** |
| Either → **FAILURE** | Parallel → **FAILURE** immediately |
| Some still RUNNING, none failed | Parallel → **RUNNING** |

> **Important**: Only direct ACTION and CONDITION children are evaluated. Nested composites under PARALLEL are treated as leaves (not recursed into).

> **Use for**: "Do multiple checks at the same time."

---

### 🔵 [CYAN] PRIORITY — "Interruptible Selector"

> *"Like a Selector but children get run every evaluation cycle regardless of last node state."*

```
      [🔵 PRIORITY]
      ╱           ╲
 [Seq: High]    [Seq: Low]
```

| What happens | Result |
|-------------|--------|
| High child → SUCCESS | Priority → **SUCCESS** |
| High → FAILURE, Low → SUCCESS | Priority → **SUCCESS** |
| All children → FAILURE | Priority → **FAILURE** |
| Low child → RUNNING | Priority → **RUNNING**, but... |

**Key difference from SELECTOR**: If the Low child is RUNNING and the High child becomes ready (SUCCESS), PRIORITY **interrupts** Low and switches to High. SELECTOR would wait for Low to finish.

> **Use for**: Higher-priority tasks that should **preempt** lower-priority ones mid-execution.

---

## Conditional Aborts

Aborts let a composite **interrupt** a running child branch when a condition changes. Set the **Abort Type** field on a composite node in the Inspector.

### Abort Types

| Type | Behaviour |
|------|-----------|
| `None` | No abort checking. Default. |
| `Self` | While a branch is RUNNING, re-evaluates its **own** condition children each frame. If a condition that was SUCCESS becomes FAILURE, the running branch is **aborted** and the composite re-evaluates from the leftmost child. |
| `LowerPriority` | While a lower-priority (rightward) branch is RUNNING, checks whether any higher-priority (leftward) sibling's condition becomes SUCCESS. If so, the lower branch is aborted and the higher branch runs instead. |
| `Both` | Combines Self + LowerPriority behaviour. |

### How Self Abort Works

```
Frame 1:  [SEQUENCE]  abortType=Self
           ├── [🟡] IsInRange → ✅ SUCCESS
           └── [🔴] MoveTo → 🔄 RUNNING ←

Frame 50: [SEQUENCE]  abortType=Self
           ├── [🟡] IsInRange → ❌ FAILURE  ← changed!
           └── [🔴] MoveTo → 🛑 ABORTED (OnAbort called)
          → Sequence restarts from leftmost child
```

The condition is re-evaluated every frame while a child to its right is RUNNING. When the condition flips from SUCCESS to FAILURE, the running child is aborted via `OnAbort()` and the composite restarts.

### How LowerPriority Abort Works

```
[🔵 SELECTOR]  abortType=LowerPriority
├── [🟣 SEQUENCE] High priority
│    ├── [🟡] EnemyDetected? → changes to ✅
│    └── [🔴] Attack
└── [🟣 SEQUENCE] Low priority (currently 🔄 RUNNING)
     └── [🔴] PatrolMove
```

When `EnemyDetected?` transitions from FAILURE → SUCCESS, the PatrolMove branch is aborted and the Attack branch starts — even if PatrolMove was mid-execution.

### Editor Validation

When you set an abort type other than `None`, the editor checks whether the composite has a **reachable Condition node** as a descendant. If not, a warning icon appears on the node and the Inspector shows:

> *"No reachable Condition node found. Add a Condition node as a descendant for this abort type to take effect."*

Conditions can be:
- Direct children of the composite
- Inside child composites that have a compatible abort type (Self/Both for Self check, LowerPriority/Both for LP check)
- Inside decorators or subtrees

### Layout Rules

| Rule | Details |
|------|---------|
| **Self abort checks left→right** | Conditions are evaluated left to right. The first condition in document order determines the abort. |
| **LowerPriority checks left→right** | Higher-priority (left) siblings are checked first. The first one whose condition flips to SUCCESS triggers the abort. |
| **Re-evaluation scope** | Only conditions at the composite level or in child composites with matching abort types are re-evaluated. |
| **OnAbort** | When a branch is aborted, the running node's `OnAbort()` method is called (e.g., MoveTo calls `agent.isStopped = true`). |

### Common Use Cases

| Pattern | Abort Type | Why |
|---------|------------|-----|
| Attack-if-in-range while patrolling | `LowerPriority` on the root SELECTOR | If a target appears, abort patrol and attack. Resume patrolling when target is lost. |
| Stay-in-cover while reloading | `Self` on the cover SEQUENCE | If the cover position is no longer safe, abort and find new cover. |
| Squad scatter on leader death | `LowerPriority` on the root PRIORITY | As soon as LeaderIndex = -1, abort all current orders and scatter. |

---

## Decorators

| Colour | Decorator | What it does |
|--------|-----------|-------------|
| 🟤 Brown | **INVERTER** | Inverts child: SUCCESS → FAILURE, FAILURE → SUCCESS. RUNNING passes through. |
| 🟤 Brown | **REPEATER** | Repeats child N times. Returns RUNNING between repetitions. |

### INVERTER — From TooltipRegistry

> *"Inverts the result of its child node. SUCCESS becomes FAILURE and vice versa. RUNNING passes through unchanged. Can optionally force always success or always failure."*

```
 [🟤 INVERTER]
      │
 [🟡 Detected?]  → SUCCESS (target found)
      ↓
 INVERTER         → FAILURE
```

| Child returns | Inverter returns |
|---------------|-----------------|
| SUCCESS | **FAILURE** |
| FAILURE | **SUCCESS** |
| RUNNING | RUNNING |

Optional overrides: `alwaysFailure` / `alwaysSuccess` ignore child and force a fixed result.

### REPEATER — From TooltipRegistry

> *"Repeats its child node a specified number of times. Returns RUNNING until the target count is reached."*

```
 [🟤 REPEATER]  (targetCount: 3)
      │
 [🔴 Action]  → runs once per repeat
```

| Step | Child returns | Repeater returns |
|------|--------------|-----------------|
| After 1st fire | SUCCESS | RUNNING (1/3) |
| After 2nd fire | SUCCESS | RUNNING (2/3) |
| After 3rd fire | SUCCESS | **SUCCESS** (3/3) |
| Child fails mid-repeat | FAILURE | **FAILURE** |

---

## Enemy-Only Nodes (Agent Trees)

> These nodes are only available in Agent trees (agent-only). For generic nodes available in any tree type, see the Generic Variable Nodes section below.

### 🟡 Conditions

| Node | Description | Returns |
|------|-------------|---------|
| `Enemy_Detected` | Runs target detection. Current detections get saved. | SUCCESS if target detected; FAILURE if none. |
| `Enemy_HasArrived` | Checks whether the NavMeshAgent has reached its destination. | SUCCESS if arrived; FAILURE if not. |
| `Enemy_HasLineOfSight` | Checks a clear line of sight (no obstacles) to target. | SUCCESS if clear; FAILURE if blocked. |
| `CheckOrder` | Compares agent's received order against an expected order. | SUCCESS on match; FAILURE otherwise. |

### 🔴 Actions

| Node | Description | RUNNING while... |
|------|-------------|------------------|
| `Enemy_EngagePosition` | Calculates a random position within a tunable cone directed toward the target. | *(instant)* |
| `Enemy_FlankPosition` | Computes a random flanking position perpendicular to the target direction. | *(instant)* |
| `Enemy_StopMovement` | Immediately stops the NavMeshAgent. | *(instant)* |
| `Enemy_FireSequence` | Full fire cycle: aim → search trajectory → fire delay → fire. Handles reload, aiming, trajectory, and firing internally. | Any phase |
| `Enemy_SelectDetectedTarget` | Selects a target from detected targets (Nearest, Farthest, Random). Writes to a Transform/GameObject variable. | *(instant)* |
| `Enemy_CheckRange` | Checks 2D distance from enemy to a target (Transform/GameObject/Vector3) against a radius with LessThan/GreaterThan. | *(instant)* |
| `ReportStatus` | Writes own status (Success/Failure/Running) to the squad blackboard. | *(instant)* |
| `SetNavAgentSpeed` | Sets NavMeshAgent.speed on this agent's GameObject. Restores original on abort. | *(instant)* |

---

## Generic Variable Nodes

These replace the old BB_* nodes. They work with **any** blackboard variable type through dynamic parameter inspection.

### 🟡 Conditions

| Node | Description |
|------|-------------|
| `CompareVariable` | Compares a blackboard variable against a value (constant or another variable) using operations: =, ≠, <, >, ≤, ≥, or vector magnitude comparisons. |
| `HasChanged` | Returns SUCCESS if the variable's value changed since last tick. Works with any type. |

### 🔴 Actions

| Node | Description |
|------|-------------|
| `MoveTo` | Sets NavMeshAgent destination to a Vector3 or Transform and waits until arrival. |
| `ExtractPosition` | Selects next position from a collection (Sequential or Random mode). Writes to an output Vector3 variable. *(instant)* |
| `WaitSeconds` | Wait N seconds. RUNNING while waiting. |
| `Cooldown` | Returns SUCCESS once per cooldown period. FAILURE while on cooldown. |
| `SetVariable` | Writes a value (constant or from another variable) into a blackboard variable. Supports all types. |
| `ClearVariable` | Resets a blackboard variable to its type default (null/zero). |
| `LogVariable` | Logs a blackboard variable's value to the Unity console. Debug-only. |
| `Toggle` | Flips a boolean blackboard variable. |

---

## State Flow: What Happens When a Child Returns RUNNING

When an action like `MoveTo` returns RUNNING:

1. The parent composite sets itself to **RUNNING**.
2. It **remembers** which child is active (e.g., child 2 of 3).
3. Next frame, the evaluator resumes that **exact child** — it does NOT restart the composite from the beginning.
4. Children to the left have already finished. Children to the right haven't started yet.
5. When the running child finally returns SUCCESS or FAILURE, the composite processes it as normal.

```
Frame 1:  [SEQUENCE]
          ├── ✅ Done
          ├── 🔄 RUNNING ← resumes here next frame
          └── ⏳ Not started

Frame 2:  [SEQUENCE]
          ├── ✅ (skipped)
          ├── ✅ SUCCESS (finished!)
          └── ⏳ starts now
```

---

## Common Patterns

### Prioritized Task Selection
```
[🔵 SELECTOR]
├── [🟣 SEQUENCE] High priority
│   ├── [🟡] Condition
│   └── [🔴] Action
└── [🟣 SEQUENCE] Fallback
    ├── [🔴] Prepare
    └── [🔴] Execute
```
Condition fails? Fallback runs. Condition succeeds? Action runs.

### Interruptible Behavior
```
[🔵 PRIORITY]
├── [🟣 SEQUENCE] Urgent
│   ├── [🟡] Urgent check
│   └── [🔴] Urgent action
└── [🟣 SEQUENCE] Normal
    └── [🔴] Normal action
```
If Urgent check becomes true mid-frame, PRIORITY interrupts Normal action immediately.

### Rate-Limited Action
```
[🟣 SEQUENCE]
├── [🟡] Cooldown (duration: 3.0)
└── [🔴] Attack
```
Attack only fires when cooldown is ready. Cooldown → FAILURE blocks the sequence until it resets.

### Repeat N Times
```
[🟤 REPEATER] (count: 5)
└── [🔴] Any action
```
Repeats the child N times, then returns SUCCESS.

---

## Node Inspector Field Controls

When you select a node, its fields appear in the Inspector. Each field's appearance depends on its **kind**:

### Variable — dropdown + S + F buttons

A dropdown of matching blackboard variables. Two small buttons appear next to it:

| Button | What it does |
|--------|-------------|
| **S** | Opens a search popup — type to filter. Useful with many variables. |
| **F** | Opens a type picker to change which types are shown. Only appears on fields accepting multiple types. |

### Toggle — C / V switch

Toggles between Constant and Variable mode. The button shows the opposite mode:

| Button shown | Current mode | Click to switch to |
|--------------|-------------|-------------------|
| **C** | Variable (blackboard variable) | Constant (direct value) |
| **V** | Constant (direct value) | Variable (blackboard variable) |

### ScriptableObjectConstant — C / V / SO three-way switch

Each click on the label cycles to the next mode:

| Button | Mode | Behaviour |
|--------|------|-----------|
| **C** | Constant | Regular input field |
| **V** | Variable | Blackboard variable dropdown |
| **SO** | SO Field | Picks a field from a ScriptableObject in the tree's **Config Sources**. Resolved at bake time — zero runtime overhead. |

### Operation — enum dropdown

A dropdown of enum values. Options may be filtered by context (e.g. fewer comparison operators for bool types).

---

## Squad Definition Editor

The Squad Definition Editor (`BehaviourTree > Open Squad Editor`) is the central UI for configuring squads.

**SquadDefinition** is a ScriptableObject that defines a squad's shared data and role composition.

### Sections in the Editor

| Section | Purpose |
|---------|---------|
| **Blackboard** | Define squad-wide variables shared between commander and agents. Variables marked **SquadData** get dynamic per-agent stride at runtime. |
| **Roles** | Define available roles (name, colour, max amount, prefab, fallback flag). The sum of `maxAmount` across all roles determines the total squad size. |
| **Binding Groups** | Per-tree bindings that map variables between the squad blackboard and each connected tree's blackboard. System bindings (AgentRoles, AgentOrders, AgentStatus, LeaderIndex) are auto-created along with their blackboard variables. |

### How Bindings Work

Each **BindingGroup** connects one tree asset to the squad. Variable bindings define:
- **Direction**: `ToSquad` (tree → squad), `FromSquad` (squad → tree), or `Both`
- **System bindings** (auto-created): AgentRoles/AgentAssignedRole, AgentOrders/AgentReceivedOrder, AgentStatus, LeaderIndex

---

## Commander Module

Commander trees orchestrate multiple agent trees in a squad. They use the **PRIORITY** composite extensively for interruptible task assignment.

### Key Components

| Component | Role |
|-----------|------|
| **CommanderTreeAsset** (editor) | Tree asset with an additional **Commander Blackboard** for squad-data variables (per-agent arrays). Links to a `commanderSquad` SquadDefinition. |
| **CommanderTreeRunner** (runtime) | MonoBehaviour on the commander GameObject. Evaluates the commander tree, then ticks all registered agents each frame — in order. |
| **AgentTreeRunner** (runtime) | MonoBehaviour on each agent GameObject. Set `commander` and `squadInstance` references in the Inspector for manual setup, or let SquadManager wire them automatically. |
| **SquadManager** | Spawns commander + agents, assigns roles, handles leader death (promote/scatter). |

### Tick Order (each frame)

```
1. Commander tree evaluates (reads squad BB, writes orders)
2. For each agent (in registration order):
   a. Agent tracked bindings pushed to agent BB
   b. Squad → Agent BB copy (per-agent offset)
   c. Agent tree evaluates
   d. Agent BB → Squad copy
```

### System Blackboard Variables (auto-created)

When a binding group is set up, `EnsureAutoBindings()` creates these variables on the squad and tree blackboards, plus the bindings between them:

| Commander BB | Squad BB | Direction | Purpose |
|-------------|----------|-----------|---------|
| AgentRoles (int, SquadData) | AgentRoles (int, SquadData) | Squad → Commander | Each agent's assigned role index |
| AgentOrders (int, SquadData) | AgentOrders (int, SquadData) | Commander → Squad | Orders assigned to each agent |
| AgentStatus (int, SquadData) | AgentStatus (int, SquadData) | Both | Current status per agent |
| LeaderIndex (int) | LeaderIndex (int) | Squad → Commander | Index of current squad leader |

### SquadData Variables

Variables marked **isSquadData** in the Commander Blackboard get their stride dynamically sized to `maxSquadSize`. At runtime, each agent slot maps to index 0..N-1 in these arrays. The Commander tree writes to `AgentOrders[slot]` and reads `AgentStatus[slot]`.

---

## Scene-Level Blackboard Overrides

Variables defined in a tree asset get their defaults from the asset. To set different values per scene instance (e.g. different patrol routes, different targets), override them on the scene GameObject's `BlackBoard` component.

### Workflow

1. **Add a variable** to the tree asset's Blackboard tab (e.g. `patrolPointsParent` of type `Transform`)
2. **Select the scene GameObject** with the `AgentTreeRunner` or `CommanderTreeRunner` component
3. **Override in the Inspector** — the BlackBoard section shows all variables:

- **Reference types** (Transform, GameObject, Component): check the **✓** box and drag a scene object into the field
- **Value types** (int, float, bool, Vector2/3/4, Color, enum): edit the value directly

Unchecking clears the override but preserves the stored value — re-checking restores it.

### When to use

| Scenario | Example |
|----------|---------|
| **Scene references** | Drag a patrol route parent, spawn point, or target from the scene into a reference slot |
| **Per-instance tuning** | Different patrol speeds or health thresholds per enemy without separate tree assets |
| **Shared prefab, different data** | Multiple enemies share one tree asset but need different patrol points or target positions |

### Survival Guarantee

Overrides are stored **by variable name**, not slot index. Renaming a variable in the asset remaps existing overrides automatically. Adding/removing other variables does not affect them.
