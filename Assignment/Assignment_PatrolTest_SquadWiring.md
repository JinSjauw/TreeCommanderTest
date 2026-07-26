# Assignment: Wiring the Patrol Test (Preset Trees)

> **Focus: the Squad Definition Editor.** You will not build or edit any trees in this assignment — the two trees already exist. Your job is to connect them with a SquadDefinition so the squad patrols in formation.

**Preset assets (already in the project):**

| Asset | Path | Role |
|-------|------|------|
| Commander tree | `Assets/ExposureTest/CommanderPatrolTest.asset` | Calculates formation, sends PATROL order |
| Agent tree | `Assets/ExposureTest/SquaddieTest.asset` | Receives order, moves to its formation slot |
| Squad prefab | `Assets/ExposureTest/SquadTest.prefab` | Has the `SquadManager` component |
| Commander prefab | `Assets/ExposureTest/CommanderTest.prefab` | Has the `CommanderTreeRunner` |
| Agent prefab | `Assets/ExposureTest/SquaddieTank.prefab` | Has the `AgentTreeRunner` |

## Goal

Wire the preset commander tree and agent tree together through a new SquadDefinition, so that when you press Play the agents move in a circle formation from patrol point to patrol point.

---

## Step 1 — Read the trees and decide which variable to pass

Open both trees (double-click the assets) and inspect their blackboards and nodes.

**CommanderPatrolTest:**
- `ExtractPosition` reads `PatrolPoints` and writes the next waypoint into `SquadMovePosition`.
- `CalculateFormation` takes `SquadMovePosition` as the centre and writes each agent's circle position into **`AgentMovePosition`** (Vector3, SquadData — one slot per agent).
- `SendOrder` writes `PATROL` into `AgentOrders`.
- `PollAgentStatus` reads `AgentStatus`.

**SquaddieTest:**
- `CheckOrder` (expects PATROL) → `ReportStatus(Running)` → `MoveTo(target: AgentMoveTarget)` → `ReportStatus(Success)`.
- **`AgentMoveTarget`** (Vector3) is *read* by `MoveTo`, but *nothing inside the agent tree writes it*.

**Conclusion — the variable you must pass to the agent is its formation position:**

```
commander  AgentMovePosition[i]  ──►  squad  ──►  agent  AgentMoveTarget
```

So the squad definition needs **one custom variable**: `AgentMovePosition` (Vector3, **SquadData** = per-agent array). Orders, roles, status and leader index are *system channels* — you do not create those by hand; they appear automatically in Step 4.

**[ SCREENSHOT: both trees open, highlighting AgentMovePosition on the commander and AgentMoveTarget on the agent ]**

---

## Step 2 — Create a new SquadDefinition

1. Open the Squad Editor via the menu `BehaviourTree > Open Squad Editor`.
2. Click **Create New Squad** and save it (e.g. `PatrolSquad`).
3. In the **Blackboard section** (top), add the custom variable from Step 1:
   - Name: `AgentMovePosition`
   - Type: `Vector3`
   - **SquadData: ON** (one slot per agent — the stride is set automatically at runtime)

Do not add `AgentRoles`, `AgentOrders`, `AgentStatus` or `LeaderIndex` manually — those are system variables and are auto-created when you add binding groups in Step 4.

**[ SCREENSHOT: the new SquadDefinition in the Squad Editor with the AgentMovePosition variable ]**

---

## Step 3 — Define the squad (roles / role composition)

In the **Roles section** (middle), click **+ Add Role**:

| Field | Value |
|-------|-------|
| Name | `ASSAULT` |
| Colour | any |
| Max Amount | `3` |
| Is Fallback | ✓ |
| Prefab | empty (falls back to the SquadManager's agent prefab) |

The sum of `Max Amount` across all roles = the total number of agent slots in the squad. Keep this number in mind — the SquadManager's `Agent Count` may not exceed it (extra agents are clamped with a warning).

**[ SCREENSHOT: the Roles section with the ASSAULT role filled in ]**

---

## Step 4 — Define the tree bindings

In the **Binding Groups section** (bottom) you create one binding group per tree. A binding says: *which tree variable reads or writes which squad variable, and in which direction* (`ToSquad` = tree → squad, `FromSquad` = squad → tree).

### 4a. Commander binding group

Click **+ Add Binding Group** and select **CommanderPatrolTest**. The system bindings appear automatically:

| Squad Variable | Tree Variable | Direction |
|----------------|---------------|-----------|
| AgentRoles | AgentRoles | FromSquad |
| AgentOrders | AgentOrders | ToSquad |
| AgentStatus | AgentStatus | FromSquad |
| LeaderIndex | LeaderIndex | FromSquad |

Click **+ Add Binding** and add the custom binding for the formation position:

| Squad Variable | Tree Variable | Direction | Why |
|----------------|---------------|-----------|-----|
| AgentMovePosition | AgentMovePosition | **ToSquad** | The commander *writes* each agent's formation slot into the squad |

**[ SCREENSHOT: the commander binding group with all five bindings ]**

### 4b. Agent binding group

Click **+ Add Binding Group** and select **SquaddieTest**. The system bindings appear automatically:

| Squad Variable | Tree Variable | Direction |
|----------------|---------------|-----------|
| AgentRoles | AgentAssignedRole | ToSquad |
| AgentOrders | AgentReceivedOrder | FromSquad |
| AgentStatus | AgentStatus | ToSquad |

Click **+ Add Binding** and add the custom binding:

| Squad Variable | Tree Variable | Direction | Why |
|----------------|---------------|-----------|-----|
| AgentMovePosition | AgentMoveTarget | **FromSquad** | The agent *reads* its own formation slot — the per-agent offset is applied automatically |

> Variable names must match **exactly** (the commander's output is `AgentMovePosition`, the agent's input is `AgentMoveTarget`). A typo or a wrong direction silently results in agents that never move.

**[ SCREENSHOT: the agent binding group with all four bindings ]**

---

## Step 5 — Wire up the SquadTest prefab

Open `Assets/ExposureTest/SquadTest.prefab` and set the **SquadManager** component fields:

| Field | Value |
|-------|-------|
| Definition | your new SquadDefinition from Step 2 |
| Commander Prefab | `CommanderTest` |
| Agent Prefab | `SquaddieTank` |
| Agent Count | `3` (≤ total role slots from Step 3) |
| Spawn On Start | ✓ |
| Leader Death Behavior | `Promote` |

**[ SCREENSHOT: the SquadManager component on the SquadTest prefab, fully assigned ]**

---

## Step 6 — Check the tree runners

Both runners must point at the correct tree assets:

1. Open `CommanderTest.prefab` → `CommanderTreeRunner` → **Authoring Asset** = `CommanderPatrolTest`.
2. Open `SquaddieTank.prefab` → `AgentTreeRunner` → **Authoring Asset** = `SquaddieTest`.

**[ SCREENSHOT: CommanderTreeRunner with CommanderPatrolTest assigned ]**

**[ SCREENSHOT: AgentTreeRunner with SquaddieTest assigned ]**

---

## Step 7 — Press Play and verify

Expected result:
- The commander and 3 agents spawn; agents take a circle formation around the commander.
- The formation moves from patrol point to patrol point (PATROL order).

If agents stand still, check in order:
1. Both binding groups exist and the custom bindings use the exact names from Step 4.
2. Directions: commander `ToSquad`, agent `FromSquad`.
3. Console warning about `Agent Count` being clamped (squad too small).
4. The agent actually receives the PATROL order — add a temporary `LogVariable` on `AgentReceivedOrder`.

**[ SCREENSHOT: Play mode, agents moving in circle formation ]**
