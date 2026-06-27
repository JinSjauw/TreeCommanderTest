using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Conditional abort helpers for composite nodes.
    /// Implements Behaviour Designer-style Self, LowerPriority, and Both abort types.
    /// </summary>
    internal static partial class TickFunctions
    {
        /// <summary>
        /// Re-evaluates child conditions for conditional abort.
        /// Called at the start of each composite's Execute().
        /// Returns the child index to resume from (may change if abort occurred).
        /// </summary>
        internal static int CheckConditionalAbort(int compositeIndex, ref TickContext ctx)
        {
            ref NodeData composite = ref ctx.nodeDatas[compositeIndex];
            int first = composite.firstChildIndex;
            int last = composite.lastChildIndex;
            if (first < 0) return ctx.activeChildIndex[compositeIndex];

            AbortType parentAbortType = composite.abortType;
            int childCount = last - first + 1;

            // BUG #1 FIX: Find the RIGHTMOST running child instead of the first.
            // This matters for Parallel composites where multiple children can be RUNNING.
            int runningChildLocal = -1;
            for (int c = 0; c < childCount; c++)
            {
                if (ctx.nodeStates[first + c] == NodeState.RUNNING)
                    runningChildLocal = c; // don't break — keep scanning for the rightmost
            }

            // ── SELF abort pass ──
            if ((parentAbortType == AbortType.Self || parentAbortType == AbortType.Both)
                && runningChildLocal >= 0)
            {
                for (int c = 0; c < childCount; c++)
                {
                    int childIndex = first + c;
                    ref NodeData childNode = ref ctx.nodeDatas[childIndex];

                    bool conditionMet;
                    bool skippedSubtreeDescendants = false;
                    int subtreeOriginalC = c;
                    if (ctx.methodInstances[childIndex] is ConditionMethod)
                    {
                        conditionMet = EvaluateLeafCondition(childIndex, ref ctx);
                    }
                    else if ((childNode.nodeType == BehaviourNodeType.SUBTREE
                           || childNode.nodeType == BehaviourNodeType.DECORATOR)
                        && childNode.firstChildIndex >= 0)
                    {
                        // insideSubtree=true so we see through all composites and decorators
                        conditionMet = EvaluateCompositeCondition(childIndex, AbortType.Self, true, ref ctx);
                        // Skip past subtree/decorator descendants — they were already handled by EvaluateCompositeCondition.
                        int containerEndLocal = childNode.lastChildIndex - first;
                        if (containerEndLocal > c) { c = containerEndLocal; skippedSubtreeDescendants = true; }
                    }
                    else
                    {
                        continue;
                    }

                    bool wasMet = ctx.lastConditionResult[childIndex];
                    ctx.lastConditionResult[childIndex] = conditionMet;

                    // BUG #1 FIX: check if ANY child to the right is RUNNING, not just the first one
                    if (wasMet != conditionMet && HasRunningSiblingToRight(first, childCount, c, ref ctx))
                    {
                        AbortSiblingsToRight(first, childCount, c, ref ctx);
                        // If we skipped subtree descendants, the abort restarts at the subtree's position
                        return skippedSubtreeDescendants ? subtreeOriginalC : c;
                    }
                }
            }

            // ── LOWER PRIORITY abort pass ──
            for (int c = 0; c < childCount; c++)
            {
                int childIndex = first + c;
                ref NodeData childNode = ref ctx.nodeDatas[childIndex];

                if ((childNode.nodeType == BehaviourNodeType.SUBTREE
                  || childNode.nodeType == BehaviourNodeType.DECORATOR)
                    && childNode.firstChildIndex >= 0)
                {
                    // DECORATOR with LP/Both abort: evaluate its first condition directly,
                    // just like a COMPOSITE with LP/Both. The decorator's wrapped subtree
                    // contains the condition.
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

        /// <summary>
        /// Recursively resets a node and its subtree to INACTIVE state.
        /// Calls OnAbort on each method-bearing node before resetting.
        /// </summary>
        internal static void AbortSubtree(int nodeIndex, ref TickContext ctx)
        {
            if (nodeIndex < 0 || nodeIndex >= ctx.nodeDatas.Length) return;

            ref NodeData node = ref ctx.nodeDatas[nodeIndex];

            // Call OnAbort before resetting state (method can read current state to clean up)
            NodeMethod method = ctx.methodInstances[nodeIndex];
            method?.OnAbort(ctx.blackBoard);

            // Reset this node
            ctx.nodeStates[nodeIndex] = NodeState.INACTIVE;
            ctx.activeChildIndex[nodeIndex] = 0;

            // Recursively reset children
            if (node.firstChildIndex >= 0)
            {
                for (int i = node.firstChildIndex; i <= node.lastChildIndex; i++)
                    AbortSubtree(i, ref ctx);
            }
        }

        // ── BUG #1 helpers ──

        /// <summary>
        /// Returns true if any child to the right of <paramref name="position"/> is RUNNING.
        /// Used instead of a single runningChildLocal to handle Parallel composites correctly.
        /// </summary>
        private static bool HasRunningSiblingToRight(int first, int childCount, int position, ref TickContext ctx)
        {
            for (int c = position + 1; c < childCount; c++)
            {
                if (ctx.nodeStates[first + c] == NodeState.RUNNING)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Aborts ALL RUNNING children to the right of <paramref name="position"/>.
        /// Handles Parallel composites where multiple siblings may be running simultaneously.
        /// </summary>
        private static void AbortSiblingsToRight(int first, int childCount, int position, ref TickContext ctx)
        {
            for (int c = position + 1; c < childCount; c++)
            {
                if (ctx.nodeStates[first + c] == NodeState.RUNNING)
                    AbortSubtree(first + c, ref ctx);
            }
        }

        // ── BUG #2 helpers ──

        /// <summary>
        /// Returns true if any node in the absolute index range [fromIndex..toIndex] is RUNNING.
        /// </summary>
        private static bool HasRunningNodeInRange(int fromIndex, int toIndex, ref TickContext ctx)
        {
            for (int i = fromIndex; i <= toIndex; i++)
            {
                if (ctx.nodeStates[i] == NodeState.RUNNING)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Aborts all RUNNING nodes in the absolute index range [fromIndex..toIndex].
        /// </summary>
        private static void AbortRunningNodesInRange(int fromIndex, int toIndex, ref TickContext ctx)
        {
            for (int i = fromIndex; i <= toIndex; i++)
            {
                if (ctx.nodeStates[i] == NodeState.RUNNING)
                    AbortSubtree(i, ref ctx);
            }
        }

        /// <summary>
        /// Evaluates a single node's method as a standalone condition.
        /// Only ConditionMethod is supported as condition-bearing for abort.
        /// Other method types return true (condition "always met") to avoid
        /// executing actions or polling decorator child state during abort checks.
        /// </summary>
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

        /// <summary>
        /// Finds and evaluates the first condition-bearing node inside a child
        /// composite subtree. Used for LowerPriority abort where the parent needs
        /// to check a child composite's condition.
        ///
        /// When <paramref name="insideSubtree"/> is true (child is within an expanded
        /// Subtree node), composites are always recursed into regardless of their
        /// own abort type — subtrees are transparent containers.
        /// </summary>
        private static bool EvaluateCompositeCondition(int compositeIndex, AbortType requiredType, bool insideSubtree, ref TickContext ctx)
        {
            ref NodeData composite = ref ctx.nodeDatas[compositeIndex];
            return FindFirstCondition(composite.firstChildIndex, composite.lastChildIndex, requiredType, insideSubtree, ref ctx);
        }

        /// <summary>
        /// Recursively finds and evaluates the first condition-bearing node within [first..last].
        ///
        /// BUG #3 FIX: When <paramref name="insideSubtree"/> is true, composites are recursed
        /// into regardless of their own abortType (subtrees are transparent containers).
        ///
        /// BUG #4 FIX: Updates lastConditionResult at the leaf's actual index, not the caller's.
        /// </summary>
        private static bool FindFirstCondition(int first, int last, AbortType requiredType, bool insideSubtree, ref TickContext ctx)
        {
            if (first < 0) return true;

            for (int i = first; i <= last; i++)
            {
                ref NodeData child = ref ctx.nodeDatas[i];

                // Method-bearing non-composite leaf → evaluate as condition
                if (ctx.methodInstances[i] != null && child.nodeType != BehaviourNodeType.COMPOSITE)
                {
                    // Decorator with children → recurse into it (transparent container).
                    // Decorators wrap a child that may contain conditions.
                    if (child.firstChildIndex >= 0 && child.nodeType == BehaviourNodeType.DECORATOR)
                    {
                        bool found = FindFirstCondition(child.firstChildIndex,
                            child.lastChildIndex, requiredType, true, ref ctx);
                        if (!found) return false;
                        continue;
                    }

                    // Only ConditionMethod nodes are meaningful for abort evaluation.
                    // Action/other method types return true unconditionally from
                    // EvaluateLeafCondition, which would shadow any real condition
                    // nodes to the right — skip them so we can reach actual conditions.
                    if (!(ctx.methodInstances[i] is ConditionMethod))
                        continue;

                    // BUG #4 FIX: update lastConditionResult at the actual condition leaf's index
                    bool result = EvaluateLeafCondition(i, ref ctx);
                    ctx.lastConditionResult[i] = result;
                    return result;
                }

                // Composite with children → recurse if abort type is compatible,
                // OR if insideSubtree (BUG #3: subtrees see through all composites)
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

        /// <summary>
        /// Recursively evaluates LowerPriority/Both composites within [first, last],
        /// walking transparently through SUBTREE and DECORATOR nodes.
        ///
        /// BUG #2 FIX: When runningChildLocal == parentPosition, the running child is
        /// INSIDE this subtree. We scan for running nodes within [first..subtreeScopeLast]
        /// and abort those to the right of the triggering LP composite.
        /// </summary>
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

            // Detect whether the running child is inside this subtree.
            // If the subtree node itself is RUNNING, the actual running node is a descendant.
            bool runningInside = ctx.nodeStates[parentFirst + parentPosition] == NodeState.RUNNING;

            // Precompute where the original subtree's descendants end, for the
            // external-sibling abort path to avoid aborting subtree-internal nodes.
            ref NodeData originalSubtree = ref ctx.nodeDatas[parentFirst + parentPosition];
            int afterSubtreeInParent = originalSubtree.lastChildIndex >= 0
                ? originalSubtree.lastChildIndex - parentFirst
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

                        // BUG #2 FIX: If running nodes exist inside this subtree,
                        // abort siblings to the RIGHT of the LP composite (not children of it).
                        if (runningInside)
                        {
                            int afterLp = childNode.lastChildIndex >= 0 ? childNode.lastChildIndex + 1 : childIndex + 1;
                            if (HasRunningNodeInRange(afterLp, subtreeScopeLast, ref ctx))
                            {
                                AbortRunningNodesInRange(afterLp, subtreeScopeLast, ref ctx);
                                didAbort = true;
                            }
                        }

                        // Running siblings OUTSIDE the subtree (to the right) must also be aborted.
                        // This is independent of runningInside — both can be true simultaneously.
                        for (int c = afterSubtreeInParent + 1; c < parentChildCount; c++)
                        {
                            if (ctx.nodeStates[parentFirst + c] == NodeState.RUNNING)
                            {
                                AbortSubtree(parentFirst + c, ref ctx);
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
