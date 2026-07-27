# Getting Started: Creating a New Squad Schema

A **SquadDefinition** (squad schema) is the bridge between one commander tree and any number of agent trees. The trees never talk to each other directly — all shared data flows through the squad's blackboard.

A squad schema has three parts:

1. **Squad Blackboard** — the shared variables (system variables are auto-created; you add custom ones).
2. **Roles** — the squad's composition. The sum of `Max Amount` = total squad size.
3. **Binding Groups** — per-tree mappings: which tree variable reads/writes which squad variable, and in which direction.

---

## The Data Flow: Commander ← SquadDef → Agent

```
                 SquadDefinition (squad blackboard)
        ┌────────────────────────────────────────────────┐
        │  AgentRoles[i]                                 │ ← system (auto)
        │  AgentOrders[i]                                │
        │  AgentStatus[i]                                │
        │  LeaderIndex                                   │
        │  AgentMovePosition[i]                          │  ← custom (yours)
        └───────▲───────────────────────▲────────────────┘
                │ ToSquad / FromSquad   │ ToSquad / FromSquad
                │ (bindings, full array)│ (bindings, one slot per agent)
        ┌───────┴────────┐      ┌───────┴────────┐
        │   Commander    │      │   Agent i      │
        │   blackboard   │      │   blackboard   │
        └────────────────┘      └────────────────┘
```

- The **commander** sees the *whole arrays*: it reads `AgentStatus[0..N-1]` and writes `AgentOrders[0..N-1]`.
- Each **agent** sees only *its own slot*: agent 2 reads `AgentOrders[2]` into its `AgentReceivedOrder` and writes its `AgentStatus` back into `AgentStatus[2]`. The agent never knows its index — the offset is applied automatically by the runner.

**Binding directions** (set per binding row in the Squad Editor):

| Direction | Data flows | Typical use |
|-----------|-----------|-------------|
| `ToSquad` | tree → squad | Commander publishes formation positions; agent reports status |
| `FromSquad` | squad → tree | Commander reads status array; agent reads its order |
| `Both` | both ways | Rare — shared scratch values |

---

## Variable Kinds: System vs SquadData vs Normal

Three kinds of variables can live on a squad blackboard:

| Kind | Created by | Examples | Stride (array size) | Editable in the UI |
|------|-----------|----------|---------------------|--------------------|
| **System** | Auto-created when a binding group is added | `AgentRoles`, `AgentOrders`, `AgentStatus`, `LeaderIndex` | Managed by the system | Locked — cannot rename, retype, or delete |
| **Custom SquadData** | You, with **SquadData ON** | `AgentMovePosition` | One slot per agent — set automatically to the squad size at runtime | Yes |
| **Custom normal** | You, with **SquadData OFF** | a shared rally point, a squad target | 1 (single value) | Yes |

- **System variables** are the fixed communication channels every squad needs (who am I, what should I do, how am I doing, who leads). Never create them by hand — adding a binding group creates them on the squad *and* on the tree, plus their bindings.
- **SquadData variables** are your custom **per-agent** channels: the variable is an array with one slot per squad member.
- **Normal variables** are your custom **squad-wide** channels: one value shared by everyone.

### How agents read/write SquadData

A SquadData variable is an array on the squad blackboard, but a tree never deals with the whole array at once — the binding bridge slices it:

```
Squad BB:   AgentMovePosition[0]  [1]  [2]      (one slot per agent)
                                 │
                        binding bridge applies offset i
                                 ▼
Agent 2 BB: AgentMoveTarget                       (normal single Vector3)
```

- **Commander ↔ squad:** copies the **whole array** in one go. This is why commander-side variables like `AgentMovePosition` are also marked SquadData — the commander needs every agent's slot (e.g. `CalculateFormation` writes a position for each agent inside `ForEachAgent`).
- **Agent ↔ squad:** copies **only slot `[i]`**, where `i` is the agent's registration index. The agent-side tree variable is always a **normal single value** (stride 1): the bridge copies squad `AgentMovePosition[2]` → agent `AgentMoveTarget` before the agent ticks, and agent `AgentStatus` → squad `AgentStatus[2]` after it ticks.
- **The agent never knows its index.** It reads and writes plain single-value variables; the offset is applied automatically around its evaluation. Agent 0 and agent 2 run the *same tree* — they just see different slots.
- **Direction still applies per copy:** `FromSquad` pulls the slot in before the tick, `ToSquad` pushes the slot back after the tick. Other agents' slots are never touched by this agent.

Rule of thumb when designing: if each agent needs its **own** value (move target, health threshold per role), make it SquadData; if **everyone** shares one value (rally point, squad enemy), keep it a normal variable.

---

## Tick Order: Commander First, Then Agents

All ticking is driven by the **CommanderTreeRunner** — agents in a squad do not tick themselves. Each frame, in this exact order:

```
1. Commander: copy squad → commander blackboard (FromSquad bindings, full arrays)

2. Commander tree evaluates

3. Commander: copy commander blackboard → squad  (ToSquad bindings, full arrays)

4. For each agent i (in registration order):
   a. copy squad → agent blackboard    (FromSquad bindings, slot [i] only)
   b. agent tree evaluates
   c. copy agent blackboard → squad    (ToSquad bindings, slot [i] only)
```

**Consequences to keep in mind while designing:**

- An order the commander writes this frame is seen by the agents **in the same frame** (agents tick after the commander).
- The agent status the commander reads is from the **previous frame** (agents haven't ticked yet this frame). Design one frame of delay into your logic.
- System channels work out of the box: roles and leader index flow squad → commander, orders flow commander → squad → agent, status flows agent → squad → commander.

---

## When Is Squad Data Pushed?

There are two moments data enters the blackboards:

### 1. Once, at spawn (SquadManager)

When the squad spawns, the SquadManager writes initial values directly (no binding needed):

| Written to | Variable | Value |
|-----------|----------|-------|
| Each agent BB | `AgentAssignedRole` | the agent's role index from the role composition |
| Squad BB | `LeaderIndex` | the initial leader |
| Commander BB | `PatrolPoints`, `AgentCount`, `SquadMovePosition` | scene setup values |

### 2. Every frame, around each tree evaluation (the binding bridge)

Squad data is **pulled in right before** a tree ticks and **pushed out right after** — that is the copy steps in the tick-order diagram above. A binding only transfers data at those moments; it is not a live link.

---

## Creating a New Squad Schema: Checklist

1. **Create the asset** — `BehaviourTree > Open Squad Editor` → **Create New Squad**. (SquadDefinitions are created from the editor, not the `Assets > Create` menu.)
2. **Add custom blackboard variables** — top section. Toggle **SquadData** for anything per-agent (stride is managed automatically). Read your trees first: every variable a tree reads but doesn't write itself must come from the squad.
3. **Add roles** — middle section. Sum of `Max Amount` = total agent slots; mark one role **Is Fallback**; optionally give a role its own prefab.
4. **Add a binding group per tree** — bottom section, **+ Add Binding Group**. System variables and system bindings (roles/orders/status/leader) are created automatically — don't add them by hand.
5. **Add custom bindings** — **+ Add Binding** inside each group. Match the variable names *exactly* as they appear in each tree, and pick the direction from the tree's point of view (does this tree write it or read it?).
6. **Wire the scene** — a `SquadManager` with: your Definition, a commander prefab (`CommanderTreeRunner` + commander tree asset), an agent prefab (`AgentTreeRunner` + agent tree asset), and `Agent Count` ≤ total role slots.

> See [Assignment: Wiring the Patrol Test](Assignment_PatrolTest_SquadWiring.md) for a guided walkthrough of this checklist with the preset patrol trees.
