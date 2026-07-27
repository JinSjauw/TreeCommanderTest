# Getting Started: Behaviour Tree Graph Editor

The Behaviour Tree Editor is a node-graph window for building and inspecting commander and agent trees. This page covers how to open it and the basic controls for navigating and editing a graph.

---

## Opening the Graph via the Top Menu

1. In the Unity **top menu bar**, go to **BehaviourTree → Open Behaviour Tree Graph**.
2. The **Behaviour Tree Editor** window opens. It starts empty — load a tree by:
   - **Selecting a tree asset** in the Project window (the window follows your selection), or
   - **Double-clicking a tree asset** (opens the window directly with that tree), or
   - **Right-clicking the empty canvas → Create New Tree → Agent Tree / Commander Tree**.

![Opening the graph via the BehaviourTree top menu](Images/ToolsMenu.png)

> The same top menu also holds **Open Squad Editor**, **Open Template Editor**, and the code-generation commands.

---

## Basic Graph Controls

### Navigating the Canvas

| Action | Control |
|--------|---------|
| Zoom in / out | Mouse scroll wheel |
| Pan the canvas | Middle mouse button drag |
| Frame the whole tree | Automatic — the graph frames all nodes when a tree is loaded |

### Selecting and Moving

| Action | Control |
|--------|---------|
| Select a node or edge | Left click |
| Box-select multiple nodes | Left drag on empty canvas |
| Move selected node(s) | Left drag on a selected node |

### Building the Tree

| Action | Control |
|--------|---------|
| Open the context menu | Right-click on empty canvas |
| Create a node | Right-click → **Create Node** → pick from the search window |
| Create + connect in one step | Drag from a node's **output port**, drop on empty canvas → the node search opens and the new node is auto-connected |
| Connect two nodes | Drag from a parent's **output port** to a child's **input port** |
| Add a sticky note | Right-click → **Add Note** |
| Extract a subtree | Select nodes → right-click → **Subtree → Extract Selection To Subtree** (or **Copy Selection Into Subtree**) |

### Editing Shortcuts

| Action | Shortcut |
|--------|----------|
| Delete selected nodes/edges | `Delete` |
| Copy / Paste selection | `Ctrl+C` / `Ctrl+V` |
| Undo / Redo | `Ctrl+Z` / `Ctrl+Y` |
| Confirm a node rename | `Enter` |

> **Play mode:** the graph becomes read-only — it mirrors the running tree with live state visuals, and structural edits (connecting/deleting) are blocked until you exit play mode.

---

## The Title Bar

The bar at the top of the graph (left to right):

- **Badge `[C]` / `[A]`** — whether the loaded tree is a **Commander** or **Agent** tree.
- **Tree name field** — edit to rename the tree asset on disk.
- **Runner dropdown** — pick a `BehaviourTreeRunnerBase` in the scene to inspect its tree (used for play-mode debugging).
- **Lock toggle** — locks the window to the current tree so clicking other assets doesn't switch it.
- **◀ / ▶ buttons** — cycle backwards/forwards through your recently opened trees.

---

## Related

- [Getting Started: Creating a New Squad Schema](Getting_Started_SquadSchema.md)
- [Assignment: Wiring the Patrol Test](Assignment_PatrolTest_SquadWiring.md)
