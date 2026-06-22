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
Frame 1: Enemy_MoveTo → RUNNING (still moving)
Frame 2: Enemy_MoveTo → RUNNING (still moving)
Frame 3: Enemy_MoveTo → SUCCESS  (arrived!)
```

### 🟡 Conditions (Yellow)

Conditions are **instant**. They check something right now and answer yes/no. They **never** return RUNNING.

```
Frame 1: Enemy_DetectTarget → SUCCESS (target found!)
Frame 1: Enemy_DetectTarget → FAILURE (no targets)
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

## All EnemyMethods (from TooltipRegistry)

### 🟡 Conditions

| Node | Description | Returns |
|------|-------------|---------|
| `Enemy_DetectTarget` | Runs target detection. Current detections get saved. | SUCCESS if target detected; FAILURE if none. |
| `Enemy_HasArrived` | Checks whether the NavMeshAgent has reached its destination. | SUCCESS if arrived; FAILURE if not. |
| `Enemy_IsAimed` | Checks if the gun is currently aimed on target. | SUCCESS if on target; FAILURE if not. |
| `Enemy_IsInFiringRange` | Checks if the selected target is within firing range. | SUCCESS if in range; FAILURE if out. |
| `Enemy_HasLineOfSight` | Checks a clear line of sight (no obstacles) to target. | SUCCESS if clear; FAILURE if blocked. |

### 🔴 Actions

| Node | Description | RUNNING while... |
|------|-------------|------------------|
| `Enemy_MoveTo` | Sets NavMeshAgent destination and waits until arrival. | Moving |
| `Enemy_MoveTo_Transform` | Same as MoveTo but destination is a Transform's position. | Moving |
| `Enemy_SelectPatrolPoint` | Selects next patrol point from children of PatrolPointsParent. Writes to TargetMovePosition. | *(instant)* |
| `Enemy_SelectEngagePosition` | Calculates a new path position to approach the current target. | *(instant)* |
| `Enemy_StopMovement` | Immediately stops the NavMeshAgent. | *(instant)* |
| `Enemy_FireSequence` | Full fire cycle: select aim point → search trajectory → wait for turret → fire. Handles reload, aiming, trajectory, and firing internally. | Any phase |
| `Enemy_SelectDetectedTarget` | Selects a target from detected targets (Nearest, Farthest, Random). Writes to selectedTarget. | *(instant)* |

---

## Common Utility Nodes

### 🟡 Conditions

| Node | Description |
|------|-------------|
| `Cooldown` | Returns SUCCESS once per cooldown period. FAILURE while on cooldown. |
| `BB_CheckBool` | Check if blackboard bool is true/false. |
| `BB_CheckGameObject` | Check if GameObject is null/active. |
| `BB_CheckTransform` | Check if Transform is null/active. |
| `BB_CompareInt` / `Float` / `Bool` | Compare two values with operators (=, ≠, <, >). |
| `BB_EdgeRisingBool` | Detects false → true transition. SUCCESS on rising edge. |
| `BB_EdgeFallingBool` | Detects true → false transition. SUCCESS on falling edge. |
| `BB_HasChanged*` | SUCCESS if value changed since last tick. |

### 🔴 Actions

| Node | Description |
|------|-------------|
| `WaitSeconds` | Wait N seconds. RUNNING while waiting. |
| `BB_SetInt` / `Float` / `Bool` / etc. | Set blackboard variable. |
| `BB_ToggleBool` | Flip a bool. |
| `BB_Clear*` | Reset variable to default. |
| `BB_Log*` | Log a blackboard value to Unity console. |

---

## State Flow: What Happens When a Child Returns RUNNING

When an action like `Enemy_MoveTo` returns RUNNING:

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
