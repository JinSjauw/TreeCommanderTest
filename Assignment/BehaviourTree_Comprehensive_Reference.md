# Behaviour Tree — Complete Node Reference

## Node Return Values

| Colour | Node Type | Returns |
|--------|-----------|---------|
| 🔴 Red | **Action** | SUCCESS, FAILURE, or **RUNNING** (continues across frames) |
| 🟡 Yellow | **Condition** | **Only** SUCCESS or FAILURE (instant, never RUNNING) |
| 🟤 Brown | **Decorator** | Transforms child's result |
| 🔵 Blue | **Selector** | SUCCESS / FAILURE / RUNNING (from children) |
| 🟣 Purple | **Sequence** | SUCCESS / FAILURE / RUNNING (from children) |
| 🟣 Magenta | **Parallel** | SUCCESS / FAILURE / RUNNING (from children) |
| 🔵 Cyan | **Priority** | SUCCESS / FAILURE / RUNNING (from children) |

---

## Composites

### SELECTOR — "OR" / Fallback

> *"Try this, else try that."* Runs children left→right, stops on first SUCCESS. Returns FAILURE only if all children fail.

```
[🔵 SELECTOR]
╱       ╲
[🔴 A]   [🔴 B]
```

| Child results | Returns |
|---------------|---------|
| A → SUCCESS | SUCCESS (B never runs) |
| A → FAILURE, B → SUCCESS | SUCCESS |
| All → FAILURE | FAILURE |
| Any → RUNNING | RUNNING (resumes that child) |

### SEQUENCE — "AND" / Chain

> *"Do this, then that."* Runs children left→right, stops on first FAILURE. Returns SUCCESS only if all succeed.

```
[🟣 SEQUENCE]
╱       ╲
[🟡 C]   [🔴 A]
```

| Child results | Returns |
|---------------|---------|
| All → SUCCESS | SUCCESS |
| Any → FAILURE | FAILURE (stops, later children never run) |
| Any → RUNNING | RUNNING (resumes that child) |

### PARALLEL — "All At Once"

Runs all incomplete children every frame. Returns FAILURE immediately if any child fails. SUCCESS only when all succeed.

> **Constraint**: Only direct ACTION and CONDITION children are evaluated. Nested composites under PARALLEL are treated as leaves.

### PRIORITY — "Interruptible Selector"

Like SELECTOR but re-evaluates all children every frame. If a higher-priority child becomes ready while a lower-priority one is RUNNING, it **interrupts** the lower child.

| Child results | Returns |
|---------------|---------|
| High → SUCCESS | SUCCESS (interrupts Low if it was RUNNING) |
| All → FAILURE | FAILURE |
| Any → RUNNING | RUNNING (but may be preempted next frame) |

---

## Decorators

### INVERTER

Inverts child result. RUNNING passes through unchanged.

| Child returns | Inverter returns |
|---------------|-----------------|
| SUCCESS | **FAILURE** |
| FAILURE | **SUCCESS** |
| RUNNING | RUNNING |

**Optional overrides**: `alwaysFailure` / `alwaysSuccess` — forces a fixed result, ignoring child.

### REPEATER

Repeats child N times. Returns RUNNING between repetitions. FAILURE if child fails mid-repeat.

| Step | Returns |
|------|---------|
| After 1st iteration | RUNNING (1/N) |
| After Nth iteration | **SUCCESS** |
| Child fails mid-repeat | **FAILURE** |

---

## Enemy Conditions (🟡 Yellow — Agent-only)

### Enemy_Detected
Runs target detection. Checks if any target is in range (optionally with LOS).

| Parameter | Kind | Description |
|-----------|------|-------------|
| Radius | SO Constant | Detection range |
| Operation | Operation | `LessThan` or `GreaterThan` |
| Check LOS | Constant (bool) | Whether to require line of sight |

### Enemy_HasArrived
Checks whether the NavMeshAgent has reached its destination.

*No parameters.*

### Enemy_HasLineOfSight
Checks clear line of sight to a target.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Target | Variable (Transform) | The target to check LOS against |

### Enemy_CheckRange
Checks 2D distance from enemy to a target against a radius.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Target | Variable (Transform/GameObject/Vector3) | Target to measure distance to |
| Radius | Toggle (float) | The radius to compare against |
| Operation | Operation | `LessThan` or `GreaterThan` |

---

## Enemy Actions (🔴 Red — Agent-only)

### Enemy_FireSequence
Full fire pipeline: aim turret → search trajectory → fire delay → fire.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Target | Variable (Transform) | Target to aim at and fire at |
| Fire Delay | SO Constant (float) | Delay after trajectory before firing |
| Spread | SO Constant (float) | Accuracy spread |
| Reload | SO Constant (float) | Reload duration after firing |
| Damage | SO Constant (int) | Damage per projectile |
| Trajectory Always Indirect | Constant (bool) | Force indirect trajectory |
| Trajectory Starting Height | SO Constant (float) | Starting height for trajectory search |

**RUNNING while**: any phase (aiming, trajectory search, fire delay, firing/reloading).

### Enemy_SelectDetectedTarget
Picks a target from detected enemies by strategy.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Selection Strategy | Operation | `Nearest`, `Farthest`, or `Random` |
| Output | Variable (Transform/GameObject) | Where to write the selected target |

*Instant — returns FAILURE if no targets detected.*

### Enemy_EngagePosition
Calculates a random position within a tunable cone directed toward the target.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Output Position | Variable (Vector3) | Where to write the result |
| Target | Variable (Transform) | The target to approach |
| Cone Angle | SO Constant (float) | Random spread half-angle |
| Min Dist | SO Constant (float) | Minimum offset from current position |
| Max Dist | SO Constant (float) | Maximum offset from current position |
| Maintain Dist | SO Constant (float) | Ideal distance from target |

*Instant.*

### Enemy_FlankPosition
Computes a random flanking position perpendicular to the target direction.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Output Position | Variable (Vector3) | Where to write the result |
| Target | Variable (Transform) | The target to flank |
| Flank Angle | SO Constant (float) | Base rotation from perpendicular toward target |
| Cone Angle | SO Constant (float) | Random spread half-angle |
| Min Dist | SO Constant (float) | Minimum offset |
| Max Dist | SO Constant (float) | Maximum offset |
| Maintain Dist | SO Constant (float) | Ideal distance from target |

*Instant.*

### Enemy_StopMovement
Immediately stops the NavMeshAgent.

*No parameters — instant.*

---

## Other Agent Actions (🔴 Red — Agent-only)

These nodes are available in agent trees only (not commander). They are not enemy-specific — any agent tree can use them.

### ReportStatus
Writes a status value to an int blackboard variable (typically `AgentStatus`). The squad bridge syncs this to the squad blackboard where the commander's `PollAgentStatus` reads it.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Target | Variable (int) | The agent's status blackboard variable (e.g. `AgentStatus`) |
| Value | Operation | `Success` (0), `Failure` (1), or `Running` (2) |

*Instant.*

### SetNavAgentSpeed
Sets the NavMeshAgent.speed on this agent's GameObject. Caches the original speed and restores it when the subtree is aborted.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Speed | ScriptableObjectConstant (float) | The speed to set |

*Instant.*

---

## Generic Actions (🔴 Red — Any tree type)

### MoveTo
Sets NavMeshAgent destination to a Vector3 or Transform and waits until arrival.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Target | Variable (Vector3/Transform) | Destination |

**RUNNING while**: moving toward destination.

### ExtractPosition
Selects the next position from a collection element (Transform children, array elements).

| Parameter | Kind | Description |
|-----------|------|-------------|
| Collection | Variable (Transform/GameObject/Transform[]/GameObject[]/Vector3[]) | The collection to pick from |
| Output | Variable (Vector3) | Where to write the selected position |
| Mode | Operation | `Sequential` or `Random` |

*Instant.*

### SetVariable
Writes a value (constant or from another variable) into a blackboard variable.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Target | Variable (any) | The blackboard variable to write to |
| Value | Toggle (any) | Source: constant value or another blackboard variable (type synced from Target) |

*Instant.*

### ClearVariable
Resets a blackboard variable to its type default (null/zero).

| Parameter | Kind | Description |
|-----------|------|-------------|
| Target | Variable (any) | The variable to clear |

*Instant.*

### LogVariable
Logs a blackboard variable's value to the Unity console (editor-only).

| Parameter | Kind | Description |
|-----------|------|-------------|
| Variable | Variable (any) | The variable to log |

*Instant.*

### Toggle
Flips a boolean blackboard variable.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Variable | Variable (bool) | The bool to flip |

*Instant.*

### WaitSeconds
Waits N seconds. Manages its own internal timer.

| Field | Type | Description |
|-------|------|-------------|
| duration | float | Seconds to wait |

**RUNNING while**: waiting.

### Cooldown
Returns SUCCESS once per cooldown period, FAILURE while on cooldown.

| Field | Type | Description |
|-------|------|-------------|
| duration | float | Cooldown period in seconds |
| remaining | float (SharedVar) | Internal timer — shared so multiple nodes can sync |

---

## Generic Conditions (🟡 Yellow — Any tree type)

### CompareVariable
Compares a blackboard variable against a value (constant or another variable).

| Parameter | Kind | Description |
|-----------|------|-------------|
| Operand A | Variable (any) | The variable to compare |
| Compare With | Toggle (any) | Value to compare against (constant or variable, type synced from A) |
| Operation | Operation | `Equal`, `NotEqual`, `Less`, `LessOrEqual`, `Greater`, `GreaterOrEqual`, `MagnitudeLess`, `MagnitudeLessOrEqual`, `MagnitudeGreater`, `MagnitudeGreaterOrEqual` |

Available operations are filtered by type: numeric types get all comparisons, reference/bool types get Equal/NotEqual only, vector types get all including magnitude.

### HasChanged
Returns SUCCESS if the variable's value changed since the last tick.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Variable | Variable (any) | The variable to monitor |

---

## Commander Nodes

### ForEachAgent
Iterates over all registered agents in the squad. Children run once per agent.

*Leaf children within the ForEachAgent scope receive the agent's current offset automatically.*

### ForEachRole
Iterates over all unique roles defined in the squad definition. Children run once per role.

### SendOrder
Issues an order to selected agents.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Order | Order dropdown | The order to issue (from OrderRegistry) |

### CheckSquadData
Reads a squad-level data variable.

### PollAgentStatus
Polls a SquadData int[] status array (e.g. `AgentStatus`) across all registered agents. Used by the commander to check if all agents have completed their current order.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Input | Variable (int[]) | The SquadData int array to poll (e.g. `AgentStatus`) |

**Return values:**
- **SUCCESS** — all agents report `0` (idle / arrived / Success). Has a consumed gate: after returning SUCCESS once, it refuses another SUCCESS until at least one agent reports `2` (Running) again, confirming a new movement cycle has started.
- **RUNNING** — at least one agent reports `2` (Running / still moving).
- **FAILURE** — any agent reports `1` (Failure / blocked).

### CalculateFormation
Computes formation positions for the squad. Agents are positioned in a circle: agent 0 at center, agents 1..N-1 evenly distributed around.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Center | Toggle (Vector3) | Center position of the formation |
| Output | Variable (Vector3[]) | Where to write the formation positions |
| Type | Operation (FormationType) | Formation shape (only `Circle` currently) |
| Radius | ScriptableObjectConstant (float) | Radius of the circle formation |

*Instant.*

### SquadReduce
Reduces a per-agent squadData array by computing the average, lowest, or highest value across all active agents.

| Parameter | Kind | Description |
|-----------|------|-------------|
| Source | Variable (int[]/float[]/Vector2[]/Vector3[]) | The per-agent array to reduce |
| Op | Operation | `Average`, `Lowest`, or `Highest` |
| Output | Variable (single) | Where to write the reduced result |

---

## DynamicParamDescriptor UI Reference

When you select a node, its fields appear in the Inspector. Each field's appearance depends on its `DynamicParamKind`:

### Variable (V)
A dropdown of matching blackboard variables. Only variables of the correct type + stride (single vs array) appear.

```
 ┌──────────────────────────────────────────────┐
 │  Target : Transform                          │
 │  ┌────────────────────────────────────────┐  │
 │  │ selectedTarget                      ▼ │  │
 │  └────────────────────────────────────────┘  │
 └──────────────────────────────────────────────┘
```

### Constant (C)
A regular input field rendered according to the type: number field for int/float, toggle for bool, object field for Unity references, vector fields for Vector2/3, colour picker for Color.

### Toggle (C / V)
Toggleable between constant and shared variable. The button shows the opposite mode:

| Button | Current mode | Click to switch to |
|--------|-------------|-------------------|
| **C** | Variable (V) | Constant |
| **V** | Constant (C) | Variable |

### Operation
A dropdown of enum values. Available options may be filtered based on the selected variable's type (e.g. CompareVariable shows fewer ops for bool types).

### ScriptableObjectConstant (C / V / SO)
Three-way toggle between Constant, Variable, and ScriptableObject field reference.

| Button | Mode | Behaviour |
|--------|------|-----------|
| **C** | Constant | Regular input field |
| **V** | Variable | Blackboard variable dropdown |
| **SO** | SO Field | Opens a search window to pick a field from any ScriptableObject in the tree's **Config Sources** (configured in the tree asset inspector) |

In **SO mode**, the field displays as `ConfigName.fieldName`. Click the button to re-pick. The value is resolved at bake time into a packed constant — zero runtime overhead.

The toggle cycles **C → V → SO → C → ...** each time you click the label.

---

## Order of Field Evaluation

1. **ResolveInputsGeneric** — SharedVar fields are read from the blackboard before Execute()
2. **Execute()** — The node's logic runs
3. **WriteOutputsGeneric** — SharedVar outputs are written back to the blackboard after Execute()

For `[SharedVar]` fields with `isOutput = true` (the default), the value is written back after Execute. Fields marked as toggle inputs (`isOutput = false`) are read-only.

---

## Node Compatibility

Nodes are restricted to certain tree types via `[NodeMethod(allowedTreeType)]`:

| allowedTreeType | Visible in editor for |
|----------------|----------------------|
| `Any` | Agent and Commander trees |
| `Agent` | Agent trees only |
| `Commander` | Commander trees only |

Enemy-specific nodes (`Enemy_*`) are `Agent`-only. Commander nodes (`ForEachAgent`, `SendOrder`, etc.) are `Commander`-only. Generic nodes (`SetVariable`, `CompareVariable`, `MoveTo`, etc.) are `Any`.
