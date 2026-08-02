# Squad Commander — Test Sheet

Thanks for taking part! You'll complete a few tasks in a Unity behaviour-tree tool. There are **no wrong answers** — we're testing the tool, not you. Please **think aloud** as you work, and ask whenever something is unclear.

You may look anywhere in the tool and use the [Graph Editor reference page](Getting_Started_GraphEditor.md) at any time.

---

## Background Concepts

**Blackboard variables**
Every tree has a blackboard — a set of named variables its nodes read from and write to (e.g. a target position, a speed, a status). The squad also has its *own* blackboard, which acts as shared storage between the commander and the agents. Trees connect to it through *bindings*.

**SquadData**
A variable on the squad or commander blackboard can be marked as *SquadData*. Instead of holding one value, it holds **one value per squad member** — like a row of mailboxes, one per agent. Each agent only ever sees its own mailbox, as a plain single value. The commander can see all of them.

**A decision you'll face**
When you add a variable, think about: who *produces* the data, who *consumes* it, and whether everyone needs the *same* value or *their own*. That tells you where the variable belongs and what shape it should have.

---

## The Situation

A squad of agents is set up and wired: squad definition, bindings, and prefabs are done, and the **agent tree already exists** — agents know how to receive an order and move.

But the **commander has no tree**. When you press Play, the squad spawns and nothing happens.

---

## Your Tasks

### Task 0 — Create the commander tree and hook up the patrol points

Create a **new commander tree** and make the **CommanderTest.prefab** run it. Then, on that tree's blackboard, add a variable that holds a reference to the patrol-points Transform (a parent object with the points as its children):

- Name it **exactly** `PatrolPoints`
- Type: **Transform**
- A **single value** (not SquadData)

Name and type are all that matter — the scene fills in the actual reference for you at runtime.

### Task 1 — Move as a formation

Get the squad moving: using your new commander tree, when the squad heads to a patrol point, **each agent should move to its own position around that point** — not everyone piling onto one spot.

**Done when:** on Play, the agents spread out into distinct positions around each patrol point.

### Task 2 — Move as a group

The commander should only send the squad to the **next patrol point once every agent has arrived** at the current one. This may require changes in more than one tree.

**Done when:** the squad advances point-to-point as a group — nobody left behind, nobody moving on early.

### Optional — Roles

Only if you have time: use role-based iteration so that **only agents of a specific role** take part in the formation (or receive a different order than the rest).

---

## Toolbox

You may use any node. These are the most relevant ones:

| Node | What it does |
|------|-------------|
| SEQUENCE / SELECTOR / PRIORITY | Standard flow control |
| `ExtractPosition` | Gets the next position from a patrol-points collection |
| `CalculateFormation` | Computes per-agent formation positions around a center |
| `ForEachAgent` | Runs its children once for every agent in the squad |
| `ForEachRole` | Runs its children once for every role in the squad |
| `SendOrder` | Writes an order value for agents to pick up |
| `CheckOrder` | (Agent side) checks which order this agent received |
| `ReportStatus` | (Agent side) writes a status value (Success / Failure / Running) |
| `PollAgentStatus` | Checks the agents' statuses; succeeds only when all are done |
| `MoveTo` | (Agent side) moves the agent to a target position |
| `SetVariable` / `LogVariable` | Utility: write a value / print a value for debugging |

---

*Take about 10–15 minutes. If you get properly stuck, I can give you a small hint — asking for one is fine and doesn't count against you.*
