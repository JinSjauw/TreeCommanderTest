using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    internal static partial class TickFunctions
    {
        /// <summary>
        /// Re-evaluates child condition for conditional abort.
        /// Called at the start of each composite's Execute().
        /// </summary>
        internal static int CheckConditionalAbort(int compositeIndex, ref TickContext ctx)
        {
            ref NodeData composite = ref ctx.nodeDatas[compositeIndex];
            int first = composite.firstChildIndex;
            int last = composite.lastChildIndex;
            if (first < 0) return ctx.activeChildIndex[compositeIndex];

            AbortType parentAbortType = composite.abortType;
            int childCount = last - first + 1;

            int runningChildLocal = -1;
            for (int child = 0; child < childCount; child++)
            {
                if (ctx.nodeStates[first + child] == NodeState.RUNNING)
                    runningChildLocal = child;
            }

            // ── SELF ──
            if ((parentAbortType == AbortType.Self || parentAbortType == AbortType.Both)
                && runningChildLocal >= 0)
            {
                for (int child = 0; child < childCount; child++)
                {
                    int childIndex = first + child;
                    ref NodeData childNode = ref ctx.nodeDatas[childIndex];

                    bool conditionMet;
                    bool skippedBranchDescendants = false;
                    int branchOriginalChild = child;
                    if (ctx.methodInstances[childIndex] is ConditionMethod)
                    {
                        conditionMet = EvaluateLeafCondition(childIndex, ref ctx);
                    }
                    else if ((childNode.nodeType == BehaviourNodeType.SUBTREE
                           || childNode.nodeType == BehaviourNodeType.DECORATOR) && childNode.firstChildIndex >= 0)
                    {
                        conditionMet = EvaluateCompositeCondition(childIndex, AbortType.Self, true, ref ctx);
                        
                        int containerEndLocal = childNode.lastChildIndex - first;
                        if (containerEndLocal > child) { child = containerEndLocal; skippedBranchDescendants = true; }
                    }
                    else
                    {
                        continue;
                    }

                    bool wasMet = ctx.lastConditionResult[childIndex];
                    ctx.lastConditionResult[childIndex] = conditionMet;


                    if (wasMet != conditionMet && HasRunningSiblingToRight(first, childCount, child, ref ctx))
                    {
                        AbortSiblingsToRight(first, childCount, child, ref ctx);

                        return skippedBranchDescendants ? branchOriginalChild : child;
                    }
                }
            }

            // ── LOWER PRIORITY ──
            for (int c = 0; c < childCount; c++)
            {
                int childIndex = first + c;
                ref NodeData childNode = ref ctx.nodeDatas[childIndex];

                if ((childNode.nodeType == BehaviourNodeType.SUBTREE
                  || childNode.nodeType == BehaviourNodeType.DECORATOR)
                    && childNode.firstChildIndex >= 0)
                {
                    AbortType decAbort = childNode.abortType;
                    if (childNode.nodeType == BehaviourNodeType.DECORATOR
                        && (decAbort == AbortType.LowerPriority || decAbort == AbortType.Both))
                    {
                        bool decConditionMet = EvaluateCompositeCondition(childIndex, decAbort, true, ref ctx);
                        bool decWasMet = ctx.lastConditionResult[childIndex];
                        ctx.lastConditionResult[childIndex] = decConditionMet;
                        if (!decWasMet && decConditionMet && HasRunningSiblingToRight(first, childCount, c, ref ctx))
                        {
                            AbortSiblingsToRight(first, childCount, c, ref ctx);
                            return c;
                        }
                        int decEndLocal = childNode.lastChildIndex - first;
                        if (decEndLocal > c) c = decEndLocal;
                        continue;
                    }

                    // Recurse into the subtree/decorator to find LP/Both composites inside
                    int abortIndex = EvaluateLpConditionsRecursive(
                        childNode.firstChildIndex, childNode.lastChildIndex,
                        first, runningChildLocal, c,
                        childNode.lastChildIndex, childCount, ref ctx);
                    if (abortIndex >= 0) return abortIndex;
                    // Skip past descendants — they were already handled above.
                    int containerEndLocal = childNode.lastChildIndex - first;
                    if (containerEndLocal > c) c = containerEndLocal;
                    continue;
                }

                if (childNode.nodeType != BehaviourNodeType.COMPOSITE) continue;
                AbortType childAbort = childNode.abortType;
                if (childAbort != AbortType.LowerPriority && childAbort != AbortType.Both) continue;

                bool conditionMet = EvaluateCompositeCondition(childIndex, childAbort, false, ref ctx);
                bool wasMet = ctx.lastConditionResult[childIndex];
                ctx.lastConditionResult[childIndex] = conditionMet;

                // BUG #1 FIX: check if ANY child to the right is RUNNING
                if (!wasMet && conditionMet && HasRunningSiblingToRight(first, childCount, c, ref ctx))
                {
                    AbortSiblingsToRight(first, childCount, c, ref ctx);
                    return c;
                }
            }

            return ctx.activeChildIndex[compositeIndex];
        }

        internal static void AbortBranch(int nodeIndex, ref TickContext ctx)
        {
            if (nodeIndex < 0 || nodeIndex >= ctx.nodeDatas.Length) return;

            ref NodeData node = ref ctx.nodeDatas[nodeIndex];

            NodeMethod method = ctx.methodInstances[nodeIndex];
            method?.OnAbort(ctx.blackBoard);

            ctx.nodeStates[nodeIndex] = NodeState.INACTIVE;
            ctx.activeChildIndex[nodeIndex] = 0;

            // Recursively reset children
            if (node.firstChildIndex >= 0)
            {
                for (int i = node.firstChildIndex; i <= node.lastChildIndex; i++)
                    AbortBranch(i, ref ctx);
            }
        }

        private static bool HasRunningSiblingToRight(int first, int childCount, int position, ref TickContext ctx)
        {
            for (int c = position + 1; c < childCount; c++)
            {
                if (ctx.nodeStates[first + c] == NodeState.RUNNING)
                    return true;
            }
            return false;
        }

        private static void AbortSiblingsToRight(int first, int childCount, int position, ref TickContext ctx)
        {
            for (int c = position + 1; c < childCount; c++)
            {
                if (ctx.nodeStates[first + c] == NodeState.RUNNING)
                    AbortBranch(first + c, ref ctx);
            }
        }

        private static bool HasRunningNodeInRange(int fromIndex, int toIndex, ref TickContext ctx)
        {
            for (int i = fromIndex; i <= toIndex; i++)
            {
                if (ctx.nodeStates[i] == NodeState.RUNNING)
                    return true;
            }
            return false;
        }

        private static void AbortRunningNodesInRange(int fromIndex, int toIndex, ref TickContext ctx)
        {
            for (int i = fromIndex; i <= toIndex; i++)
            {
                if (ctx.nodeStates[i] == NodeState.RUNNING)
                    AbortBranch(i, ref ctx);
            }
        }

        private static bool EvaluateLeafCondition(int nodeIndex, ref TickContext ctx)
        {
            NodeMethod method = ctx.methodInstances[nodeIndex];
            if (method == null) return true;

            method.ResolveInputsGeneric(ctx.blackBoard);

            NodeState result = NodeState.SUCCESS;
            if (method is ConditionMethod condition)
            {
                result = condition.Execute(ctx);
            }

            return result == NodeState.SUCCESS;
        }

        private static bool EvaluateCompositeCondition(int compositeIndex, AbortType requiredType, bool insideSubtree, ref TickContext ctx)
        {
            ref NodeData composite = ref ctx.nodeDatas[compositeIndex];
            return FindFirstCondition(composite.firstChildIndex, composite.lastChildIndex, requiredType, insideSubtree, ref ctx);
        }

        private static bool FindFirstCondition(int first, int last, AbortType requiredType, bool insideSubtree, ref TickContext ctx)
        {
            if (first < 0) return true;

            for (int i = first; i <= last; i++)
            {
                ref NodeData child = ref ctx.nodeDatas[i];

                // Method-bearing non-composite leaf → evaluate as condition
                if (ctx.methodInstances[i] != null && child.nodeType != BehaviourNodeType.COMPOSITE)
                {
                    if (child.firstChildIndex >= 0 && child.nodeType == BehaviourNodeType.DECORATOR)
                    {
                        bool found = FindFirstCondition(child.firstChildIndex,
                            child.lastChildIndex, requiredType, true, ref ctx);
                        if (!found) return false;
                        continue;
                    }

                    if (!(ctx.methodInstances[i] is ConditionMethod))
                        continue;

                    bool result = EvaluateLeafCondition(i, ref ctx);
                    ctx.lastConditionResult[i] = result;
                    return result;
                }

                // Composite with children → recurse if abort type is compatible,
                if (child.firstChildIndex >= 0 && child.nodeType == BehaviourNodeType.COMPOSITE)
                {
                    if (!insideSubtree)
                    {
                        bool hasRequired = child.abortType == requiredType ||
                                           child.abortType == AbortType.Both;
                        if (!hasRequired) continue;
                    }

                    bool found = FindFirstCondition(child.firstChildIndex,
                        child.lastChildIndex, requiredType, insideSubtree, ref ctx);
                    if (!found) return false;
                    continue;
                }

                // Subtree with children → always recurse with insideSubtree=true
                if (child.firstChildIndex >= 0 && child.nodeType == BehaviourNodeType.SUBTREE)
                {
                    bool found = FindFirstCondition(child.firstChildIndex,
                        child.lastChildIndex, requiredType, true, ref ctx);
                    if (!found) return false;
                }
            }

            return true;
        }

        private static int EvaluateLpConditionsRecursive(
            int first, int last,
            int parentFirst,
            int runningChildLocal,
            int parentPosition,
            int subtreeScopeLast,
            int parentChildCount,
            ref TickContext ctx)
        {
            int childCount = last - first + 1;

            bool runningInside = ctx.nodeStates[parentFirst + parentPosition] == NodeState.RUNNING;

            ref NodeData originalBranch = ref ctx.nodeDatas[parentFirst + parentPosition];
            int afterSubtreeInParent = originalBranch.lastChildIndex >= 0
                ? originalBranch.lastChildIndex - parentFirst
                : parentPosition;

            for (int i = 0; i < childCount; i++)
            {
                int childIndex = first + i;
                ref NodeData childNode = ref ctx.nodeDatas[childIndex];

                if (childNode.nodeType == BehaviourNodeType.COMPOSITE)
                {
                    AbortType childAbort = childNode.abortType;
                    if (childAbort != AbortType.LowerPriority
                        && childAbort != AbortType.Both) continue;

                    bool conditionMet = EvaluateCompositeCondition(childIndex, childAbort, false, ref ctx);
                    bool wasMet = ctx.lastConditionResult[childIndex];
                    ctx.lastConditionResult[childIndex] = conditionMet;

                    if (!wasMet && conditionMet)
                    {
                        bool didAbort = false;

                        if (runningInside)
                        {
                            int afterLp = childNode.lastChildIndex >= 0 ? childNode.lastChildIndex + 1 : childIndex + 1;
                            if (HasRunningNodeInRange(afterLp, subtreeScopeLast, ref ctx))
                            {
                                AbortRunningNodesInRange(afterLp, subtreeScopeLast, ref ctx);
                                didAbort = true;
                            }
                        }

                        for (int c = afterSubtreeInParent + 1; c < parentChildCount; c++)
                        {
                            if (ctx.nodeStates[parentFirst + c] == NodeState.RUNNING)
                            {
                                AbortBranch(parentFirst + c, ref ctx);
                                didAbort = true;
                            }
                        }

                        if (didAbort)
                            return parentPosition;
                    }
                }
                else if ((childNode.nodeType == BehaviourNodeType.SUBTREE
                       || childNode.nodeType == BehaviourNodeType.DECORATOR)
                    && childNode.firstChildIndex >= 0)
                {
                    int abortIndex = EvaluateLpConditionsRecursive(
                        childNode.firstChildIndex, childNode.lastChildIndex,
                        parentFirst, runningChildLocal, parentPosition,
                        subtreeScopeLast, parentChildCount, ref ctx);
                    if (abortIndex >= 0) return abortIndex;
                }
            }
            return -1;
        }
    }
}
