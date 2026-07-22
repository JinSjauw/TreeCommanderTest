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

A SquadDefinition is a ScriptableObject (`Assets > Create > BehaviourTree > Squad Definition`) that defines:

1. **Squad Blackboard** — variables shared by all agents and the commander (e.g. AgentRoles, AgentOrders, AgentStatus)
2. **Roles** — named roles with colour, max amount, prefab, and fallback settings
3. **Binding Groups** — per-tree mappings between the squad blackboard and each connected tree's blackboard

### Opening the Squad Editor

- `BehaviourTree > Open Squad Editor` in the Unity menu bar
- Or double-click a SquadDefinition asset

The editor has three sections:

#### 1. Blackboard (top)

Define squad-wide variables. Variables marked **SquadData** automatically get per-agent array sizing at runtime.

To add a variable: type a name, click **+**, choose a type. The SquadData toggle creates a per-agent array variable.

#### 2. Roles (middle)

Each role is a foldout with:

| Field | Description |
|-------|-------------|
| **Name** | Display name (e.g. "Scout", "Leader") |
| **Colour** | UI colour for the role |
| **Max Amount** | How many agents can have this role. The sum across all roles = total squad size. |
| **Is Fallback** | If checked, agents with no matching role get assigned this one |
| **Prefab** | Optional GameObject prefab override for this role |

#### 3. Binding Groups (bottom)

Each connected tree asset gets a binding group. Click **+ Add Binding Group** and select a tree asset to create one. Inside each group:

- **System bindings** are auto-created (AgentRoles, AgentOrders, AgentStatus, LeaderIndex)
- You can add **custom bindings** for additional data

Each binding has:

| Field | Description |
|-------|-------------|
| **Squad Variable** | Variable on the squad's blackboard |
| **Tree Variable** | Variable on the tree's blackboard |
| **Direction** | `ToSquad` (tree → squad), `FromSquad` (squad → tree), or `Both` |

---

## What is Squad Data?

Squad data is the information that flows between the commander, the squad blackboard, and individual agents. It comes in two flavours:

### System Variables (auto-created)

These are created automatically when you add a binding group. They cannot be deleted or renamed.

| Variable | Commander | Agent | Purpose |
|----------|-----------|-------|---------|
| **AgentRoles** / **AgentAssignedRole** | Array (one per agent) | Single value | Which role each agent is assigned. Commander reads it. Agent writes it. |
| **AgentOrders** / **AgentReceivedOrder** | Array (one per agent) | Single value | What order each agent should execute. Commander writes it. Agent reads it. |
| **AgentStatus** | Array (one per agent) | Single value | Current status of each agent (idle, busy, completed). Both read and write. |
| **LeaderIndex** | Single int | — | Index of the current squad leader. Squad → Commander only. |

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
| `ForEachRole` | Action | Iterates over all unique roles in the squad definition. Use for role-specific behaviour. |
| `SendOrder` | Action | Issues an order (from the OrderRegistry) to agents. Can be inside ForEachAgent/ForEachRole. |
| `ReportStatus` | Action | Writes own status to the squad blackboard. Used by both agents and commanders. |
| `CheckOrder` | Condition | Checks what order the agent has received. Used in agent trees to branch on orders. |
| `PollAgentStatus` | Condition | Reads a specific agent's reported status. Commander uses this to check if an order is done. |
| `SelectAgent` | Action | Selects an agent by index for subsequent operations. |
| `CalculateFormation` | Action | Computes circle formation positions. Agent 0 at center, rest evenly distributed. |
| `GetHighestAgent` | Action | Finds the agent with the highest squad-data value (e.g. fastest, most damaged). |
| `GetLowestAgent` | Action | Finds the agent with the lowest squad-data value. |
| `GetNearestAgent` | Action | Finds the agent nearest to a given position. |
| `CheckSquadData` | Condition | Reads a squad-level data variable and checks a condition. |
| `SquadReduce` / `ArrayReduce` | Action | Reduces squad/array data with an accumulator (e.g. sum all values). |

### Typical Commander Tree Pattern

```
[ROOT]
  │
  └── [🔵 PRIORITY]
        │
        ├── [🟣 SEQUENCE] — Leader Death: Scatter
        │    ├── [🟡] CompareVariable: LeaderIndex == -1
        │    └── [🟣 SEQUENCE]
        │         ├── [🔴] ForEachAgent
        │         │    └── [🔴] SendOrder(Order: "Scatter")
        │         └── [🔴] WaitSeconds(5.0)
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
        │    ├── [🟡] Enemy_DetectTarget
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
