# Behavior Tree System — Complete Data Flow Architecture

> **Generated:** 2026-06-22  
> **Purpose:** Full scan of all BT modules for designing Squad Formations + Flank/Suppress tactics.

---

## Table of Contents

1. [System Architecture Overview](#1-system-architecture-overview)
2. [Module Map](#2-module-map)
3. [Node Hierarchy & Type System](#3-node-hierarchy--type-system)
4. [Blackboard Data Flow](#4-blackboard-data-flow)
5. [Commander → Agent Communication Pipeline](#5-commander--agent-communication-pipeline)
6. [Squad System](#6-squad-system)
7. [Runtime Execution Flow](#7-runtime-execution-flow)
8. [Composite Execution Patterns](#8-composite-execution-patterns)
9. [Agent Methods Reference](#9-agent-methods-reference)
10. [Tree Baking Pipeline](#10-tree-baking-pipeline)
11. [Key Data Flow Graphs](#11-key-data-flow-graphs)
12. [Design Analysis: Formations + Flank/Suppress](#12-design-analysis-formations--flanksuppress)

---

## 1. System Architecture Overview

This is a custom behaviour tree framework built for Unity with a **Commander/Agent** topology. The system uses a ScriptableObject-based tree editor, bakes trees into flat arrays for runtime, and communicates between commander and agents exclusively through **squad blackboard channels**.

```
┌────────────────────────────────────────────────────────────────────┐
│  EDITOR LAYER                                                      │
│  BehaviourTreeEditor (graph view) → BehaviourNode tree             │
│  BlackBoardView (variable list)   → BlackboardDefinition           │
│  SquadDefinitionEditor            → SquadDefinition + bindings     │
│  TrackedVariablesView             → TrackedBindingGroups           │
├────────────────────────────────────────────────────────────────────┤
│  CORE LAYER (shared data types)                                    │
│  BehaviourNode hierarchy  |  BlackBoard + Storage  |  Squad types  │
│  NodeData / FieldData     |  NodeMethod / FieldBinding             │
├────────────────────────────────────────────────────────────────────┤
│  RUNTIME LAYER                                                     │
│  TreeBaker        → converts editor assets to flat runtime arrays │
│  TreeEvaluator    → tick-based evaluator with resume support      │
│  CommanderTreeRunner → orchestrates commander + N agents          │
│  AgentTreeRunner     → single agent behaviour                     │
│  SquadInstance       → bidirectional data bridge                  │
│  NodeMethod classes  → all leaf/composite/decorator logic         │
└────────────────────────────────────────────────────────────────────┘
```

**Three tiers of communication:**

```
Commander BB ←──SquadInstance.Copy──→ Squad BB ←──SquadInstance.Copy──→ Agent BB
     │                                                                       │
     └── CommanderTreeRunner.EvaluateCommander()                             │
     └── CommanderTreeRunner.TickAgents() ──────────────────────────────────┘
```

---

## 2. Module Map

```
Assets/
├── BehaviourTree/
│   ├── Core/                          # Shared data model (no runtime deps)
│   │   ├── Nodes/
│   │   │   ├── BehaviourNode.cs       # Abstract base (name, children, guid, graphPosition)
│   │   │   ├── BehaviourNodeType.cs   # enum: ROOT, COMPOSITE, CONDITION, ACTION, DECORATOR, SUBTREE
│   │   │   ├── RootNode.cs            # Tree entry point
│   │   │   ├── CompositeNode.cs       # Has methodName, fieldEntries, abortType
│   │   │   ├── DecoratorNode.cs       # Has methodName, fieldEntries, BlackBoardTypeID
│   │   │   ├── LeafNode.cs            # ACTION or CONDITION type
│   │   │   └── SubtreeNode.cs         # References another tree asset + bindings
│   │   ├── BlackBoard.cs             # MonoBehaviour, IBlackBoardAccess, currentAgentOffset
│   │   ├── BlackboardDefinition.cs    # ScriptableObject schema (variable list)
│   │   ├── BlackboardVariable.cs      # BlackboardVariable<T> (typed, stride support)
│   │   ├── BlackboardVariableBase.cs  # Abstract base (Name, Stride, TypeName, isSquadData)
│   │   ├── TypedBlackboardStorage.cs  # Typed-array backend (SlotLocation map)
│   │   ├── IBlackBoardAccess.cs       # Get<T>/Set<T>/GetBoxed/SetBoxed + typed accessors (slot-based)
│   │   ├── IBlackboardStorage.cs      # Storage backend contract
│   │   ├── NodeData.cs               # Flat runtime node struct (8 fields)
│   │   ├── NodeFieldEntry.cs         # Editor-side parameter entry (union of typed values)
│   │   ├── NodeMethod.cs             # Abstract base + FieldBinding + FieldReader
│   │   ├── NodeMethodAttribute.cs    # [NodeMethod("name", AllowedTreeType)]
│   │   ├── NodeState.cs              # NONE, FAILURE, SUCCESS, RUNNING, INACTIVE
│   │   ├── AbortType.cs              # None, Self, LowerPriority, Both (byte enum)
│   │   ├── OrderRegistry.cs          # Project-wide singleton: order name → index mapping
│   │   ├── SquadDefinition.cs        # SO: squad BB schema, roles, per-tree bindings
│   │   ├── SquadConnection.cs        # Tree→Squad link + assignedRoles
│   │   ├── SquadRole.cs              # name, colour, maxAmount, isFallback
│   │   ├── FieldData.cs              # 8-byte packed param [mode:byte|value:int]
│   │   ├── FieldTypeHelper.cs        # Static type resolution & display names
│   │   ├── BTreeVarAttribute.cs      # [SharedVar], [SharedArray] attributes
│   │   ├── BlackboardValueOverride.cs # Per-component typed override storage
│   │   └── BlackboardVariableJsonUtility.cs # JSON serialization utility
│   │
│   ├── Runtime/                       # Runtime execution
│   │   ├── TreeBaker.cs              # Bakes BehaviourNode tree → NodeData[] + FieldData[]
│   │   ├── TreeEvaluator.cs          # Tick-based evaluator, flat arrays, resume support
│   │   ├── TickContext.cs            # Per-frame context (ref struct, mutable state arrays)
│   │   ├── CompositeTick.cs          # TickComposite dispatch
│   │   ├── DecoratorTick.cs          # TickDecorator dispatch
│   │   ├── LeafTick.cs               # TickLeaf dispatch (ACTION + CONDITION)
│   │   ├── SubtreeTick.cs            # TickSubtree (transparent, delegates to first child)
│   │   ├── ConditionalAbort.cs       # Two-pass Self + LowerPriority abort logic
│   │   ├── FieldReader.cs            # Ref struct reading FieldData[] + BlackBoard
│   │   ├── MethodRegistry.cs         # Static: scans assemblies, registers NodeMethod types
│   │   ├── BehaviourTreeRunnerBase.cs # Abstract base: bake, init, tracked bindings
│   │   ├── TreeRunner.cs             # AgentTreeRunner (sealed, independent or driven)
│   │   ├── CommanderTreeRunner.cs    # Orchestrates agents, three-phase per-frame flow
│   │   ├── SquadInstance.cs          # Squad BB + lazy-resolved bidirectional copy
│   │   ├── SquadSpawner.cs           # Spawns commander + N agents, wires squads
│   │   ├── CompositeMethod.cs        # Abstract: Execute(nodeIndex, ref TickContext)
│   │   ├── TrackedBinding.cs         # Component field → BB variable, grouped by tree
│   │   ├── RuntimeAssetHelper.cs     # GetOrBake() factory
│   │   ├── RuntimeBTreeAsset.cs      # SO holding baked NodeData[], FieldData[], BB def
│   │   ├── RuntimeDebugProvider.cs   # Exposes runtime state for editor debug window
│   │   └── Methods/
│   │       ├── CheckOrderMethod.cs   # Agent: compare AgentReceivedOrder vs expected
│   │       ├── SendOrderMethod.cs    # Commander: write order to AgentOrders
│   │       ├── ForEachAgentMethod.cs # Commander: iterate all agents
│   │       ├── ForEachRoleMethod.cs  # Commander: iterate agents matching a role
│   │       ├── SelectAgentMethod.cs  # Commander: scope to single agent by ID
│   │       ├── GetNearestAgentMethod.cs  # Commander: select nearest agent to reference
│   │       ├── GetHighestAgentMethod.cs  # Commander: select agent with max value
│   │       ├── GetLowestAgentMethod.cs   # Commander: select agent with min value
│   │       ├── SequenceMethod.cs     # Sequence: stops on FAILURE
│   │       ├── SelectorMethod.cs     # Selector: stops on SUCCESS
│   │       ├── ParallelMethod.cs     # Parallel: all children every tick, fail-fast
│   │       ├── PriorityMethod.cs     # Priority: re-evaluates from first child every tick
│   │       ├── EnemyMethods.cs       # Enemy_HasArrived, SelectDetectedTarget, FireSequence, etc.
│   │       ├── DecoratorMethods.cs   # Inverter, Repeater
│   │       ├── TimeMethods.cs        # WaitSeconds, Cooldown
│   │       ├── VariableMethods.cs    # SetVariable, CompareVariable, CheckVariable, MoveTo, etc.
│   │       └── CommanderTestMethods.cs # Stub test nodes (no-op)
│   │
│   ├── Editor/                        # Editor tooling (graph view, inspectors, search)
│   └── Docs/
│       ├── BlackboardSystemOverview.md  # Existing blackboard docs
│       └── BehaviorTree-DataFlows.md    # THIS FILE
│
├── Scripts/
│   ├── Enemy/
│   │   ├── EnemyController.cs        # Agent facade: movement, targeting, firing, patrol
│   │   ├── EnemyManager.cs           # Pool-based spawner for independent enemies
│   │   ├── EnemyDetectionSystem.cs   # OverlapSphere detection + SelectionStrategy
│   │   ├── DeathHandler.cs           # Corpse instantiation + pool return
│   │   ├── SelectionStrategy.cs      # enum: Nearest, Farthest, Random
│   │   └── CorpseSpawn.cs            # Ragdoll joint disconnection
│   ├── HealthComponent.cs            # Health, damage, death trigger
│   ├── Gun Handling/
│   │   ├── GunHandling.cs            # Aim, trajectory, fire pipeline
│   │   ├── TrajectorySystem.cs       # Trajectory search & validation
│   │   ├── TurretController.cs       # Turret aim control
│   │   └── ...
│   ├── Camera/CameraController.cs
│   └── ObjectPool/                   # Pooling infrastructure
```

---

## 3. Node Hierarchy & Type System

### 3.1 Node Inheritance Chain

```
ScriptableObject
  └─ BehaviourNode (abstract, editor-side)
       ├─ RootNode          → NodeType: ROOT, one child
       ├─ CompositeNode     → NodeType: COMPOSITE, has methodName + abortType
       ├─ DecoratorNode     → NodeType: DECORATOR, one child + methodName
       ├─ LeafNode          → NodeType: ACTION | CONDITION, methodName
       └─ SubtreeNode       → NodeType: SUBTREE, references another tree asset
```

### 3.2 NodeMethod Runtime Hierarchy

```
NodeMethod (abstract)
  ├─ ActionMethod        → Execute() returns SUCCESS/FAILURE (rarely RUNNING)
  ├─ ConditionMethod     → Execute() returns SUCCESS/FAILURE ONLY (never RUNNING)
  ├─ DecoratorMethod     → Execute(NodeState childResult) wraps child
  └─ CompositeMethod     → Execute(int nodeIndex, ref TickContext) controls children
```

### 3.3 NodeData (Runtime Flat Struct)

When trees are baked, the hierarchical `BehaviourNode` tree becomes a flat `NodeData[]` array:

```
struct NodeData {
    BehaviourNodeType nodeType;   // ROOT, COMPOSITE, CONDITION, ACTION, DECORATOR, SUBTREE
    int firstChildIndex;          // -1 if no children
    int lastChildIndex;           // inclusive range for composites
    string methodName;            // key for MethodRegistry lookup
    BlackBoardType blackBoardTypeID; // SELF or SQUAD
    int fieldDataStartIndex;      // index into FieldData[] for this node's params
    int fieldDataCount;           // how many FieldData entries
    AbortType abortType;          // only meaningful for COMPOSITE nodes
}
```

### 3.4 FieldData (Packed Parameter)

Every node parameter is stored as an 8-byte `FieldData`:

```
[StructLayout(Explicit)]
struct FieldData {
    [FieldOffset(0)] byte mode;   // 0=constant, 1=variable, 2=boxed_constant, 3=stride_marker
    [FieldOffset(1)] int value;   // packed int/float bits, or slot index, or boxed index
}
```

This achieves **~10x compression** vs the editor-side `NodeFieldEntry`.

### 3.5 AllowedTreeType Filter

```
[NodeMethod("name", allowedTreeType = AllowedTreeType.Any)]       // unrestricted
[NodeMethod("name", allowedTreeType = AllowedTreeType.Agent)]     // agent only
[NodeMethod("name", allowedTreeType = AllowedTreeType.Commander)] // commander only
```

**Commander-only nodes:** `SendOrder`, `ForEachAgent`, `ForEachRole`, `SelectAgent`, `GetNearestAgent`, `GetHighestAgent`, `GetLowestAgent`  
**Agent-only nodes:** `CheckOrder`, all `Enemy_*` methods  
**Unrestricted:** Sequence, Selector, Parallel, Priority, Inverter, Repeater, WaitSeconds, Cooldown, all VariableMethods

---

## 4. Blackboard Data Flow

### 4.1 Storage Model

```
BlackboardDefinition (schema, SO)
    └── sharedVariables: List<BlackboardVariableBase>  [SerializeReference, polymorphic]
         ├── BlackboardVariable<int>    Name="AgentRoles",    Stride=N, isSquadData=true
         ├── BlackboardVariable<int>    Name="AgentOrders",   Stride=N, isSquadData=true
         ├── BlackboardVariable<float>  Name="Health",        Stride=1
         └── BlackboardVariable<Vector3> Name="Position",     Stride=1

At runtime:
TypedBlackboardStorage
    ├── map[]       = SlotLocation[totalSlots] // virtual slot → typed array + local index
    ├── slotTypes[] = Type[totalSlots]         // declared type per slot
    ├── slotKinds[] = BlackboardSlotKind[]     // Value or Reference
    └── floats[]/ints[]/vec3s[]/objects[] ...  // typed value arrays + object[] fallback

Slot layout (positional):
    Variable "AgentRoles"  (Stride=N) → slots 0..N-1
    Variable "AgentOrders" (Stride=N) → slots N..2N-1
    Variable "Health"      (Stride=1) → slot 2N
    Variable "Position"    (Stride=1) → slot 2N+1
```

### 4.2 currentAgentOffset

The key mechanism enabling per-agent data access without array copying:

```csharp
// In BlackBoard.cs:
public T Get<T>(int slot) {
    return storage.Get<T>(slot + currentAgentOffset);
}

public void Set<T>(int slot, T value) {
    storage.Set<T>(slot, value + currentAgentOffset);
}
```

When `ForEachAgent` or `ForEachRole` iterates, it sets `bb.currentAgentOffset = agentIndex`, making all leaf nodes automatically read/write the correct per-agent slot.

### 4.3 IBlackBoardAccess Interface

Minimal contract decoupling node methods from the concrete `BlackBoard`:

```csharp
interface IBlackBoardAccess {
    T    Get<T>(int slot);
    void Set<T>(int slot, T value);
    object GetBoxed(int slot);
    void SetBoxed(int slot, object value);
}
```

### 4.4 FieldBinding — Compiled Accessors

`FieldBinding` compiles Expression-tree delegates at init time:

```csharp
class FieldBinding {
    FieldInfo fieldInfo;
    int bbSlotIndex;     // -1 = constant, >=0 = variable slot
    bool isOutput;       // writes to BB after Execute()

    Action<NodeMethod, IBlackBoardAccess> readFromBB;   // compiled delegate
    Action<NodeMethod, IBlackBoardAccess> writeToBB;     // compiled delegate
}
```

This avoids reflection overhead every tick. Falls back to `GetBoxed`/`SetBoxed` with type coercion on IL2CPP AOT where Expression trees are unavailable.

---

## 5. Commander → Agent Communication Pipeline

### 5.1 The Full Per-Frame Sequence

```
CommanderTreeRunner.Update()
│
├─ 1. PushTrackedBindings()
│     └─ Reads component fields → writes commander BB
│
├─ 2. EvaluateCommander()
│     ├─ a. CopySquadsToTree(this)         [squad BB → commander BB]
│     │      └─ SquadInstance.CopyToBB(commander.BlackBoard, ..., agentOffset=-1)
│     │           Copies FromSquad/Both bindings. Stride>1 vars: bulk copy all slots.
│     ├─ b. evaluator.agentCount = registeredAgents.Count
│     ├─ c. evaluator.Evaluate(blackBoard)
│     │      └─ Commander tree ticks. ForEachAgent/ForEachRole composites set
│     │         currentAgentOffset. SendOrderMethod writes to AgentOrders[agentIndex].
│     │         The tree reads AgentRoles (FromSquad → last frame's agent states).
│     └─ d. CopySquadsFromTree(this)        [commander BB → squad BB]
│            └─ SquadInstance.CopyFromBB(commander.BlackBoard, ..., agentOffset=-1)
│                 Copies ToSquad/Both bindings: AgentOrders written to squad BB.
│
└─ 3. TickAgents() — for each agent at index i:
      ├─ a. agent.PushTrackedBindings()
      │     └─ Reads agent component fields → writes agent BB
      ├─ b. CopySquadsToTree(agent, i)       [squad BB → agent BB]
      │     └─ SquadInstance.CopyToBB(agent.BlackBoard, ..., agentOffset=i)
      │          Stride>1 vars: squad-side slot + agentOffset → single agent BB slot.
      │          AgentReceivedOrder is now populated with the commander's order.
      ├─ c. agent.Evaluate()
      │     └─ Agent tree ticks. CheckOrderMethod compares AgentReceivedOrder vs expected.
      │        EnemyMethods call EnemyController primitives.
      └─ d. CopySquadsFromTree(agent, i)      [agent BB → squad BB]
            └─ SquadInstance.CopyFromBB(agent.BlackBoard, ..., agentOffset=i)
                 Agent's AgentAssignedRole (ToSquad) written back to squad BB.
```

### 5.2 Variable Binding Directions

| Variable | Commander Side | Agent Side | Direction (Commander) | Direction (Agent) |
|---|---|---|---|---|
| `AgentRoles` | reads (FromSquad) | writes (ToSquad) | Squad → Commander BB | Agent BB → Squad |
| `AgentOrders` | writes (ToSquad) | reads (FromSquad) | Commander BB → Squad | Squad → Agent BB |

This is auto-configured by `SquadDefinition.OnValidate()`.

### 5.3 Order Flow Step-by-Step

```
1. Commander tree: ForEachRole(role=Assault) → SendOrder("Attack", agentOffset=2)
   └─ Writes "Attack" order index to AgentOrders[2] in commander BB

2. CopySquadsFromTree(commander, agentOffset=-1)
   └─ AgentOrders[0..N-1] bulk-copied to squad BB

3. TickAgents: agent[2]
   ├─ CopySquadsToTree(agent[2], agentOffset=2)
   │   └─ Squad BB AgentOrders[2] → agent BB AgentReceivedOrder (single slot)
   └─ agent[2].Evaluate()
       └─ CheckOrder("Attack") → SUCCESS → Enter Attack subtree
```

---

## 6. Squad System

### 6.1 Key Types

```
SquadDefinition (ScriptableObject)
├── blackboardDefinition: BlackboardDefinition    # Squad's variable schema
├── availableRoles: List<SquadRole>                # Role definitions
│   └── SquadRole { name, colour, maxAmount, isFallback }
└── bindingGroups: List<SquadBindingGroup>         # Per-tree variable mappings
    └── SquadBindingGroup {
          treeAsset: BehaviourTreeAssetBase
          treeAssetGuid: string                     # Build-safe matching
          bindings: List<VariableBinding>           # Individual mappings
        }
        └── VariableBinding {
              treeVariableName: string
              squadVariableName: string
              direction: BindingDirection           # ToSquad, FromSquad, Both
            }

SquadInstance (MonoBehaviour, runtime)
├── definition: SquadDefinition
├── blackBoard: BlackBoard                         # Squad's own BB
├── copyToCache: Dictionary<BBDef, int[]>           # [squadSlot, treeSlot, stride] triplets
└── copyFromCache: Dictionary<BBDef, int[]>         # [treeSlot, squadSlot, stride] triplets

SquadSpawner (MonoBehaviour)
├── definition: SquadDefinition
├── commanderPrefab: GameObject
├── agentPrefab: GameObject
└── agentCount: int
```

### 6.2 SquadInstance Copy Mechanism

`EnsureResolved(treeDef)` lazily builds flat copy arrays:

1. Finds the `SquadBindingGroup` matching `treeDef` by asset reference or GUID
2. For each `VariableBinding`:
   - Resolves squad-side and tree-side variable indices
   - Computes base slot offsets by summing preceding variable strides
   - Builds `[srcSlot, dstSlot, stride]` triplets per direction

`CopyToBB(treeBB, treeDef, agentOffset)`:
- `agentOffset == -1` (commander): bulk copies all stride slots (both sides have stride>1)
- `agentOffset >= 0` (agent): copies single slot with squad-side offset by agentIndex

### 6.3 SquadSpawner Spawn Order

```
1. Instantiate commander prefab → commanderRunner.Initialize()
2. Read maxSize = commanderRunner.GetMaxSquadDataStride()
3. Create SquadInstance GameObject → squadInstance.Initialize(definition, maxSize)
4. commanderRunner.RegisterSquad(squadInstance)
5. For each agent (clamped to maxSize):
   a. Instantiate agent prefab
   b. agent.commander = commanderRunner; agent.squadInstance = squadInstance
   c. agent.Initialize() → OnPostInitialize auto-registers with commander + squad
   d. agent.RunIndependently = false
   e. AssignRole(agent, i) → writes role index to agent BB variable "AgentAssignedRole"
```

---

## 7. Runtime Execution Flow

### 7.1 TreeBaker

Converts editor tree → runtime arrays:

```
BehaviourTreeAssetBase (SO)
    └── root: BehaviourNode (tree graph)
    └── blackboardDefinition (tree's own variables)

TreeBaker.BakeTree(root, asset, ...)
│
├── 1. Unwrap RootNode → first child as effective root
├── 2. Create merged BlackboardDefinition
│      ├── Copy tree's own variables
│      ├── Copy commander BB variables (agent trees: force stride=1)
│      └── scopeVarIndexByName: Dictionary<string,int> for variable→slot resolution
├── 3. Flatten tree recursively via ProcessChildren()
│      ├── Subtree nodes: expand referenced tree in-place (circular detection)
│      ├── scopePrefix system: isolates subtree variable scopes by runtimeGuid
│      └── Record child ranges as firstChildIndex/lastChildIndex
├── 4. FillNodeData → output arrays:
│      ├── NodeData[] — flat node structure
│      ├── FieldData[] — packed parameters
│      ├── fieldTypeNames[] — parallel type names (for dynamic-type nodes)
│      ├── boxedConstants[] — boxed values for types >4 bytes
│      ├── nodeGuids[] — GUID per node (debug/tracking)
│      └── maxTreeDepth — recursion guard
└── 5. Return RuntimeBehaviourTreeAsset
```

### 7.2 TreeEvaluator

Tick-based (not stack-based) evaluator:

```
TreeEvaluator(nodeDatas, fieldDatas, fieldTypeNames, boxedConstants, maxTreeDepth)
│
├── Allocates fixed-size arrays:
│   ├── NodeState[] nodeStates          (INACTIVE → RUNNING → SUCCESS/FAILURE)
│   ├── int[] activeChildIndex          (resume position for composites)
│   ├── int[] runningAgentIndex         (resume position for ForEachAgent)
│   └── bool[] lastConditionResult      (for conditional-abort detection)
│
├── Instantiates NodeMethod[]:
│   └── MethodRegistry.CreateInstance(methodName) for each node with a method
│       └── DeserializeFields(FieldData[], FieldBinding[], boxedConstants)
│           └── Packs constants onto [SharedVar] fields, sets bbSlotIndex
│
└── Evaluate(BlackBoard):
    ├── Populate TickContext struct
    └── TickDispatcher.TickNode(0, ref ctx)
        ├── TickLeaf      (ACTION + CONDITION)
        ├── TickDecorator (DECORATOR)
        ├── TickSubtree   (SUBTREE — delegates to first child)
        └── TickComposite (COMPOSITE + ROOT)
```

### 7.3 TickContext

```csharp
struct TickContext {
    NodeData[] nodeDatas;           // read-only tree structure
    NodeMethod[] methodInstances;   // method objects per node
    NodeState[] nodeStates;         // mutable: populated as side-effect
    int[] activeChildIndex;         // mutable: composites read/write
    int[] runningAgentIndex;        // mutable: ForEachAgent loop position
    int agentCount;                 // set by CommanderTreeRunner before evaluate
    BlackBoard blackBoard;          // runtime BB
    bool[] lastConditionResult;     // for conditional-abort edge detection
}
```

### 7.4 BehaviourTreeRunnerBase

Template Method for both AgentTreeRunner and CommanderTreeRunner:

```
Initialize()
├── RuntimeAssetHelper.GetOrBake(authoringAsset)
│   └── TreeBaker.BakeTree() → RuntimeBehaviourTreeAsset
├── blackBoard.Initialize(runtimeAsset.blackboardDefinition)
├── evaluator = new TreeEvaluator(nodeDatas, fieldDatas, ...)
├── EnsureComponent<RuntimeDebugProvider>
└── OnPostInitialize()  [virtual hook]
    └── ResolveTrackedBindings()
        ├── Match active tree by GUID against TrackedBindingGroup.targetTreeGuid
        ├── Resolve FieldInfo/PropertyInfo via reflection
        └── Compute slot offsets via ComputeSlotOffset()

Each frame:
PushTrackedBindings()
└── For each resolved TrackedBinding:
    ├── Read component field/property value
    └── blackBoard.SetBoxed(slot, value)

Evaluate()
└── evaluator.Evaluate(blackBoard)
```

### 7.5 Conditional Aborts

Two-pass algorithm at the start of each composite's `Execute()`:

**Pass 1 — SELF abort** (triggered by `AbortType.Self` or `Both`):
1. Find the currently RUNNING child
2. Re-evaluate all children's conditions
3. If a condition changed and a higher-index sibling is RUNNING → abort the running sibling, restart from the changed condition

**Pass 2 — LOWER PRIORITY abort** (triggered by `AbortType.LowerPriority` or `Both`):
1. Find child composites with LP/Both abort
2. If their condition transitions from `false` to `true` and a right-sibling is RUNNING → abort running sibling, restart from triggered composite

`AbortSubtree(nodeIndex)` recursively:
1. Calls `method.OnAbort(blackBoard)` (cleanup hook)
2. Sets state to INACTIVE
3. Resets `activeChildIndex[nodeIndex] = 0`
4. Recurses into children

---

## 8. Composite Execution Patterns

### 8.1 Sequence — "AND" Chain

```
Ticks children left→right. Stops on first FAILURE.
Returns SUCCESS only when ALL children succeed.
Resumes from last active child on RUNNING.
```

### 8.2 Selector — "OR" Fallback

```
Ticks children left→right. Stops on first SUCCESS.
Returns FAILURE only when ALL children fail.
Resumes from last active child on RUNNING.
```

### 8.3 Parallel — "All At Once"

```
Every tick, all incomplete children are ticked.
FAIL-FAST: any child FAILURE → overall FAILURE.
RUNNING while any child still running.
SUCCESS when ALL children have succeeded.
Uses per-instance ParallelChildState[] to track completion.
```

### 8.4 Priority — "Interruptible Selector"

```
Same as Selector but re-evaluates from first child EVERY tick.
If child 3 was RUNNING last frame, it re-checks children 0,1,2 first.
A higher-priority sibling becoming ready interrupts the running one.
Used for: "always prefer urgent tasks over normal ones."
```

### 8.5 ForEachAgent — "Iterate All Agents"

```
Commander-only. Double nested loop (agents × children):
  For agent 0..agentCount-1:
    Set currentAgentOffset = agentIndex
    For each child:
      Tick child
      If RUNNING → save both indices, return RUNNING
      Continue past SUCCESS and FAILURE
Returns SUCCESS when all agents × all children complete.
Never returns FAILURE.
```

### 8.6 ForEachRole — "Iterate Agents by Role"

```
Same as ForEachAgent but filters agents:
  Reads AgentRoles[agentIndex] (int via GetBoxed)
  Compares against targetRoleSlot (constant or BB variable)
  Only matching agents have their children ticked
```

### 8.7 SelectAgent — "Single Agent Scope"

```
Commander-only. Ticks child for one specific agent at targetAgentID.
Sets currentAgentOffset = targetAgentID.
Invalid ID (< 0 or >= agentCount) → FAILURE.
Child result propagates as composite result.
```

### 8.8 GetNearestAgent / GetHighestAgent / GetLowestAgent

```
Commander-only composites. Scan all agents' squad-data values:
  GetNearestAgent:  min distance to referencePosition
  GetHighestAgent:  max value of squad-data variable
  GetLowestAgent:   min value of squad-data variable

Write selected agent index to targetAgentIDSlot.
Then tick children with currentAgentOffset = selected agent.
```

---

## 9. Agent Methods Reference

### 9.1 Commander-Side Methods

| Method | Type | Description |
|---|---|---|
| `SendOrder` | Action | Writes `orderValue` to `AgentOrders[currentAgentOffset]` |
| `ForEachAgent` | Composite | Iterates all agents, ticks children for each |
| `ForEachRole` | Composite | Iterates agents matching `targetRoleSlot` |
| `SelectAgent` | Composite | Ticks child for a single agent at `targetAgentID` |
| `GetNearestAgent` | Composite | Selects agent closest to `referencePosition` |
| `GetHighestAgent` | Composite | Selects agent with max value of a variable |
| `GetLowestAgent` | Composite | Selects agent with min value of a variable |

### 9.2 Agent-Side Methods

| Method | Type | Description |
|---|---|---|
| `CheckOrder` | Condition | Compares `AgentReceivedOrder` vs `expectedOrder` |
| `Enemy_HasArrived` | Condition | Checks NavMeshAgent.pathStatus |
| `Enemy_CheckRange` | Condition | Checks XZ distance vs radius (LessThan/GreaterThan) |
| `Enemy_HasLineOfSight` | Condition | Physics.Linecast to target |
| `Enemy_SelectDetectedTarget` | Action | Runs detection, selects by strategy, writes to BB |
| `Enemy_FireSequence` | Action | Full fire pipeline: Aim→Trajectory→Fire (multi-phase, returns RUNNING) |
| `Enemy_SelectPatrolPoint` | Action | Picks patrol waypoint (Random/Sequential) |
| `MoveTo` | Action | NavMeshAgent.SetDestination + wait for arrival (returns RUNNING) |
| `WaitSeconds` | Action | Accumulates deltaTime, returns RUNNING until elapsed |
| `Cooldown` | Condition | Returns SUCCESS once per duration period |

### 9.3 Variable Methods

| Method | Type | Description |
|---|---|---|
| `SetVariable` | Action | Write value to BB slot |
| `ClearVariable` | Action | Reset to type default |
| `LogVariable` | Action | Debug.Log value(s) |
| `CompareVariable` | Condition | Compare variable vs value (Equal, NotEqual, Less, Greater, MagnitudeLess, ...) |
| `CheckVariable` | Condition | Check state (IsTrue, IsFalse, IsZero, IsNull, IsNotNull, ...) |
| `HasChanged` | Condition | Detect value change since last tick |
| `EdgeDetect` | Condition | Detect boolean rising/falling edge |
| `Toggle` | Action | Flip boolean variable |
| `SetFromTransform` | Action | Write Transform.position to Vector2/3 variable |
| `MoveTo` | Action | NavMeshAgent movement to a position from BB |

---

## 10. Tree Baking Pipeline

```
[Editor]                              [Runtime]
BehaviourTreeAssetBase                RuntimeBehaviourTreeAsset
  ├── root: BehaviourNode              ├── runtimeNodeData: NodeData[]
  │   ├── CompositeNode                ├── runtimeNodeGuids: string[]
  │   │   ├── fieldEntries[]           ├── runtimeFieldData: FieldData[]
  │   │   └── abortType                ├── fieldTypeNames: string[]
  │   ├── LeafNode                     ├── boxedConstants: object[]
  │   │   ├── methodName               ├── blackboardDefinition: BlackboardDefinition
  │   │   ├── leafType                 ├── maxTreeDepth: int
  │   │   └── fieldEntries[]           ├── sourceTreeGuid: string
  │   ├── DecoratorNode                └── sourceTree: Object (editor-only)
  │   ├── SubtreeNode
  │   │   ├── subTreeAsset ──→ subtree tree flattened in-place
  │   │   └── bindings[] ──→ variable passthrough to parent scope
  │   └── RootNode (unwrapped)
  ├── blackboardDefinition → merged with commander BB
  ├── commanderBlackboardDefinition → commander-only BB (null for agent trees)
  └── squadConnections[] → squad bindings

TreeBaker.BakeTree()
│
├── Subtree expansion:
│   └── Recursively reads subtree's BehaviourNode tree → flattens into same arrays
│       scopePrefix = runtimeGuid ensures variable isolation
│       Circular references detected via stack tracking
│
├── Field entry → FieldData packing:
│   ├── Constant int/float/bool/enum  → packed inline (8 bytes)
│   ├── Constant Vector3/Transform/GO → boxed in boxedConstants[]
│   ├── BB variable                   → slot index from scopeVarIndexByName
│   ├── Order constant                → OrderRegistry.FindInstance().GetIndex(name)
│   └── Array variables               → stride marker emitted after variable entry
│
└── Blackboard merging:
    ├── Tree's own variables preserved
    ├── Commander BB variables copied (agent trees: stride forced to 1)
    └── Root scope variable index map built
```

---

## 11. Key Data Flow Graphs

### 11.1 Independent Agent Tree (No Commander)

```
[Component.Health]                                  [EnemyController]
       │                                                   ↑
       │ TrackedBinding.Push                              │ EnemyMethods
       ▼                                                   │
  ┌──────────┐    TreeEvaluator.Evaluate()         ┌──────┴──────┐
  │ BlackBoard│◄───────────────────────────────────│ Agent Tree  │
  │          │                                    │             │
  │ Health=50│──── FieldReader.Get("Health") ────►│ CompareVar  │
  │ Target=T │                                    │  → SUCCESS  │
  └──────────┘                                    └─────────────┘
```

### 11.2 Commander → Agent Through Squad

```
  ┌──────────────────┐         ┌──────────────┐         ┌──────────────────┐
  │ CommanderTreeRunner│        │ SquadInstance │         │ AgentTreeRunner  │
  │                  │        │              │        │                  │
  │ EvaluateCommander │        │  Squad BB    │        │ Evaluate()       │
  │  ┌────────────┐  │ CopySB│ ┌──────────┐ │CopyBB │  ┌────────────┐  │
  │  │ForEachRole │  │──────►│ │AgentOrders│ │──────►│  │CheckOrder  │  │
  │  │ ↓          │  │       │ │ [0]=Fire  │ │       │  │ ↓          │  │
  │  │SendOrder   │  │       │ │ [1]=Flank │ │       │  │Enemy_      │  │
  │  │ "Fire"     │  │       │ │ [2]=Idle  │ │       │  │ FireSequence│  │
  │  └────────────┘  │       │ └──────────┘ │       │  └────────────┘  │
  │                  │◄──────│              │◄──────│                  │
  │                  │CopyFB │ ┌──────────┐ │CopyFB │  AgentAssignedRole│
  │                  │       │ │AgentRoles│ │       │  = "Assault"     │
  │                  │       │ └──────────┘ │       │                  │
  └──────────────────┘         └──────────────┘         └──────────────────┘

Legend:
  CopySB = CopySquadsToTree   (squad → tree, FromSquad direction)
  CopyFB = CopySquadsFromTree (tree → squad, ToSquad direction)
  CopyBB = SquadInstance.CopyToBB / CopyFromBB
```

### 11.3 Agent Offset Mechanism

```
Squad BB: AgentOrders[3] = [0:Idle, 1:Flank, 2:Attack]

ForEachAgent iteration:
  agentIndex=0: bb.currentAgentOffset=0
    → leaf nodes read AgentOrders[0] = Idle
  agentIndex=1: bb.currentAgentOffset=1
    → leaf nodes read AgentOrders[1] = Flank
  agentIndex=2: bb.currentAgentOffset=2
    → leaf nodes read AgentOrders[2] = Attack
```

---

## 12. Design Analysis: Formations + Flank/Suppress

> **This section is design thinking only — no implementation code.**

### 12.1 Current System Capabilities (What Exists Now)

**What we can already do:**
- Assign **roles** to agents via `SquadRole` and `AgentAssignedRole`
- Iterate agents by role via `ForEachRole` composite
- Send **orders** per-agent via `SendOrder` → `AgentOrders` → `AgentReceivedOrder`
- Agents **branch** on received orders via `CheckOrder` condition
- Select specific agents by criteria: `GetNearestAgent`, `GetHighestAgent`, `GetLowestAgent`
- Squads **own their schema**: variables can be added to `SquadDefinition.blackboardDefinition`
- `SquadInstance` provides **bidirectional data sync** between squad BB and tree BBs

**What is missing for formations & tactics:**
1. No **formation definition** concept — no "Wedge", "Line", "Column" shapes
2. No **formation position calculation** — no algorithm to compute per-agent target positions
3. No agent **position sharing** on the squad BB — currently only roles and orders flow
4. `MoveTo` exists for agents but no **coordinated movement** (all agents move independently)
5. No **Flank** or **Suppress** as structured tactics — these are just order names in `OrderRegistry`
6. No **spatial reasoning** on the commander side — `GetNearestAgent` exists but no "line of sight from point", "angle from target", or "encircle target" queries

### 12.2 Proposed Data Model Extensions

#### A. Add Squad BB Variables for Formation State

The `SquadDefinition.blackboardDefinition` needs new squad-data variables (`isSquadData=true`, `Stride=N`):

| Variable | Type | Purpose |
|---|---|---|
| `AgentPosition` | Vector3 | Each agent's current world position (reported by agent tree) |
| `AgentFormationTarget` | Vector3 | Each agent's formation-move destination |
| `AgentHealth` | float | Each agent's current health (for tactical decisions) |
| `FormationType` | int | Current formation index (single value, not per-agent) |
| `FormationOrigin` | Vector3 | Squad center / reference point for formation |
| `FormationFacing` | Vector3 | Direction the formation faces |
| `SquadTarget` | Transform | Shared target all agents orient toward |
| `TacticPhase` | int | Current tactical phase (Approach, Flank, Suppress, Assault) |

#### B. Agent Reports Position + Status Each Frame

Agent tree needs a leaf node that runs early in the tree:
```
[Agent Report Sequence]
├── SetVariable → write transform.position to AgentPosition (ToSquad binding)
└── SetVariable → write HealthComponent.health to AgentHealth (ToSquad binding)
```

This makes agent positions available on the squad BB for the commander to read.

#### C. Commander Formation/Role Assignment Tree

```
[Commander Root]
└── [SEQUENCE: Tactical Loop]
    ├── [SELECTOR: Pick Tactic]
    │   ├── [SEQUENCE: Flank Tactic]
    │   │   ├── CheckVariable: TacticPhase == Approach
    │   │   └── [ForEachRole: role=Assault]
    │   │       └── SendOrder("MoveToFlankPosition")
    │   └── [SEQUENCE: Suppress Tactic]
    │       ├── CheckVariable: TacticPhase == Suppress
    │       └── [ForEachRole: role=FireSupport]
    │           └── SendOrder("SuppressTarget")
    └── [ForEachRole: role=All] (or ForEachAgent)
        └── CalculateFormationPositions sends AgentFormationTarget per agent
```

### 12.3 Formation Design

#### Formation Types (enum or OrderRegistry entries)
```
- Line      — agents spread in a row perpendicular to facing direction
- Column    — agents in a column along facing direction
- Wedge     — V-shaped formation pointing toward target
- EchelonLeft   — diagonal stagger, left flank forward
- EchelonRight  — diagonal stagger, right flank forward
- Encircle  — agents spread in an arc around target
- Diamond   — one forward, two on flanks, one rear
```

#### Formation Position Calculation

Needs a new **commander-side action method** (e.g., `CalculateFormation`) that:
1. Reads `FormationType`, `FormationOrigin`, `FormationFacing`, `SquadTarget` from commander BB
2. Reads `agentCount` from `TickContext`
3. Computes per-agent target positions using geometry
4. Writes `AgentFormationTarget[i]` for each agent

Example for a 4-agent Line formation facing target:
```
       T (target)
       │
  ←────┼────→  (line perpendicular to facing)
  A0   A1  A2  A3  (agents spread evenly)

Spacing = totalWidth / (agentCount - 1)
For each agent i:
  offset = (i - (agentCount-1)/2) * spacing
  position = origin + right * offset
  AgentFormationTarget[i] = position
```

#### Movement to Formation

Agent side:
```
[Agent Root]
└── [SELECTOR]
    ├── [SEQUENCE: Formation Move]
    │   ├── CheckOrder("MoveToFormation")
    │   └── MoveTo(target=AgentFormationTarget)
    ├── [SEQUENCE: Attack]
    │   ├── CheckOrder("Attack")
    │   └── Enemy_FireSequence
    └── [SEQUENCE: Idle]
        └── CheckOrder("Idle")
```

### 12.4 Flank Tactic Design

**Concept:** Split squad into two groups — one pins the enemy (Suppress/Fix), while the other moves to attack from the side (Flank).

**State machine on commander tree:**

```
Phase 1: EVALUATE
  └─ Check if enemy detected, check agent count, check terrain

Phase 2: ASSIGN ROLES
  ├─ Assign 2 agents to "FireSupport" role (suppress)
  └─ Assign 2 agents to "Assault" role (flank)

Phase 3: EXECUTE SIMULTANEOUSLY
  ├─ [PARALLEL]
  │   ├─ [ForEachRole: "FireSupport"]
  │   │   └─ SendOrder("Suppress")
  │   │       └─ Agent: select target, fire repeatedly (suppressing fire)
  │   └─ [ForEachRole: "Assault"]
  │       └─ [SEQUENCE]
  │           ├─ SendOrder("MoveToFlankPosition")
  │           │   └─ Agent: move to position 90° offset from fire-support line
  │           └─ [Priority] (interruptible)
  │               ├─ [SEQUENCE: Self-Preservation]
  │               │   ├─ CheckVariable: Health < 30%
  │               │   └─ SendOrder("Retreat")
  │               └─ [SEQUENCE: Engage]
  │                   ├─ CheckVariable: IsInFlankPosition
  │                   └─ SendOrder("Attack")
  │                       └─ Agent: move in + fire at target from flank

Phase 4: ASSESS
  └─ Check if target destroyed or agents lost → re-evaluate tactic
```

**Key data needed for Flank:**
- `SquadTarget` position (Vector3 on squad BB)
- `FlankOffset` — distance perpendicular to the line between fire-support position and target
- `IsInFlankPosition` — agent self-check: distance to flank target position < threshold
- `FireSupportLineOrigin` and `FireSupportLineDirection` — for computing flank positions

**Flank position calculation (commander side):**
```
FireSupport line: from fire-support center to target
Flank position: point rotated 90° from that line, at flankDistance from target

direction = (target - fireSupportCenter).normalized
right = Cross(up, direction).normalized
flankPosition = target + right * flankDistance
```

### 12.5 Suppress Tactic Design

**Concept:** One or more agents maintain continuous fire on enemy position to keep them pinned/under cover while other agents maneuver.

**Agent-side Suppress behavior:**
```
[Agent: Suppress]
└── [REPEATER] (infinite / until order changes)
    └── [SEQUENCE]
        ├── Cooldown(0.5) — fire rate limiter
        ├── Enemy_SelectDetectedTarget (strategy=Nearest)
        └── Enemy_FireSequence
```

This keeps firing at the target continuously, consuming ammo/attention but pinning the enemy.

**Suppress parameters:**
- `suppressDuration` — how long to suppress before reassessing
- `suppressSpread` — random offset added to aim point (suppressive fire isn't precise)
- `suppressAmmoThreshold` — stop suppressing if ammo < threshold

### 12.6 Coordination Challenges

**Problem: Simultaneous Execution**

In a behaviour tree, nodes execute sequentially. To have Suppress and Flank happen "simultaneously", the commander uses the `PARALLEL` composite:

```
[SEQUENCE: Flank+Suppress Tactic]
├── [Action: CalculateFlankPositions]     # Compute formation targets
├── [Action: AssignRoles]                 # Set AgentRoles on squad BB
└── [PARALLEL]
    ├── [ForEachRole: "FireSupport"]
    │   └── SendOrder("Suppress")
    └── [ForEachRole: "Assault"]
        └── SendOrder("MoveToFlankPosition")
```

But the PARALLEL composite returns SUCCESS only when both sides complete. Since `SendOrder` returns SUCCESS immediately (fire-and-forget), the parallel completes instantly. The agents then receive their orders in the next `TickAgents()` cycle and execute independently.

**This means:**
- Commander: send orders (instant) → both sides "done"
- Agents: each runs its own tree independently with the received order
- The agents effectively execute **in parallel** because each has its own TreeEvaluator

**Problem: Synchronization**

How does the commander know when all Assault agents have reached flank position?
- Option A: Agent writes a boolean `AgentIsInPosition` to squad BB. Commander reads via `ForEachRole`.
- Option B: Commander uses `GetNearestAgent` / `GetLowestAgent` to check distances.
- Option C: Timer-based — after N seconds, assume agents are in position.

**Recommendation:** Option A is simplest — add `AgentIsInPosition` (bool, isSquadData) to squad BB.

**Problem: Dynamic Role Reassignment**

If an Assault agent dies, the FireSupport agent should switch to Assault:
- Commander reads `AgentHealth` from squad BB
- If `AgentHealth[i] <= 0` for an Assault agent, reassign a FireSupport agent to Assault
- This requires a dynamic role-reassignment composite (new method: `ReassignRole`)

### 12.7 New Methods Needed (Summary)

#### Commander-Side

| Method | Type | Purpose |
|---|---|---|
| `CalculateFormation` | Action | Compute per-agent formation positions, write to AgentFormationTarget |
| `ReassignRole` | Action | Change an agent's role based on tactical situation |
| `EvaluateTactic` | Condition | Check if conditions are right for a tactic (enemy in range, agents available, etc.) |
| `SetTacticPhase` | Action | Progress the tactic state machine |

#### Agent-Side

| Method | Type | Purpose |
|---|---|---|
| `ReportPosition` | Action | Write transform.position to squad BB variable |
| `ReportHealth` | Action | Write health to squad BB variable |
| `MoveInFormation` | Action | Move to formation target, maintain spacing from neighbors |
| `IsInPosition` | Condition | Check if distance to formation target < threshold |

#### Squad BB Variables Needed

| Variable | Type | Stride | Purpose |
|---|---|---|---|
| `AgentPosition` | Vector3 | N (squad) | Agent world positions |
| `AgentFormationTarget` | Vector3 | N (squad) | Where each agent should move |
| `AgentHealth` | float | N (squad) | Health per agent |
| `AgentIsInPosition` | bool | N (squad) | Ready status per agent |
| `FormationType` | int | 1 | Current formation (enum index) |
| `FormationOrigin` | Vector3 | 1 | Squad center point |
| `FormationFacing` | Vector3 | 1 | Squad facing direction |
| `SquadTarget` | Transform | 1 | Shared enemy target |
| `TacticPhase` | int | 1 | State machine phase |

### 12.8 OrderRegistry Extensions

New order names to add:
```
- MoveToFormation   — agent should move to its AgentFormationTarget
- Flank             — agent should execute flank maneuver
- Suppress           — agent should execute suppressive fire
- HoldPosition       — agent should stay put and engage from current position
- Assault            — agent should move in aggressively
- Retreat            — agent should fall back
- Regroup            — agent should move toward FormationOrigin
```

### 12.9 Recommended Implementation Order

1. **Phase 1: Position Sharing**
   - Add `AgentPosition` variable to squad BB
   - Create `ReportPosition` agent method
   - Create `SetFromTransform` binding for agent positions (existing `SetFromTransform` can be used)
   - Verify positions flow to commander BB

2. **Phase 2: Formation Positions**
   - Add `FormationType`, `FormationOrigin`, `FormationFacing`, `AgentFormationTarget` to squad BB
   - Create `CalculateFormation` commander action method
   - Create `MoveInFormation` agent action method (or reuse `MoveTo` with `AgentFormationTarget`)
   - Test with simple Line formation

3. **Phase 3: Role-Based Tactic Dispatch**
   - Add `SquadTarget`, `TacticPhase` to squad BB
   - Create `EvaluateTactic` condition and `SetTacticPhase` action
   - Build commander tree: SELECTOR of tactic branches
   - Agents use `CheckOrder` to branch on received orders

4. **Phase 4: Flank + Suppress**
   - Add order names: "Suppress", "Flank", "Assault"
   - Build agent Suppress subtree (continuous fire with cooldown)
   - Build agent Flank subtree (move to flank position, then engage)
   - Build commander Flank+Suppress composite (PARALLEL of ForEachRole)

5. **Phase 5: Dynamic Adaptation**
   - Add `AgentHealth`, `AgentIsInPosition` to squad BB
   - Create `ReassignRole` and `IsInPosition` methods
   - Commander tree adapts to casualties and position readiness

---

*End of document. All paths below are relative to `Assets/` unless otherwise stated.*
