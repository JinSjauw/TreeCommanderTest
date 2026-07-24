# Commander & Squad — Getting Started

## Overview

The commander system lets one **Commander Tree** coordinate multiple **Agent Trees** through a shared **SquadDefinition**. Data flows between them via variable bindings on a squad blackboard.

```
                     SquadDefinition
                    ┌──────────────────┐
                    │  Squad Blackboard │
                    │  - AgentRoles[]   │
                    │  - AgentOrders[]  │
                    │  - AgentStatus[]  │
                    │  - LeaderIndex    │
                    └──────┬───────────┘
                           │ bindings
              ┌────────────┼────────────┐
              ▼            ▼            ▼
        Commander       Agent 0       Agent 1
     ┌──────────────┐ ┌──────────┐ ┌──────────┐
     │CommanderTree │ │AgentTree │ │AgentTree │
     │   Runner     │ │  Runner  │ │  Runner  │
     │              │ │          │ │          │
     │ - reads stats│ │- receives│ │- receives│
     │ - writes     │ │  orders  │ │  orders  │
     │   orders     │ │ - reports│ │ - reports│
     └──────────────┘ │  status  │ │  status  │
                      └──────────┘ └──────────┘
```

### Tree Types

| Tree Type | Asset to Create | Runner Component | Purpose |
|-----------|----------------|------------------|---------|
| **Agent** | `Agent Tree Asset` | `AgentTreeRunner` | Controls a single enemy. Has its own blackboard. |
| **Commander** | `Commander Tree Asset` | `CommanderTreeRunner` | Coordinates multiple agents. Has a Commander Blackboard for per-agent array data. |

A commander tree cannot control agents without a **SquadDefinition** — the squad is the bridge.

---

## The Squad Tab (Both Tree Types)

The **Squad tab** is available on both agent and commander tree assets. It manages the tree's connections to squads.

### On an Agent Tree

The Squad tab lists the squads this agent tree can join. Each connection specifies:
- **Squad**: the SquadDefinition to connect to
- **Assigned Roles**: which roles this agent can play in that squad (optional)

```
[Squad Tab — Agent Tree]
  ┌──────────────────────────────────────────────┐
  │  Squad Connections                            │
  │                                              │
  │  ┌── Squad: "PatrolSquad" ────────────────┐  │
  │  │  Roles: [Scout] [Leader]               │  │
  │  │  [- Remove]                            │  │
  │  └────────────────────────────────────────┘  │
  │                                              │
  │  [+ Add Squad Connection]                    │
  └──────────────────────────────────────────────┘
```

### On a Commander Tree

The Squad tab behaves the same way — it lists squads the commander can connect to. However, the commander tree also has a **Commander tab** for its primary squad assignment (see below).

---

## The Commander Tab (Commander Tree Only)

The **Commander tab** is where you select the squad this commander will command. It only appears on commander tree assets.

```
[Commander Tab — Commander Tree]
  ┌──────────────────────────────────────────────┐
  │  Commanded Squad: [PatrolSquad]              │
  │  [Open in Squad Editor]                      │
  │                                              │
  │  Bindings for "PatrolSquad":                 │
  │  ┌────────────────────────────────────────┐  │
  │  │ Squad Var    │ Tree Var      │ Dir     │  │
  │  │──────────────┼───────────────┼─────────│  │
  │  │ AgentRoles   │ AgentRoles    │ ◄ Squad │  │
  │  │ AgentOrders  │ AgentOrders   │ Squad ► │  │
  │  │ AgentStatus  │ AgentStatus   │ ◄► Both │  │
  │  │ LeaderIndex  │ LeaderIndex   │ ◄ Squad │  │
  │  └────────────────────────────────────────┘  │
  │  [+ Add Binding]                             │
  │                                              │
  │  Max members: 4                              │
  └──────────────────────────────────────────────┘
```

From this tab you can:
1. **Select the squad** via a search popup
2. **Open the Squad Editor** to configure roles and the squad blackboard
3. **View and edit bindings** between the commander's blackboard and the squad's blackboard
4. See the **max squad size** (auto-calculated from the squad's role composition)

---

## SquadDefinition

A SquadDefinition is a ScriptableObject (`Assets > Create > BehaviourTree > Squad Definition`) that defines a squad's composition and shared data. It has three parts:

- **Squad Blackboard** — variables shared between all agents and the commander
- **Roles** — named roles with colour, max amount, prefab, and fallback settings
- **Binding Groups** — per-tree mappings between the squad blackboard and each connected tree's blackboard

### Opening the Squad Editor

Two ways to open it:
- **Menu bar**: `BehaviourTree > Open Squad Editor`
- **Double-click** a SquadDefinition asset in the Project window

The editor window has three sections arranged vertically:

#### 1. Blackboard Section (top)

This is where you define variables on the squad's blackboard. These variables are shared with all connected trees through bindings.

**Adding a variable:**
1. Type a name in the text field at the top of the section
2. Click **+**
3. A type picker popup appears — choose a type (int, float, Vector3, Transform, etc.)
4. Toggle **SquadData** on if this should be a per-agent array (its stride = squad size at runtime)

**What the UI shows for each variable:**
- **Name** — the variable name (locked for system variables)
- **Type** — the variable type (locked for system variables)
- **Stride** — 1 for single values, > 1 for arrays (locked for system variables)
- **SquadData badge** — shown with a teal background for SquadData variables
- **System variable badge** — shown with an orange background for system variables (cannot be deleted or renamed)

**SquadData vs single:**
- A **SquadData** variable (e.g. `AgentOrders` with stride 4) creates one slot per agent. The commander reads `AgentOrders[0..N-1]` and the binding bridge copies the correct slot to each agent.
- A **single** variable (e.g. `SquadMovePosition` with stride 1) stores one value shared by all trees.

#### 2. Roles Section (middle)

Roles define the squad's composition. The sum of `maxAmount` across all roles determines the total squad size.

Each role is a foldout with these fields:

| Field | Description |
|-------|-------------|
| **Name** | Display name (e.g. "Scout", "Leader") |
| **Colour** | UI colour for the role (used in the graph editor and debug views) |
| **Max Amount** | How many agents can have this role. Sum across all roles = total squad size |
| **Is Fallback** | If checked, agents whose role doesn't match any defined role are assigned this one |
| **Prefab** | Optional GameObject prefab override. If set, the SquadManager spawns this prefab for agents with this role instead of the fallback agent prefab |

**Adding a role:** Click **+ Add Role** at the bottom of the section. Fill in the fields in the new foldout.

**Role index assignment:** Roles are indexed in order (0, 1, 2, ...). The `AgentRoles` SquadData array stores each agent's role index, which maps to this list. When you add a role, its index is determined by its position.

#### 3. Binding Groups Section (bottom)

Each connected tree asset gets a binding group. Click **+ Add Binding Group** and select a tree asset to create one. The system variables and bindings are auto-created when the group is first set up.

**Inside a binding group**, each row maps one variable between the squad blackboard and the tree's blackboard:

| Field | Description |
|-------|-------------|
| **Squad Variable** | Variable on the squad's blackboard (dropdown) |
| **Tree Variable** | Variable on the tree's blackboard (dropdown) |
| **Direction** | `ToSquad` (tree → squad), `FromSquad` (squad → tree), or `Both` |
| **Remove** | Trash icon — deletes the binding row (disabled for system bindings) |

**System bindings** (auto-created, orange-highlighted, remove disabled):
- `AgentRoles`, `AgentOrders`, `AgentStatus`, `LeaderIndex`
- These are locked because the fields they map to are `isSystemVariable = true`

**Custom bindings** (user-added, no special styling, removable):
- Add via the **+ Add Binding** button
- Pick any squad variable, any tree variable, and a direction

**Direction behaviour:**
| Direction | Data flow | Use case |
|-----------|-----------|----------|
| `ToSquad` | Tree → Squad | Agent writes its status to the shared squad array |
| `FromSquad` | Squad → Tree | Commander reads role data from the squad |
| `Both` | Squad ↔ Tree | Both sides read and write the same variable (e.g. formation positions) |

---

## What is Squad Data?

Squad data is the information that flows between the commander, the squad blackboard, and individual agents. It comes in two flavours:

### System Variables & Bindings (auto-created)

When a SquadBindingGroup is created for a tree asset, `EnsureAutoBindings()` automatically:

1. **Creates the blackboard variables** on the squad blackboard and the tree's blackboard if they don't already exist
2. **Creates the binding rows** between them

| Squad Variable | Commander Variable | Agent Variable | Direction | Purpose |
|----------------|-------------------|----------------|-----------|---------|
| **AgentRoles** (int, SquadData) | AgentRoles (int, SquadData) | AgentAssignedRole (int) | Squad → Commander (commander) / Tree → Squad (agent) | Which role each agent is assigned |
| **AgentOrders** (int, SquadData) | AgentOrders (int, SquadData) | AgentReceivedOrder (int) | Commander → Squad (commander) / Squad → Agent (agent) | What order each agent should execute |
| **AgentStatus** (int, SquadData) | AgentStatus (int, SquadData) | AgentStatus (int) | Squad → Commander (commander) / Tree → Squad (agent) | Current status per agent |
| **LeaderIndex** (int) | LeaderIndex (int) | — | Squad → Commander (commander only) | Index of current squad leader |

> System variables are created with `isSystemVariable = true`, which locks their name, type, and stride in the editor. They cannot be deleted or renamed through the UI.

### SquadData Variables (user-defined)

Variables marked **SquadData** in the commander or squad blackboard get their stride (array size) dynamically set to the squad's max agent count at runtime.

```
Example: Commander Blackboard with SquadData
┌────────────────────────────────────────┐
│ Variable         Type     Stride       │
│────────────────────────────────────────│
│ AgentOrders      int      4  ← SquadData (stride = squad size)
│ FormationPos     Vector3  4  ← SquadData
│ SquadMovePos     Vector3  1  ← normal (single value)
└────────────────────────────────────────┘
```

At runtime:
- `AgentOrders[0]` = order for agent 0
- `AgentOrders[1]` = order for agent 1
- etc.

In the Squad Editor, the SquadData toggle appears in the type-creation popup. Variables created with SquadData enabled will have their stride managed automatically.

---

## How the Agent Tree Interacts with the Squad

### Data Flow per Frame

```
Commander Tree Evaluates:
  1. Copy squad data → commander BB (FromSquad bindings)
  2. Run commander tree
  3. Copy commander BB → squad (ToSquad bindings)

For Each Agent (in registration order):
  1. Copy squad data → agent BB, offset to this agent's slot
  2. Run agent tree
  3. Copy agent BB → squad, offset to this agent's slot
```

### Per-Agent Offset

When the commander copies data to/from an agent, it applies an **agent offset**. This means:
- Agent 0 reads from `AgentOrders[0]`, writes to `AgentStatus[0]`
- Agent 1 reads from `AgentOrders[1]`, writes to `AgentStatus[1]`

The agent tree never needs to know its index — the offset is applied automatically by the `CommanderTreeRunner` before the agent evaluates.

### Agent Tree Setup

The agent tree needs to:
1. **Define blackboard variables** that match the bindings: `AgentAssignedRole` (int), `AgentReceivedOrder` (int), `AgentStatus` (int)
2. **Use `CheckOrder`** to read `AgentReceivedOrder` and decide what to do
3. **Use `ReportStatus`** to write its status back

### Commander Tree Setup

The commander tree needs to:
1. **Define a Commander Blackboard** with SquadData arrays for `AgentOrders`, plus any custom data
2. **Use `ForEachAgent`** to iterate over agents
3. **Use `SendOrder`** to issue orders
4. **Use `PollAgentStatus`** or check squad data directly to read agent state

---

## Agent ↔ Commander Coordination

The commander coordinates agents by sending orders and monitoring their status. This is the standard coordination pattern:

### Status Variables

Each agent has an `AgentStatus` variable that it writes to. The squad blackboard has a corresponding `AgentStatus` SquadData int[] array — one slot per agent. These are auto-created when the binding group is set up.

| Squad Variable | Agent Variable | Direction |
|----------------|---------------|-----------|
| AgentStatus (int[]) | AgentStatus (int) | Tree → Squad |

> Both the squad `AgentStatus` (int, SquadData) and agent `AgentStatus` (int) are created automatically when the binding group is first set up.

### Status Values (AgentStatusValue)

| Value | Name | Meaning |
|-------|------|---------|
| `0` | Success | Idle / arrived / completed |
| `1` | Failure | Failed / blocked / error |
| `2` | Running | Currently executing a coordinated action |

### ReportStatus (Agent node)

`ReportStatus` is an **instant** Agent-only action that writes a status value to the agent's `AgentStatus` slot. It takes two parameters:

- **Target** — the `AgentStatus` int variable on the agent's blackboard
- **Value** — `Success` (0), `Failure` (1), or `Running` (2)

The squad bridge automatically copies the agent's slot to the squad's `AgentStatus[]` array at the correct index.

**When to report Running**: place `ReportStatus(AgentStatus, Running)` at the start of any running action that the commander needs to synchronise on — for example, before a `MoveTo` that should complete before the commander issues the next order. This signals to `PollAgentStatus` that a new movement cycle has started.

### PollAgentStatus (Commander node)

`PollAgentStatus` is a Commander action that reads the squad's `AgentStatus[]` array and returns:

- **SUCCESS** — all agents report `0` (idle/arrived). Has a **consumed gate**: after returning SUCCESS once, it blocks further SUCCESS until at least one agent reports `2` (Running) again. This prevents the commander from spuriously re-triggering on stale data.
- **RUNNING** — at least one agent reports `2` (Running)
- **FAILURE** — any agent reports `1` (Failure)

### Typical Coordination Flow

```
Commander tree:

[SEQUENCE] — Send patrol order
 ├── [🔴] ForEachAgent
 │    └── [🔴] SendOrder(Order: "MoveToPosition")
 └── [🔴] PollAgentStatus(AgentStatus)  ← RUNNING while agents move

      ↓ agents finish moving → PollAgentStatus returns SUCCESS

[SEQUENCE] — Send next order
 ├── [🔴] ForEachAgent
 │    └── [🔴] SendOrder(Order: "AttackTarget")
 └── [🔴] PollAgentStatus(AgentStatus)
```

```
Agent tree responding to "MoveToPosition":

[SEQUENCE] — Move (responds to MoveToPosition order)
 ├── [🟡] CheckOrder(Order: "MoveToPosition")
 ├── [🔴] ReportStatus(AgentStatus, Running)   ← signals "started moving"
 └── [🔴] MoveTo(targetMovePosition)            ← RUNNING while moving
      ↓ MoveTo returns SUCCESS → automatically falls through to next frame
```

The agent reports `Running` when it starts moving. The commander's `PollAgentStatus` sees Running(2) and returns RUNNING — it keeps ticking. When `MoveTo` finishes, the agent no longer actively writes to `AgentStatus` — but on the next frame the binding copies the cached value. To explicitly mark completion, the agent can run `ReportStatus(AgentStatus, Success)` after `MoveTo`.

### Custom SquadData Variables

The same pattern works for custom data. Any variable marked **SquadData** on the commander blackboard gets a per-agent array at runtime. Bindings copy the correct slot to/from each agent automatically.

| Squad Variable | Commander Variable | Agent Variable | Direction | Purpose |
|----------------|-------------------|----------------|-----------|---------|
| AgentMoveTarget (Vector3[]) | AgentMoveTarget (Vector3[]) | targetMovePosition (Vector3) | Both | Commander writes per-agent move target, agent reads its own |

### Example: Creating a Custom Communication Channel

This walks through creating a custom channel where the commander tells each agent where to move.

**Step 1 — Create the blackboard variables**

| Blackboard | Variable Name | Type | SquadData |
|------------|--------------|------|-----------|
| Squad | `AgentMoveTarget` | Vector3 | ✅ Yes (per-agent array) |
| Commander | `AgentMoveTarget` | Vector3 | ✅ Yes |
| Agent | `targetMovePosition` | Vector3 | No (single value) |

**Step 2 — Create the binding**

In the Squad Editor, add a binding group for your commander tree and agent tree. Then add a custom binding:

| Squad Variable | Commander Variable | Agent Variable | Direction |
|----------------|-------------------|----------------|-----------|
| AgentMoveTarget | AgentMoveTarget | targetMovePosition | Both |

**Step 3 — Commander writes per-agent positions** (inside `ForEachAgent`)

```
SetVariable(target: AgentMoveTarget[agentIndex],
            value: FormationPositions[agentIndex])
```

The `[agentIndex]` offset is applied automatically — agent 0 reads slot 0, agent 1 reads slot 1, etc.

**Step 4 — Agent reads its slot**

```
MoveTo(Target: targetMovePosition)
```

The agent never needs to know its index. The binding bridge copies `AgentMoveTarget[agentIndex]` → `targetMovePosition` each frame.

---

## Auto-Bound Blackboard Variables

Several commander and agent nodes have fields that are **auto-bound** — they resolve to a blackboard variable **by name** at bake time and do not appear in the node's inspector. These variables are auto-created by `EnsureAutoBindings()` when a binding group is set up.

| Node | Auto-Bound Variable | Required On | Type |
|------|--------------------|-------------|------|
| `ForEachRole` | `AgentRoles` | Commander BB | int, SquadData |
| `SendOrder` | `AgentOrders` | Commander BB | int, SquadData |
| `CheckOrder` | `AgentReceivedOrder` | Agent BB | int (single) |
| `ReportStatus` | *User picks the target variable* | Agent BB | int (single) |

---

## End-to-End Wiring Guide

This walks through the full setup from scratch: creating the assets, configuring bindings, and wiring everything in a scene.

### 1. Create the SquadDefinition

In the Project window: `Assets > Create > BehaviourTree` — note that Squad Definition is created from the Squad Editor, not from the Create menu. Open the Squad Editor (`BehaviourTree > Open Squad Editor`) and click **Create New Squad**.

In the Squad Editor, configure:

- **Roles** (middle section): add one role per role type in your squad (e.g. Leader with maxAmount 1, Scout with maxAmount 3). The sum of maxAmount = total squad size.
- **Blackboard** (top section): system variables (`AgentRoles`, `AgentOrders`, `AgentStatus`, `LeaderIndex`) are auto-created when binding groups are set up. Add any custom squad-data variables here too.

### 2. Create the Commander Tree

`Assets > Create > BehaviourTree > Commander Tree`. Open it by double-clicking. Then:

- **Commander tab**: click **Select Squad** and pick your SquadDefinition. This creates a binding group — system variables and bindings are auto-created on both the commander BB and squad BB.
- **Blackboard tab**: verify the system variables appeared (AgentRoles, AgentOrders, AgentStatus, LeaderIndex — all int, SquadData). Add any additional commander variables (e.g. `FormationPositions` Vector3[], `SquadMovePosition` Vector3).
- **Build the tree**: add your commander logic (e.g. PRIORITY → SEQUENCE → ForEachAgent → SendOrder).

### 3. Create the Agent Tree

`Assets > Create > BehaviourTree > Agent Tree`. Open it by double-clicking. Then:

- **Squad tab**: click **+ Add Squad Connection**, select your SquadDefinition. This creates a binding group — agent-side variables (`AgentAssignedRole`, `AgentReceivedOrder`, `AgentStatus`) are auto-created.
- **Blackboard tab**: verify the agent variables appeared. Add any additional variables needed (e.g. `targetMovePosition` Vector3, `selectedTarget` Transform).
- **Build the tree**: add your agent logic (e.g. SELECTOR → SEQUENCE → CheckOrder → ReportStatus → MoveTo).

### 4. Verify the Bindings

In the SquadDefinition's **Squad Editor**, check the **Binding Groups** section. You should see two binding groups — one for the commander tree, one for the agent tree. Each should have the auto-created system bindings.

If you added custom variables (like `AgentMoveTarget` Vector3 → `targetMovePosition` Vector3), add custom bindings for them here.

### 5. Create Prefabs

- **Commander prefab**: a GameObject with `CommanderTreeRunner` component. Assign your commander tree asset to it.
- **Agent prefab(s)**: a GameObject with `AgentTreeRunner` component. Assign your agent tree asset to it. If roles have specific prefabs, assign them in the SquadDefinition's Roles section.

### 6. Set Up the Scene

1. Create an empty GameObject in the scene, name it "SquadManager".
2. Add the `SquadManager` component.
3. Assign the fields:

| Field | What to assign |
|-------|---------------|
| **Definition** | Your SquadDefinition asset |
| **Commander Prefab** | The commander prefab from step 5 |
| **Agent Prefab** | Fallback agent prefab (used when a role has no specific prefab) |
| **Agent Count** | Number of agents to spawn |
| **Patrol Points Parent** | (Optional) A Transform with waypoint children for patrol routes |
| **Leader Death Behavior** | `Promote` (next agent becomes leader) or `Scatter` (no leader) |

4. Press Play. The SquadManager spawns the commander and agents, assigns roles, and sets up the initial leader. The commander tree starts evaluating and agents respond to orders.

### Manual Wiring (without SquadManager)

If you want to control spawning yourself instead of using SquadManager:

1. Instantiate the **SquadInstance** manually:
   ```csharp
   SquadInstance squad = new GameObject("Squad").AddComponent<SquadInstance>();
   squad.Initialize(definition, maxAgents);
   ```
2. Instantiate the **commander** and call `commanderRunner.Initialize()`, then `commanderRunner.RegisterSquad(squad)`.
3. For each **agent**: instantiate, call `agent.Initialize()`, set `agent.commander = commanderRunner`, `agent.squadInstance = squad`, then `commanderRunner.RegisterAgent(agent)`.

## SquadManager (Runtime Wiring)

The `SquadManager` MonoBehaviour handles spawning and wiring everything at runtime.

### Setup

1. Create a GameObject in the scene (e.g. "SquadManager").
2. Add the `SquadManager` component.
3. Assign:

| Field | Description |
|-------|-------------|
| **Definition** | The SquadDefinition asset |
| **Commander Prefab** | Prefab with `CommanderTreeRunner` component |
| **Agent Prefab** | Fallback prefab for agents (role-specific prefabs override this) |
| **Agent Count** | How many agents to spawn |
| **Patrol Points Parent** | Transform with waypoint children |
| **Leader Death Behavior** | `Promote` (next agent becomes leader) or `Scatter` (no leader) |

### Spawn Process

```
SquadManager.SpawnSquad():
  1. Instantiate commander prefab → get CommanderTreeRunner
  2. Build role-index array from SquadDefinition roles
  3. Create SquadInstance GameObject with squad blackboard
  4. Register commander with squad
  5. Spawn N agents, each:
     - Gets prefab from role (or fallback)
     - Registers with commander
     - Gets role index written to AgentAssignedRole
  6. Set initial leader (agent 0 by default)
```

You can also call `SpawnInFormation(center, count, radius)` to spawn agents in a circle formation.

---

## Commander-Specific Nodes

These nodes are only available in commander trees:

| Node | Type | What it does |
|------|------|-------------|
| `ForEachAgent` | Action | Iterates over all registered agents. Children run once per agent with the agent's offset applied. |
| `ForEachRole` | Action | Iterates over all unique roles in the squad definition. Requires an `AgentRoles` (int, SquadData) variable on the commander BB — auto-bound by name. |
| `SendOrder` | Action | Issues an order (from the OrderRegistry) to agents. Requires an `AgentOrders` (int, SquadData) variable on the commander BB — auto-bound by name. |
| `PollAgentStatus` | Action | Polls the agent status array to check if all agents have finished their orders. Returns SUCCESS when all idle, RUNNING while any are active, FAILURE on error. |
| `CalculateFormation` | Action | Computes circle formation positions. Agent 0 at center, rest evenly distributed. |
| `CheckSquadData` | Condition | Reads a squad-level data variable and checks a condition. |
| `SquadReduce` | Action | Reduces squad data with an accumulator (e.g. sum all values). |

### Typical Commander Tree Pattern

```
[ROOT]
  │
  └── [🟣 SEQUENCE] — Normal: Formation Patrol
       ├── [🔴] ExtractPosition → SquadMovePosition
       ├── [🔴] CalculateFormation → FormationPositions
       └── [🔴] ForEachAgent
            └── [🟣 SEQUENCE]
                 ├── [🔴] SetVariable(targetMovePosition,
                 │         FormationPositions[agentIndex])
                 └── [🔴] SendOrder(Order: "MoveToPosition")
```

### Agent Tree Responding to Orders

```
[ROOT]
  │
  └── [🔵 SELECTOR]
        ├── [🟣 SEQUENCE]: Attack
        │    ├── [🟡] CheckOrder(Order: "AttackTarget")
        │    ├── [🟡] Enemy_Detected
        │    ├── [🔴] Enemy_SelectDetectedTarget
        │    └── [🔴] Enemy_FireSequence
        │
        ├── [🟣 SEQUENCE]: Move
        │    ├── [🟡] CheckOrder(Order: "MoveToPosition")
        │    └── [🔴] MoveTo(targetMovePosition)
        │
        └── [🟣 SEQUENCE]: Fallback
             ├── [🟡] CheckOrder(Order: "Scatter")
             └── [🔴] WaitSeconds(3.0)
```

Each branch checks the order via `CheckOrder`. If it matches, the branch runs. If not, the SELECTOR falls through to the next branch.

---

## OrderRegistry

Orders are identified by name (not index). Create an `OrderRegistry` asset via `Assets > Create > BehaviourTree > Order Registry` to define the available order names.

- Order names are stored in a list. Index 0 is the default/idle state.
- `SendOrder` and `CheckOrder` reference orders by name
- The baker resolves names to indices at bake time, so reordering the registry doesn't break existing trees

Add orders like: "Idle", "MoveToPosition", "AttackTarget", "Scatter", "Regroup", etc.
