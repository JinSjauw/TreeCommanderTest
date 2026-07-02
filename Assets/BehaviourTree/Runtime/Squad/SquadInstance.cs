using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Runtime MonoBehaviour that holds a squad's own BlackBoard and handles
    /// bidirectional data copying between the squad BB and any connected tree's BB.
    /// 
    /// Bindings are resolved lazily: when a tree registers with this squad,
    /// EnsureResolved() matches the tree's BlackboardDefinition against the
    /// SquadDefinition's binding groups to build flat copy arrays.
    /// 
    /// Copy arrays use triplets: [srcSlot, dstSlot, stride]. Stride > 1 means
    /// the squad-side slot is per-agent data and needs an agentOffset applied.
    /// </summary>
    [RequireComponent(typeof(BlackBoard))]
    public class SquadInstance : MonoBehaviour
    {
        [SerializeField] private SquadDefinition definition;
        [SerializeField] private BlackBoard blackBoard;

        /// <summary>Per-tree-definition cache for squad→tree copy pairs: [squadSlot, treeSlot, ...].</summary>
        private Dictionary<BlackboardDefinition, int[]> copyToCache;

        /// <summary>Per-tree-definition cache for tree→squad copy pairs: [treeSlot, squadSlot, ...].</summary>
        private Dictionary<BlackboardDefinition, int[]> copyFromCache;

        public BlackBoard BlackBoard => blackBoard;
        public SquadDefinition Definition => definition;

        public void Initialize(SquadDefinition squadDefinition, int maxAgents)
        {
            definition = squadDefinition;
            if (blackBoard == null)
                blackBoard = GetComponent<BlackBoard>();

            if (definition != null)
            {
                definition.EnsureStrideApplied(maxAgents);
                blackBoard.Initialize(definition.blackboardDefinition);
            }

            copyToCache = new Dictionary<BlackboardDefinition, int[]>();
            copyFromCache = new Dictionary<BlackboardDefinition, int[]>();
        }

        public void EnsureResolved(BlackboardDefinition treeDef)
        {
            if (copyToCache.ContainsKey(treeDef))
                return;

            if (definition == null || definition.bindingGroups == null)
            {
                copyToCache[treeDef] = new int[0];
                copyFromCache[treeDef] = new int[0];
                return;
            }

            // Find the SquadBindingGroup whose tree asset owns this BlackboardDefinition
            SquadBindingGroup matchedGroup = null;
            for (int i = 0; i < definition.bindingGroups.Count; i++)
            {
                SquadBindingGroup group = definition.bindingGroups[i];
                if (group.treeAsset == null) continue;

                if (treeDef.sourceTreeAsset != null && group.treeAsset == treeDef.sourceTreeAsset)
                {
                    matchedGroup = group;
                    break;
                }
                
                if (!string.IsNullOrEmpty(group.treeAssetGuid) &&
                    group.treeAssetGuid == treeDef.sourceTreeGuid)
                {
                    matchedGroup = group;
                    break;
                }
            }

            if (matchedGroup == null || matchedGroup.bindings == null || matchedGroup.bindings.Count == 0)
            {
                copyToCache[treeDef] = new int[0];
                copyFromCache[treeDef] = new int[0];
                return;
            }

            BlackboardDefinition squadDef = definition.blackboardDefinition;
            if (squadDef == null)
            {
                copyToCache[treeDef] = new int[0];
                copyFromCache[treeDef] = new int[0];
                return;
            }

            List<int> toTree = new List<int>();
            List<int> fromTree = new List<int>();

            for (int i = 0; i < matchedGroup.bindings.Count; i++)
            {
                VariableBinding binding = matchedGroup.bindings[i];
                if (binding == null) continue;

                int squadVarIndex = squadDef.GetVariableIndex(binding.squadVariableName);
                int treeVarIndex = treeDef.GetVariableIndex(binding.treeVariableName);

                if (squadVarIndex < 0 || treeVarIndex < 0)
                {
                    if (squadVarIndex < 0)
                        Debug.LogWarning($"[SquadInstance] Binding skipped: squad variable '{binding.squadVariableName}' not found on squad '{definition.name}'.");
                    else
                        Debug.LogWarning($"[SquadInstance] Binding skipped: tree variable '{binding.treeVariableName}' not found on tree '{treeDef.name}'. Add it to the tree blackboard or ignore this warning.");
                    continue;
                }

                int squadBaseSlot = ComputeBaseSlot(squadDef, squadVarIndex);
                int treeBaseSlot = ComputeBaseSlot(treeDef, treeVarIndex);

                IReadOnlyList<BlackboardVariableBase> squadVars = squadDef.GetAllVariables();
                int squadStride = 1;
                if (squadVarIndex >= 0 && squadVarIndex < squadVars.Count)
                {
                    BlackboardVariableBase squadVar = squadVars[squadVarIndex];
                    squadStride = (squadVar.Stride > 1) ? squadVar.Stride : 1;
                }

                Debug.Log($"[SquadInstance.EnsureResolved] squad='{definition.name}' tree='{treeDef.name}' " +
                          $"binding='{binding.squadVariableName}' dir={binding.direction} " +
                          $"squadBaseSlot={squadBaseSlot} treeBaseSlot={treeBaseSlot} " +
                          $"squadStride={squadStride} isSquadData={squadVars[squadVarIndex].isSquadData}");

                // FromSquad: squad → tree (triplet: srcSlot, dstSlot, stride)
                if (binding.direction == BindingDirection.FromSquad || binding.direction == BindingDirection.Both)
                {
                    toTree.Add(squadBaseSlot);
                    toTree.Add(treeBaseSlot);
                    toTree.Add(squadStride);
                }

                // ToSquad: tree → squad (triplet: srcSlot, dstSlot, stride)
                if (binding.direction == BindingDirection.ToSquad || binding.direction == BindingDirection.Both)
                {
                    fromTree.Add(treeBaseSlot);
                    fromTree.Add(squadBaseSlot);
                    fromTree.Add(squadStride);
                }
            }

            copyToCache[treeDef] = toTree.ToArray();
            copyFromCache[treeDef] = fromTree.ToArray();
        }

        public void CopyToBB(BlackBoard treeBB, BlackboardDefinition treeDef, int agentOffset = -1)
        {
            if (!copyToCache.TryGetValue(treeDef, out int[] triplets) || triplets.Length == 0)
                return;

            for (int i = 0; i < triplets.Length; i += 3)
            {
                int srcSlot = triplets[i];
                int dstSlot = triplets[i + 1];
                int stride = triplets[i + 2];

                if (agentOffset >= 0)
                {
                    // Per-agent copy: offset only the squad-side slot
                    int offset = (stride > 1 && agentOffset < stride) ? agentOffset : 0;
                    int actualSrc = srcSlot + offset;
                    object value = blackBoard.GetBoxedRaw(actualSrc);
                    treeBB.SetBoxedRaw(dstSlot, value);
                }
                else if (stride > 1)
                {
                    // Commander sync: copy all stride slots (both sides have stride > 1)
                    for (int j = 0; j < stride; j++)
                    {
                        int actualSrc = srcSlot + j;
                        int actualDst = dstSlot + j;
                        object value = blackBoard.GetBoxedRaw(actualSrc);

                        treeBB.SetBoxedRaw(actualDst, value);
                    }
                }
                else
                {
                    treeBB.SetBoxedRaw(dstSlot, blackBoard.GetBoxedRaw(srcSlot));
                }
            }
        }

        public void CopyFromBB(BlackBoard treeBB, BlackboardDefinition treeDef, int agentOffset = -1)
        {
            if (!copyFromCache.TryGetValue(treeDef, out int[] triplets) || triplets.Length == 0)
                return;

            for (int i = 0; i < triplets.Length; i += 3)
            {
                int srcSlot = triplets[i];
                int dstSlot = triplets[i + 1];
                int stride = triplets[i + 2];

                if (agentOffset >= 0)
                {
                    // Per-agent copy: offset only the squad-side slot
                    int offset = (stride > 1 && agentOffset < stride) ? agentOffset : 0;
                    int actualDst = dstSlot + offset;
                    object srcValue = treeBB.GetBoxedRaw(srcSlot);

                    blackBoard.SetBoxedRaw(actualDst, srcValue);
                }
                else if (stride > 1)
                {
                    // Commander sync: copy all stride slots (both sides have stride > 1)
                    for (int j = 0; j < stride; j++)
                        blackBoard.SetBoxedRaw(dstSlot + j, treeBB.GetBoxedRaw(srcSlot + j));
                }
                else
                {
                    blackBoard.SetBoxedRaw(dstSlot, treeBB.GetBoxedRaw(srcSlot));
                }
            }
        }

        /// <summary>
        /// Computes the base slot offset for a variable at the given index
        /// by summing strides of all preceding variables.
        /// </summary>
        private static int ComputeBaseSlot(BlackboardDefinition def, int variableIndex)
        {
            if (def == null) return 0;

            IReadOnlyList<BlackboardVariableBase> vars = def.GetAllVariables();
            int baseSlot = 0;
            for (int i = 0; i < variableIndex && i < vars.Count; i++)
            {
                if (vars[i] == null) continue;
                int stride = vars[i].Stride;
                baseSlot += (stride > 1) ? stride : 1;
            }
            return baseSlot;
        }
    }
}
