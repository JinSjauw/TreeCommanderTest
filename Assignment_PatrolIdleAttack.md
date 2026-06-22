# Assignment: Patrol-Idle-Attack Loop

## Goal

Build a behaviour tree for an enemy that:

1. **Patrols** between waypoints.
2. **Idles** briefly at each waypoint.
3. **Attacks** any detected target, then returns to patrolling when the target is lost.

The loop should run continuously — patrol → idle → patrol → idle → (interrupt with attack) → patrol → ...

## Requirements

- The tree must use at least one **SELECTOR** and at least one **SEQUENCE**.
- Attacks must use **`Enemy_FireSequence`** (it handles the full fire pipeline for you — reload, aim, trajectory, fire — so you don't need to build that yourself).
- The enemy must **move** between patrol points on a NavMesh.
- There must be an **idle wait** at each patrol point before moving to the next one.

> A **`patrolPointsParent`** Transform has already been created and assigned for you on the blackboard. `Enemy_SelectPatrolPoint` uses it to pick waypoints — you don't need to set it up yourself.

## Field Types: Constants vs Shared (Blackboard) Variables

Every node field can be one of two things:

### Constant
A fixed value you type in. It never changes at runtime.

> Example: `reloadDuration = 1.5` on `Enemy_FireSequence`. Always waits 1.5 seconds.

### Shared (Blackboard) Variable
A value stored on the **blackboard** that can be **read** and **written** by multiple nodes across the whole tree. One node writes to it, another node reads from it later.

> Example: `Enemy_SelectPatrolPoint` **writes** a patrol position to `targetMovePosition`. `Enemy_MoveTo` **reads** `targetMovePosition` to know where to go.

In the node inspector, shared fields show a dropdown to pick which blackboard variable to use. Constant fields show a regular input box.

## How the Blackboard Works

The blackboard is the tree's shared memory:

- Variables are **named slots** that hold values (int, float, bool, Vector3, Transform, etc.).
- Variables exist in the **Blackboard Definition** (a separate asset). You define them once, then any node can reference them.
- Nodes **write** to variables (e.g., `Enemy_SelectDetectedTarget` writes a Transform to `selectedTarget`).
- Other nodes **read** those variables (e.g., `Enemy_FireSequence` reads `selectedTarget` to know what to fire at).
- Variables persist between frames — a value set on frame 50 is still there on frame 51.
- Variables are **scoped to the tree** they belong to. Each behaviour tree has its own blackboard — variables defined in one tree are not accessible from another tree.

To add a variable: open the Blackboard Definition in the editor, click **+**, give it a name and type.

## Controls

| Key / Action | What it does |
|-------------|-------------|
| **A** | Frame all nodes in the graph view (fits the entire tree on screen) |
| **Ctrl+Z** | Undo |
| **Ctrl+Y** | Redo |
| **Right-click** in graph | Open the node creation menu |
| **Space** | Open the node creation menu |
| **Drag from port** | Connect nodes (output → input) |
| **Left-click** node | Select it and show its properties in the inspector |
| **Delete** key | Delete selected node or edge |

## Hints

- Think about which branch should be tried **first** each frame (attack or patrol?).
- `Enemy_FireSequence` needs a `selectedTarget` Transform — how do you get one onto the blackboard?
- `Enemy_SelectPatrolPoint` writes a Vector3 to the blackboard — the same variable can be read by `Enemy_MoveTo`.
- `WaitSeconds` needs a blackboard float to track elapsed time.
- `patrolPointsParent` is already on the blackboard — you don't need to create it.
- The tree loops automatically: when the root's child finishes (SUCCESS or FAILURE), the whole tree re-evaluates from the top next frame.

## Nodes You Have Available

### Composites

| Colour | Node | Purpose |
|--------|------|---------|
| 🔵 Blue | **SELECTOR** | "Try this, else try that." Runs children left→right, stops on first SUCCESS. |
| 🟣 Purple | **SEQUENCE** | "Do this, then that." Runs children left→right, stops on first FAILURE. |
| 🟣 Magenta | **PARALLEL** | Runs all children at once every frame. |
| 🔵 Cyan | **PRIORITY** | Like SELECTOR, but re-evaluates from the first child every tick (can interrupt). |

### Enemy Conditions (🟡 Yellow — return SUCCESS or FAILURE only)

| Node | What it checks |
|------|---------------|
| `Enemy_DetectTarget` | Is any enemy detected right now? |
| `Enemy_HasArrived` | Has the NavMeshAgent reached its destination? |
| `Enemy_IsAimed` | Is the turret aimed at the target? |
| `Enemy_IsInFiringRange` | Is the target within firing range? |
| `Enemy_HasLineOfSight` | Is there a clear line of sight to the target? |

### Enemy Actions (🔴 Red — can return RUNNING across multiple frames)

| Node | What it does | RUNNING while... |
|------|-------------|------------------|
| `Enemy_SelectPatrolPoint` | Pick a patrol point (random or sequential) | *(instant)* |
| `Enemy_MoveTo` | Move to a Vector3 position | Moving toward destination |
| `Enemy_MoveTo_Transform` | Move to a Transform's position | Moving toward destination |
| `Enemy_SelectEngagePosition` | Calculate an approach position near the target | *(instant)* |
| `Enemy_StopMovement` | Stop the NavMeshAgent | *(instant)* |
| **`Enemy_FireSequence`** | **Full fire pipeline: reload → aim → trajectory → fire** | **Any phase** |
| `Enemy_SelectDetectedTarget` | Pick a target from detected enemies | *(instant)* |

### Utility Actions (🔴 Red)

| Node | What it does |
|------|-------------|
| **`WaitSeconds`** | Wait N seconds. RUNNING while waiting, SUCCESS when done. |
