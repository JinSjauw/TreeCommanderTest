# Architecture Problems

## Node Palette Problems

### 1. Hard `GetComponent<EnemyController>()` coupling (CRITICAL)
6 out of 8 enemy BT nodes resolve `EnemyController` via `GetComponent` instead of the component they actually need.

| Node | What it actually needs | What it resolves |
|---|---|---|
| `Enemy_Detected` | `EnemyDetectionSystem` | `EnemyController` → `.Detection` |
| `Enemy_CheckRange` | `NavMeshAgent.transform` | `EnemyController` → `.Agent.transform` |
| `Enemy_HasLineOfSight` | `EnemyDetectionSystem` | `EnemyController` → `.HasLineOfSightToTarget()` |
| `Enemy_HasArrived` | `NavMeshAgent` | `EnemyController` → `.HasArrivedAtDestination()` |
| `Enemy_SelectDetectedTarget` | `EnemyDetectionSystem` | `EnemyController` → `.SelectTarget()` |
| `Enemy_StopMovement` | `NavMeshAgent` | `EnemyController` → `.StopMoving()` |

Every node goes through `EnemyController` as a middleman. If `EnemyController` is removed or refactored, all 6 break. Nodes should resolve the specific component they need.

### 2. Duplicated logic between BT nodes and components
- `Enemy_CheckRange` does its own 2D distance math, but `EnemyDetectionSystem.IsTargetInRange()` already exists.
- `Enemy_Detected` manually iterates targets and checks LOS, while `EnemyDetectionSystem` provides both range and LOS checks.
- Changing detection/ranging logic requires updating both the component AND multiple BT nodes.

### 3. Two competing parameter binding paths
- **16 nodes** use dynamic params: `GetDynamicParamDescriptors()` + `DeserializeParameters()` + manual `BB.GetBoxed()`/`SetBoxed()`.
- **7 nodes** use legacy `[SharedVar]`: public fields + `FieldBinding[]` + auto `ResolveInputsGeneric`/`WriteOutputsGeneric`.

A new node author must understand both paths. Commander composites use `[SharedVar]` but then bypass the resolved value via `GetSlotByName()` for per-agent offset math — a confusing dual-purpose pattern.

### 4. `[SharedVar]` nodes have hidden hardcoded variable names
`SendOrderMethod` and `CheckOrderMethod` auto-bind to `"AgentOrders"` / `"AgentReceivedOrder"`. If the blackboard definition renames these variables, the nodes silently break. The coupling is invisible in the editor (fields are `[HideInInspector]`).

### 5. State lives on shared NodeMethod instances
| Node | Instance state |
|---|---|
| `HasChanged` | `previousValue` |
| `EdgeDetect` | `previous` |
| `Enemy_FireSequence` | `phase`, `initialized`, `gunHandling`, `target` |
| `ExtractPosition` | `lastIndex` |
| `Cooldown` | `remaining` |
| `MoveTo` | `agent` (cached NavMeshAgent) |

This works only because each agent has its own `TreeEvaluator` → its own `methodInstances[]`. If evaluators were ever shared across agents, these nodes would corrupt each other's state. The `OnAbort`/`Reset` lifecycle is also inconsistent — `Enemy_FireSequence` implements it, `MoveTo` doesn't (leaves `agent` cached).

### 6. Missing useful node types
- No `RandomSelector` / `RandomSequence` composite.
- No `RetryUntilSuccess` / `RepeatUntilFailure` decorator.
- No `WaitForCondition` (only time-based `WaitSeconds`).
- Patrol behavior relies on `ExtractPosition` doing sequential/random mode internally rather than using generic composites.

---

## Tracked Bindings Problems

### 1. Reflection-based reads every frame (PERFORMANCE)
`PushTrackedBindings()` calls `PropertyInfo.GetValue()` / `FieldInfo.GetValue()` for every binding, every frame. The BT node `FieldBinding` system uses Expression-compiled delegates. Tracked bindings don't. For <20 bindings this is fine, but inconsistent with the BT node approach and doesn't scale well.

### 2. Silent type-mismatch drops
If a tracked binding's member type doesn't EXACTLY match the BB slot type (for value types), the write is silently dropped by `TypedBlackboardStorage.CanWriteBoxed()`. No runtime warning. Only caught by editor validation in `TrackedVariablesView`. If a BB variable type is changed after bindings are configured, bindings silently stop working.

### 3. Overwrite conflict with BT node writes
Execution order:
```
Frame N:   PushTrackedBindings() → writes Health=50
           Evaluate() → BT node writes Health=0   (Health=0 for rest of frame N)

Frame N+1: PushTrackedBindings() → overwrites Health=50 again
           BT node's write from frame N is gone
```
No separation between "component sensor" (read-only to BT) and "BT working state" (writeable) variables. Silent data fights each frame.

### 4. O(B × V) init cost for ComputeSlotOffset
`ComputeSlotOffset()` sums strides of all preceding variables for each binding. With 50 variables and 20 bindings, that's ~1000 stride summations. A precomputed offset array would reduce this to O(B). One-time cost — not critical but avoidable.

### 5. `[HideInInspector]` makes debugging hard
`trackedBindingGroups` is `[HideInInspector][SerializeField]`. At runtime or on prefabs, you can't see bindings in the default inspector. You MUST open the BT editor window. Ad-hoc debugging is blocked.

### 6. No runtime binding API
Bindings are design-time only. For a stateless enemy approach where different configs might inject different bindings at spawn time, this doesn't work. You must pre-bake all bindings on the prefab.

### 7. GUID-based tree matching can silently disconnect
`ResolveTrackedBindings()` matches the active tree by GUID. If a tree asset is duplicated or replaced, the bindings group disconnects and `trackedBindingsToPush` ends up empty — no warning.

---

## Stateless Enemy Refactor — Known Breakage Points

If `EnemyController` is removed/gutted, these break immediately:

1. **6 BT nodes** that do `GetComponent<EnemyController>()` — all need rewriting to resolve `GunHandling`, `EnemyDetectionSystem`, or `NavMeshAgent` directly.
2. **`EnemyManager.SpawnEnemy()`** calls `controller.SetMasks(...)`, `controller.Agent.avoidancePriority`, `controller.Agent.Warp(...)`, `controller.OnDestructionEvent += ReturnEnemy`. Needs a new home.
3. **Mask configuration** (`OnEnable` / `SetMasks`) currently on `EnemyController` — must move to `EnemyDetectionSystem` + `GunHandling` directly, or to a small spawn-time config injector.
4. **`CalculateNewPathToTarget()` / `CalculateTargetPosition()`** — pathfinding logic with random cone offsets lives on `EnemyController`. Must move into a BT `ActionMethod` node or a dedicated `EnemyPathfinding` component.
5. **Tracked bindings** could push `EnemyConfig` values into BB, but then those values are read-only to the BT. If a BT node needs to modify a setting at runtime, it needs a separate variable.
