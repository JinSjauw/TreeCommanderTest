# Typed Blackboard Storage (Proposition A) — Implementation Plan

> Execute task-by-task in order. Each phase is independently testable and committable. Steps use checkbox (`- [ ]`) syntax. Do not skip the manual checkpoint at the end of each phase — the user verifies before moving on.

**Goal:** Replace the type-erased `object[]` blackboard storage with per-type arrays behind an indirection map, eliminating per-tick boxing on all typed hot paths, while keeping the flat virtual slot numbering (and therefore the baker, `FieldData`, stride markers, and `currentAgentOffset`) completely unchanged.

**Architecture:** `TypedBlackboardStorage` implements the existing `IBlackboardStorage` interface, so nothing above storage level changes shape. Virtual slots map to `(typed array, local index)` via a `SlotLocation[]` built at `Initialize`. New allocation-free typed accessors (`GetFloat`, `GetInt`, `GetVector3`, ...) are exposed through a new `IBlackboardTypedAccess` interface; `IBlackBoardAccess` extends it so `FieldBinding`/`TrackedBinding` compiled delegates call them. Reference types and custom value types stay in an `object[]` fallback; the boxed API (`GetBoxed`/`SetBoxed`) keeps working for editor tooling, JSON, and dynamic-type nodes.

**Tech Stack:** Unity 6000.3.5f2, C# (netstandard 2.1), Unity Test Framework 1.6.0 (NUnit, EditMode).

**Out of scope (later plans):** build-time getter/setter codegen, DOTS / `NativeArray<T>`, editor UI changes (none needed — boxed API unchanged), any change to `FieldData` or `TreeBaker`.

**Key design invariants:**
1. Virtual slot numbering is identical to the legacy flat layout: variable order, stride expansion. All baked slot indices remain valid.
2. `GetBoxed` on an enum slot must return an **enum instance**, not an `int` (boxed consumers — squad copy, editor, JSON — expect the declared type).
3. Only int32-backed enums go into the int array; exotic backing types fall back to `object[]`.
4. Generic `Get<T>`/`Set<T>` may box (compatibility path). Typed accessors never box. Hot paths must use typed accessors.

---

## Running the tests

**UI:** `Window → Test Runner → EditMode → Run All`

**CLI** (adjust editor install path if needed):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.5f2\Editor\Unity.exe" -batchmode -projectPath "d:\Dev\TreeCommanderTest" -runTests -testPlatform EditMode -testResults "d:\Dev\TreeCommanderTest\TestResults\editmode.xml" -quit
```

Expected: exit code 0; `editmode.xml` shows all tests passed.

---

## Phase 0 — Test scaffolding + storage contract suite

Locks current `ManagedBlackboardStorage` behavior into a reusable contract suite. The same suite runs against `TypedBlackboardStorage` from Phase 2 on — this is what makes the swap safe.

### Task 0.1: EditMode test assembly

**Files:**
- Create: `Assets/BehaviourTree/Tests/EditMode/BehaviourTree.Runtime.Tests.asmdef`

Note: the name `BehaviourTree.Runtime.Tests` is deliberate — [TreeBaker.cs](file:///d:/Dev/TreeCommanderTest/Assets/BehaviourTree/Runtime/TreeBaker.cs) already declares `[assembly: InternalsVisibleTo("BehaviourTree.Runtime.Tests")]`, giving the tests access to Runtime internals (needed in Phase 6).

- [ ] **Step 1: Create the asmdef**

```json
{
    "name": "BehaviourTree.Runtime.Tests",
    "rootNamespace": "BehaviourTree.Tests",
    "references": [
        "BehaviourTree.Core",
        "BehaviourTree.Runtime",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": true,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Verify the assembly appears in Test Runner → EditMode with no compile errors.**

### Task 0.2: Storage contract suite

**Files:**
- Create: `Assets/BehaviourTree/Tests/EditMode/BlackboardStorageContractTests.cs`

- [ ] **Step 1: Write the contract suite**

```csharp
using System.Collections.Generic;
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    /// <summary>
    /// Contract tests every IBlackboardStorage implementation must satisfy.
    /// One TestFixture attribute per implementation.
    /// </summary>
    [TestFixture(typeof(ManagedBlackboardStorage))]
    public class BlackboardStorageContractTests<TStorage> where TStorage : IBlackboardStorage, new()
    {
        private enum TestState { Idle = 0, Running = 1 }

        private static List<BlackboardVariableBase> BuildVariables()
        {
            return new List<BlackboardVariableBase>
            {
                new BlackboardVariable<float>("health", 1, 100f),                  // slot 0
                new BlackboardVariable<int>("roles", 3, 7),                        // slots 1-3
                new BlackboardVariable<Vector3>("targetPos", 1, Vector3.one),      // slot 4
                new BlackboardVariable<string>("label", 1, "hello"),               // slot 5 (reference kind)
                new BlackboardVariable<TestState>("state", 1, TestState.Running),  // slot 6 (enum)
            };
        }

        private static TStorage CreateStorage()
        {
            var storage = new TStorage();
            storage.Initialize(BuildVariables());
            return storage;
        }

        [Test]
        public void Layout_SlotCountAndRangesMatchStrideExpansion()
        {
            TStorage storage = CreateStorage();
            Assert.AreEqual(7, storage.Count);

            storage.GetVariableSlotRange(0, out int baseSlot0, out int stride0);
            Assert.AreEqual(0, baseSlot0);
            Assert.AreEqual(1, stride0);

            storage.GetVariableSlotRange(1, out int baseSlot1, out int stride1);
            Assert.AreEqual(1, baseSlot1);
            Assert.AreEqual(3, stride1);

            storage.GetVariableSlotRange(4, out int baseSlot4, out int stride4);
            Assert.AreEqual(6, baseSlot4);
            Assert.AreEqual(1, stride4);
        }

        [Test]
        public void Seed_InitialValuesArePresent()
        {
            TStorage storage = CreateStorage();
            Assert.AreEqual(100f, storage.Get<float>(0));
            Assert.AreEqual(7, storage.Get<int>(1));
            Assert.AreEqual(7, storage.Get<int>(3)); // stride element
            Assert.AreEqual(Vector3.one, storage.Get<Vector3>(4));
            Assert.AreEqual("hello", storage.Get<string>(5));
            Assert.AreEqual(TestState.Running, storage.Get<TestState>(6));
        }

        [Test]
        public void TypedRoundtrip_ValueTypesAndRefs()
        {
            TStorage storage = CreateStorage();
            storage.Set(0, 42.5f);
            storage.Set(2, 99);
            storage.Set(4, new Vector3(1, 2, 3));
            storage.Set(5, "world");
            storage.Set(6, TestState.Idle);

            Assert.AreEqual(42.5f, storage.Get<float>(0));
            Assert.AreEqual(99, storage.Get<int>(2));
            Assert.AreEqual(new Vector3(1, 2, 3), storage.Get<Vector3>(4));
            Assert.AreEqual("world", storage.Get<string>(5));
            Assert.AreEqual(TestState.Idle, storage.Get<TestState>(6));
        }

        [Test]
        public void BoxedRoundtrip_EnumComesBackAsEnumNotInt()
        {
            TStorage storage = CreateStorage();
            storage.SetBoxed(6, TestState.Idle);
            object boxed = storage.GetBoxed(6);
            Assert.IsInstanceOf<TestState>(boxed);
            Assert.AreEqual(TestState.Idle, boxed);
        }

        [Test]
        public void BoxedRoundtrip_StrideElements()
        {
            TStorage storage = CreateStorage();
            storage.SetBoxed(1, 10);
            storage.SetBoxed(2, 20);
            storage.SetBoxed(3, 30);
            Assert.AreEqual(10, storage.GetBoxed(1));
            Assert.AreEqual(20, storage.GetBoxed(2));
            Assert.AreEqual(30, storage.GetBoxed(3));
        }

        [Test]
        public void TypeMismatch_SetIsRejected()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                TStorage storage = CreateStorage();
                storage.Set(0, 123); // int into float slot
                Assert.AreEqual(100f, storage.Get<float>(0)); // unchanged
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void SlotKinds_ValueAndReference()
        {
            TStorage storage = CreateStorage();
            Assert.AreEqual(BlackboardSlotKind.Value, storage.GetSlotKind(0));
            Assert.AreEqual(BlackboardSlotKind.Value, storage.GetSlotKind(6)); // enum = value type
            Assert.AreEqual(BlackboardSlotKind.Reference, storage.GetSlotKind(5)); // string
        }

        [Test]
        public void GetT_ConvertsStoredFloatToInt()
        {
            TStorage storage = CreateStorage();
            storage.Set(0, 5f);
            Assert.AreEqual(5, storage.Get<int>(0));
        }

        [Test]
        public void SetBoxed_NullOnValueSlotRejected_NullOnRefSlotAccepted()
        {
            TStorage storage = CreateStorage();
            storage.SetBoxed(0, null);
            Assert.AreEqual(100f, storage.Get<float>(0));

            storage.SetBoxed(5, null);
            Assert.IsNull(storage.GetBoxed(5));
        }

        [Test]
        public void OutOfRange_GetReturnsDefault()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                TStorage storage = CreateStorage();
                Assert.AreEqual(0f, storage.Get<float>(999));
                Assert.IsNull(storage.GetBoxed(999));
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
```

- [ ] **Step 2: Run EditMode tests.** Expected: all 10 PASS against `ManagedBlackboardStorage`. If any fail, the contract doesn't match reality — fix the test to describe actual legacy behavior before continuing.

- [ ] **Step 3: Commit**

```powershell
git add Assets/BehaviourTree/Tests
git commit -m "test: add blackboard storage contract suite (baseline for typed storage)"
```

**Manual checkpoint:** user runs the suite from the Test Runner UI to confirm the harness works.

---

## Phase 1 — Layout builder (pure logic)

The virtual-slot → typed-array mapping as a pure static function. No storage yet.

### Task 1.1: `BlackboardStorageLayout`

**Files:**
- Create: `Assets/BehaviourTree/Core/Blackboard/BlackboardStorageLayout.cs`
- Test: `Assets/BehaviourTree/Tests/EditMode/BlackboardStorageLayoutTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class BlackboardStorageLayoutTests
    {
        private enum IntEnum { A = 0, B = 1 }
        private enum LongEnum : long { A = 0, B = 1 }
        private struct CustomStruct { public int x; }

        [Test]
        public void Classify_KnownTypes()
        {
            Assert.AreEqual(BlackboardArrayId.Float, BlackboardStorageLayout.Classify(typeof(float)));
            Assert.AreEqual(BlackboardArrayId.Int, BlackboardStorageLayout.Classify(typeof(int)));
            Assert.AreEqual(BlackboardArrayId.Bool, BlackboardStorageLayout.Classify(typeof(bool)));
            Assert.AreEqual(BlackboardArrayId.Vector2, BlackboardStorageLayout.Classify(typeof(Vector2)));
            Assert.AreEqual(BlackboardArrayId.Vector3, BlackboardStorageLayout.Classify(typeof(Vector3)));
            Assert.AreEqual(BlackboardArrayId.Vector4, BlackboardStorageLayout.Classify(typeof(Vector4)));
            Assert.AreEqual(BlackboardArrayId.Color, BlackboardStorageLayout.Classify(typeof(Color)));
            Assert.AreEqual(BlackboardArrayId.Quaternion, BlackboardStorageLayout.Classify(typeof(Quaternion)));
        }

        [Test]
        public void Classify_EnumsAndFallbacks()
        {
            Assert.AreEqual(BlackboardArrayId.Int, BlackboardStorageLayout.Classify(typeof(IntEnum)));
            Assert.AreEqual(BlackboardArrayId.Object, BlackboardStorageLayout.Classify(typeof(LongEnum)));
            Assert.AreEqual(BlackboardArrayId.Object, BlackboardStorageLayout.Classify(typeof(CustomStruct)));
            Assert.AreEqual(BlackboardArrayId.Object, BlackboardStorageLayout.Classify(typeof(Transform)));
            Assert.AreEqual(BlackboardArrayId.Object, BlackboardStorageLayout.Classify(typeof(string)));
            Assert.AreEqual(BlackboardArrayId.Object, BlackboardStorageLayout.Classify(typeof(double)));
            Assert.AreEqual(BlackboardArrayId.Object, BlackboardStorageLayout.Classify(null));
        }

        [Test]
        public void Build_VirtualSlotsMatchLegacyFlatLayout()
        {
            var variables = new List<BlackboardVariableBase>
            {
                new BlackboardVariable<float>("health", 1, 0f),            // slot 0 → floats[0]
                new BlackboardVariable<int>("roles", 3, 0),                // slots 1-3 → ints[0..2]
                new BlackboardVariable<float>("speed", 1, 0f),             // slot 4 → floats[1]
                new BlackboardVariable<Transform>("target", 1, null),      // slot 5 → objects[0]
                new BlackboardVariable<IntEnum>("state", 1, IntEnum.A),    // slot 6 → ints[3]
            };

            BlackboardStorageLayout.Build(variables, out SlotLocation[] map, out System.Type[] slotTypes,
                out BlackboardSlotKind[] slotKinds, out int[] sizes);

            Assert.AreEqual(7, map.Length);
            Assert.AreEqual(new SlotLocation(BlackboardArrayId.Float, 0), map[0]);
            Assert.AreEqual(new SlotLocation(BlackboardArrayId.Int, 0), map[1]);
            Assert.AreEqual(new SlotLocation(BlackboardArrayId.Int, 1), map[2]);
            Assert.AreEqual(new SlotLocation(BlackboardArrayId.Int, 2), map[3]);
            Assert.AreEqual(new SlotLocation(BlackboardArrayId.Float, 1), map[4]);
            Assert.AreEqual(new SlotLocation(BlackboardArrayId.Object, 0), map[5]);
            Assert.AreEqual(new SlotLocation(BlackboardArrayId.Int, 3), map[6]);

            Assert.AreEqual(2, sizes[(int)BlackboardArrayId.Float]);
            Assert.AreEqual(4, sizes[(int)BlackboardArrayId.Int]);
            Assert.AreEqual(1, sizes[(int)BlackboardArrayId.Object]);

            Assert.AreEqual(BlackboardSlotKind.Reference, slotKinds[5]);
            Assert.AreEqual(BlackboardSlotKind.Value, slotKinds[6]); // enum = value type
            Assert.AreEqual(typeof(IntEnum), slotTypes[6]);
        }

        [Test]
        public void Build_NullAndEmpty_ProduceNullMap()
        {
            BlackboardStorageLayout.Build(null, out SlotLocation[] map, out _, out _, out int[] sizes);
            Assert.IsNull(map);
            Assert.AreEqual(BlackboardStorageLayout.ArrayCount, sizes.Length);

            BlackboardStorageLayout.Build(new List<BlackboardVariableBase>(), out map, out _, out _, out _);
            Assert.IsNull(map);
        }
    }
}
```

- [ ] **Step 2: Run tests — verify they FAIL** (types don't exist yet).

- [ ] **Step 3: Implement `BlackboardStorageLayout`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>Identifies which typed array a blackboard slot physically lives in.</summary>
    public enum BlackboardArrayId : byte
    {
        Object = 0,
        Int = 1,
        Float = 2,
        Bool = 3,
        Vector2 = 4,
        Vector3 = 5,
        Vector4 = 6,
        Color = 7,
        Quaternion = 8,
        Count // sentinel — length of the array-size table
    }

    /// <summary>Where a virtual slot's value physically lives.</summary>
    public readonly struct SlotLocation : IEquatable<SlotLocation>
    {
        public readonly BlackboardArrayId ArrayId;
        public readonly int LocalIndex;

        public SlotLocation(BlackboardArrayId arrayId, int localIndex)
        {
            ArrayId = arrayId;
            LocalIndex = localIndex;
        }

        public bool Equals(SlotLocation other) => ArrayId == other.ArrayId && LocalIndex == other.LocalIndex;
        public override bool Equals(object obj) => obj is SlotLocation other && Equals(other);
        public override int GetHashCode() => ((int)ArrayId << 24) ^ LocalIndex;
    }

    /// <summary>
    /// Builds the virtual-slot → typed-array mapping for a variable list.
    /// Virtual slot numbering is IDENTICAL to the legacy flat object[] layout
    /// (variable order, stride expansion). Only physical storage changes.
    /// </summary>
    public static class BlackboardStorageLayout
    {
        public const int ArrayCount = (int)BlackboardArrayId.Count;

        public static BlackboardArrayId Classify(Type type)
        {
            if (type == null) return BlackboardArrayId.Object;
            if (type == typeof(float)) return BlackboardArrayId.Float;
            if (type == typeof(bool)) return BlackboardArrayId.Bool;
            if (type == typeof(int)) return BlackboardArrayId.Int;
            if (type.IsEnum)
            {
                // Only int32-backed enums go to the int array; exotic backing
                // types stay boxed in the object array.
                return Enum.GetUnderlyingType(type) == typeof(int)
                    ? BlackboardArrayId.Int
                    : BlackboardArrayId.Object;
            }
            if (type == typeof(Vector2)) return BlackboardArrayId.Vector2;
            if (type == typeof(Vector3)) return BlackboardArrayId.Vector3;
            if (type == typeof(Vector4)) return BlackboardArrayId.Vector4;
            if (type == typeof(Color)) return BlackboardArrayId.Color;
            if (type == typeof(Quaternion)) return BlackboardArrayId.Quaternion;
            return BlackboardArrayId.Object;
        }

        public static void Build(
            IReadOnlyList<BlackboardVariableBase> variables,
            out SlotLocation[] map,
            out Type[] slotTypes,
            out BlackboardSlotKind[] slotKinds,
            out int[] arraySizes)
        {
            arraySizes = new int[ArrayCount];

            if (variables == null || variables.Count == 0)
            {
                map = null;
                slotTypes = null;
                slotKinds = null;
                return;
            }

            int totalSlots = 0;
            for (int i = 0; i < variables.Count; i++)
            {
                int stride = variables[i].Stride;
                totalSlots += (stride > 1) ? stride : 1;
            }

            map = new SlotLocation[totalSlots];
            slotTypes = new Type[totalSlots];
            slotKinds = new BlackboardSlotKind[totalSlots];

            // Single pass: arraySizes doubles as the next-free-local-index
            // counter while assigning, ending as the final per-array sizes.
            int slot = 0;
            for (int varIndex = 0; varIndex < variables.Count; varIndex++)
            {
                BlackboardVariableBase variable = variables[varIndex];
                Type type = variable.GetValueType();
                BlackboardArrayId arrayId = Classify(type);
                int stride = variable.Stride;
                int actualStride = (stride > 1) ? stride : 1;

                for (int element = 0; element < actualStride; element++)
                {
                    slotTypes[slot + element] = type;
                    slotKinds[slot + element] = (type != null && !type.IsValueType)
                        ? BlackboardSlotKind.Reference
                        : BlackboardSlotKind.Value;
                    map[slot + element] = new SlotLocation(arrayId, arraySizes[(int)arrayId]);
                    arraySizes[(int)arrayId]++;
                }

                slot += actualStride;
            }
        }
    }
}
```

- [ ] **Step 4: Run tests — verify PASS.**

- [ ] **Step 5: Commit**

```powershell
git add Assets/BehaviourTree/Core/Blackboard/BlackboardStorageLayout.cs Assets/BehaviourTree/Tests
git commit -m "feat: add BlackboardStorageLayout — virtual slot to typed-array mapping"
```

**Manual checkpoint:** user reviews the classification table (invariant #3) and confirms the supported type set before storage is built on it.

## Phase 2 — `TypedBlackboardStorage` core (`IBlackboardStorage`)

The new storage implementing the existing interface with generic/boxed paths only (typed accessors arrive in Phase 3). After this phase the contract suite runs against BOTH implementations.

### Task 2.1: Storage implementation

**Files:**
- Create: `Assets/BehaviourTree/Core/Blackboard/TypedBlackboardStorage.cs`
- Modify: `Assets/BehaviourTree/Tests/EditMode/BlackboardStorageContractTests.cs` (add one fixture attribute)

- [ ] **Step 1: Add the second fixture to the contract suite**

In `BlackboardStorageContractTests.cs`, replace:

```csharp
    [TestFixture(typeof(ManagedBlackboardStorage))]
```

with:

```csharp
    [TestFixture(typeof(ManagedBlackboardStorage))]
    [TestFixture(typeof(TypedBlackboardStorage))]
```

- [ ] **Step 2: Run tests — verify FAIL** (`TypedBlackboardStorage` doesn't exist yet).

- [ ] **Step 3: Implement the storage**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Blackboard storage backed by one array per supported value type, plus an
    /// object[] fallback for reference types and custom value types.
    /// Virtual slot numbering matches the legacy flat object[] layout exactly;
    /// a SlotLocation map translates virtual slots to (typed array, local index).
    ///
    /// The generic Get&lt;T&gt;/Set&lt;T&gt; and boxed paths may allocate (compatibility).
    /// Hot paths must use the IBlackboardTypedAccess accessors (Phase 3).
    /// </summary>
    public sealed class TypedBlackboardStorage : IBlackboardStorage
    {
        private BlackboardDefinition definition;
        private IReadOnlyList<BlackboardVariableBase> runtimeVariables;

        private object[] objects;
        private int[] ints;
        private float[] floats;
        private bool[] bools;
        private Vector2[] vec2s;
        private Vector3[] vec3s;
        private Vector4[] vec4s;
        private Color[] colors;
        private Quaternion[] quats;

        private SlotLocation[] map;
        private Type[] slotTypes;
        private BlackboardSlotKind[] slotKinds;

        public BlackboardDefinition Definition => definition;
        public int Count => map?.Length ?? 0;

        public void Initialize(BlackboardDefinition definition)
        {
            this.definition = definition;
            if (definition == null)
            {
                runtimeVariables = null;
                ClearArrays();
                return;
            }

            runtimeVariables = definition.GetAllVariables();
            InitializeFromVariables(runtimeVariables);
        }

        public void Initialize(IReadOnlyList<BlackboardVariableBase> variables)
        {
            definition = null;
            runtimeVariables = variables;
            InitializeFromVariables(variables);
        }

        private void ClearArrays()
        {
            map = null;
            slotTypes = null;
            slotKinds = null;
            objects = null;
            ints = null;
            floats = null;
            bools = null;
            vec2s = null;
            vec3s = null;
            vec4s = null;
            colors = null;
            quats = null;
        }

        private void InitializeFromVariables(IReadOnlyList<BlackboardVariableBase> variables)
        {
            if (variables == null || variables.Count == 0)
            {
                ClearArrays();
                return;
            }

            BlackboardStorageLayout.Build(variables, out map, out slotTypes, out slotKinds, out int[] sizes);

            objects = new object[sizes[(int)BlackboardArrayId.Object]];
            ints = new int[sizes[(int)BlackboardArrayId.Int]];
            floats = new float[sizes[(int)BlackboardArrayId.Float]];
            bools = new bool[sizes[(int)BlackboardArrayId.Bool]];
            vec2s = new Vector2[sizes[(int)BlackboardArrayId.Vector2]];
            vec3s = new Vector3[sizes[(int)BlackboardArrayId.Vector3]];
            vec4s = new Vector4[sizes[(int)BlackboardArrayId.Vector4]];
            colors = new Color[sizes[(int)BlackboardArrayId.Color]];
            quats = new Quaternion[sizes[(int)BlackboardArrayId.Quaternion]];

            SeedFromVariables(variables);
        }

        private void SeedFromVariables(IReadOnlyList<BlackboardVariableBase> variables)
        {
            // Phase 8 replaces this boxed seeding with typed seeding.
            // Boxing here happens once per Initialize, not per tick.
            int slot = 0;
            for (int varIndex = 0; varIndex < variables.Count; varIndex++)
            {
                BlackboardVariableBase variable = variables[varIndex];
                int stride = variable.Stride;
                int actualStride = (stride > 1) ? stride : 1;
                for (int element = 0; element < actualStride; element++)
                    WriteBoxedUnchecked(slot + element, variable.GetBoxedValue(element));
                slot += actualStride;
            }
        }

        public void GetVariableSlotRange(int variableIndex, out int baseSlot, out int stride)
        {
            if (runtimeVariables == null || variableIndex < 0 || variableIndex >= runtimeVariables.Count)
            {
                baseSlot = -1;
                stride = -1;
                Debug.LogError($"[TypedBlackboardStorage] GetVariableSlotRange: variableIndex {variableIndex} is out of bounds ({(runtimeVariables?.Count ?? 0)} variables). Returning sentinel (-1, -1).");
                return;
            }

            baseSlot = 0;
            stride = 1;

            for (int prevIndex = 0; prevIndex < variableIndex; prevIndex++)
            {
                int prevStride = runtimeVariables[prevIndex].Stride;
                baseSlot += (prevStride > 1) ? prevStride : 1;
            }

            stride = runtimeVariables[variableIndex].Stride;
            if (stride <= 1) stride = 1;
        }

        public BlackboardSlotKind GetSlotKind(int index)
        {
            if (slotKinds == null || index < 0 || index >= slotKinds.Length) return BlackboardSlotKind.Value;
            return slotKinds[index];
        }

        public T Get<T>(int index)
        {
            if (map == null || index < 0 || index >= map.Length)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Blackboard] Invalid index or map[] is NULL: {index} : {typeof(T).Name}");
#endif
                return default;
            }

            SlotLocation loc = map[index];
            switch (loc.ArrayId)
            {
                case BlackboardArrayId.Float:
                {
                    float f = floats[loc.LocalIndex];
                    if (typeof(T) == typeof(float)) return (T)(object)f;
                    try { return (T)Convert.ChangeType(f, typeof(T)); }
                    catch { return default; }
                }
                case BlackboardArrayId.Int:
                {
                    int i = ints[loc.LocalIndex];
                    if (typeof(T) == typeof(int)) return (T)(object)i;
                    if (typeof(T).IsEnum) return (T)Enum.ToObject(typeof(T), i);
                    try { return (T)Convert.ChangeType(i, typeof(T)); }
                    catch { return default; }
                }
                case BlackboardArrayId.Bool:
                {
                    bool b = bools[loc.LocalIndex];
                    if (typeof(T) == typeof(bool)) return (T)(object)b;
                    try { return (T)Convert.ChangeType(b, typeof(T)); }
                    catch { return default; }
                }
                case BlackboardArrayId.Vector2:
                    return typeof(T) == typeof(Vector2) ? (T)(object)vec2s[loc.LocalIndex] : default;
                case BlackboardArrayId.Vector3:
                    return typeof(T) == typeof(Vector3) ? (T)(object)vec3s[loc.LocalIndex] : default;
                case BlackboardArrayId.Vector4:
                    return typeof(T) == typeof(Vector4) ? (T)(object)vec4s[loc.LocalIndex] : default;
                case BlackboardArrayId.Color:
                    return typeof(T) == typeof(Color) ? (T)(object)colors[loc.LocalIndex] : default;
                case BlackboardArrayId.Quaternion:
                    return typeof(T) == typeof(Quaternion) ? (T)(object)quats[loc.LocalIndex] : default;
                default:
                {
                    object value = objects[loc.LocalIndex];
                    if (value is T typed) return typed;
                    if (value == null) return default;
                    try { return (T)Convert.ChangeType(value, typeof(T)); }
                    catch
                    {
#if UNITY_EDITOR
                        Debug.LogWarning($"[Blackboard] Unexpected type in BB: index {index} expected {typeof(T).Name} but found {value.GetType().Name}.");
#endif
                        return default;
                    }
                }
            }
        }

        public void Set<T>(int index, T value)
        {
            if (!CanWrite<T>(index, value))
            {
                Debug.LogWarning($"[Blackboard.Set] Type mismatch at index {index}: expected {(slotTypes != null && index < slotTypes.Length ? slotTypes[index]?.Name : "unknown")}, got {typeof(T).Name}");
                return;
            }

            SlotLocation loc = map[index];
            switch (loc.ArrayId)
            {
                case BlackboardArrayId.Float: floats[loc.LocalIndex] = (float)(object)value; break;
                case BlackboardArrayId.Int:
                    // CanWrite guarantees exact type match; Convert handles boxed enums.
                    ints[loc.LocalIndex] = typeof(T).IsEnum ? Convert.ToInt32(value) : (int)(object)value;
                    break;
                case BlackboardArrayId.Bool: bools[loc.LocalIndex] = (bool)(object)value; break;
                case BlackboardArrayId.Vector2: vec2s[loc.LocalIndex] = (Vector2)(object)value; break;
                case BlackboardArrayId.Vector3: vec3s[loc.LocalIndex] = (Vector3)(object)value; break;
                case BlackboardArrayId.Vector4: vec4s[loc.LocalIndex] = (Vector4)(object)value; break;
                case BlackboardArrayId.Color: colors[loc.LocalIndex] = (Color)(object)value; break;
                case BlackboardArrayId.Quaternion: quats[loc.LocalIndex] = (Quaternion)(object)value; break;
                default: objects[loc.LocalIndex] = value; break;
            }
        }

        public object GetBoxed(int index)
        {
            if (map == null || index < 0 || index >= map.Length) return null;

            SlotLocation loc = map[index];
            switch (loc.ArrayId)
            {
                case BlackboardArrayId.Float: return floats[loc.LocalIndex];
                case BlackboardArrayId.Int:
                {
                    // Invariant #2: boxed reads of enum slots return the enum instance.
                    Type slotType = slotTypes[index];
                    return (slotType != null && slotType.IsEnum)
                        ? Enum.ToObject(slotType, ints[loc.LocalIndex])
                        : ints[loc.LocalIndex];
                }
                case BlackboardArrayId.Bool: return bools[loc.LocalIndex];
                case BlackboardArrayId.Vector2: return vec2s[loc.LocalIndex];
                case BlackboardArrayId.Vector3: return vec3s[loc.LocalIndex];
                case BlackboardArrayId.Vector4: return vec4s[loc.LocalIndex];
                case BlackboardArrayId.Color: return colors[loc.LocalIndex];
                case BlackboardArrayId.Quaternion: return quats[loc.LocalIndex];
                default: return objects[loc.LocalIndex];
            }
        }

        public void SetBoxed(int index, object value)
        {
            if (!CanWriteBoxed(index, value)) return;
            WriteBoxedUnchecked(index, value);
        }

        private void WriteBoxedUnchecked(int index, object value)
        {
            if (map == null || index < 0 || index >= map.Length || value == null) return;

            SlotLocation loc = map[index];
            switch (loc.ArrayId)
            {
                case BlackboardArrayId.Float: floats[loc.LocalIndex] = Convert.ToSingle(value); break;
                case BlackboardArrayId.Int: ints[loc.LocalIndex] = Convert.ToInt32(value); break; // handles boxed enums
                case BlackboardArrayId.Bool: bools[loc.LocalIndex] = Convert.ToBoolean(value); break;
                case BlackboardArrayId.Vector2: vec2s[loc.LocalIndex] = (Vector2)value; break;
                case BlackboardArrayId.Vector3: vec3s[loc.LocalIndex] = (Vector3)value; break;
                case BlackboardArrayId.Vector4: vec4s[loc.LocalIndex] = (Vector4)value; break;
                case BlackboardArrayId.Color: colors[loc.LocalIndex] = (Color)value; break;
                case BlackboardArrayId.Quaternion: quats[loc.LocalIndex] = (Quaternion)value; break;
                default: objects[loc.LocalIndex] = value; break;
            }
        }

        private bool CanWrite<T>(int index, T value)
        {
            if (map == null || index < 0 || index >= map.Length) return false;

            Type slotType = slotTypes?[index];
            if (slotType == null) return true;

            if (!slotType.IsValueType)
            {
                if (value == null) return true;
                return slotType.IsAssignableFrom(typeof(T));
            }

            if (value == null) return false;
            return slotType == typeof(T);
        }

        private bool CanWriteBoxed(int index, object value)
        {
            if (map == null || index < 0 || index >= map.Length) return false;

            Type slotType = slotTypes?[index];
            if (slotType == null) return true;

            if (!slotType.IsValueType)
            {
                if (value == null) return true;
                return slotType.IsAssignableFrom(value.GetType());
            }

            if (value == null) return false;
            return slotType == value.GetType();
        }
    }
}
```

- [ ] **Step 4: Run the contract suite — verify all 20 tests PASS (10 per implementation).**

- [ ] **Step 5: Commit**

```powershell
git add Assets/BehaviourTree/Core/Blackboard/TypedBlackboardStorage.cs Assets/BehaviourTree/Tests
git commit -m "feat: add TypedBlackboardStorage — per-type arrays behind slot indirection map"
```

**Manual checkpoint:** none needed beyond green tests — nothing at runtime uses the new storage yet.

---

## Phase 3 — Typed accessors (`IBlackboardTypedAccess`)

The allocation-free hot-path API. Pure addition; no call sites change yet.

### Task 3.1: Interface + storage implementation

**Files:**
- Create: `Assets/BehaviourTree/Core/Blackboard/IBlackboardTypedAccess.cs`
- Modify: `Assets/BehaviourTree/Core/Blackboard/TypedBlackboardStorage.cs` (add interface + methods)
- Test: `Assets/BehaviourTree/Tests/EditMode/TypedBlackboardStorageAccessorTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class TypedBlackboardStorageAccessorTests
    {
        private enum TestState { Idle = 0, Running = 1 }

        private static TypedBlackboardStorage CreateStorage()
        {
            var variables = new List<BlackboardVariableBase>
            {
                new BlackboardVariable<float>("f", 1, 1.5f),            // slot 0
                new BlackboardVariable<int>("i", 1, 7),                 // slot 1
                new BlackboardVariable<bool>("b", 1, true),             // slot 2
                new BlackboardVariable<Vector2>("v2", 1, Vector2.one),  // slot 3
                new BlackboardVariable<Vector3>("v3", 1, Vector3.one),  // slot 4
                new BlackboardVariable<Vector4>("v4", 1, Vector4.one),  // slot 5
                new BlackboardVariable<Color>("c", 1, Color.red),       // slot 6
                new BlackboardVariable<Quaternion>("q", 1, Quaternion.identity), // slot 7
                new BlackboardVariable<TestState>("e", 1, TestState.Running),    // slot 8 (enum → int array)
                new BlackboardVariable<string>("s", 1, "x"),            // slot 9 (object array)
            };
            var storage = new TypedBlackboardStorage();
            storage.Initialize(variables);
            return storage;
        }

        [Test]
        public void TypedGet_ReturnsSeededValues()
        {
            IBlackboardTypedAccess s = CreateStorage();
            Assert.AreEqual(1.5f, s.GetFloat(0));
            Assert.AreEqual(7, s.GetInt(1));
            Assert.AreEqual(true, s.GetBool(2));
            Assert.AreEqual(Vector2.one, s.GetVector2(3));
            Assert.AreEqual(Vector3.one, s.GetVector3(4));
            Assert.AreEqual(Vector4.one, s.GetVector4(5));
            Assert.AreEqual(Color.red, s.GetColor(6));
            Assert.AreEqual(Quaternion.identity, s.GetQuaternion(7));
            Assert.AreEqual((int)TestState.Running, s.GetInt(8));
            Assert.AreEqual("x", s.GetObject<string>(9));
        }

        [Test]
        public void TypedSet_Roundtrips()
        {
            IBlackboardTypedAccess s = CreateStorage();
            s.SetFloat(0, 9.25f);
            s.SetInt(1, 42);
            s.SetBool(2, false);
            s.SetVector2(3, new Vector2(2, 3));
            s.SetVector3(4, new Vector3(4, 5, 6));
            s.SetVector4(5, new Vector4(7, 8, 9, 10));
            s.SetColor(6, Color.blue);
            s.SetQuaternion(7, Quaternion.Euler(10, 20, 30));
            s.SetInt(8, (int)TestState.Idle);
            s.SetObject(9, "y");

            Assert.AreEqual(9.25f, s.GetFloat(0));
            Assert.AreEqual(42, s.GetInt(1));
            Assert.AreEqual(false, s.GetBool(2));
            Assert.AreEqual(new Vector2(2, 3), s.GetVector2(3));
            Assert.AreEqual(new Vector3(4, 5, 6), s.GetVector3(4));
            Assert.AreEqual(new Vector4(7, 8, 9, 10), s.GetVector4(5));
            Assert.AreEqual(Color.blue, s.GetColor(6));
            Assert.AreEqual(Quaternion.Euler(10, 20, 30), s.GetQuaternion(7));
            Assert.AreEqual(0, s.GetInt(8));
            Assert.AreEqual("y", s.GetObject<string>(9));
        }

        [Test]
        public void TypedAccess_AndBoxedAccess_Agree()
        {
            TypedBlackboardStorage storage = CreateStorage();
            IBlackboardTypedAccess s = storage;

            s.SetFloat(0, 3.5f);
            s.SetInt(8, (int)TestState.Idle);

            Assert.AreEqual(3.5f, storage.GetBoxed(0));
            Assert.AreEqual(3.5f, storage.Get<float>(0));
            Assert.IsInstanceOf<TestState>(storage.GetBoxed(8)); // invariant #2
            Assert.AreEqual(TestState.Idle, storage.Get<TestState>(8));
        }
    }
}
```

- [ ] **Step 2: Run tests — verify FAIL** (`IBlackboardTypedAccess` doesn't exist).

- [ ] **Step 3: Create `IBlackboardTypedAccess`**

```csharp
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Allocation-free typed access to blackboard storage. The implementation
    /// resolves the virtual slot through the layout map and reads the typed
    /// array directly. This is the hot-path API; GetBoxed/SetBoxed remain for
    /// tooling, JSON, and dynamic-type nodes.
    /// </summary>
    public interface IBlackboardTypedAccess
    {
        float GetFloat(int slot);
        void SetFloat(int slot, float value);
        int GetInt(int slot);
        void SetInt(int slot, int value);
        bool GetBool(int slot);
        void SetBool(int slot, bool value);
        Vector2 GetVector2(int slot);
        void SetVector2(int slot, Vector2 value);
        Vector3 GetVector3(int slot);
        void SetVector3(int slot, Vector3 value);
        Vector4 GetVector4(int slot);
        void SetVector4(int slot, Vector4 value);
        Color GetColor(int slot);
        void SetColor(int slot, Color value);
        Quaternion GetQuaternion(int slot);
        void SetQuaternion(int slot, Quaternion value);
        T GetObject<T>(int slot) where T : class;
        void SetObject(int slot, object value);
    }
}
```

- [ ] **Step 4: Implement it on `TypedBlackboardStorage`**

Change the class declaration:

```csharp
    public sealed class TypedBlackboardStorage : IBlackboardStorage, IBlackboardTypedAccess
```

Add to the class body:

```csharp
        // ── IBlackboardTypedAccess ──────────────────────────────────
        // No bounds checks here beyond the map/array indexer: hot path.
        // Slot validity is guaranteed by the bake; invalid slots throw
        // IndexOutOfRange, same as any direct array access.

        public float GetFloat(int slot) => floats[map[slot].LocalIndex];
        public void SetFloat(int slot, float value) => floats[map[slot].LocalIndex] = value;
        public int GetInt(int slot) => ints[map[slot].LocalIndex];
        public void SetInt(int slot, int value) => ints[map[slot].LocalIndex] = value;
        public bool GetBool(int slot) => bools[map[slot].LocalIndex];
        public void SetBool(int slot, bool value) => bools[map[slot].LocalIndex] = value;
        public Vector2 GetVector2(int slot) => vec2s[map[slot].LocalIndex];
        public void SetVector2(int slot, Vector2 value) => vec2s[map[slot].LocalIndex] = value;
        public Vector3 GetVector3(int slot) => vec3s[map[slot].LocalIndex];
        public void SetVector3(int slot, Vector3 value) => vec3s[map[slot].LocalIndex] = value;
        public Vector4 GetVector4(int slot) => vec4s[map[slot].LocalIndex];
        public void SetVector4(int slot, Vector4 value) => vec4s[map[slot].LocalIndex] = value;
        public Color GetColor(int slot) => colors[map[slot].LocalIndex];
        public void SetColor(int slot, Color value) => colors[map[slot].LocalIndex] = value;
        public Quaternion GetQuaternion(int slot) => quats[map[slot].LocalIndex];
        public void SetQuaternion(int slot, Quaternion value) => quats[map[slot].LocalIndex] = value;
        public T GetObject<T>(int slot) where T : class => objects[map[slot].LocalIndex] as T;
        public void SetObject(int slot, object value) => objects[map[slot].LocalIndex] = value;
```

- [ ] **Step 5: Run ALL EditMode tests — verify PASS** (contract suite + layout + accessors).

- [ ] **Step 6: Commit**

```powershell
git add Assets/BehaviourTree/Core Assets/BehaviourTree/Tests
git commit -m "feat: add IBlackboardTypedAccess typed accessors on TypedBlackboardStorage"
```

**Manual checkpoint:** user eyeballs the accessor semantics (enum-as-int, `GetObject<T>` for ref types) — this is the API the codegen will target later.

## Phase 4 — `BlackBoard` plumbing + storage swap

Wires the typed accessors through `IBlackBoardAccess` (applying `currentAgentOffset`, same semantics as the existing generic methods) and swaps the storage instance. After this phase the whole runtime runs on `TypedBlackboardStorage`.

### Task 4.1: Extend `IBlackBoardAccess` and `BlackBoard`

**Files:**
- Modify: `Assets/BehaviourTree/Core/Blackboard/IBlackBoardAccess.cs`
- Modify: `Assets/BehaviourTree/Core/Blackboard/BlackBoard.cs`
- Test: `Assets/BehaviourTree/Tests/EditMode/BlackBoardTypedAccessTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class BlackBoardTypedAccessTests
    {
        private GameObject go;
        private BlackBoard bb;
        private BlackboardDefinition def;

        [SetUp]
        public void SetUp()
        {
            def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<float>("health", 1, 50f));   // slot 0
            def.sharedVariables.Add(new BlackboardVariable<int>("agentRoles", 4, 0));     // slots 1-4
            def.sharedVariables.Add(new BlackboardVariable<Transform>("target", 1, null)); // slot 5

            go = new GameObject("bb-test");
            bb = go.AddComponent<BlackBoard>();
            bb.Initialize(def);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void TypedAccess_NoOffset_ReadsExactSlot()
        {
            Assert.AreEqual(50f, bb.GetFloat(0));
            bb.SetFloat(0, 75f);
            Assert.AreEqual(75f, bb.GetFloat(0));
        }

        [Test]
        public void TypedAccess_WithAgentOffset_ReadsOffsetSlot()
        {
            IBlackboardTypedAccess raw = (IBlackboardTypedAccess)bb.Storage;
            raw.SetInt(2, 42); // raw slot 2 = agentRoles element 1

            bb.currentAgentOffset = 1;
            Assert.AreEqual(42, bb.GetInt(1)); // agentRoles base + offset = raw 2

            bb.SetInt(1, 99);
            Assert.AreEqual(99, raw.GetInt(2)); // write also applies offset

            bb.currentAgentOffset = 0;
        }

        [Test]
        public void SetObject_RefSlot_Roundtrips()
        {
            var targetGo = new GameObject("target");
            try
            {
                bb.SetObject(5, targetGo.transform);
                Assert.AreEqual(targetGo.transform, bb.GetObject<Transform>(5));
                // generic + boxed paths see the same value
                Assert.AreEqual(targetGo.transform, bb.Get<Transform>(5));
                Assert.AreEqual(targetGo.transform, bb.GetBoxed(5));
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void GenericAccess_StillWorks_AlongsideTyped()
        {
            bb.SetFloat(0, 12.5f);
            Assert.AreEqual(12.5f, bb.Get<float>(0));

            bb.Set(0, 33f);
            Assert.AreEqual(33f, bb.GetFloat(0));
        }
    }
}
```

- [ ] **Step 2: Run tests — verify FAIL** (typed methods don't exist on `BlackBoard`).

- [ ] **Step 3: Extend `IBlackBoardAccess`**

In [IBlackBoardAccess.cs](file:///d:/Dev/TreeCommanderTest/Assets/BehaviourTree/Core/Blackboard/IBlackBoardAccess.cs), change the interface declaration to inherit the typed contract:

```csharp
    public interface IBlackBoardAccess : IBlackboardTypedAccess
    {
        T Get<T>(int slot);
        void Set<T>(int slot, T value);
        object GetBoxed(int slot);
        void SetBoxed(int slot, object value);

        object GetBoxedRaw(int slot);
        void SetBoxedRaw(int slot, object value);
    }
```

(`BlackBoard` is the only implementer in the codebase — verify with a search for `IBlackBoardAccess` before proceeding.)

- [ ] **Step 4: Swap storage and add typed methods to `BlackBoard`**

In [BlackBoard.cs](file:///d:/Dev/TreeCommanderTest/Assets/BehaviourTree/Core/Blackboard/BlackBoard.cs):

**4a.** Add a cached typed view next to the `storage` field:

```csharp
        private BlackboardDefinition definition;
        private IBlackboardStorage storage;
        private IBlackboardTypedAccess typed; // same instance as storage, typed view
```

**4b.** In `Initialize`, replace `storage = new ManagedBlackboardStorage();` with:

```csharp
            if (storage == null)
            {
                storage = new TypedBlackboardStorage();
                typed = (IBlackboardTypedAccess)storage;
            }
```

**4c.** In `ClearSerializedReferences`, add `typed = null;` next to `storage = null;`.

**4d.** Add the typed accessors (offset semantics mirror the existing generic `Get<T>`/`Set<T>`; null-guard pattern copied from those methods):

```csharp
        /// <summary>Typed hot-path accessors — apply currentAgentOffset, no boxing.</summary>
        public float GetFloat(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetFloat(slot + currentAgentOffset);
        }

        public void SetFloat(int slot, float value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetFloat(slot + currentAgentOffset, value);
        }

        public int GetInt(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetInt(slot + currentAgentOffset);
        }

        public void SetInt(int slot, int value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetInt(slot + currentAgentOffset, value);
        }

        public bool GetBool(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetBool(slot + currentAgentOffset);
        }

        public void SetBool(int slot, bool value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetBool(slot + currentAgentOffset, value);
        }

        public Vector2 GetVector2(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetVector2(slot + currentAgentOffset);
        }

        public void SetVector2(int slot, Vector2 value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetVector2(slot + currentAgentOffset, value);
        }

        public Vector3 GetVector3(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetVector3(slot + currentAgentOffset);
        }

        public void SetVector3(int slot, Vector3 value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetVector3(slot + currentAgentOffset, value);
        }

        public Vector4 GetVector4(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetVector4(slot + currentAgentOffset);
        }

        public void SetVector4(int slot, Vector4 value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetVector4(slot + currentAgentOffset, value);
        }

        public Color GetColor(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetColor(slot + currentAgentOffset);
        }

        public void SetColor(int slot, Color value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetColor(slot + currentAgentOffset, value);
        }

        public Quaternion GetQuaternion(int slot)
        {
            if (typed == null) { EditorWarnStorageNull(); return default; }
            return typed.GetQuaternion(slot + currentAgentOffset);
        }

        public void SetQuaternion(int slot, Quaternion value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }
            typed.SetQuaternion(slot + currentAgentOffset, value);
        }

        public T GetObject<T>(int slot) where T : class
        {
            if (typed == null) { EditorWarnStorageNull(); return null; }
            return typed.GetObject<T>(slot + currentAgentOffset);
        }

        public void SetObject(int slot, object value)
        {
            if (typed == null) { EditorWarnStorageNull(); return; }

            typed.SetObject(slot + currentAgentOffset, value);

            // Keep serialized reference in sync, same rule as SetBoxed.
            if (currentAgentOffset == 0 && definition != null && slot >= 0 && slot < serializedReferences.Count
                && storage.GetSlotKind(slot) == BlackboardSlotKind.Reference)
            {
                serializedReferences[slot] = value as UnityEngine.Object;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void EditorWarnStorageNull()
        {
            Debug.LogWarning("[Blackboard] Storage is NULL");
        }
```

- [ ] **Step 5: Run ALL EditMode tests — verify PASS.**

- [ ] **Step 6: Manual playmode smoke** — open a simple agent tree scene (e.g. `BasicExampleDirectV1` demo), enter playmode, confirm: tree runs without warnings, BlackBoard inspector values update.

- [ ] **Step 7: Commit**

```powershell
git add Assets/BehaviourTree/Core/Blackboard Assets/BehaviourTree/Tests
git commit -m "feat: swap BlackBoard to TypedBlackboardStorage, expose typed accessors with agent offset"
```

**Manual checkpoint:** user plays a demo scene and confirms normal behavior + inspector display. This is the point of no return for the storage swap (legacy class still exists but is now unused).

---

## Phase 5 — `FieldBinding` uses typed accessors

Compiled delegates currently call generic `Get<T>`/`Set<T>` (boxing in the generic dispatch). After this phase, per-node BB reads/writes for the 9 supported types + int-backed enums are allocation-free.

### Task 5.1: `TypedAccessorMap` + `CompileAccessors` rewrite

**Files:**
- Create: `Assets/BehaviourTree/Core/Blackboard/TypedAccessorMap.cs`
- Modify: `Assets/BehaviourTree/Runtime/Bindings/FieldBinding.cs`
- Test: `Assets/BehaviourTree/Tests/EditMode/FieldBindingTypedAccessTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class FieldBindingTypedAccessTests
    {
        private enum TestState { Idle = 0, Running = 1 }

        private class StubMethod : ActionMethod
        {
            public float speed;
            public TestState state;
            public string label; // object-array path
            public override NodeState Execute(Runtime.TickContext ctx) => NodeState.SUCCESS;
        }

        private GameObject go;
        private BlackBoard bb;
        private BlackboardDefinition def;

        [SetUp]
        public void SetUp()
        {
            def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<float>("speed", 1, 0f));      // slot 0
            def.sharedVariables.Add(new BlackboardVariable<TestState>("state", 1, TestState.Idle)); // slot 1
            def.sharedVariables.Add(new BlackboardVariable<string>("label", 1, ""));      // slot 2

            go = new GameObject("fb-test");
            bb = go.AddComponent<BlackBoard>();
            bb.Initialize(def);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(def);
        }

        private static FieldBinding MakeBinding(string fieldName, int slot)
        {
            return new FieldBinding
            {
                fieldInfo = typeof(StubMethod).GetField(fieldName),
                bbSlotIndex = slot,
                isOutput = true
            };
        }

        [Test]
        public void FloatField_ReadWrite_NoBoxingPath()
        {
            var method = new StubMethod();
            FieldBinding binding = MakeBinding(nameof(StubMethod.speed), 0);
            binding.CompileAccessors(typeof(StubMethod));
            Assert.IsTrue(binding.IsCompiled);

            bb.SetFloat(0, 3.5f);
            binding.ReadFromBBGeneric(method, bb);
            Assert.AreEqual(3.5f, method.speed);

            method.speed = 9f;
            binding.WriteToBBGeneric(method, bb);
            Assert.AreEqual(9f, bb.GetFloat(0));
        }

        [Test]
        public void EnumField_ReadWrite_ViaIntArray()
        {
            var method = new StubMethod();
            FieldBinding binding = MakeBinding(nameof(StubMethod.state), 1);
            binding.CompileAccessors(typeof(StubMethod));

            bb.Set(1, TestState.Running);
            binding.ReadFromBBGeneric(method, bb);
            Assert.AreEqual(TestState.Running, method.state);

            method.state = TestState.Idle;
            binding.WriteToBBGeneric(method, bb);

            // Written as int into the int array, but boxed reads still return the enum.
            Assert.AreEqual(0, ((IBlackboardTypedAccess)bb.Storage).GetInt(1));
            Assert.IsInstanceOf<TestState>(bb.Storage.GetBoxed(1));
            Assert.AreEqual(TestState.Idle, bb.Get<TestState>(1));
        }

        [Test]
        public void RefField_ReadWrite_ViaObjectArray()
        {
            var method = new StubMethod();
            FieldBinding binding = MakeBinding(nameof(StubMethod.label), 2);
            binding.CompileAccessors(typeof(StubMethod));

            bb.SetObject(2, "hello");
            binding.ReadFromBBGeneric(method, bb);
            Assert.AreEqual("hello", method.label);

            method.label = "world";
            binding.WriteToBBGeneric(method, bb);
            Assert.AreEqual("world", bb.GetObject<string>(2));
        }
    }
}
```

- [ ] **Step 2: Run tests — verify current behavior** (these should actually PASS already via the generic path — they pin behavior; the *implementation* changes underneath in the next steps).

- [ ] **Step 3: Create `TypedAccessorMap`** (shared by `FieldBinding` and the tracked-binding compiler — both live in the Runtime assembly, which references Core)

```csharp
using System;
using System.Reflection;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Maps a field/member Type to the typed IBlackboardTypedAccess accessor
    /// for that type. Used when compiling read/write delegates so they hit
    /// allocation-free typed accessors instead of generic Get&lt;T&gt;/Set&lt;T&gt;.
    ///
    /// MUST agree with BlackboardStorageLayout.Classify: a type maps to a typed
    /// accessor here iff Classify puts it in the matching typed array.
    /// </summary>
    public static class TypedAccessorMap
    {
        public static bool IsIntBackedEnum(Type t) =>
            t != null && t.IsEnum && Enum.GetUnderlyingType(t) == typeof(int);

        public static MethodInfo GetGetter(Type t)
        {
            if (t == typeof(float)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetFloat));
            if (t == typeof(bool)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetBool));
            if (t == typeof(int) || IsIntBackedEnum(t)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetInt));
            if (t == typeof(Vector2)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetVector2));
            if (t == typeof(Vector3)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetVector3));
            if (t == typeof(Vector4)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetVector4));
            if (t == typeof(Color)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetColor));
            if (t == typeof(Quaternion)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetQuaternion));
            return null;
        }

        public static MethodInfo GetSetter(Type t)
        {
            if (t == typeof(float)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetFloat));
            if (t == typeof(bool)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetBool));
            if (t == typeof(int) || IsIntBackedEnum(t)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetInt));
            if (t == typeof(Vector2)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetVector2));
            if (t == typeof(Vector3)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetVector3));
            if (t == typeof(Vector4)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetVector4));
            if (t == typeof(Color)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetColor));
            if (t == typeof(Quaternion)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetQuaternion));
            return null;
        }
    }
}
```

- [ ] **Step 4: Rewrite `CompileAccessors` in [FieldBinding.cs](file:///d:/Dev/TreeCommanderTest/Assets/BehaviourTree/Runtime/Bindings/FieldBinding.cs)**

Replace the body of `CompileAccessors` and add the two helpers:

```csharp
        public void CompileAccessors(Type declaringType)
        {
            if (bbSlotIndex < 0 || fieldInfo == null || skipAutoResolve) return;
            Type fieldType = fieldInfo.FieldType;
            if (fieldType == null) return;

            try
            {
                ParameterExpression instParam = Expression.Parameter(typeof(NodeMethod), "inst");
                ParameterExpression bbParam = Expression.Parameter(typeof(IBlackBoardAccess), "bb");
                UnaryExpression castInst = Expression.Convert(instParam, declaringType);
                MemberExpression fieldExpr = Expression.Field(castInst, fieldInfo);
                ConstantExpression slotConst = Expression.Constant(bbSlotIndex);

                Expression readValue = BuildReadExpression(bbParam, slotConst, fieldType);
                readDelegate = Expression.Lambda<Action<NodeMethod, IBlackBoardAccess>>(
                    Expression.Assign(fieldExpr, readValue), instParam, bbParam).Compile();

                if (isOutput)
                {
                    MethodCallExpression writeCall = BuildWriteCall(bbParam, slotConst, fieldExpr, fieldType);
                    writeDelegate = Expression.Lambda<Action<NodeMethod, IBlackBoardAccess>>(
                        writeCall, instParam, bbParam).Compile();
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Builds the BB-read expression for a field: typed accessor when the field
        /// type maps to one (allocation-free), generic Get&lt;T&gt; otherwise.
        /// Enums read as int and are cast back to the enum type.
        /// </summary>
        private static Expression BuildReadExpression(ParameterExpression bbParam, ConstantExpression slotConst, Type fieldType)
        {
            MethodInfo typedGetter = TypedAccessorMap.GetGetter(fieldType);
            if (typedGetter != null)
            {
                Expression call = Expression.Call(bbParam, typedGetter, slotConst);
                return fieldType.IsEnum ? Expression.Convert(call, fieldType) : call;
            }

            MethodInfo genericGet = typeof(IBlackBoardAccess).GetMethod("Get").MakeGenericMethod(fieldType);
            return Expression.Call(bbParam, genericGet, slotConst);
        }

        private static MethodCallExpression BuildWriteCall(ParameterExpression bbParam, ConstantExpression slotConst, MemberExpression fieldExpr, Type fieldType)
        {
            MethodInfo typedSetter = TypedAccessorMap.GetSetter(fieldType);
            if (typedSetter != null)
            {
                Expression valueExpr = fieldType.IsEnum
                    ? Expression.Convert(fieldExpr, typeof(int))
                    : (Expression)fieldExpr;
                return Expression.Call(bbParam, typedSetter, slotConst, valueExpr);
            }

            MethodInfo genericSet = typeof(IBlackBoardAccess).GetMethod("Set").MakeGenericMethod(fieldType);
            return Expression.Call(bbParam, genericSet, slotConst, fieldExpr);
        }
```

- [ ] **Step 5: Run ALL EditMode tests — verify PASS.**

- [ ] **Step 6: Manual playmode smoke** — play a demo tree using float/enum/Vector3 variables; confirm behavior unchanged.

- [ ] **Step 7: Commit**

```powershell
git add Assets/BehaviourTree/Core/Blackboard/TypedAccessorMap.cs Assets/BehaviourTree/Runtime/Bindings/FieldBinding.cs Assets/BehaviourTree/Tests
git commit -m "perf: FieldBinding compiled delegates use typed accessors (no boxing for supported types)"
```

**Manual checkpoint:** user confirms playmode behavior. Optionally deep-profile one frame: `GetBoxed` calls from `FieldBinding` should be gone for supported types.

---

## Phase 6 — `TrackedBinding` typed push

Per-frame tracked-binding pushes currently box every value (`Func<object>` + `SetBoxed`). After this phase, supported member types push through typed accessors.

### Task 6.1: Typed push delegate

**Files:**
- Modify: `Assets/BehaviourTree/Runtime/Bindings/TrackedBinding.cs`
- Modify: `Assets/BehaviourTree/Runtime/Execution/Runners/BehaviourTreeRunnerBase.cs`
- Test: `Assets/BehaviourTree/Tests/EditMode/TrackedBindingPushTests.cs`

- [ ] **Step 1: Write the failing tests**

`CompileTrackedBindingDelegate` is currently `private static` — the test requires changing it to `internal static` (the test assembly has internals access via the existing `InternalsVisibleTo`). Do that change first, then write:

```csharp
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class TrackedBindingPushTests
    {
        private class StubComp : MonoBehaviour
        {
            public float health;
            public int count;
        }

        private GameObject compGo;
        private GameObject bbGo;
        private BlackBoard bb;
        private BlackboardDefinition def;

        [SetUp]
        public void SetUp()
        {
            compGo = new GameObject("comp");
            def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<float>("health", 1, 0f)); // slot 0
            def.sharedVariables.Add(new BlackboardVariable<int>("count", 1, 0));      // slot 1
            bbGo = new GameObject("bb");
            bb = bbGo.AddComponent<BlackBoard>();
            bb.Initialize(def);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(compGo);
            Object.DestroyImmediate(bbGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void FloatMember_PushesThroughTypedAccessor()
        {
            StubComp comp = compGo.AddComponent<StubComp>();
            comp.health = 88f;

            var binding = new TrackedBinding
            {
                targetComponent = comp,
                memberName = nameof(StubComp.health),
                isProperty = false
            };
            binding.cachedFieldInfo = typeof(StubComp).GetField(nameof(StubComp.health));

            BehaviourTreeRunnerBase.CompileTrackedBindingDelegate(binding);

            Assert.IsNotNull(binding.typedPushDelegate);
            binding.typedPushDelegate(bb, 0);
            Assert.AreEqual(88f, bb.GetFloat(0));
        }

        [Test]
        public void IntMember_PushesThroughTypedAccessor()
        {
            StubComp comp = compGo.AddComponent<StubComp>();
            comp.count = 12;

            var binding = new TrackedBinding
            {
                targetComponent = comp,
                memberName = nameof(StubComp.count),
                isProperty = false
            };
            binding.cachedFieldInfo = typeof(StubComp).GetField(nameof(StubComp.count));

            BehaviourTreeRunnerBase.CompileTrackedBindingDelegate(binding);

            Assert.IsNotNull(binding.typedPushDelegate);
            binding.typedPushDelegate(bb, 1);
            Assert.AreEqual(12, bb.GetInt(1));
        }
    }
}
```

- [ ] **Step 2: Run tests — verify FAIL** (`typedPushDelegate` doesn't exist; method not accessible).

- [ ] **Step 3: Add the delegate field to [TrackedBinding.cs](file:///d:/Dev/TreeCommanderTest/Assets/BehaviourTree/Runtime/Bindings/TrackedBinding.cs)**

Replace the `readDelegate` field comment block and field with:

```csharp
        // TEMPORARY (until codegen): compiled delegates avoid per-frame reflection.
        // typedPushDelegate is the allocation-free path for member types with a
        // typed storage accessor (float/int/bool/vectors/Color/Quaternion/enums).
        // readDelegate (boxed) remains the fallback for all other member types.
        [NonSerialized] public Func<object> readDelegate;
        [NonSerialized] public Action<IBlackBoardAccess, int> typedPushDelegate;
```

- [ ] **Step 4: Extend `CompileTrackedBindingDelegate` in [BehaviourTreeRunnerBase.cs](file:///d:/Dev/TreeCommanderTest/Assets/BehaviourTree/Runtime/Execution/Runners/BehaviourTreeRunnerBase.cs)**

Change `private static void CompileTrackedBindingDelegate` to `internal static void CompileTrackedBindingDelegate`, and insert the typed branch right after `memberAccess` is built (before the existing `castResult` fallback):

```csharp
                // Typed path: member type has a storage accessor → push without boxing.
                Type memberType = binding.isProperty
                    ? binding.cachedPropertyInfo.PropertyType
                    : binding.cachedFieldInfo.FieldType;

                MethodInfo typedSetter = TypedAccessorMap.GetSetter(memberType);
                if (typedSetter != null)
                {
                    ParameterExpression bbParam = Expression.Parameter(typeof(IBlackBoardAccess), "bb");
                    ParameterExpression slotParam = Expression.Parameter(typeof(int), "slot");

                    Expression valueExpr = memberType.IsEnum
                        ? Expression.Convert(memberAccess, typeof(int))
                        : (Expression)memberAccess;

                    var lambda = Expression.Lambda<Action<Component, IBlackBoardAccess, int>>(
                        Expression.Call(bbParam, typedSetter, slotParam, valueExpr),
                        compParam, bbParam, slotParam);

                    Action<Component, IBlackBoardAccess, int> compiled = lambda.Compile();
                    Component captured = comp;
                    binding.typedPushDelegate = (bb, slot) => compiled(captured, bb, slot);
                    return;
                }

                // Boxed fallback (unchanged) for all other member types.
```

(The existing `castResult` / `Func<object>` code below stays as-is, minus its now-duplicated `Component captured = comp;` — keep one `captured` local; adjust names so it compiles.)

- [ ] **Step 5: Update `PushTrackedBindings`**

Replace the loop body's read/push with:

```csharp
                if (binding.typedPushDelegate != null)
                {
                    binding.typedPushDelegate(blackBoard, binding.variableIndex);
                    continue;
                }

                object value = binding.readDelegate?.Invoke();
                blackBoard.SetBoxed(binding.variableIndex, value);
```

- [ ] **Step 6: Run ALL EditMode tests — verify PASS.**

- [ ] **Step 7: Manual playmode smoke** — play a scene with tracked bindings (e.g. enemy scenes using `EnemyManager`), confirm bound component values still appear on the blackboard each frame.

- [ ] **Step 8: Commit**

```powershell
git add Assets/BehaviourTree/Runtime/Bindings/TrackedBinding.cs Assets/BehaviourTree/Runtime/Execution/Runners/BehaviourTreeRunnerBase.cs Assets/BehaviourTree/Tests
git commit -m "perf: tracked bindings push through typed accessors for supported member types"
```

**Manual checkpoint:** user verifies tracked bindings in playmode.

## Phase 7 — Squad copy fast path (`CopySlotsFrom`)

Squad sync and agent compaction currently copy slot-by-slot through `GetBoxedRaw`/`SetBoxedRaw` — one box per element per frame. With typed arrays these become `Array.Copy` region copies.

### Task 7.1: `CopySlotsFrom` on storage + call-site rewiring

**Files:**
- Modify: `Assets/BehaviourTree/Core/Blackboard/IBlackboardStorage.cs`
- Modify: `Assets/BehaviourTree/Core/Blackboard/TypedBlackboardStorage.cs`
- Modify: `Assets/BehaviourTree/Core/Blackboard/ManagedBlackboardStorage.cs` (boxed fallback impl, until Phase 8 deletes the class)
- Modify: `Assets/BehaviourTree/Core/Blackboard/BlackBoard.cs` (pass-through)
- Modify: `Assets/BehaviourTree/Runtime/Squad/SquadInstance.cs`
- Modify: `Assets/BehaviourTree/Runtime/Execution/Runners/CommanderTreeRunner.cs`
- Test: `Assets/BehaviourTree/Tests/EditMode/CopySlotsFromTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class CopySlotsFromTests
    {
        private enum TestState { Idle = 0, Running = 1 }

        private static List<BlackboardVariableBase> BuildVariables()
        {
            return new List<BlackboardVariableBase>
            {
                new BlackboardVariable<float>("f", 2, 0f),                 // slots 0-1
                new BlackboardVariable<int>("roles", 4, 0),                // slots 2-5
                new BlackboardVariable<TestState>("state", 1, TestState.Idle), // slot 6
                new BlackboardVariable<string>("label", 1, ""),            // slot 7
            };
        }

        private static TypedBlackboardStorage CreateStorage()
        {
            var s = new TypedBlackboardStorage();
            s.Initialize(BuildVariables());
            return s;
        }

        [Test]
        public void RegionCopy_CopiesAllValuesAcrossArrays()
        {
            TypedBlackboardStorage src = CreateStorage();
            TypedBlackboardStorage dst = CreateStorage();

            src.Set(0, 1.5f);
            src.Set(1, 2.5f);
            src.Set(2, 10);
            src.Set(5, 40);
            src.Set(6, TestState.Running);
            src.Set(7, "copied");

            dst.CopySlotsFrom(src, 0, 0, 8);

            Assert.AreEqual(1.5f, dst.Get<float>(0));
            Assert.AreEqual(2.5f, dst.Get<float>(1));
            Assert.AreEqual(10, dst.Get<int>(2));
            Assert.AreEqual(40, dst.Get<int>(5));
            Assert.AreEqual(TestState.Running, dst.Get<TestState>(6));
            Assert.AreEqual("copied", dst.Get<string>(7));
        }

        [Test]
        public void SelfCopy_OverlappingRange_CompactsLikeMemmove()
        {
            // Agent-compaction pattern: shift elements down by one within a stride region.
            TypedBlackboardStorage s = CreateStorage();
            s.Set(2, 1);
            s.Set(3, 2);
            s.Set(4, 3);
            s.Set(5, 4);

            // Remove element at local index 0 (slot 2): copy [3..5] down to [2..4].
            s.CopySlotsFrom(s, 3, 2, 3);

            Assert.AreEqual(2, s.Get<int>(2));
            Assert.AreEqual(3, s.Get<int>(3));
            Assert.AreEqual(4, s.Get<int>(4));
        }

        [Test]
        public void DifferentLayoutOrder_CopiesToCorrectTypedArray()
        {
            // Different layout: same variables in different order, so the same
            // value type sits at different virtual slots. Run-detection must
            // land values in the right typed array regardless of slot numbering.
            TypedBlackboardStorage src = CreateStorage();

            var dstVars = new List<BlackboardVariableBase>
            {
                new BlackboardVariable<string>("label", 1, ""),            // slot 0
                new BlackboardVariable<float>("f", 2, 0f),                 // slots 1-2
                new BlackboardVariable<int>("roles", 4, 0),                // slots 3-6
                new BlackboardVariable<TestState>("state", 1, TestState.Idle), // slot 7
            };
            var dst = new TypedBlackboardStorage();
            dst.Initialize(dstVars);

            src.Set(0, 7.5f);          // src float slot 0
            dst.CopySlotsFrom(src, 0, 1, 1); // dst float slot is 1

            Assert.AreEqual(7.5f, dst.Get<float>(1));
        }
    }
}
```

- [ ] **Step 2: Run tests — verify FAIL** (`CopySlotsFrom` doesn't exist).

- [ ] **Step 3: Add to `IBlackboardStorage`**

```csharp
        /// <summary>
        /// Copies a range of virtual slots from another storage into this one.
        /// Implementations use fast typed region copies when both sides share a
        /// layout; otherwise falls back to per-slot boxed copies.
        /// Overlapping ranges within the same storage are supported (memmove semantics).
        /// </summary>
        void CopySlotsFrom(IBlackboardStorage source, int sourceSlot, int destSlot, int count);
```

- [ ] **Step 4: Implement on `TypedBlackboardStorage`**

```csharp
        public void CopySlotsFrom(IBlackboardStorage source, int sourceSlot, int destSlot, int count)
        {
            if (source is TypedBlackboardStorage src && src.map != null && map != null)
            {
                int remaining = count;
                int s = sourceSlot;
                int d = destSlot;

                while (remaining > 0)
                {
                    SlotLocation srcLoc = src.map[s];
                    SlotLocation dstLoc = map[d];

                    if (srcLoc.ArrayId == dstLoc.ArrayId)
                    {
                        // Extend the run while both sides stay in the same typed
                        // array with contiguous local indices (same layout).
                        int run = 1;
                        while (run < remaining
                            && src.map[s + run].ArrayId == srcLoc.ArrayId
                            && map[d + run].ArrayId == srcLoc.ArrayId
                            && src.map[s + run].LocalIndex == srcLoc.LocalIndex + run
                            && map[d + run].LocalIndex == dstLoc.LocalIndex + run)
                        {
                            run++;
                        }

                        CopyRun(src, srcLoc, dstLoc, run);
                        s += run;
                        d += run;
                        remaining -= run;
                    }
                    else
                    {
                        // Layout mismatch for this slot — boxed fallback.
                        WriteBoxedUnchecked(d, src.GetBoxed(s));
                        s++;
                        d++;
                        remaining--;
                    }
                }
                return;
            }

            // Non-typed source (shouldn't happen in practice) — boxed fallback.
            for (int i = 0; i < count; i++)
                SetBoxed(destSlot + i, source.GetBoxed(sourceSlot + i));
        }

        private void CopyRun(TypedBlackboardStorage src, SlotLocation srcLoc, SlotLocation dstLoc, int length)
        {
            // Array.Copy handles overlapping ranges within the same array (memmove).
            switch (srcLoc.ArrayId)
            {
                case BlackboardArrayId.Float: System.Array.Copy(src.floats, srcLoc.LocalIndex, floats, dstLoc.LocalIndex, length); break;
                case BlackboardArrayId.Int: System.Array.Copy(src.ints, srcLoc.LocalIndex, ints, dstLoc.LocalIndex, length); break;
                case BlackboardArrayId.Bool: System.Array.Copy(src.bools, srcLoc.LocalIndex, bools, dstLoc.LocalIndex, length); break;
                case BlackboardArrayId.Vector2: System.Array.Copy(src.vec2s, srcLoc.LocalIndex, vec2s, dstLoc.LocalIndex, length); break;
                case BlackboardArrayId.Vector3: System.Array.Copy(src.vec3s, srcLoc.LocalIndex, vec3s, dstLoc.LocalIndex, length); break;
                case BlackboardArrayId.Vector4: System.Array.Copy(src.vec4s, srcLoc.LocalIndex, vec4s, dstLoc.LocalIndex, length); break;
                case BlackboardArrayId.Color: System.Array.Copy(src.colors, srcLoc.LocalIndex, colors, dstLoc.LocalIndex, length); break;
                case BlackboardArrayId.Quaternion: System.Array.Copy(src.quats, srcLoc.LocalIndex, quats, dstLoc.LocalIndex, length); break;
                default: System.Array.Copy(src.objects, srcLoc.LocalIndex, objects, dstLoc.LocalIndex, length); break;
            }
        }
```

Note: `CopyRun` accesses `src`'s private arrays — legal C# (same class). Overlapping self-copy with `destSlot < sourceSlot` is memmove-safe in `Array.Copy`; the test covers the only overlap pattern used today (compaction shifts down). If a future caller shifts **up** within the same array, verify `Array.Copy` overlap semantics for that direction too.

- [ ] **Step 5: Implement boxed fallback on `ManagedBlackboardStorage`** (keeps the interface satisfiable until Phase 8)

```csharp
        public void CopySlotsFrom(IBlackboardStorage source, int sourceSlot, int destSlot, int count)
        {
            // Buffer to keep memmove semantics for overlapping self-copies.
            object[] buffer = new object[count];
            for (int i = 0; i < count; i++)
                buffer[i] = source.GetBoxed(sourceSlot + i);
            for (int i = 0; i < count; i++)
                SetBoxed(destSlot + i, buffer[i]);
        }
```

- [ ] **Step 6: Add the `BlackBoard` pass-through**

In [BlackBoard.cs](file:///d:/Dev/TreeCommanderTest/Assets/BehaviourTree/Core/Blackboard/BlackBoard.cs), next to `SetBoxedRaw`:

```csharp
        /// <summary>Raw slot-range copy (no agent offset). Used by squad sync and agent compaction.</summary>
        public void CopySlotsRawFrom(BlackBoard source, int sourceSlot, int destSlot, int count)
        {
            if (storage == null || source == null || source.storage == null || count <= 0) return;
            storage.CopySlotsFrom(source.storage, sourceSlot, destSlot, count);
        }
```

- [ ] **Step 7: Rewire the call sites**

**7a.** In [SquadInstance.cs](file:///d:/Dev/TreeCommanderTest/Assets/BehaviourTree/Runtime/Squad/SquadInstance.cs) `CopyToBB`, replace the `stride > 1` branch body (the `for (int j...)` boxed loop) with:

```csharp
                else if (stride > 1)
                {
                    // Commander sync: copy all stride slots (both sides have stride > 1)
                    treeBB.CopySlotsRawFrom(blackBoard, srcSlot, dstSlot, stride);
                }
```

**7b.** In `CopyFromBB`, replace the `stride > 1` branch body with:

```csharp
                else if (stride > 1)
                {
                    // Commander sync: copy all stride slots (both sides have stride > 1)
                    blackBoard.CopySlotsRawFrom(treeBB, srcSlot, dstSlot, stride);
                }
```

(The single-element branches stay boxed — one element doesn't benefit from region copy. Optional later: route them through typed accessors too.)

**7c.** In [CommanderTreeRunner.cs](file:///d:/Dev/TreeCommanderTest/Assets/BehaviourTree/Runtime/Execution/Runners/CommanderTreeRunner.cs) agent compaction, replace the shift loop (`for (int slot = removedIndex; ...)` with `GetBoxedRaw`/`SetBoxedRaw`) with:

```csharp
                            squad.BlackBoard.CopySlotsRawFrom(squad.BlackBoard,
                                currentSlot + removedIndex + 1, currentSlot + removedIndex,
                                activeCount - 1 - removedIndex);
```

- [ ] **Step 8: Run ALL EditMode tests — verify PASS.**

- [ ] **Step 9: Manual playmode smoke** — play a commander/squad scene (e.g. `FormationScene` / `CommanderDemo`): agents receive squad data, formation positions update, agent register/unregister works without errors.

- [ ] **Step 10: Commit**

```powershell
git add Assets/BehaviourTree/Core/Blackboard Assets/BehaviourTree/Runtime Assets/BehaviourTree/Tests
git commit -m "perf: squad sync and agent compaction use typed region copies instead of boxed loops"
```

**Manual checkpoint:** user verifies the squad scene end-to-end. This was the largest per-frame allocation source.

---

## Phase 7.5 — Slot version counters + offset-aware `CopySlot`

Node-layer primitives consumed by the param-unification plan's Phase 7.5: `HasChanged` needs change *detection* without reading a value (version counters), `SetVariable` needs a slot→slot *move* without materializing the value (`CopySlot`). Both are small additions that keep dynamic nodes allocation-free.

**Design note:** version counters must be bumped on **every** write path (typed setters, generic `Set<T>`, `WriteBoxedUnchecked`, `CopySlotsFrom` destinations) — exhaustiveness is the risk; the tests pin each path.

### Task 7.5.1: Per-slot version counters

**Files:**
- Modify: `Assets/BehaviourTree/Core/Blackboard/IBlackboardStorage.cs`
- Modify: `Assets/BehaviourTree/Core/Blackboard/TypedBlackboardStorage.cs`
- Modify: `Assets/BehaviourTree/Core/Blackboard/ManagedBlackboardStorage.cs` (stub, until Phase 8 deletes it)
- Modify: `Assets/BehaviourTree/Core/Blackboard/BlackBoard.cs` (offset-aware pass-through)
- Test: `Assets/BehaviourTree/Tests/EditMode/SlotVersionTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using BehaviourTree.Core;
using NUnit.Framework;

namespace BehaviourTree.Tests
{
    public class SlotVersionTests
    {
        private static TypedBlackboardStorage CreateStorage()
        {
            var s = new TypedBlackboardStorage();
            s.Initialize(new List<BlackboardVariableBase>
            {
                new BlackboardVariable<float>("f", 1, 1f),   // slot 0
                new BlackboardVariable<int>("i", 2, 0),      // slots 1-2
            });
            return s;
        }

        [Test]
        public void Versions_StartAtZero_AfterInitializeAndSeeding()
        {
            TypedBlackboardStorage s = CreateStorage();
            Assert.AreEqual(0, s.GetSlotVersion(0));
            Assert.AreEqual(0, s.GetSlotVersion(1));
        }

        [Test]
        public void TypedSetter_BumpsOnlyThatSlot()
        {
            TypedBlackboardStorage s = CreateStorage();
            ((IBlackboardTypedAccess)s).SetFloat(0, 2f);
            Assert.AreEqual(1, s.GetSlotVersion(0));
            Assert.AreEqual(0, s.GetSlotVersion(1));
        }

        [Test]
        public void GenericSet_AndBoxedSet_Bump()
        {
            TypedBlackboardStorage s = CreateStorage();
            s.Set(0, 3f);
            s.SetBoxed(1, 7);
            Assert.AreEqual(1, s.GetSlotVersion(0));
            Assert.AreEqual(1, s.GetSlotVersion(1));
        }

        [Test]
        public void Reads_DoNotBump()
        {
            TypedBlackboardStorage s = CreateStorage();
            _ = s.Get<float>(0);
            _ = s.GetBoxed(1);
            Assert.AreEqual(0, s.GetSlotVersion(0));
            Assert.AreEqual(0, s.GetSlotVersion(1));
        }

        [Test]
        public void CopySlotsFrom_BumpsDestinationRange()
        {
            TypedBlackboardStorage src = CreateStorage();
            TypedBlackboardStorage dst = CreateStorage();
            dst.CopySlotsFrom(src, 1, 1, 2); // copies slots 1-2
            Assert.AreEqual(0, dst.GetSlotVersion(0));
            Assert.AreEqual(1, dst.GetSlotVersion(1));
            Assert.AreEqual(1, dst.GetSlotVersion(2));
        }
    }
}
```

- [ ] **Step 2: Run tests — verify FAIL** (`GetSlotVersion` doesn't exist).

- [ ] **Step 3: Add to `IBlackboardStorage`**

```csharp
        /// <summary>
        /// Monotonic per-slot write counter. Zero after Initialize (seeding does
        /// not count). Bumped on every write to the slot — typed setters, generic
        /// Set, boxed writes, and CopySlotsFrom destinations. Reads never bump.
        /// Used by change-detection nodes (e.g. HasChanged) to avoid value reads.
        /// </summary>
        int GetSlotVersion(int slot);
```

- [ ] **Step 4: Implement on `TypedBlackboardStorage`**

Add field + allocation **after** `SeedFromVariables(...)` in `InitializeFromVariables` (zeroed — seeding must not count):

```csharp
        private int[] versions;

        public int GetSlotVersion(int slot)
        {
            if (versions == null || slot < 0 || slot >= versions.Length) return 0;
            return versions[slot];
        }
```

Bump `versions[index]++` in every write path: at the end of `Set<T>` (after `CanWrite` passes), `WriteBoxedUnchecked` (after the null/out-of-range guard), every typed setter (`SetFloat`/`SetInt`/`SetBool`/`SetVector2/3/4`/`SetColor`/`SetQuaternion`/`SetObject`), and in `CopySlotsFrom` for each destination slot written (both `CopyRun` runs and the boxed fallback path — simplest: bump the destination slot at each write point, or the `d..d+count` region after the copy loop). Also reset `versions = null` in `ClearArrays`.

- [ ] **Step 5: Stub on `ManagedBlackboardStorage`** (legacy fallback until Phase 8)

```csharp
        public int GetSlotVersion(int slot) => 0; // legacy: no change tracking
```

- [ ] **Step 6: Offset-aware pass-through on `BlackBoard`**

```csharp
        /// <summary>Per-slot write version (applies currentAgentOffset). Change-detection without value reads.</summary>
        public int GetSlotVersion(int slot)
        {
            if (storage == null) return 0;
            return storage.GetSlotVersion(slot + currentAgentOffset);
        }
```

- [ ] **Step 7: Run ALL EditMode tests — verify PASS.**

- [ ] **Step 8: Commit**

```powershell
git add Assets/BehaviourTree/Core/Blackboard Assets/BehaviourTree/Tests
git commit -m "feat: per-slot version counters for value-free change detection"
```

### Task 7.5.2: Offset-aware `BlackBoard.CopySlot`

**Files:**
- Modify: `Assets/BehaviourTree/Core/Blackboard/IBlackBoardAccess.cs`
- Modify: `Assets/BehaviourTree/Core/Blackboard/BlackBoard.cs`
- Test: `Assets/BehaviourTree/Tests/EditMode/CopySlotTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class CopySlotTests
    {
        [Test]
        public void CopySlot_SameType_CopiesValue()
        {
            var def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<float>("a", 1, 1.5f)); // slot 0
            def.sharedVariables.Add(new BlackboardVariable<float>("b", 1, 0f));   // slot 1
            var go = new GameObject("bb");
            try
            {
                var bb = go.AddComponent<BlackBoard>();
                bb.Initialize(def);
                bb.CopySlot(0, 1);
                Assert.AreEqual(1.5f, bb.GetFloat(1));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(def); }
        }

        [Test]
        public void CopySlot_MismatchedTypes_ConvertsThroughBoxedFallback()
        {
            var def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<int>("i", 1, 7));   // slot 0
            def.sharedVariables.Add(new BlackboardVariable<float>("f", 1, 0f)); // slot 1
            var go = new GameObject("bb");
            try
            {
                var bb = go.AddComponent<BlackBoard>();
                bb.Initialize(def);
                bb.CopySlot(0, 1); // int slot -> float slot: boxed Convert.ToSingle path
                Assert.AreEqual(7f, bb.GetFloat(1));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(def); }
        }
    }
}
```

- [ ] **Step 2: Run tests — verify FAIL.**

- [ ] **Step 3: Add to `IBlackBoardAccess`**

```csharp
        /// <summary>
        /// Slot-to-slot copy within this blackboard (applies currentAgentOffset
        /// to both slots). Same-type pairs copy inside the typed arrays — the
        /// value never materializes in managed code. Mismatched-type pairs
        /// convert through the boxed fallback.
        /// </summary>
        void CopySlot(int sourceSlot, int destSlot);
```

- [ ] **Step 4: Implement on `BlackBoard`**

```csharp
        public void CopySlot(int sourceSlot, int destSlot)
        {
            if (storage == null) return;
            storage.CopySlotsFrom(storage,
                sourceSlot + currentAgentOffset, destSlot + currentAgentOffset, 1);
        }
```

- [ ] **Step 5: Run ALL EditMode tests — verify PASS.**

- [ ] **Step 6: Commit**

```powershell
git add Assets/BehaviourTree/Core/Blackboard Assets/BehaviourTree/Tests
git commit -m "feat: offset-aware BlackBoard.CopySlot for value-free variable moves"
```

**Manual checkpoint:** none until the param-unification Phase 7.5 consumers (`HasChanged`, `SetVariable`) land — the primitives are test-covered in isolation.

---

## Phase 8 — Typed seeding + delete legacy storage + docs

Cleanup phase: no boxing left even at `Initialize`, legacy class removed, docs aligned.

### Task 8.1: Typed seeding

**Files:**
- Modify: `Assets/BehaviourTree/Core/Blackboard/TypedBlackboardStorage.cs` (`SeedFromVariables`)

- [ ] **Step 1: Replace `SeedFromVariables` with a typed switch**

```csharp
        private void SeedFromVariables(IReadOnlyList<BlackboardVariableBase> variables)
        {
            int slot = 0;
            for (int varIndex = 0; varIndex < variables.Count; varIndex++)
            {
                BlackboardVariableBase variable = variables[varIndex];
                int stride = variable.Stride;
                int actualStride = (stride > 1) ? stride : 1;

                switch (variable)
                {
                    case BlackboardVariable<float> v:
                        for (int e = 0; e < actualStride; e++) floats[map[slot + e].LocalIndex] = v.GetValue(e);
                        break;
                    case BlackboardVariable<int> v:
                        for (int e = 0; e < actualStride; e++) ints[map[slot + e].LocalIndex] = v.GetValue(e);
                        break;
                    case BlackboardVariable<bool> v:
                        for (int e = 0; e < actualStride; e++) bools[map[slot + e].LocalIndex] = v.GetValue(e);
                        break;
                    case BlackboardVariable<Vector2> v:
                        for (int e = 0; e < actualStride; e++) vec2s[map[slot + e].LocalIndex] = v.GetValue(e);
                        break;
                    case BlackboardVariable<Vector3> v:
                        for (int e = 0; e < actualStride; e++) vec3s[map[slot + e].LocalIndex] = v.GetValue(e);
                        break;
                    case BlackboardVariable<Vector4> v:
                        for (int e = 0; e < actualStride; e++) vec4s[map[slot + e].LocalIndex] = v.GetValue(e);
                        break;
                    case BlackboardVariable<Color> v:
                        for (int e = 0; e < actualStride; e++) colors[map[slot + e].LocalIndex] = v.GetValue(e);
                        break;
                    case BlackboardVariable<Quaternion> v:
                        for (int e = 0; e < actualStride; e++) quats[map[slot + e].LocalIndex] = v.GetValue(e);
                        break;
                    default:
                        // Enums (boxed as their enum type → WriteBoxedUnchecked converts
                        // to int), reference types, custom types.
                        for (int e = 0; e < actualStride; e++)
                            WriteBoxedUnchecked(slot + e, variable.GetBoxedValue(e));
                        break;
                }

                slot += actualStride;
            }
        }
```

- [ ] **Step 2: Run ALL EditMode tests — verify PASS** (the `Seed_InitialValuesArePresent` contract test covers this).

- [ ] **Step 3: Commit**

```powershell
git add Assets/BehaviourTree/Core/Blackboard/TypedBlackboardStorage.cs
git commit -m "perf: typed initial-value seeding (no boxing at Initialize)"
```

### Task 8.2: Delete `ManagedBlackboardStorage` + update docs

**Files:**
- Delete: `Assets/BehaviourTree/Core/Blackboard/ManagedBlackboardStorage.cs`
- Modify: `Assets/BehaviourTree/Tests/EditMode/BlackboardStorageContractTests.cs` (remove legacy fixture)
- Modify: `Assets/BehaviourTree/Docs/BlackboardSystemOverview.md` (§1.5)
- Modify: `Assets/BehaviourTree/Docs/Compiled-Delegates-Reasoning.md` (mark "future path" as landed)

- [ ] **Step 1: Verify no remaining references** — search the whole project for `ManagedBlackboardStorage`. Expected hits: the class file itself, the contract test fixture attribute, the two docs. Nothing else. If code references remain, migrate them first.

- [ ] **Step 2: Remove the legacy fixture from the contract suite**

```csharp
    [TestFixture(typeof(TypedBlackboardStorage))]
```

- [ ] **Step 3: Delete `ManagedBlackboardStorage.cs` and run ALL tests — verify PASS.**

- [ ] **Step 4: Update docs**

In `BlackboardSystemOverview.md` §1.5, replace the `ManagedBlackboardStorage` section with a description of `TypedBlackboardStorage`: per-type arrays + `SlotLocation` map + typed accessors, boxed API retained for tooling/dynamic nodes, custom value types and exotic enums boxed in the object array.

In `Compiled-Delegates-Reasoning.md`, mark the "Future path: typed arrays" section as implemented (Step 1 done; Step 2 DOTS pending), and update the "What gets deleted" list: `Expression.Compile` remains only as the editor-iteration bridge until the build-time codegen lands (next plan).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "chore: remove ManagedBlackboardStorage, update blackboard docs for typed storage"
```

**Manual checkpoint:** full project recompile + one playmode smoke of each demo scene type (agent, commander, tracked bindings).

---

## Final verification checklist

- [ ] All EditMode tests green (contract suite, layout, accessors, BlackBoard, FieldBinding, tracked bindings, CopySlotsFrom).
- [ ] Playmode: agent tree scene (`BasicExampleDirectV1`) — values tick correctly.
- [ ] Playmode: commander/squad scene (`FormationScene` / `CommanderDemo`) — squad data syncs, agent register/unregister works.
- [ ] Playmode: tracked bindings scene (enemy/`EnemyManager`) — bound values push each frame.
- [ ] BlackBoard inspector/debug view shows live values while playing (boxed read path intact).
- [ ] Profiler comparison on a representative scene: GC allocs per frame during ticking ≈ 0 from BB reads/writes/tracked pushes/squad copies. Remaining allocs: dynamic-type nodes (`GetBoxed` in `VariableMethods` etc.) — expected, addressed case-by-case or by codegen.

## Risks / notes for the reviewer

- **Enum identity**: enum slots live in the int array. Any code path reading them boxed gets a reconstructed enum via `Enum.ToObject` (allocates once per boxed read — boxed reads are tooling/dynamic only). Covered by contract test `BoxedRoundtrip_EnumComesBackAsEnumNotInt`.
- **`TypedAccessorMap` ↔ `BlackboardStorageLayout.Classify` agreement**: a type has a typed accessor iff it classifies into the matching array. If you add a type to one, add it to the other, plus `Get<T>`/`Set<T>`/`GetBoxed`/`WriteBoxedUnchecked`/`CopyRun`/seeding. The contract suite catches mismatches for covered types.
- **Dynamic squad strides** (`isSquadData`): layout rebuild goes through `Initialize`, same as legacy. `CopySlotsFrom` degrades to boxed per-slot copies if layouts ever mismatch.
- **Generic `Get<T>`/generic path boxing** is intentional (compatibility). If profiling later shows a hot caller still on the generic path, route it through typed accessors.
