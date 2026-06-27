// using BehaviourTree.Core;
// using UnityEngine;

// namespace BehaviourTree.Runtime.Methods
// {
//     /// <summary>
//     /// Scans all agents for the one nearest to a reference position,
//     /// selects that agent, then ticks children. On RUNNING, persists the
//     /// selected agent. Returns FAILURE if no agent is found.
//     /// Sets ctx.agentIndex so children access the selected agent's slots.
//     /// </summary>
//     //[NodeMethod("GetNearestAgent", allowedTreeType = AllowedTreeType.Commander)]
//     public sealed class GetNearestAgentMethod : CompositeMethod
//     {
//         [SharedVar(SkipAutoResolve = true)] public int targetAgentIDSlot;
//         [SharedVar(SkipAutoResolve = true)] public int agentPositionSlot;
//         [SharedVar(SkipAutoResolve = true)] public int referencePositionSlot;

//         public override NodeState Execute(int nodeIndex, ref TickContext ctx)
//         {
//             ref NodeData node = ref ctx.nodeDatas[nodeIndex];
//             if (node.firstChildIndex < 0) return NodeState.SUCCESS;

//             BlackBoard bb = ctx.blackBoard;

//             // Resume from saved state if children were RUNNING last tick
//             if (ctx.runningAgentIndex[nodeIndex] != 0)
//             {
//                 int savedAgentIndex = ctx.runningAgentIndex[nodeIndex] - 1;
//                 ctx.agentIndex = savedAgentIndex;
//                 NodeState resumeResult = TickDispatcher.TickNode(node.firstChildIndex, ref ctx);
//                 if (resumeResult == NodeState.RUNNING)
//                     return NodeState.RUNNING;

//                 ctx.runningAgentIndex[nodeIndex] = 0;
//                 return resumeResult;
//             }

//             int count = ctx.agentCount;

//             // Resolve raw slot offsets for per-agent reads/writes
//             int rawTargetSlot = GetSlotByName(nameof(targetAgentIDSlot));
//             int rawPositionSlot = GetSlotByName(nameof(agentPositionSlot));
//             int rawReferenceSlot = GetSlotByName(nameof(referencePositionSlot));

//             if (rawPositionSlot < 0 || rawReferenceSlot < 0 || rawTargetSlot < 0 || count <= 0)
//                 return NodeState.FAILURE;

//             object refBoxed = bb.GetBoxed(rawReferenceSlot);
//             Vector3 referencePosition = refBoxed is Vector3 vec ? vec : Vector3.zero;

//             int bestAgentIndex = -1;
//             float nearestDistance = float.MaxValue;

//             for (int agentIndex = 0; agentIndex < count; agentIndex++)
//             {
//                 object posBoxed = bb.GetBoxedRaw(rawPositionSlot + agentIndex);
//                 if (!(posBoxed is Vector3 agentPosition))
//                 {
//                     continue;
//                 }

//                 float distance = Vector3.Distance(agentPosition, referencePosition);
//                 if (distance < nearestDistance)
//                 {
//                     nearestDistance = distance;
//                     bestAgentIndex = agentIndex;
//                 }
//             }

//             if (bestAgentIndex < 0)
//                 return NodeState.FAILURE;

//             bb.SetBoxed(rawTargetSlot, bestAgentIndex);

//             ctx.agentIndex = bestAgentIndex;
//             NodeState result = TickDispatcher.TickNode(node.firstChildIndex, ref ctx);

//             if (result == NodeState.RUNNING)
//             {
//                 ctx.runningAgentIndex[nodeIndex] = bestAgentIndex + 1;
//                 return NodeState.RUNNING;
//             }

//             ctx.runningAgentIndex[nodeIndex] = 0;
//             return result;
//         }
//     }
// }
