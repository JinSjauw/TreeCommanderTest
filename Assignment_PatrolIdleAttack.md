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
