# Assignment: Patrol-Idle-Attack Loop

## Goal

Build a behaviour tree for an enemy that:

1. **Patrols** between waypoints.
2. **Idles** briefly at each waypoint.
3. **Attacks** any detected target, then returns to patrolling when the target is lost.

The loop runs continuously — patrol → idle → patrol → idle → (interrupt with attack) → patrol → ...

## Requirements

- Use at least one **SELECTOR** and at least one **SEQUENCE**.
- Use **`Enemy_FireSequence`** for attacks (it handles the full fire pipeline — reload, aim, trajectory, fire).
- The enemy must **move** between patrol points on a NavMesh.
- There must be an **idle wait** at each patrol point before moving to the next one.

> `patrolPointsParent` (Transform) is already on the blackboard. `ExtractPosition` uses it as a collection to pick waypoints.

## Hints

- **Attack or patrol first?** Which branch should the SELECTOR try first each frame?
- Target selection: `Enemy_FireSequence` needs a `selectedTarget` Transform — use `Enemy_SelectDetectedTarget` (with strategy) to get one.
- Patrol routing: `ExtractPosition` reads `patrolPointsParent` + a mode (Sequential/Random) and writes a Vector3 to `targetMovePosition`. Then `MoveTo` navigates there.
- Idle: `WaitSeconds` has a `duration` float — set it to the idle time.
- The tree loops automatically: when the root's child finishes, the whole tree re-evaluates from the top next frame.

## Challenge: Conditional Abort

The basic tree waits for the patrol branch (move → idle → move → ...) to finish before checking for targets again. This means the enemy walks the entire patrol route before reacting to a target.

**Improvement**: Use a **conditional abort** on the root SELECTOR to interrupt the patrol branch as soon as a target is detected — even while the enemy is mid-patrol.

Read [Conditional Aborts](file:///d:/Dev/TreeCommanderTest/ConditionalAborts.md) to understand the four abort types and decide which one is right for this pattern. The attack branch already has an `Enemy_Detected` condition — that is the condition the abort will re-evaluate every frame.
