using System;
using System.Collections.Generic;
using UnityEngine;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
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
                        description = "Assigns a value to a blackboard variable. The value can be a constant you type in, or copied from another variable. Works with all types (numbers, positions, booleans, object references).",
                        returnValues = "SUCCESS — value was written successfully\nFAILURE — target variable is invalid or no value source is available",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Target", description = "The variable to write the value into." },
                            new NodeFieldDescription { fieldName = "Value", description = "The value to assign. Toggle between a constant you type in or a variable to copy from." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "ClearVariable",
                    data = new NodeTooltipData
                    {
                        nodeName = "Clear Variable",
                        description = "Resets a blackboard variable to its default value (zero for numbers, empty for objects). Use this to clean up temporary data when a behaviour finishes.",
                        returnValues = "SUCCESS — variable was cleared\nFAILURE — target variable is invalid",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Target", description = "The variable to reset to its default value." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "LogVariable",
                    data = new NodeTooltipData
                    {
                        nodeName = "Log Variable",
                        description = "Prints the current value of a blackboard variable to the Unity Console. If the variable is an array, all elements are logged. Useful for debugging your tree during play mode.",
                        returnValues = "SUCCESS — value was logged\nFAILURE — variable slot is invalid",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Variable", description = "The variable whose value you want to print to the console." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "Toggle",
                    data = new NodeTooltipData
                    {
                        nodeName = "Toggle",
                        description = "Flips a boolean variable: true becomes false, false becomes true. A quick way to switch states without needing a Set Variable node.",
                        returnValues = "SUCCESS — value was toggled\nFAILURE — variable slot is invalid",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Variable", description = "The boolean variable to flip. Must be a bool type." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "SetFromTransform",
                    data = new NodeTooltipData
                    {
                        nodeName = "Set From Transform",
                        description = "Copies a GameObject's position into a Vector2 or Vector3 variable. If the target is Vector2, only the X and Z axes are copied (ignoring Y). The output format is chosen automatically based on the variable's type.",
                        returnValues = "SUCCESS — position was copied\nFAILURE — target or source is invalid, or source is not a Transform",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Target", description = "The Vector2 or Vector3 variable to write the position into." },
                            new NodeFieldDescription { fieldName = "Source", description = "The Transform variable to read the world position from." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "MoveTo",
                    data = new NodeTooltipData
                    {
                        nodeName = "Move To",
                        description = "Tells the agent to move toward a target location using its NavMeshAgent. The target can be a position (Vector2/Vector3) or a Transform to follow. Keeps running until the agent reaches the destination. If aborted (e.g. by a higher-priority branch), the agent stops moving.",
                        returnValues = "SUCCESS — agent arrived at destination\nRUNNING — agent is still moving\nFAILURE — target is invalid or no NavMeshAgent found on the agent",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Target", description = "Where the agent should move to. Can be a Vector2, Vector3, or Transform variable." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "SendOrder",
                    data = new NodeTooltipData
                    {
                        nodeName = "Send Order",
                        description = "Sends a command order to the current agent(s). Agent trees can check for this order using the Check Order node. Only available in Commander trees.",
                        returnValues = "SUCCESS — order was sent\nFAILURE — (never fails under normal conditions)",
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
                        returnValues = "SUCCESS — wait time has elapsed\nRUNNING — still counting down\nFAILURE — (never fails under normal conditions)",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "duration", description = "How many seconds to wait before continuing." }
                        }
                    }
                },

                // ═══ Commander & Agent Actions ═══
                new MethodTooltipEntry
                {
                    methodName = "ReportStatus",
                    data = new NodeTooltipData
                    {
                        nodeName = "Report Status",
                        description = "Writes a status value (Success, Failure, or Running) into an integer variable. This is how an agent reports its current status back to the Commander tree — the commander can then read this with the Poll Agent Status node. Only available in Agent trees.",
                        returnValues = "SUCCESS — status was written\nFAILURE — target variable is invalid",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Target", description = "The integer variable to write the status into. The Commander reads this to coordinate the squad." },
                            new NodeFieldDescription { fieldName = "Value", description = "Which status to report: Success (0), Failure (1), or Running (2)." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "CalculateFormation",
                    data = new NodeTooltipData
                    {
                        nodeName = "Calculate Formation",
                        description = "Computes a position in a formation pattern for each agent. Use inside a For Each Agent or For Each Role loop — each agent gets a unique offset position. Currently supports Circle formation: agents are evenly spaced around the center at the given radius. Only available in Commander trees.",
                        returnValues = "SUCCESS — position was calculated and written\nFAILURE — output variable is invalid or no agents available",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Center", description = "The center point of the formation. Can be a constant Vector3 or read from a variable." },
                            new NodeFieldDescription { fieldName = "Output", description = "The Vector3 array variable to write each agent's formation position into." },
                            new NodeFieldDescription { fieldName = "Type", description = "The formation shape. Currently only Circle is available." },
                            new NodeFieldDescription { fieldName = "Radius", description = "The radius of the formation. Agents are placed at this distance from the center." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "ArrayReduce",
                    data = new NodeTooltipData
                    {
                        nodeName = "Array Reduce",
                        description = "Reduces an array of numbers or vectors into a single value. Average computes the mean of all elements. Lowest finds the minimum (by value for numbers, by magnitude for vectors). Highest finds the maximum. Works on regular arrays stored in the blackboard.",
                        returnValues = "SUCCESS — reduction completed and output written\nFAILURE — source or output is invalid, array is empty, or all elements are null",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Source", description = "The array variable to reduce. Supports int[], float[], Vector2[], and Vector3[]." },
                            new NodeFieldDescription { fieldName = "Op", description = "How to reduce the array: Average (mean of all values), Lowest (minimum), or Highest (maximum)." },
                            new NodeFieldDescription { fieldName = "Output", description = "Where to store the resulting single value. The type automatically matches the array's element type." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "SquadReduce",
                    data = new NodeTooltipData
                    {
                        nodeName = "Squad Reduce",
                        description = "Works like Array Reduce, but operates on squad data — it reads one value per agent from a squad-wide array. Useful for finding the average health of all agents, which agent has the lowest ammo, etc. Only available in Commander trees.",
                        returnValues = "SUCCESS — reduction completed and output written\nFAILURE — source or output is invalid, no agents, or all values are null",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Source", description = "The squad data array to reduce. Supports int[], float[], Vector2[], and Vector3[]." },
                            new NodeFieldDescription { fieldName = "Op", description = "How to reduce the array: Average (mean of all values), Lowest (minimum), or Highest (maximum)." },
                            new NodeFieldDescription { fieldName = "Output", description = "Where to store the resulting single value. The type automatically matches the array's element type." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "SetNavAgentSpeed",
                    data = new NodeTooltipData
                    {
                        nodeName = "Set NavAgent Speed",
                        description = "Changes the agent's NavMeshAgent movement speed at runtime. The original speed is saved and automatically restored when this node is aborted or the behaviour stops. Use this to temporarily slow down or speed up an agent during specific behaviours (e.g. sprint, sneak). Only available in Agent trees.",
                        returnValues = "SUCCESS — speed was set\nFAILURE — no NavMeshAgent found on the agent",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Value", description = "The new movement speed for the NavMeshAgent. Can be a constant value or read from a variable." }
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
                        description = "Checks a variable against a value or another variable. Supports equals, not equals, less than, greater than, and magnitude comparisons for vectors. The available operations change based on the variable's type — for example, magnitude comparisons only appear for Vector2 and Vector3.",
                        returnValues = "SUCCESS — comparison is true\nFAILURE — comparison is false or Operand A is invalid",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Operand A", description = "The first variable to compare (left side). Its type determines which operations are available." },
                            new NodeFieldDescription { fieldName = "Operand B", description = "The value to compare against (right side). Toggle between a constant or another variable." },
                            new NodeFieldDescription { fieldName = "Operation", description = "The comparison to perform: Equal, Not Equal, Less/Greater (for numbers), or Magnitude (for vectors)." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "CheckVariable",
                    data = new NodeTooltipData
                    {
                        nodeName = "Check Variable",
                        description = "Checks a variable's state without needing a comparison value. Booleans: is true / is false. Numbers and vectors: is zero / is not zero. References: is null / is not null / is active / is inactive. The available checks depend on the variable's type.",
                        returnValues = "SUCCESS — condition is met\nFAILURE — condition is not met or variable is invalid",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Variable", description = "The variable to check. The available conditions change based on its type." },
                            new NodeFieldDescription { fieldName = "Condition", description = "What to check for: IsTrue/IsFalse (bool), IsZero/IsNotZero (numbers/vectors), IsNull/IsNotNull/IsActive/IsInactive (references)." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "HasChanged",
                    data = new NodeTooltipData
                    {
                        nodeName = "Has Changed",
                        description = "Detects when a variable's value has changed since the previous frame. On the first check, it always returns FAILURE (no previous value to compare against). Useful for triggering actions when data updates — for example, reacting when a target position changes.",
                        returnValues = "SUCCESS — value changed since last check\nFAILURE — value is unchanged, first check, or variable is invalid",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Variable", description = "The variable to monitor for changes. Works with any type." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "EdgeDetect",
                    data = new NodeTooltipData
                    {
                        nodeName = "Edge Detect",
                        description = "Detects when a boolean variable flips from one state to another. Rising edge triggers when the value goes from false to true. Falling edge triggers when it goes from true to false. On the first check, always returns FAILURE (nothing to compare against). Use this to fire one-shot reactions to state changes.",
                        returnValues = "SUCCESS — edge detected (rising or falling as configured)\nFAILURE — no edge detected, first check, or variable is invalid",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Variable", description = "The boolean variable to monitor for edge transitions. Must be a bool type." },
                            new NodeFieldDescription { fieldName = "Edge", description = "Which transition to detect: Rising (false→true) or Falling (true→false)." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "CheckOrder",
                    data = new NodeTooltipData
                    {
                        nodeName = "Check Order",
                        description = "Checks whether the agent has received a specific order from the Commander tree. SUCCEEDS when the received order matches, FAILS otherwise. Only available in Agent trees.",
                        returnValues = "SUCCESS — received order matches the expected order\nFAILURE — order does not match",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "expectedOrder", description = "The order to check for. Succeeds when the commander has sent this order." }
                        }
                    }
                },

                // ═══ Commander Conditions ═══
                new MethodTooltipEntry
                {
                    methodName = "PollAgentStatus",
                    data = new NodeTooltipData
                    {
                        nodeName = "Poll Agent Status",
                        description = "Reads the status array reported by all agents and determines the overall squad state. If any agent has failed (status = 1), this node fails immediately. If any agent is still running (status = 2), it stays RUNNING. If all agents have completed, it returns SUCCESS. Use this to coordinate when all agents have finished their tasks. Only available in Commander trees.",
                        returnValues = "SUCCESS — all agents have arrived / completed\nRUNNING — at least one agent is still running\nFAILURE — any agent has failed, or input variable is invalid",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Input", description = "The integer array variable holding each agent's current status (written by Report Status nodes)." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "CheckSquadData",
                    data = new NodeTooltipData
                    {
                        nodeName = "Check Squad Data",
                        description = "Scans a squad-wide array (one value per agent) and checks if any element meets a condition. IsAnyNull succeeds when any agent's value is null, zero, or unset. IsAnyNotNull succeeds when any agent has a valid value. Succeeds as soon as a match is found — ideal for detecting threats or finding available targets. Only available in Commander trees.",
                        returnValues = "SUCCESS — at least one element matches the condition\nFAILURE — no element matches, or input variable is invalid",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Input", description = "The squad data array to scan. Supports Transform[], GameObject[], bool[], int[], float[], Vector2[], and Vector3[]." },
                            new NodeFieldDescription { fieldName = "Operation", description = "What to look for: IsAnyNull (any empty/invalid entry) or IsAnyNotNull (any valid entry)." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "Cooldown",
                    data = new NodeTooltipData
                    {
                        nodeName = "Cooldown",
                        description = "A gate that only lets the tree pass once, then blocks for a set time. SUCCEEDS on the first tick, then FAILS until the cooldown timer runs out. After the timer elapses, it opens again for one tick. Use this to prevent a behaviour from triggering too frequently.",
                        returnValues = "SUCCESS — cooldown has elapsed, gate is open\nFAILURE — cooldown is still active, gate is closed",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "duration", description = "How long the cooldown lasts before the gate opens again." },
                            new NodeFieldDescription { fieldName = "remaining", description = "A variable that tracks the remaining cooldown time. Can be shared between nodes." },
                            new NodeFieldDescription { fieldName = "useCustomTick", description = "When enabled, uses a custom time value instead of real delta time for the countdown." },
                            new NodeFieldDescription { fieldName = "customTickValue", description = "How much time passes per tick when using a custom tick. Only relevant when Use Custom Tick is enabled." }
                        }
                    }
                },

                // ═══ Enemy Agent Nodes ═══
                new MethodTooltipEntry
                {
                    methodName = "Enemy_CheckRange",
                    data = new NodeTooltipData
                    {
                        nodeName = "Check Range",
                        description = "Checks whether the distance from the agent to a target is within a given radius. Supports Less Than or Greater Than comparisons. The radius can be a constant value or read from a blackboard variable. Only available in Agent trees.",
                        returnValues = "SUCCESS — distance satisfies the range condition\nFAILURE — target is invalid, no radius configured, or distance does not satisfy the condition",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Target", description = "The target to measure distance to. Accepts Transform, GameObject, or Vector3." },
                            new NodeFieldDescription { fieldName = "Radius", description = "The distance threshold. Can be a constant float or read from a blackboard variable." },
                            new NodeFieldDescription { fieldName = "Operation", description = "How to compare: LessThan (within range) or GreaterThan (outside range)." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "Enemy_Detected",
                    data = new NodeTooltipData
                    {
                        nodeName = "Detected",
                        description = "Scans for targets within a radius using the EnemyDetectionSystem. Combines range checking and optional line-of-sight verification into a single condition. Succeeds as soon as any qualifying target is found. Only available in Agent trees.",
                        returnValues = "SUCCESS — at least one target detected within range (and with line of sight, if enabled)\nFAILURE — no targets detected, or none pass the range/LOS checks",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Radius", description = "The detection radius. Targets beyond this distance are ignored." },
                            new NodeFieldDescription { fieldName = "Operation", description = "How to compare distance: LessThan or GreaterThan against the radius." },
                            new NodeFieldDescription { fieldName = "Check LOS", description = "When enabled, targets must also pass a line-of-sight check (no obstacles between agent and target)." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "Enemy_HasLineOfSight",
                    data = new NodeTooltipData
                    {
                        nodeName = "Has Line of Sight",
                        description = "Checks whether there is a clear line of sight to a specific target Transform using Physics.Linecast. Use this to verify the agent can see a target before engaging. Only available in Agent trees.",
                        returnValues = "SUCCESS — clear line of sight to the target\nFAILURE — line of sight blocked, target is invalid, or detection system is unavailable",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Target", description = "The Transform to check line of sight against. Must be a Transform variable." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "Enemy_HasArrived",
                    data = new NodeTooltipData
                    {
                        nodeName = "Has Arrived",
                        description = "Checks whether the NavMeshAgent has reached its destination. Returns SUCCESS when the path is complete and the remaining distance is within the agent's stopping distance (plus a 0.1 unit tolerance). Only available in Agent trees.",
                        returnValues = "SUCCESS — agent has arrived at destination\nFAILURE — agent is still moving or NavMeshAgent is unavailable",
                        fieldDescriptions = new NodeFieldDescription[0]
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "Enemy_StopMovement",
                    data = new NodeTooltipData
                    {
                        nodeName = "Stop Movement",
                        description = "Immediately stops the NavMeshAgent by setting isStopped to true. A quick fire-and-forget action — always succeeds unless the agent is missing. Only available in Agent trees.",
                        returnValues = "SUCCESS — agent was stopped\nFAILURE — no NavMeshAgent found",
                        fieldDescriptions = new NodeFieldDescription[0]
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "Enemy_EngagePosition",
                    data = new NodeTooltipData
                    {
                        nodeName = "Engage Position",
                        description = "Computes a random tactical position in a cone directed toward the target and writes it to a Vector3 variable. The cone behavior adapts based on distance: close to the target, the cone widens and the direction blends backward (defensive retreat); far from the target, the cone narrows and pushes forward (aggressive advance). Use the output with a Move To node. Only available in Agent trees.",
                        returnValues = "SUCCESS — engage position calculated and written\nFAILURE — output or target variable is invalid",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Position", description = "The Vector3 variable to write the calculated engage position into." },
                            new NodeFieldDescription { fieldName = "Target", description = "The target Transform to orient the engage cone toward." },
                            new NodeFieldDescription { fieldName = "Cone Angle", description = "The base angle (in degrees) of the cone. The cone widens when close to the target." },
                            new NodeFieldDescription { fieldName = "Min Dist", description = "The minimum distance from the agent to pick a position." },
                            new NodeFieldDescription { fieldName = "Max Dist", description = "The maximum distance from the agent to pick a position." },
                            new NodeFieldDescription { fieldName = "Maintain Dist", description = "The target distance to maintain from the target. Closer than this triggers defensive backward movement; farther triggers aggressive forward movement." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "Enemy_FlankPosition",
                    data = new NodeTooltipData
                    {
                        nodeName = "Flank Position",
                        description = "Computes a random flanking position perpendicular to the target direction and writes it to a Vector3 variable. The agent automatically flanks the opposite side of the target (target on right → flank left, target on left → flank right). Flank Angle blends between pure side-flank (0°) and heading directly toward the target (90°). Same distance-based cone modulation as Engage Position. Only available in Agent trees.",
                        returnValues = "SUCCESS — flank position calculated and written\nFAILURE — output or target variable is invalid",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Position", description = "The Vector3 variable to write the calculated flank position into." },
                            new NodeFieldDescription { fieldName = "Target", description = "The target Transform to flank around." },
                            new NodeFieldDescription { fieldName = "Flank Angle", description = "Blends between pure side-flank (0°) and heading straight at the target (90°). Lower values give wider flanking arcs." },
                            new NodeFieldDescription { fieldName = "Cone Angle", description = "The base angle (in degrees) for random variation in the movement cone." },
                            new NodeFieldDescription { fieldName = "Min Dist", description = "The minimum distance from the agent to pick a position." },
                            new NodeFieldDescription { fieldName = "Max Dist", description = "The maximum distance from the agent to pick a position." },
                            new NodeFieldDescription { fieldName = "Maintain Dist", description = "The target distance to maintain from the target. Closer triggers defensive backward movement; farther triggers aggressive forward movement." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "Enemy_SelectDetectedTarget",
                    data = new NodeTooltipData
                    {
                        nodeName = "Select Detected Target",
                        description = "Runs the EnemyDetectionSystem to scan for targets, then picks one using a selection strategy. The chosen target is written to a blackboard variable (as Transform or GameObject) for other nodes to use. Only available in Agent trees.",
                        returnValues = "SUCCESS — a target was selected and written\nFAILURE — no targets detected, no target selected by strategy, or output variable is invalid",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Output", description = "Where to store the selected target. Accepts Transform or GameObject variable types." },
                            new NodeFieldDescription { fieldName = "Selection Strategy", description = "How to pick among detected targets: Nearest (closest target) or Random." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "Enemy_FireSequence",
                    data = new NodeTooltipData
                    {
                        nodeName = "Fire Sequence",
                        description = "Runs a complete multi-phase firing cycle: aim at the target, search for a valid trajectory, wait for a fire delay, then fire a single shot. Stays RUNNING through all phases and returns SUCCESS after one shot is fired. Re-enter this node for subsequent shots. The trajectory search can fail, causing the node to return FAILURE. Only available in Agent trees.",
                        returnValues = "SUCCESS — one shot was fired successfully\nRUNNING — aiming, searching trajectory, waiting for fire delay, or reloading\nFAILURE — target is invalid, GunHandling is unavailable, or no valid firing trajectory found",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Target", description = "The Transform to aim and fire at." },
                            new NodeFieldDescription { fieldName = "Fire Delay", description = "How many seconds to wait between aiming and firing." },
                            new NodeFieldDescription { fieldName = "Spread", description = "The accuracy spread angle. Higher values mean less accurate shots." },
                            new NodeFieldDescription { fieldName = "Reload", description = "How many seconds the reload cycle takes after firing." },
                            new NodeFieldDescription { fieldName = "Damage", description = "How much damage each projectile deals." },
                            new NodeFieldDescription { fieldName = "Trajectory Always Indirect", description = "When enabled, the projectile always follows an indirect (arced) trajectory." },
                            new NodeFieldDescription { fieldName = "Trajectory Starting Height", description = "The starting height offset for indirect projectile trajectories." }
                        }
                    }
                },
                new MethodTooltipEntry
                {
                    methodName = "ExtractPosition",
                    data = new NodeTooltipData
                    {
                        nodeName = "Extract Position",
                        description = "Extracts a Vector3 position from a collection element. Handles single Transforms (reads child transforms), single GameObjects (reads child transforms), or arrays (Transform[], GameObject[], Vector3[]). Supports Sequential mode (cycles through elements in order) and Random mode (picks randomly each tick). A generic utility for patrol point cycling or waypoint extraction. Available on any tree type.",
                        returnValues = "SUCCESS — position extracted and written\nFAILURE — input or output is invalid, or the collection is empty",
                        fieldDescriptions = new[]
                        {
                            new NodeFieldDescription { fieldName = "Collection", description = "The source to extract a position from. Accepts Transform, GameObject, Transform[], GameObject[], or Vector3[]." },
                            new NodeFieldDescription { fieldName = "Output", description = "The Vector3 variable to write the extracted position into." },
                            new NodeFieldDescription { fieldName = "Mode", description = "How to pick an element: Sequential (cycle through in order) or Random (pick any element)." }
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

                NodeMethod temp = MethodRegistry.CreateInstance(entry.methodName);
                var descriptors = NodeParamSchema.GetForMethod(temp);
                if (descriptors == null || descriptors.Length == 0) continue;

                var existingDescriptions = entry.data.fieldDescriptions;
                var newDescriptions = new NodeFieldDescription[descriptors.Length];

                for (int f = 0; f < descriptors.Length; f++)
                {
                    string fieldName = descriptors[f].fieldName
                                       ?? descriptors[f].label.ToLowerInvariant().Replace(" ", "");
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
