# Usability Test: Pre-Brief (Participant Handout)

Read this before starting. Ask the moderator if anything is unclear — once the test begins, the moderator will not explain concepts.

---

## Blackboard Variables

Every tree has a **blackboard** — a set of named variables the tree's nodes read from and write to (e.g. a target position, a speed, a status). You can see and edit a tree's blackboard in the **Shared** tab of the tree editor.

The squad also has its **own blackboard**, which acts as shared storage between the commander and the agents. Trees connect to it through **bindings**.

## SquadData

A variable on the squad or commander blackboard can be marked as **SquadData**. This simply means: instead of holding *one* value, it holds *one value per squad member* — like a row of mailboxes, one per agent.

- Agents never see the whole row; each agent only ever sees **its own mailbox**, as a plain single value.
- The commander can see (and write) **all of them**.

## A Decision You Will Face

When you add a variable, think about:

- **Who produces** the data and **who consumes** it?
- Does everyone need the **same value**, or does each squad member need **their own**?

That tells you whether the variable belongs to a single tree or to the squad — and what shape it should have.

---

## Reference

If you need a reminder on the editor controls (opening the graph, adding nodes, the side panel, assigning a tree to a runner), you may use the [Graph Editor getting-started page](Getting_Started_GraphEditor.md) during the test.

You may look anywhere in the tool. **Think aloud as you work.**
