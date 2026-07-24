# Assignment: Squad Patrol (Commander Tree)

> **Reference**: See [Commander & Squad — Getting Started](Commander_Getting_Started.md) for detailed explanations of squad definitions, commander trees, the Squad/Commander tabs, squad data, and how agent trees interact with squads.

## Prerequisites

Complete the [Patrol-Idle-Attack Assignment](Assignment_PatrolIdleAttack.md) first. You will reuse and modify that agent tree.

## Goal

Take the individual patrol-idle-attack agents and make them patrol **as a squad** — moving together in a formation as a group, rather than each patrolling independently.

The commander tree should:

1. **Form up** the squad in a circle formation.
2. **Move the formation** to a patrol position.
3. **Wait** for all agents to arrive before moving to the next position.

## Requirements

- Create a **Commander Tree Asset** (`Assets > Create > BehaviourTree > Commander Tree`).
- Create a **SquadDefinition** that defines roles and connects the commander tree to the agent tree.
- Use a **SEQUENCE** to run the patrol loop: extract position → calculate formation → for each agent.
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

## Setup

### 1. Create the Commander Tree

Create a new Commander Tree asset (`Assets > Create > BehaviourTree > Commander Tree`). Assign it to the `CommanderTest` prefab — drag the tree asset into the prefab's `CommanderTreeRunner` component's **Authoring Asset** field.

### 2. Create the SquadDefinition

Create a new SquadDefinition via the Squad Editor (`BehaviourTree > Open Squad Editor` → "Create New Squad"). Define roles (e.g. Leader with maxAmount 1, Scouts with maxAmount 3).

### 3. Hook Up the Squad

In the **Commander tab** of your commander tree asset, select the SquadDefinition as the commanded squad. System variables and bindings are auto-created.

In the **Squad tab** of your agent tree asset, add a connection to the same SquadDefinition. System variables are auto-created on the agent side too.

### 4. Verify Bindings

Open the SquadDefinition in the Squad Editor and check the **Binding Groups** section. You should see both the commander tree and agent tree listed with their auto-created system bindings.

Add any custom bindings needed (e.g. `AgentMoveTarget` Vector3, SquadData on commander side → `targetMovePosition` Vector3 on agent side).

---

## Hints

- Start simple: get the commander forming up and moving to one position before adding patrol sequencing.
- Use `LogVariable` to debug what orders agents are receiving.
- `agentIndex` inside `ForEachAgent` is the loop variable — use it to index into `FormationPositions`.
- If agents don't move, check bindings: both direction and variable name must match on squad, commander, and agent sides.

## Extension Ideas

- **Attack target**: commander detects enemies via squad-level data and sends `AttackTarget` to specific roles.
- **Dynamic roles**: read `AgentRoles` in the commander tree and give different orders per role (scouts flank, leaders engage).
- **Regroup**: if agents get too far apart, send a `Regroup` order with a central position.
