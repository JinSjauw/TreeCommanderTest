# Assignment: Squad Patrol (Commander Tree)

> **Reference**: See [Commander & Squad — Getting Started](file:///d:/Dev/TreeCommanderTest/Commander_Getting_Started.md) for detailed explanations of squad definitions, commander trees, the Squad/Commander tabs, squad data, and how agent trees interact with squads.

## Prerequisites

Complete the [Patrol-Idle-Attack Assignment](file:///d:/Dev/TreeCommanderTest/Assignment_PatrolIdleAttack.md) first. You will reuse and modify that agent tree.

## Goal

Take the individual patrol-idle-attack agents and make them patrol **as a squad** — moving together in a formation as a group, rather than each patrolling independently.

The commander tree should:

1. **Form up** the squad in a circle formation.
2. **Move the formation** to a patrol position.
3. **Wait** for all agents to arrive before moving to the next position.
4. **Interrupt** and scatter if the leader is killed.

## Requirements

- Create a **Commander Tree Asset** (`Assets > Create > BehaviourTree > Commander Tree`).
- Create a **SquadDefinition** that defines roles and connects the commander tree to the agent tree.
- Use the **SquadManager** or manual setup to link the commander, squad instance, and agents.
- The commander tree must use **PRIORITY** for the leader-death scatter interrupt.
- Use **`CalculateFormation`** to compute formation positions.
- Use **`ForEachAgent`** with a SEQUENCE to set each agent's target position and send a `MoveToPosition` order.
- The agent tree must be updated to **receive orders** via `CheckOrder`.
- Use **`SendOrder`** to issue orders from the commander.

---

## How Data Flows

```
┌─────────────────────────────────────────────────────────────┐
│                     Squad Blackboard                         │
│  AgentRoles[0..N-1]  AgentOrders[0..N-1]  AgentStatus[0..N-1]│
└───────┬────────────────────┬─────────────────────┬──────────┘
        │ FromSquad          │ ToSquad             │ Both
        ▼                    ▼                     ▼
┌──────────────┐    ┌────────────────────┐  ┌──────────────────┐
│  Commander   │    │  Agent 0 (offset 0)│  │ Agent 1 (offset 1)│
│  reads roles │    │  reads Orders[0]   │  │ reads Orders[1]  │
│  writes      │    │  writes Status[0]  │  │ writes Status[1] │
│  Orders[]    │    └────────────────────┘  └──────────────────┘
└──────────────┘
```

- The **Commander tab** on your commander tree asset selects the squad and shows bindings.
- **System bindings** (AgentRoles, AgentOrders, AgentStatus, LeaderIndex) are auto-created.
- Each agent reads/writes at `slot[agentIndex]` — the offset is applied automatically.
- To pass additional data (like `targetMovePosition`), add **custom bindings** via the Squad Editor.

---

## Setup Steps

### 1. Create the SquadDefinition

1. `Assets > Create > BehaviourTree > Squad Definition`, name it "PatrolSquad".
2. Open it (`BehaviourTree > Open Squad Editor` or double-click).
3. Define roles, e.g.:
   - **Leader** (maxAmount: 1, colour: gold)
   - **Scout** (maxAmount: 3, colour: blue)
4. The squad size will be 4 (1 Leader + 3 Scouts).

### 2. Add Squad Blackboard Variables

In the Squad Editor's blackboard section, add:
- `AgentRoles` (int, SquadData) — auto-created
- `AgentOrders` (int, SquadData) — auto-created
- `AgentStatus` (int, SquadData) — auto-created
- `AgentMoveTarget` (Vector3, SquadData) — **manual**: this is the per-agent position the commander writes and the agent reads

### 3. Create the Commander Tree

1. `Assets > Create > BehaviourTree > Commander Tree`, name it "SquadPatrolCommander".
2. In the **Commander tab**, select "PatrolSquad" as the commanded squad.
3. System bindings are auto-created.
4. Add a custom binding for `AgentMoveTarget` (step 5 below).

### 4. Update the Agent Tree

Reuse your existing patrol tree. Add to the agent's blackboard:
- `AgentReceivedOrder` (int) — receives orders from the squad
- `AgentAssignedRole` (int) — role assigned by SquadManager
- `AgentStatus` (int) — reports status to squad

### 5. Set Up Bindings in the Squad Editor

Add the commander tree and agent tree as binding groups in your SquadDefinition.

**System bindings** (auto-created):

| Squad Var | Commander Var | Agent Var | Direction |
|-----------|---------------|-----------|-----------|
| AgentRoles | AgentRoles | — | Squad → Commander |
| AgentOrders | AgentOrders | — | Commander → Squad |
| AgentStatus | AgentStatus | AgentStatus | Both |
| LeaderIndex | LeaderIndex | — | Squad → Commander |
| AgentOrders | — | AgentReceivedOrder | Squad → Agent |
| AgentRoles | — | AgentAssignedRole | Squad → Agent |

**Custom binding** (you add):

| Squad Var | Commander Var | Agent Var | Direction |
|-----------|---------------|-----------|-----------|
| AgentMoveTarget | AgentMoveTarget | targetMovePosition | Both |

This lets the commander write to `AgentMoveTarget[index]` inside `ForEachAgent`, and the agent reads it as `targetMovePosition`.

### 6. Required Blackboard Variables

#### Commander Blackboard

| Variable | Type | SquadData | Purpose |
|----------|------|-----------|---------|
| `SquadMovePosition` | Vector3 | No | Where the formation should center |
| `FormationPositions` | Vector3[] | No | Per-agent formation positions (stride = squad size) |
| `AgentMoveTarget` | Vector3 | Yes | Per-agent movement target |
| `PatrolPoints` | Transform | No | Patrol points parent |
| `AgentCount` | int | No | Number of registered agents |

#### Agent Blackboard

| Variable | Type | Purpose |
|----------|------|---------|
| `AgentAssignedRole` | int | Role index assigned by SquadManager |
| `AgentReceivedOrder` | int | Current order from the squad |
| `targetMovePosition` | Vector3 | Where the commander says to go |
| `selectedTarget` | Transform | Enemy target |
| `patrolPointsParent` | Transform | Squad's shared patrol points |

---

## Commander Tree Structure

```
[ROOT]
  │
  └── [🔵 PRIORITY]
        │
        ├── [🟣 SEQUENCE] — Leader Death Scatter
        │    ├── [🟡] CompareVariable: LeaderIndex == -1
        │    └── [🟣 SEQUENCE]
        │         ├── [🔴] SendOrder(Order: "Scatter")
        │         └── [🔴] WaitSeconds(duration: 5.0)
        │
        └── [🟣 SEQUENCE] — Normal Squad Patrol
             ├── [🟡] Cooldown(duration: 2.0)         ← rate-limit
             ├── [🔴] ExtractPosition(PatrolPoints → SquadMovePosition, Sequential)
             ├── [🔴] CalculateFormation(→ FormationPositions,
             │         Center: SquadMovePosition, Radius: 5.0)
             └── [🔴] ForEachAgent
                  └── [🟣 SEQUENCE]
                       ├── [🔴] SetVariable(target: AgentMoveTarget[agentIndex],
                       │         value: FormationPositions[agentIndex])
                       └── [🔴] SendOrder(Order: "MoveToPosition")
```

### How It Works

1. **PRIORITY** tries the scatter branch first. If `LeaderIndex` = -1 (leader dead), it scatters.
2. **Normal branch**: every 2 seconds the commander:
   - Picks the next patrol point
   - Computes formation positions around that point
   - For each agent: writes their formation position to `AgentMoveTarget[index]` and sends the `MoveToPosition` order
3. Squad bindings copy `AgentMoveTarget[index]` → each agent's `targetMovePosition`.

---

## Updated Agent Tree

The agent tree needs to check what order it received before deciding what to do.

```
[ROOT]
  │
  └── [🔵 SELECTOR]
        │
        ├── [🟣 SEQUENCE] — Attack
        │    ├── [🟡] CheckOrder(Order: "AttackTarget")
        │    ├── [🟡] Enemy_Detected(Radius: 20, LessThan)
        │    ├── [🔴] Enemy_SelectDetectedTarget(Nearest → selectedTarget)
        │    └── [🔴] Enemy_FireSequence(Target: selectedTarget)
        │
        ├── [🟣 SEQUENCE] — Move
        │    ├── [🟡] CheckOrder(Order: "MoveToPosition")
        │    └── [🔴] MoveTo(Target: targetMovePosition)
        │
        └── [🟣 SEQUENCE] — Scatter / idle
             ├── [🟡] CheckOrder(Order: "Scatter")
             └── [🔴] WaitSeconds(duration: 3.0)
```

Each branch checks the order. If it matches, the branch runs. If not, SELECTOR falls through.

---

## Hints

- Start simple: get the commander forming up and moving to one position before adding patrol sequencing.
- Use `LogVariable` to debug what orders agents are receiving.
- `agentIndex` inside `ForEachAgent` is the loop variable — use it to index into `FormationPositions`.
- If agents don't move, check bindings: both direction and variable name must match on squad, commander, and agent sides.
- Set up a `SquadManager` on a GameObject with the commander and agent prefabs assigned — it handles all the wiring.
- Monitor `LeaderIndex` to test leader death → scatter.

## Extension Ideas

- **Attack target**: commander detects enemies via squad-level data and sends `AttackTarget` to specific roles.
- **Dynamic roles**: read `AgentRoles` in the commander tree and give different orders per role (scouts flank, leaders engage).
- **Regroup**: if agents get too far apart, send a `Regroup` order with a central position.
