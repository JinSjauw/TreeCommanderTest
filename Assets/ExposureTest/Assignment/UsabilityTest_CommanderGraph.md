# Usability Test: Commander Graph — Coordination & Synchronization

## 1. Test Goal

Evaluate whether a participant with some technical understanding can:

- navigate the basic tree-editing flow: **create a new tree, assign it to a runner**, add a blackboard variable, and use the graph to complete a small scenario;
- reason about **blackboard scope**: decide where a variable should live (tree-local vs squad-level) and what shape it should have;
- understand and correctly use the two commander-specific structural nodes:
  - **iteration over squad members** (`ForEachAgent` / `ForEachRole`),
  - **group synchronization** (`PollAgentStatus`).

This is a **usability test, not a tutorial**. Do not teach during the test. Observe, take notes, and only give hints when the participant is stuck (see the hint ladder).

**Target duration:** 10–15 minutes. **Format:** think-aloud.

---

## 2. Participant Context / Prerequisites

The participant receives a project where the squad setup is **already working**:

- A **SquadDefinition** exists, with roles and bindings configured.
- The **SquadTest prefab** is wired (SquadManager, commander prefab, agent prefab).
- The agent tree already exists and is assigned — agents already know how to receive an order and move.

**Before the test, the moderator removes the tree asset from the commander prefab's `CommanderTreeRunner`.** The participant starts with a commander that runs *nothing* — creating the tree and hooking it up is part of the test.

**Handout:** give the participant the [Pre-Brief](UsabilityTest_PreBrief.md) to read before starting. They may keep it during the test, along with the [Graph Editor getting-started page](Getting_Started_GraphEditor.md).

---

## 3. Assignment

Current behavior: when Play is pressed, the commander and agents spawn, but **nothing happens** — the commander has no tree.

### Task 0 — Create the commander tree and hook up the patrol points

> Create a **new commander tree** and make the commander run it. Then, on that tree's blackboard, add a variable that holds a reference to the patrol-points Transform (a parent object with the points as its children):
>
> - Name it **exactly** `PatrolPoints`
> - Type: **Transform**
> - A **single value** (not SquadData)
>
> Name and type are all that matter — the scene fills in the actual reference at runtime.

### Task 1 — Formation coordination

> Get the squad moving **as a formation**: using the new commander tree, when the squad heads to a patrol point, each agent should move to **its own position** around that point, instead of everyone piling onto one spot.

You will need to add at least one new variable. Where that variable lives and what shape it has is part of the task.

**Done when:** pressing Play shows the agents spreading out into distinct positions around each patrol point.

### Task 2 — Group synchronization

> Make the squad **move as a group**: the commander should only send the squad to the *next* patrol point once **every agent has arrived** at the current one.

This may require changes in **more than one tree**.

**Done when:** the squad advances point-to-point as a group — no agent gets left behind, and the commander doesn't move on early.

### Optional Extension — Role-specific version

> Use role-based iteration so that **only agents of a specific role** take part in the formation (or receive a different order than the rest).

---

## 4. Allowed Node List

The participant may use any node, but these are the relevant ones. Descriptions are intentionally brief — figuring out *where* and *how* to use them is the test.

| Node | One-line description |
|------|---------------------|
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

## 5. Hint Ladder

Give hints **one at a time**, only when the participant is genuinely stuck. Stop hinting as soon as they move again. Record which hints were needed.

- **Hint 1 (hookup):** "A tree asset does nothing until something runs it. Look at the commander prefab — which component executes a tree, and what does it need to know?"
- **Hint 2 (scope):** "Think about the data each agent needs to move into formation. Does every agent need the *same* value, or its *own* value? Where could you store something so the commander and all agents can reach it?"
- **Hint 3 (structure):** "To give every agent its own value, you need to do something *for each* agent — look at the iteration nodes. The formation math works best *inside* that iteration."
- **Hint 4 (synchronization):** "For the waiting: the commander can *poll* the agents' statuses — but that only works if agents actually *report* when they start and finish something. That report happens on the agent side."

---

## 6. Debrief Questions

Ask verbally after the test (or note observations if time is short):

1. How did you figure out how to get the commander to run your tree? Was anything about that step unclear?
2. Where did you decide the new data should live, and why?
3. At what point did you realize each agent needed its *own* value? What gave it away?
4. In your own words: how does a value get from the commander to one specific agent?
5. Before using it, what did you *expect* the status-polling node to do? Did it match?
6. For the waiting behavior — what made you look at the agent tree? (Or: what stopped you?)
7. Was anything in the UI unclear, misleading, or hard to find? (tabs, blackboard, binding list, node search, runner assignment)

---

## 7. Moderator Notes / Observation Points

**Setup checklist (before the participant starts):**
- [ ] Squad wiring verified working (with a reference tree, agents patrol in formation on Play)
- [ ] **Tree asset removed from the commander prefab's `CommanderTreeRunner`** (Authoring Asset = None)
- [ ] Pre-Brief handout ready; screen recording / notes ready
- [ ] Pre-Brief read by participant, no extra explanation given

**Observe and note:**

| Moment | What to watch for |
|--------|-------------------|
| Tree creation | Do they create the right tree *type* (commander vs agent)? Do they create it via the graph context menu, the Create menu, or get lost? |
| Runner assignment | Do they find the **Authoring Asset** field on the runner unprompted? Do they try pressing Play before assigning anything? Do they notice the field is empty? |
| Start | Do they inspect the agent tree to understand what the commander must provide (order, target)? |
| Variable creation | *Where* do they create the variable (agent BB / commander BB / squad BB)? Do they hesitate about scope? |
| Variable shape | Do they mark it as per-agent data without a hint? If they pick a plain single value, when (if ever) do they notice the problem? |
| Iteration | Do they find the iteration node themselves? Do they place the formation logic inside it, or next to it? |
| Binding | Do they remember/realize the new variable needs a binding to reach the agents? Direction chosen correctly? |
| Synchronization | Do they realize the agent tree must *report* status, unprompted? Do they place the reports around the movement (start/finish) or elsewhere? |
| Order of operations | Do they put the wait *after* issuing the movement, or somewhere that can't work? |
| General UX | Node search usage, tab confusion (Squads vs Commander), any misclicks, dead ends, or moments of surprise |

**Note for scoring:** record time per task, hints used (which number), and whether the extension was attempted.

**Do not:**
- confirm or deny a choice while the test is running ("interesting — what makes you pick that?" is fine);
- point at specific UI elements unless giving a ladder hint;
- explain the tick order or binding direction — that is what is being tested.
