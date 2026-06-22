using System;
using System.Collections.Generic;
using UnityEngine;
using BehaviourTree.Core;
using UnityEditor;

namespace BehaviourTree.Editor
{
    [Serializable]
    public class NodeFieldDescription
    {
        [HideInInspector] public string fieldName;
        [TextArea(1, 3)]
        public string description;
    }

    [Serializable]
    public class NodeTooltipData
    {
        public string nodeName;
        [TextArea(2, 5)]
        public string description;
        [TextArea(2, 4)]
        public string returnValues;
        public NodeFieldDescription[] fieldDescriptions;
    }

    public class TooltipRegistry : ScriptableObject
    {
        private const string AssetPath = "Assets/BehaviourTree/TooltipRegistry.asset";

        [Header("Special Node Types")]
        [SerializeField] private NodeTooltipData rootTooltip;
        [SerializeField] private NodeTooltipData subtreeTooltip;

        [Header("Method Tooltips (keyed by method name)")]
        [SerializeField] private List<MethodTooltipEntry> methodTooltips = new();

        [Serializable]
        public class MethodTooltipEntry
        {
            public string methodName;
            public NodeTooltipData data;
        }

        private static TooltipRegistry loadedAsset;
        private Dictionary<string, NodeTooltipData> tooltipLookup;

        public static TooltipRegistry Load()
        {
            if (loadedAsset == null)
            {
                loadedAsset = AssetDatabase.LoadAssetAtPath<TooltipRegistry>(AssetPath);
                if (loadedAsset == null)
                {
                    EnsureAssetExists();
                    loadedAsset = AssetDatabase.LoadAssetAtPath<TooltipRegistry>(AssetPath);
                }
            }
            return loadedAsset;
        }

        public static void InvalidateCache()
        {
            loadedAsset = null;
        }

        public static NodeTooltipData GetTooltip(BehaviourNodeType nodeType)
        {
            var registry = Load();
            return registry != null ? registry.GetTooltipInternal(nodeType) : GetDefaultTooltip(nodeType.ToString());
        }

        public static NodeTooltipData GetTooltip(string methodName)
        {
            var registry = Load();
            return registry != null ? registry.GetTooltipInternal(methodName) : GetDefaultTooltip(methodName);
        }

        public static NodeTooltipData GetTooltip(BehaviourNode node)
        {
            if (node is LeafNode actionNode)
                return GetTooltip(actionNode.methodName);
            if (node is DecoratorNode decoratorNode)
                return GetTooltip(decoratorNode.methodName);
            if (node is CompositeNode compositeNode)
                return GetTooltip(compositeNode.methodName);

            return GetTooltip(node.NodeType);
        }

        private NodeTooltipData GetTooltipInternal(BehaviourNodeType nodeType)
        {
            if (nodeType == BehaviourNodeType.ROOT) return rootTooltip;
            if (nodeType == BehaviourNodeType.SUBTREE) return subtreeTooltip;
            return GetDefaultTooltip(nodeType.ToString());
        }

        private NodeTooltipData GetTooltipInternal(string methodName)
        {
            EnsureLookup();
            if (tooltipLookup != null && tooltipLookup.TryGetValue(methodName, out var data))
                return data;

            // Auto-create entry for new methods
            var entry = new MethodTooltipEntry
            {
                methodName = methodName,
                data = new NodeTooltipData
                {
                    nodeName = methodName,
                    description = "No description available.",
                    returnValues = ""
                }
            };
            methodTooltips.Add(entry);
            if (tooltipLookup != null)
                tooltipLookup[methodName] = entry.data;
            return entry.data;
        }

        private void EnsureLookup()
        {
            if (tooltipLookup == null)
            {
                tooltipLookup = new Dictionary<string, NodeTooltipData>();
                for (int i = 0; i < methodTooltips.Count; i++)
                {
                    if (methodTooltips[i] != null && !string.IsNullOrEmpty(methodTooltips[i].methodName))
                        tooltipLookup[methodTooltips[i].methodName] = methodTooltips[i].data;
                }
            }
        }

        private static NodeTooltipData GetDefaultTooltip(string name)
        {
            return new NodeTooltipData
            {
                nodeName = name,
                description = "No description available.",
                returnValues = ""
            };
        }

        private void OnEnable()
        {
            InitializeDefaultTooltips();
            AutoDeriveFieldDescriptions();
        }

        private void OnValidate()
        {
            AutoDeriveFieldDescriptions();
            InvalidateCache();
        }

        private static void EnsureAssetExists()
        {
            string directory = System.IO.Path.GetDirectoryName(AssetPath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                string parent = System.IO.Path.GetDirectoryName(directory);
                string folder = System.IO.Path.GetFileName(directory);
                AssetDatabase.CreateFolder(parent, folder);
            }

            if (AssetDatabase.LoadAssetAtPath<TooltipRegistry>(AssetPath) == null)
            {
                var registry = CreateInstance<TooltipRegistry>();
                AssetDatabase.CreateAsset(registry, AssetPath);
                AssetDatabase.SaveAssets();
            }
        }

        private void InitializeDefaultTooltips()
        {
            if (rootTooltip == null || string.IsNullOrEmpty(rootTooltip.nodeName))
            {
                rootTooltip = new NodeTooltipData
                {
                    nodeName = "Root",
                    description = "The starting point of a behaviour tree. Every tree has one Root node; it connects to the rest of the tree and holds the blackboard. The Root itself is never executed — only its child runs.",
                    returnValues = ""
                };
            }

            if (subtreeTooltip == null || string.IsNullOrEmpty(subtreeTooltip.nodeName))
            {
                subtreeTooltip = new NodeTooltipData
                {
                    nodeName = "Subtree",
                    description = "Runs another behaviour tree as a reusable sub-tree. The subtree's nodes are copied into the main tree at build time, keeping their own local variables separate. Use to share common behaviour across multiple trees.",
                    returnValues = "Passes through the subtree's final result."
                };
            }

            if (methodTooltips == null) methodTooltips = new List<MethodTooltipEntry>();
            if (methodTooltips.Count > 0) return;

            methodTooltips = new List<MethodTooltipEntry>
            {
                // ═══ Composites ═══
                new MethodTooltipEntry
                {
                    methodName = "SELECTOR",
                    data = new NodeTooltipData
                    {
                        nodeName = "Selector",
                        description = "Runs children from left to right, looking for one that succeeds. Stops on the first SUCCESS; only moves to the next child if the current one FAILS. If a child is still RUNNING, it picks up from where it left off next frame.",
                        returnValues = "SUCCESS — any child succeeds\nFAILURE — all children fail\nRUNNING — current child is still running"
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "SEQUENCE",
                    data = new NodeTooltipData
                    {
                        nodeName = "Sequence",
                        description = "Runs children from left to right in order. Stops on the first FAILURE; only moves to the next child if the current one SUCCEEDS. If a child is still RUNNING, it picks up from where it left off next frame.",
                        returnValues = "SUCCESS — all children succeed\nFAILURE — any child fails\nRUNNING — current child is still running"
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "PRIORITY",
                    data = new NodeTooltipData
                    {
                        nodeName = "Priority",
                        description = "Like a Selector, but always re-evaluates from the first child every frame. Use this when a higher-priority option may become available while a lower-priority child is still running — the tree will switch back to the higher-priority path.",
                        returnValues = "SUCCESS — any child succeeds\nFAILURE — all children fail\nRUNNING — current child is still running"
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "PARALLEL",
                    data = new NodeTooltipData
                    {
                        nodeName = "Parallel",
                        description = "Runs all children at the same time every frame. FAILS immediately if any child fails. SUCCEEDS when all children have finished successfully. Keeps running as long as any child is still active.",
                        returnValues = "SUCCESS — all children succeed\nFAILURE — any child fails\nRUNNING — any child still running"
                    }
                },

                // ═══ Commander Composites ═══
                new MethodTooltipEntry
                {
                    methodName = "ForEachRole",
                    data = new NodeTooltipData
                    {
                        nodeName = "For Each Role",
                        description = "Loops over all agents assigned to a specific role and runs the children for each one. Other roles are skipped. Continues processing all matching agents even if some succeed or fail — only pauses the loop when a child is still running.",
                        returnValues = "SUCCESS — all matching agents completed\nRUNNING — an agent is still running",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "targetRoleSlot", description = "The role to filter agents by. Only agents assigned to this role will be processed." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "ForEachAgent",
                    data = new NodeTooltipData
                    {
                        nodeName = "For Each Agent",
                        description = "Loops over every registered agent in the squad and runs the children for each one. Each agent sees its own per-agent variables. Continues processing all agents — only pauses when a child is still running.",
                        returnValues = "SUCCESS — all agents completed\nRUNNING — an agent is still running"
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "SelectAgent",
                    data = new NodeTooltipData
                    {
                        nodeName = "Select Agent",
                        description = "Runs the children for a single specific agent, identified by an agent ID variable. Each agent sees its own per-agent variables. If the agent ID is invalid, it fails immediately without running children.",
                        returnValues = "SUCCESS — child succeeds\nFAILURE — child fails or agent ID is invalid\nRUNNING — child is still running",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "targetAgentID", description = "The ID of the agent to run children for." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "GetNearestAgent",
                    data = new NodeTooltipData
                    {
                        nodeName = "Get Nearest Agent",
                        description = "Finds the agent closest to a reference position and runs the children for that agent. Once an agent is selected, it is locked in until the children finish (even if a closer agent appears). Fails if no agents are available.",
                        returnValues = "SUCCESS — child succeeds\nFAILURE — child fails or no agents found\nRUNNING — child is still running",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "targetAgentIDSlot", description = "Where to store the selected agent's ID." },
                            new NodeFieldDescription { fieldName = "agentPositionSlot", description = "The variable holding each agent's position for distance checks." },
                            new NodeFieldDescription { fieldName = "referencePositionSlot", description = "The reference point; the nearest agent to this position is selected." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "GetLowestAgent",
                    data = new NodeTooltipData
                    {
                        nodeName = "Get Lowest Agent",
                        description = "Finds the agent with the lowest value on a numeric variable (e.g. health, ammo) and runs the children for that agent. Once selected, the agent is locked in until the children finish. Fails if no agents are available.",
                        returnValues = "SUCCESS — child succeeds\nFAILURE — child fails or no agents found\nRUNNING — child is still running",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "targetAgentIDSlot", description = "Where to store the selected agent's ID." },
                            new NodeFieldDescription { fieldName = "variableValueSlot", description = "The per-agent variable to compare. The agent with the lowest value wins." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "GetHighestAgent",
                    data = new NodeTooltipData
                    {
                        nodeName = "Get Highest Agent",
                        description = "Finds the agent with the highest value on a numeric variable (e.g. health, ammo) and runs the children for that agent. Once selected, the agent is locked in until the children finish. Fails if no agents are available.",
                        returnValues = "SUCCESS — child succeeds\nFAILURE — child fails or no agents found\nRUNNING — child is still running",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "targetAgentIDSlot", description = "Where to store the selected agent's ID." },
                            new NodeFieldDescription { fieldName = "variableValueSlot", description = "The per-agent variable to compare. The agent with the highest value wins." }
                        }
                    }
                },

                // ═══ Decorators ═══
                new MethodTooltipEntry
                {
                    methodName = "Inverter",
                    data = new NodeTooltipData
                    {
                        nodeName = "Inverter",
                        description = "Flips the child's result: SUCCESS becomes FAILURE, FAILURE becomes SUCCESS. While the child is still running, the result passes through unchanged.",
                        returnValues = "Opposite of child result, or forced by Always Failure / Always Success",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "alwaysFailure", description = "When enabled, always returns FAILURE regardless of the child's result." },
                            new NodeFieldDescription { fieldName = "alwaysSuccess", description = "When enabled, always returns SUCCESS regardless of the child's result." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "Repeater",
                    data = new NodeTooltipData
                    {
                        nodeName = "Repeater",
                        description = "Runs the child multiple times. After each child SUCCESS it restarts the child until the target number of repetitions is reached. If the child FAILS, it stops immediately with FAILURE.",
                        returnValues = "SUCCESS — child succeeded the required number of times\nFAILURE — child failed\nRUNNING — still repeating",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "targetCount", description = "How many times the child must succeed before the Repeater itself succeeds." }
                        }
                    }
                },

                // ═══ Actions ═══
                new MethodTooltipEntry
                {
                    methodName = "SetVariable",
                    data = new NodeTooltipData
                    {
                        nodeName = "Set Variable",
                        description = "Assigns a value to a blackboard variable. The value can be a constant you type in, or copied from another variable. Works with all types (numbers, positions, booleans, object references)."
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "ClearVariable",
                    data = new NodeTooltipData
                    {
                        nodeName = "Clear Variable",
                        description = "Resets a blackboard variable to its default value (zero for numbers, empty for objects)."
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "LogVariable",
                    data = new NodeTooltipData
                    {
                        nodeName = "Log Variable",
                        description = "Prints the current value of a blackboard variable to the Unity Console. Useful for debugging your tree during play mode."
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "Toggle",
                    data = new NodeTooltipData
                    {
                        nodeName = "Toggle",
                        description = "Flips a boolean variable on or off. If it was true, it becomes false; if it was false, it becomes true."
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "SetFromTransform",
                    data = new NodeTooltipData
                    {
                        nodeName = "Set From Transform",
                        description = "Copies a GameObject's position into a Vector2 or Vector3 variable. The output format is chosen automatically based on the variable's type."
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "MoveTo",
                    data = new NodeTooltipData
                    {
                        nodeName = "Move To",
                        description = "Tells the agent to move toward a target location using its NavMeshAgent. The target can be a position or a Transform to follow. Keeps running until the destination is reached."
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "SendOrder",
                    data = new NodeTooltipData
                    {
                        nodeName = "Send Order",
                        description = "Sends a command order to the current agent(s). Agent trees can check for this order using the Check Order node. Only available in Commander trees.",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "orderValue", description = "The order to send. Agents receive this value and can react to it." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "WaitSeconds",
                    data = new NodeTooltipData
                    {
                        nodeName = "Wait",
                        description = "Pauses the tree for a set number of seconds, then continues. While waiting, the node stays in the RUNNING state.",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "duration", description = "How many seconds to wait before continuing." }
                        }
                    }
                },

                // ═══ Conditionals ═══
                new MethodTooltipEntry
                {
                    methodName = "CompareVariable",
                    data = new NodeTooltipData
                    {
                        nodeName = "Compare Variable",
                        description = "Checks a variable against a value or another variable. Supports equals, not equals, less than, greater than, and distance comparisons for positions. SUCCEEDS when the comparison is true, FAILS otherwise."
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "CheckVariable",
                    data = new NodeTooltipData
                    {
                        nodeName = "Check Variable",
                        description = "Checks a variable's state without needing a comparison value. Booleans: is true / is false. Numbers: is zero / is not zero. Object references: is null / is not null / is active / is inactive. SUCCEEDS when the condition is met."
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "HasChanged",
                    data = new NodeTooltipData
                    {
                        nodeName = "Has Changed",
                        description = "Detects when a variable's value has changed since the previous frame. SUCCEEDS if the value is different. On the first check, it always returns FAILURE (no previous value to compare against)."
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "EdgeDetect",
                    data = new NodeTooltipData
                    {
                        nodeName = "Edge Detect",
                        description = "Detects when a boolean variable flips. Choose rising edge (off to on) or falling edge (on to off). On the first check, it always returns FAILURE (nothing to compare against)."
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "CheckOrder",
                    data = new NodeTooltipData
                    {
                        nodeName = "Check Order",
                        description = "Checks whether the agent has received a specific order from the Commander tree. SUCCEEDS when the received order matches, FAILS otherwise. Only available in Agent trees.",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "expectedOrder", description = "The order to check for. Succeeds when the commander has sent this order." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "Cooldown",
                    data = new NodeTooltipData
                    {
                        nodeName = "Cooldown",
                        description = "A gate that only lets the tree pass once, then blocks for a set time. SUCCEEDS on the first tick, then FAILS until the cooldown timer runs out. After the timer elapses, it opens again for one tick.",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "duration", description = "How long the cooldown lasts before the gate opens again." },
                            new NodeFieldDescription { fieldName = "remaining", description = "A variable that tracks the remaining cooldown time. Can be shared between nodes." },
                            new NodeFieldDescription { fieldName = "useCustomTick", description = "When enabled, uses a custom time value instead of real delta time for the countdown." },
                            new NodeFieldDescription { fieldName = "customTickValue", description = "How much time passes per tick when using a custom tick. Only relevant when Use Custom Tick is enabled." }
                        }
                    }
                },
            };

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

#if UNITY_EDITOR

        private void AutoDeriveFieldDescriptions()
        {
            if (methodTooltips == null) return;

            for (int i = 0; i < methodTooltips.Count; i++)
            {
                var entry = methodTooltips[i];
                if (entry == null || entry.data == null || string.IsNullOrEmpty(entry.methodName)) continue;

                var paramInfos = MethodMetadataCache.GetParamsForMethod(entry.methodName);
                if (paramInfos == null || paramInfos.Count == 0) continue;

                var existingDescriptions = entry.data.fieldDescriptions;
                var newDescriptions = new NodeFieldDescription[paramInfos.Count];

                for (int f = 0; f < paramInfos.Count; f++)
                {
                    string fieldName = paramInfos[f].fieldName;
                    string existingDesc = FindExistingDescription(existingDescriptions, fieldName);

                    newDescriptions[f] = new NodeFieldDescription
                    {
                        fieldName = fieldName,
                        description = existingDesc ?? ""
                    };
                }

                entry.data.fieldDescriptions = newDescriptions;
            }
        }

        private static string FindExistingDescription(NodeFieldDescription[] existing, string fieldName)
        {
            if (existing == null) return null;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && existing[i].fieldName == fieldName)
                    return existing[i].description;
            }
            return null;
        }
#endif
    }
}
