# Getting Started with Behaviour Trees

## Opening the Editor

1. **Create a tree asset** in the Project window:
   - `Assets > Create > BehaviourTree > Agent Tree` (for a single enemy)
   - `Assets > Create > BehaviourTree > Commander Tree` (for a squad commander)
2. Name it (e.g. "PatrolTree") and **double-click** it to open the Behaviour Tree Editor window.

The editor has three tabs at the top:
- **Graph** — build your tree visually
- **Squad** — link this tree to a squad (available on both agent and commander trees)
- **Commander** — configure the squad this commander commands (commander trees only)

---

## Building a Basic Tree

### Adding Nodes

Right-click anywhere in the graph (or press **Space**) to open the node creation menu. Nodes are grouped:

| Group | What's inside |
|-------|---------------|
| **Composites** | SELECTOR, SEQUENCE, PARALLEL, PRIORITY — control flow |
| **Methods** | All actions (🔴) and conditions (🟡) — the actual behaviour |
| **Decorators** | INVERTER, REPEATER — modify child results |
| **Subtrees** | Other tree assets you can embed |

### Connecting Nodes

Each node has an **output port** (circle at the bottom) and an **input port** (circle at the top).

1. Drag from a node's **output** port.
2. Drop onto another node's **input** port.

A root node is always present. Everything connects below it.

### Typical Structure

```
[🟢 ROOT]
   │
   └── [🔵 SELECTOR]     ← tries children left→right
         │
         ├── [🟣 SEQUENCE]    ← attack branch
         │    ├── [🟡] Enemy_DetectTarget
         │    └── [🔴] Enemy_FireSequence
         │
         └── [🟣 SEQUENCE]    ← patrol branch
              ├── [🔴] ExtractPosition
              └── [🔴] MoveTo
```

---

## What's Inside a Node

Select a node to show its fields in the Inspector. Each field has a label, type, and an input area. Fields come in several kinds:

### Variable (V)
A dropdown to pick a **blackboard variable**. Only variables with the right type are shown.

```
 ┌──────────────────────────────────────────────┐
 │  Target : Transform                          │
 │  ┌────────────────────────────────────────┐  │
 │  │ selectedTarget                      ▼ │  │
 │  └────────────────────────────────────────┘  │
 └──────────────────────────────────────────────┘
```

### Constant (C)
A regular input box — number, toggle, colour, vector, or object field depending on the type.

```
 ┌──────────────────────────────────────────────┐
 │  Spread     ┌────────────────────────────┐   │
 │             │ 0.5                        │   │
 │             └────────────────────────────┘   │
 └──────────────────────────────────────────────┘
```

### Toggle (C / V)
A **C** / **V** button switches between constant and variable mode.

| Button shows | You're in | Click to switch to |
|-------------|-----------|-------------------|
| **C** | Variable mode | Constant mode |
| **V** | Constant mode | Variable mode |

### Operation
A dropdown of operations (e.g. LessThan, Random, Sequential).

### ScriptableObject Constant (C / V / SO)
A three-way toggle. In **SO** mode you pick a field from a ScriptableObject config source. The button cycles **C → V → SO → C → ...**.

---

## Removing Nodes and Connections

- **Select a node** and press **Delete** to remove it.
- **Select a connection** (click the line) and press **Delete** to remove just that connection.
- **Drag a new connection** from a port to replace the existing one.

---

## Creating the Blackboard

The blackboard is the tree's shared memory — variables defined here can be read and written by any node.

### Adding a Variable

In the Blackboard panel (right side of the editor):
1. Type a name in the text field at the bottom.
2. Click **+** or press Enter.
3. A popup opens — pick a **type** (int, float, bool, Vector3, Transform, etc.).
4. The variable appears in the list. You can rename it, change its type, or delete it.

### What Variables Are For

| Variable type | Common uses |
|--------------|-------------|
| `Vector3` | Patrol positions, movement targets |
| `Transform` | Enemy targets, patrol point parents |
| `float` | Health, speed, timers |
| `int` | Agent index, order IDs, ammo count |
| `bool` | Status flags, detection state |

### How Nodes Use Variables

A node field can either hold a **constant value** (typed directly) or **reference a variable** (picked from a dropdown). When a node writes to a variable, any node reading that variable sees the new value next frame.

For example:
- `ExtractPosition` **writes** a patrol position to `targetMovePosition`
- `MoveTo` **reads** `targetMovePosition` to know where to go

### Blackboard Properties

Each variable has:

| Property | Meaning |
|----------|---------|
| **Name** | Used by nodes to reference the variable |
| **Type** | int, float, bool, Vector2/3/4, Transform, GameObject, Color, Enum |
| **Stride** | 1 (single value) or > 1 (array — used by commander squad-data) |
| **SquadData** | (Commander only) Stride adjusts to squad size at runtime |

---

## Assigning the Tree to a GameObject

1. Add the appropriate runner component to the GameObject:
   - **Agent tree**: `AgentTreeRunner` component
   - **Commander tree**: `CommanderTreeRunner` component
2. The `BlackBoard` component is added automatically.
3. Drag the tree asset into the **Authoring Asset** field on the runner.
4. The blackboard builds its variable slots automatically — you can override values per-instance in the Inspector.

---

## Overriding Blackboard Values Per-Instance

On the GameObject's `BlackBoard` component Inspector:

- **Reference types** (Transform, GameObject): Drag a scene object into the slot. A checkbox marks it as overridden.
- **Value types** (int, float, Vector3): Override fields appear for each variable. They survive blackboard definition changes because they are stored by variable name, not by slot index.

---

## Editor Controls Reference

| Key / Action | What it does |
|-------------|-------------|
| **A** | Frame all nodes (fit tree on screen) |
| **Ctrl+Z** | Undo |
| **Ctrl+Y** | Redo |
| **Right-click** / **Space** | Open node creation menu |
| **Drag from port** | Connect nodes (output → input) |
| **Left-click** node | Select and show properties |
| **Delete** | Delete selected node or edge |
