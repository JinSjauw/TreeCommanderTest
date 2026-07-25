# Binding Accessor Codegen — Implementation Plan

> Execute task-by-task in order. Each phase is independently testable and committable. Steps use checkbox (`- [ ]`) syntax. Do not skip the manual checkpoint at the end of each phase — the user verifies before moving on.

**Goal:** Replace runtime `Expression.Compile` for blackboard field/tracked bindings with C# accessor code generated in the editor — eliminating the IL2CPP hazards (interpreter-fallback delegates, `MakeGenericMethod` value-type instantiations missing from the AOT image) and the per-session compile cost, while keeping `Expression.Compile` as the editor-iteration fallback.

**Architecture:** The generator emits **one accessor pair per (NodeMethod type, public field)** — `void Read(NodeMethod m, IBlackBoardAccess bb, int slot)` / `Write(...)` — with the slot passed at runtime. Nothing is tree-specific: no asset format change, no bake ordinals, no staleness per tree. Generated accessors call the typed `IBlackboardTypedAccess` API (Phase 3 of the storage refactor) or generic `Get<T>/Set<T>` for object-array types; a static registry (`GeneratedAccessorRegistry`, Runtime asmdef) is populated via `[RuntimeInitializeOnLoadMethod]`. At bind time `FieldBinding.BindAccessors` consults the registry first (with a field-type staleness check) and falls back to `CompileAccessors` with an editor warning. Tracked bindings follow the same pattern via an opt-in `[GenerateBindingAccessors]` attribute on component types.

**Tech Stack:** Unity 6000.3.5f2, C# (netstandard 2.1), Unity Test Framework (EditMode), existing `CliTestRunner` batch bootstrap for test runs.

**Relationship to other plans:**
- Builds on `TypedBlackboardStorage-Plan.md` (all phases landed): generated code targets `IBlackboardTypedAccess`.
- Orthogonal to the param-unification plan's Phase 7 (`ParamReader`/`OnParametersReady`): that plan removes hand-written *parameter decoding*; this plan removes *delegate compilation*. Task 7.5's typed consumers are unaffected.
- Prerequisite reading: `Docs/Compiled-Delegates-Reasoning.md` (why `Expression.Compile` is temporary).

**Key design invariants:**
1. Generated accessors are **slot-agnostic** — slot arrives as a parameter, so one accessor serves every instance of a method type across all trees. No asset/baker changes.
2. **Fallback always works.** If the registry misses (not generated yet, stale field type), behavior is exactly today's: `CompileAccessors` via `Expression.Compile` (editor). A staleness warning tells the user to regenerate.
3. Generated code contains only statically-compiled generic instantiations (`bb.Get<Transform>(slot)` etc.) — the exact thing that is AOT-safe, unlike runtime `MakeGenericMethod`.
4. The emitter decides typed-vs-generic via `TypedAccessorMap` **at generation time**, so emitter/runtime classification can never drift apart.

**Out of scope:** `DeserializeParameters`/`ParamReader` decoding (param-unification plan), `DeserializeFields`' one-time constant `FieldInfo.SetValue` (init-time, not per-tick; may be codegen'd later), DOTS/`NativeArray<T>`, actually deleting `Expression.Compile` (it stays as the editor fallback until the DOTS step).

---

## Running the tests

Same as the storage plan: `CliTestRunner` batch bootstrap (`-runTests` no-ops in this project). Ensure the editor is closed first.

```powershell
& "E:\Unity Versions\6000.3.5f2\Editor\Unity.exe" -batchmode -projectPath "d:\Dev\TreeCommanderTest" -executeMethod BehaviourTree.Tests.CliTestRunner.RunEditModeTests -logFile "d:\Dev\TreeCommanderTest\TestResults\editor.log"
```

The run detaches — poll `TestResults/editmode-summary.txt` for `RESULT total=N passed=N failed=0`. Verify the run's own log line `[CliTestRunner] total=...` when in doubt.

---

## Phase 0 — Shared binding enumeration

The generator must enumerate exactly the fields `MethodRegistry` binds. `CreateBindings` is `internal` — make it public so the editor generator reuses it (no mirrored reflection logic to drift).

### Task 0.1: Expose `MethodRegistry.CreateBindings`

**Files:**
- Modify: `Assets/BehaviourTree/Runtime/MethodRegistry.cs`
- Modify: `Assets/BehaviourTree/Editor/BehaviourTree.Editor.asmdef` (verify only)

- [ ] **Step 1: Change visibility**

In [MethodRegistry.cs](file:///d:/Dev/TreeCommanderTest/Assets/BehaviourTree/Runtime/MethodRegistry.cs#L163):

```csharp
        /// <summary>
        /// Builds the binding descriptors for all public instance fields of a method
        /// type. Used by the registry cache, the runtime deserializer, and the
        /// editor-side accessor generator (must stay the single source of field selection).
        /// </summary>
        public static FieldBinding[] CreateBindings(Type methodType)
```

- [ ] **Step 2: Verify the Editor assembly references Runtime**

Open `Assets/BehaviourTree/Editor/BehaviourTree.Editor.asmdef` and confirm its `references` contains `BehaviourTree.Runtime` (by name or GUID). Editor scripts already use runtime types, so this should already hold — fix if not.

- [ ] **Step 3: Run the full EditMode suite — expect unchanged green (45/45).**

- [ ] **Step 4: Commit**

```powershell
git add Assets/BehaviourTree/Runtime/MethodRegistry.cs Assets/BehaviourTree/Editor/BehaviourTree.Editor.asmdef
git commit -m "chore: make MethodRegistry.CreateBindings public for the accessor generator"
```

**Manual checkpoint:** none — trivial change.

---

## Phase 1 — `GeneratedAccessorRegistry` + `BindAccessors` (runtime path)

The runtime half: registry + generated-first bind logic. Tested with hand-registered accessors before any generator exists.

### Task 1.1: Registry + bind logic

**Files:**
- Create: `Assets/BehaviourTree/Runtime/Bindings/GeneratedAccessorRegistry.cs`
- Modify: `Assets/BehaviourTree/Runtime/Bindings/FieldBinding.cs` (add `BindAccessors`)
- Modify: `Assets/BehaviourTree/Runtime/NodeMethod.cs` (call `BindAccessors` instead of `CompileAccessors`)
- Test: `Assets/BehaviourTree/Tests/EditMode/GeneratedAccessorRegistryTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class GeneratedAccessorRegistryTests
    {
        private class StubMethod : ActionMethod
        {
            public float speed;
            public override NodeState Execute(Runtime.TickContext ctx) => NodeState.SUCCESS;
        }

        [TearDown]
        public void TearDown() => GeneratedAccessorRegistry.Clear();

        [Test]
        public void TryGet_Miss_ReturnsFalse()
        {
            bool found = GeneratedAccessorRegistry.TryGet(
                typeof(StubMethod), typeof(StubMethod).GetField(nameof(StubMethod.speed)),
                out _, out _);
            Assert.IsFalse(found);
        }

        [Test]
        public void Register_And_Hit_ReturnsDelegates()
        {
            GeneratedAccessorRegistry.Register(typeof(StubMethod), nameof(StubMethod.speed), typeof(float),
                (m, bb, slot) => ((StubMethod)m).speed = bb.GetFloat(slot),
                (m, bb, slot) => bb.SetFloat(slot, ((StubMethod)m).speed));

            bool found = GeneratedAccessorRegistry.TryGet(
                typeof(StubMethod), typeof(StubMethod).GetField(nameof(StubMethod.speed)),
                out GeneratedAccessorRegistry.ReadAccessor read,
                out GeneratedAccessorRegistry.WriteAccessor write);

            Assert.IsTrue(found);
            Assert.IsNotNull(read);
            Assert.IsNotNull(write);
        }

        [Test]
        public void StaleFieldType_Misses()
        {
            // Registered as float; field is still float — sanity hit first.
            GeneratedAccessorRegistry.Register(typeof(StubMethod), nameof(StubMethod.speed), typeof(int),
                (m, bb, slot) => { }, (m, bb, slot) => { });

            bool found = GeneratedAccessorRegistry.TryGet(
                typeof(StubMethod), typeof(StubMethod).GetField(nameof(StubMethod.speed)),
                out _, out _);

            Assert.IsFalse(found); // registered type != actual field type → stale
        }

        [Test]
        public void BindAccessors_PrefersGenerated_OverCompiled()
        {
            GeneratedAccessorRegistry.Register(typeof(StubMethod), nameof(StubMethod.speed), typeof(float),
                (m, bb, slot) => ((StubMethod)m).speed = bb.GetFloat(slot) + 1000f, // marker
                (m, bb, slot) => bb.SetFloat(slot, ((StubMethod)m).speed));

            var def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<float>("speed", 1, 3.5f));
            var go = new GameObject("bb");
            try
            {
                var bb = go.AddComponent<BlackBoard>();
                bb.Initialize(def);

                var binding = new FieldBinding
                {
                    fieldInfo = typeof(StubMethod).GetField(nameof(StubMethod.speed)),
                    bbSlotIndex = 0,
                    isOutput = true
                };
                binding.BindAccessors(typeof(StubMethod));

                var method = new StubMethod();
                binding.ReadFromBBGeneric(method, bb);
                Assert.AreEqual(1003.5f, method.speed); // generated accessor ran, not the compiled one
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void BindAccessors_FallsBack_WhenNoGeneratedEntry()
        {
            var def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<float>("speed", 1, 3.5f));
            var go = new GameObject("bb");
            try
            {
                var bb = go.AddComponent<BlackBoard>();
                bb.Initialize(def);

                var binding = new FieldBinding
                {
                    fieldInfo = typeof(StubMethod).GetField(nameof(StubMethod.speed)),
                    bbSlotIndex = 0,
                    isOutput = true
                };
                binding.BindAccessors(typeof(StubMethod)); // no registration → Expression fallback

                var method = new StubMethod();
                binding.ReadFromBBGeneric(method, bb);
                Assert.AreEqual(3.5f, method.speed); // compiled accessor behavior

                method.speed = 9f;
                binding.WriteToBBGeneric(method, bb);
                Assert.AreEqual(9f, bb.GetFloat(0));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(def);
            }
        }
    }
}
```

- [ ] **Step 2: Run — verify FAIL** (types don't exist).

- [ ] **Step 3: Implement `GeneratedAccessorRegistry`**

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Registry for build-time-generated binding accessors. The generated assembly
    /// populates this via [RuntimeInitializeOnLoadMethod]; FieldBinding.BindAccessors
    /// consults it before falling back to Expression.Compile.
    ///
    /// Accessors are slot-agnostic: the slot is supplied per call, so one accessor
    /// serves every instance of a method type.
    /// </summary>
    public static class GeneratedAccessorRegistry
    {
        public delegate void ReadAccessor(NodeMethod method, IBlackBoardAccess bb, int slot);
        public delegate void WriteAccessor(NodeMethod method, IBlackBoardAccess bb, int slot);

        private struct Entry
        {
            public Type fieldType;
            public ReadAccessor read;
            public WriteAccessor write;
        }

        private static readonly Dictionary<(Type methodType, string fieldName), Entry> entries = new();

        public static int Count => entries.Count;

        public static void Clear() => entries.Clear();

        public static void Register(Type methodType, string fieldName, Type fieldType,
            ReadAccessor read, WriteAccessor write)
        {
            entries[(methodType, fieldName)] = new Entry { fieldType = fieldType, read = read, write = write };
        }

        /// <summary>
        /// Looks up accessors for (methodType, field). Returns false when no entry
        /// exists OR when the entry was generated for a different field type
        /// (stale generated code — caller should fall back + warn).
        /// </summary>
        public static bool TryGet(Type methodType, FieldInfo field,
            out ReadAccessor read, out WriteAccessor write)
        {
            if (field != null && entries.TryGetValue((methodType, field.Name), out Entry e)
                && e.fieldType == field.FieldType)
            {
                read = e.read;
                write = e.write;
                return true;
            }

            read = null;
            write = null;
            return false;
        }
    }
}
```

- [ ] **Step 4: Add `BindAccessors` to [FieldBinding.cs](file:///d:/Dev/TreeCommanderTest/Assets/BehaviourTree/Runtime/Bindings/FieldBinding.cs)**

Insert above `CompileAccessors`:

```csharp
        /// <summary>
        /// Production bind path: use the build-time-generated accessor when one is
        /// registered for (declaringType, field) with a matching field type;
        /// otherwise fall back to Expression.Compile (editor iteration).
        /// </summary>
        public void BindAccessors(Type declaringType)
        {
            if (bbSlotIndex < 0 || fieldInfo == null || skipAutoResolve) return;

            if (GeneratedAccessorRegistry.TryGet(declaringType, fieldInfo,
                    out GeneratedAccessorRegistry.ReadAccessor read,
                    out GeneratedAccessorRegistry.WriteAccessor write))
            {
                int slot = bbSlotIndex;
                readDelegate = (m, bb) => read(m, bb, slot);
                if (isOutput && write != null)
                    writeDelegate = (m, bb) => write(m, bb, slot);
                return;
            }

#if UNITY_EDITOR
            UnityEngine.Debug.LogWarning(
                $"[FieldBinding] No generated accessor for {declaringType.Name}.{fieldInfo.Name} " +
                "(or stale field type) — falling back to Expression.Compile. " +
                "Regenerate via 'Behaviour Tree/Generate Binding Accessors'.");
#endif
            CompileAccessors(declaringType);
        }
```

- [ ] **Step 5: Switch the production call site in [NodeMethod.cs](file:///d:/Dev/TreeCommanderTest/Assets/BehaviourTree/Runtime/NodeMethod.cs#L123)**

In `DeserializeFields`, replace `binding.CompileAccessors(GetType());` with:

```csharp
                    binding.BindAccessors(GetType());
```

(`CompileAccessors` stays public — tests and the fallback use it.)

- [ ] **Step 6: Run ALL EditMode tests — verify PASS** (5 new + existing 45; expect staleness warnings in logs from the fallback tests — harmless).

- [ ] **Step 7: Commit**

```powershell
git add Assets/BehaviourTree/Runtime Assets/BehaviourTree/Tests
git commit -m "feat: GeneratedAccessorRegistry + BindAccessors — generated-first binding with Expression fallback"
```

**Manual checkpoint:** none — nothing registers yet, so all bindings still take the fallback path (behavior identical, just a new editor warning per binding; that's expected and motivates Phase 3).

## Phase 2 — `AccessorEmitter` (pure codegen text)

The emitter turns `(methodType, field)` into C# accessor-registration text. Pure string building, fully unit-tested before any file is written. Classification delegates to `TypedAccessorMap` at generation time (invariant #4).

### Task 2.1: Emitter

**Files:**
- Create: `Assets/BehaviourTree/Editor/Codegen/AccessorEmitter.cs`
- Modify: `Assets/BehaviourTree/Tests/EditMode/BehaviourTree.Runtime.Tests.asmdef` (add `BehaviourTree.Editor` reference)
- Test: `Assets/BehaviourTree/Tests/EditMode/AccessorEmitterTests.cs`

- [ ] **Step 1: Add the Editor reference to the test asmdef**

In `BehaviourTree.Runtime.Tests.asmdef`, add `"BehaviourTree.Editor"` to `references` (EditMode tests may reference editor assemblies).

- [ ] **Step 2: Write the failing tests**

```csharp
using BehaviourTree.Core;
using BehaviourTree.EditorTools.Codegen;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class AccessorEmitterTests
    {
        private enum TestState { Idle = 0, Running = 1 }

        private class StubMethod : ActionMethod
        {
            public float speed;
            public TestState state;
            public Transform target;
            public string label;
            public override NodeState Execute(Runtime.TickContext ctx) => NodeState.SUCCESS;
        }

        private static string Emit(string fieldName)
        {
            var field = typeof(StubMethod).GetField(fieldName);
            return AccessorEmitter.EmitRegistration(typeof(StubMethod), field);
        }

        [Test]
        public void FloatField_UsesTypedAccessor()
        {
            string code = Emit(nameof(StubMethod.speed));
            StringAssert.Contains("bb.GetFloat(slot)", code);
            StringAssert.Contains("bb.SetFloat(slot,", code);
            StringAssert.Contains(".StubMethod)m).speed", code); // global:: full name
            StringAssert.Contains("typeof(float)", code);
        }

        [Test]
        public void EnumField_ReadsInt_WithCastBack()
        {
            string code = Emit(nameof(StubMethod.state));
            StringAssert.Contains("TestState)bb.GetInt(slot)", code); // cast back, full name prefix
            StringAssert.Contains("bb.SetInt(slot, (int)", code);
        }

        [Test]
        public void RefField_UsesGenericAccessors()
        {
            string code = Emit(nameof(StubMethod.target));
            StringAssert.Contains("bb.Get<global::UnityEngine.Transform>(slot)", code);
            StringAssert.Contains("bb.Set(slot,", code);
            StringAssert.Contains(".StubMethod)m).target", code);
        }

        [Test]
        public void StringField_UsesGenericAccessors()
        {
            string code = Emit(nameof(StubMethod.label));
            StringAssert.Contains("bb.Get<string>(slot)", code); // string is special-cased to the keyword
            StringAssert.Contains("bb.Set(slot,", code);
            StringAssert.Contains(".StubMethod)m).label", code);
        }
    }
}
```

- [ ] **Step 3: Run — verify FAIL** (`AccessorEmitter` doesn't exist; test asmdef may also fail to compile until the Editor reference resolves — fix that first).

- [ ] **Step 4: Implement `AccessorEmitter`**

```csharp
using System;
using System.Reflection;
using System.Text;
using BehaviourTree.Core;

namespace BehaviourTree.EditorTools.Codegen
{
    /// <summary>
    /// Emits C# registration code for one (method type, field) binding accessor.
    /// Typed-vs-generic classification goes through TypedAccessorMap at generation
    /// time, so it cannot drift from the runtime layout.
    /// </summary>
    public static class AccessorEmitter
    {
        /// <summary>
        /// Returns one GeneratedAccessorRegistry.Register(...) statement for the field,
        /// or null when the field needs no accessor (skipAutoResolve).
        /// </summary>
        public static string EmitRegistration(Type methodType, FieldInfo field)
        {
            if (methodType == null || field == null) return null;

            var sharedVar = field.GetCustomAttribute<SharedVarAttribute>();
            if (sharedVar != null && sharedVar.SkipAutoResolve) return null;

            Type fieldType = field.FieldType;
            string cast = $"(({FormatTypeName(methodType)})m).{field.Name}";
            string fieldTypeName = FormatTypeName(fieldType);

            MethodInfo getter = TypedAccessorMap.GetGetter(fieldType);
            MethodInfo setter = TypedAccessorMap.GetSetter(fieldType);

            string readBody;
            string writeBody;

            if (getter != null && fieldType.IsEnum)
            {
                // int-backed enum: read as int, cast back; write cast to int.
                readBody = $"{cast} = ({fieldTypeName})bb.{getter.Name}(slot);";
                writeBody = $"bb.{setter.Name}(slot, (int){cast});";
            }
            else if (getter != null)
            {
                readBody = $"{cast} = bb.{getter.Name}(slot);";
                writeBody = $"bb.{setter.Name}(slot, {cast});";
            }
            else
            {
                readBody = $"{cast} = bb.Get<{fieldTypeName}>(slot);";
                writeBody = $"bb.Set(slot, {cast});";
            }

            var sb = new StringBuilder(256);
            sb.Append("            R(typeof(").Append(FormatTypeName(methodType)).Append("), \"")
              .Append(field.Name).Append("\", typeof(").Append(fieldTypeName).Append("),\n");
            sb.Append("                (m, bb, slot) => ").Append(readBody).Append('\n');
            sb.Append("                (m, bb, slot) => ").Append(writeBody).Append(");\n");
            return sb.ToString();
        }

        /// <summary>Full-name type formatting valid in any namespace context (global::).</summary>
        public static string FormatTypeName(Type t)
        {
            if (t == null) return "object";
            if (t == typeof(float)) return "float";
            if (t == typeof(int)) return "int";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(string)) return "string";
            if (t == typeof(object)) return "object";
            if (t == typeof(void)) return "void";
            return "global::" + t.FullName.Replace('+', '.');
        }
    }
}
```

Note: tests reference stub types in the same file, so `global::BehaviourTree.Tests.AccessorEmitterTests+StubMethod` — nested types format with `.` via the `Replace('+', '.')`. The `StringAssert.Contains` patterns above match within that. Adjust `FormatTypeName` if your C# version context rejects `global::` (it is valid C# 8+; Unity supports it).

- [ ] **Step 5: Run — verify PASS.**

- [ ] **Step 6: Commit**

```powershell
git add Assets/BehaviourTree/Editor/Codegen Assets/BehaviourTree/Tests
git commit -m "feat: AccessorEmitter — per-field accessor registration codegen"
```

**Manual checkpoint:** user skims one emitted snippet in the test log/output and confirms the shape matches what the runtime registry expects.

---

## Phase 3 — Generator command + generated assembly

The editor command that scans all `NodeMethod` types and writes the generated file. After this phase, playmode runs fully on generated accessors (warnings gone).

### Task 3.1: `BindingAccessorGenerator`

**Files:**
- Create: `Assets/BehaviourTree/Editor/Codegen/BindingAccessorGenerator.cs`
- Generated (by running it): `Assets/BehaviourTree/Generated/BehaviourTree.Generated.asmdef`, `Assets/BehaviourTree/Generated/GeneratedBindingAccessors.cs`
- Test: `Assets/BehaviourTree/Tests/EditMode/BindingAccessorGeneratorTests.cs`

- [ ] **Step 1: Write the failing test** (file-content level, not a full generation run)

```csharp
using System.Reflection;
using BehaviourTree.Core;
using BehaviourTree.EditorTools.Codegen;
using NUnit.Framework;

namespace BehaviourTree.Tests
{
    public class BindingAccessorGeneratorTests
    {
        private class GenStubMethod : ActionMethod
        {
            public float speed;
            public override NodeState Execute(Runtime.TickContext ctx) => NodeState.SUCCESS;
        }

        [Test]
        public void BuildFile_ContainsRegisterAll_AndAccessor()
        {
            var fields = typeof(GenStubMethod).GetFields(BindingFlags.Public | BindingFlags.Instance);
            string file = BindingAccessorGenerator.BuildFile(
                new[] { (typeof(GenStubMethod), fields) });

            StringAssert.Contains("// <auto-generated/>", file);
            StringAssert.Contains("namespace BehaviourTree.Generated", file);
            StringAssert.Contains("public static class GeneratedBindingAccessors", file);
            StringAssert.Contains("RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)", file);
            StringAssert.Contains("GeneratedAccessorRegistry.Clear();", file);
            StringAssert.Contains("bb.GetFloat(slot)", file);
            StringAssert.Contains("GenStubMethod", file);
        }
    }
}
```

- [ ] **Step 2: Run — verify FAIL.**

- [ ] **Step 3: Implement the generator**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.EditorTools.Codegen
{
    /// <summary>
    /// Scans all NodeMethod types (via MethodRegistry, the single source of field
    /// selection) and writes GeneratedBindingAccessors.cs into
    /// Assets/BehaviourTree/Generated/. Run manually from the menu, or let the
    /// editor staleness warning remind you after changing method fields.
    /// </summary>
    public static class BindingAccessorGenerator
    {
        public const string OutputFolder = "Assets/BehaviourTree/Generated";
        public const string OutputFile = OutputFolder + "/GeneratedBindingAccessors.cs";
        public const string AsmdefFile = OutputFolder + "/BehaviourTree.Generated.asmdef";

        private const string AsmdefContent =
@"{
    ""name"": ""BehaviourTree.Generated"",
    ""rootNamespace"": ""BehaviourTree.Generated"",
    ""references"": [
        ""BehaviourTree.Core"",
        ""BehaviourTree.Runtime""
    ],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""autoReferenced"": true,
    ""noEngineReferences"": false
}
";

        [MenuItem("Behaviour Tree/Generate Binding Accessors")]
        public static void Generate()
        {
            var methodFields = new List<(Type type, FieldInfo[] fields)>();

            foreach (string methodName in MethodRegistry.GetMethodNames())
            {
                Type type = MethodRegistry.GetMethodType(methodName);
                if (type == null) continue;

                FieldBinding[] bindings = MethodRegistry.CreateBindings(type);
                if (bindings == null || bindings.Length == 0) continue;

                var fields = new List<FieldInfo>(bindings.Length);
                foreach (FieldBinding b in bindings)
                    if (b?.fieldInfo != null)
                        fields.Add(b.fieldInfo);

                if (fields.Count > 0)
                    methodFields.Add((type, fields.ToArray()));
            }

            // Stable order → stable diffs in the generated file.
            methodFields.Sort((a, b) => string.CompareOrdinal(a.type.FullName, b.type.FullName));

            Directory.CreateDirectory(OutputFolder);
            if (!File.Exists(AsmdefFile))
                File.WriteAllText(AsmdefFile, AsmdefContent);
            File.WriteAllText(OutputFile, BuildFile(methodFields));
            AssetDatabase.Refresh();

            Debug.Log($"[BindingAccessorGenerator] Wrote {OutputFile} ({methodFields.Count} method types).");
        }

        /// <summary>Pure file builder — unit-testable without touching the asset DB.</summary>
        public static string BuildFile(IReadOnlyList<(Type type, FieldInfo[] fields)> methodFields)
        {
            var sb = new StringBuilder(8192);
            sb.Append("// <auto-generated/> by BindingAccessorGenerator — do not edit. Regenerate via 'Behaviour Tree/Generate Binding Accessors'.\n");
            sb.Append("#pragma warning disable\n");
            sb.Append("using System;\n");
            sb.Append("using BehaviourTree.Core;\n");
            sb.Append("using UnityEngine;\n\n");
            sb.Append("namespace BehaviourTree.Generated\n{\n");
            sb.Append("    public static class GeneratedBindingAccessors\n    {\n");
            sb.Append("        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]\n");
            sb.Append("        public static void RegisterAll()\n        {\n");
            sb.Append("            GeneratedAccessorRegistry.Clear();\n");

            foreach ((Type type, FieldInfo[] fields) in methodFields)
            {
                foreach (FieldInfo field in fields)
                {
                    string registration = AccessorEmitter.EmitRegistration(type, field);
                    if (registration != null)
                        sb.Append(registration);
                }
            }

            sb.Append("        }\n\n");
            sb.Append("        private static void R(Type methodType, string fieldName, Type fieldType,\n");
            sb.Append("            GeneratedAccessorRegistry.ReadAccessor read, GeneratedAccessorRegistry.WriteAccessor write)\n");
            sb.Append("            => GeneratedAccessorRegistry.Register(methodType, fieldName, fieldType, read, write);\n");
            sb.Append("    }\n}\n");
            return sb.ToString();
        }
    }
}
```

- [ ] **Step 4: Run the EditMode suite — verify PASS.**

- [ ] **Step 5: Run the generator for real**

In the editor: `Behaviour Tree → Generate Binding Accessors`. Expected console line: `[BindingAccessorGenerator] Wrote Assets/BehaviourTree/Generated/GeneratedBindingAccessors.cs (N method types).` Inspect the file: one `R(...)` per bindable field, typed accessors for the 9 supported types, generic `Get<T>/Set<T>` for the rest.

- [ ] **Step 6: Playmode smoke** — enter playmode on an agent tree scene. Expected: **zero** `[FieldBinding] No generated accessor...` warnings in the console, tree behaves normally.

- [ ] **Step 7: Commit** (generated files ARE versioned — they're regenerated on demand, but committing keeps CI/builds reproducible without an editor pass)

```powershell
git add Assets/BehaviourTree/Editor/Codegen Assets/BehaviourTree/Generated Assets/BehaviourTree/Tests
git commit -m "feat: binding accessor generator + generated accessors for all NodeMethod types"
```

**Manual checkpoint:** user inspects `GeneratedBindingAccessors.cs` and confirms playmode warning-free. From here on, all `[SharedVar]` bindings run on generated static code.

## Phase 4 — Staleness ergonomics + build guarantee

The fallback keeps behavior correct when methods change, but the warning is easy to miss. This phase makes staleness visible where it matters (validator + bake-time reminder) and adds the hard guarantee: builds always regenerate accessors via `IPreprocessBuildWithReport`. Generation NEVER happens per bake or per play — only manually (menu) or per build.

### Task 4.1: Validation menu item + bake hook (validation only)

**Files:**
- Create: `Assets/BehaviourTree/Editor/Codegen/GeneratedAccessorValidator.cs`
- Modify: `Assets/BehaviourTree/Runtime/TreeBaker.cs` (editor bake entry point only)

- [ ] **Step 1: Implement the validator**

```csharp
using System;
using System.Text;
using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.EditorTools.Codegen
{
    /// <summary>
    /// Reports NodeMethod fields that have no generated accessor (or a stale one).
    /// Staleness happens when a field type changes without regenerating — the runtime
    /// silently falls back to Expression.Compile, which breaks AOT builds.
    /// </summary>
    public static class GeneratedAccessorValidator
    {
        [MenuItem("Behaviour Tree/Validate Binding Accessors")]
        public static void Validate()
        {
            int missing = 0;
            int ok = 0;
            var report = new StringBuilder();

            foreach (string methodName in MethodRegistry.GetMethodNames())
            {
                Type type = MethodRegistry.GetMethodType(methodName);
                if (type == null) continue;

                foreach (FieldBinding b in MethodRegistry.CreateBindings(type))
                {
                    if (b?.fieldInfo == null || b.skipAutoResolve) continue;

                    if (GeneratedAccessorRegistry.TryGet(type, b.fieldInfo, out _, out _))
                    {
                        ok++;
                    }
                    else
                    {
                        missing++;
                        report.AppendLine($"  {type.FullName}.{b.fieldInfo.Name} ({b.fieldInfo.FieldType.Name})");
                    }
                }
            }

            if (missing == 0)
            {
                Debug.Log($"[GeneratedAccessorValidator] All {ok} bindable fields have generated accessors.");
            }
            else
            {
                Debug.LogWarning(
                    $"[GeneratedAccessorValidator] {missing} field(s) missing/stale (of {ok + missing}):\n{report}" +
                    "Run 'Behaviour Tree/Generate Binding Accessors'.");
            }
        }
    }
}
```

Note: the validator relies on `RegisterAll` having run. In the editor, `RuntimeInitializeOnLoadMethod` only fires on play — so for edit-time validation, call `GeneratedBindingAccessors.RegisterAll()` reflectively at the top of `Validate()` if the generated type exists:

```csharp
            // Ensure the generated table is populated outside playmode.
            Type generated = Type.GetType("BehaviourTree.Generated.GeneratedBindingAccessors, BehaviourTree.Generated");
            generated?.GetMethod("RegisterAll")?.Invoke(null, null);
```

(Insert before the foreach. If the generated assembly doesn't exist yet, validation reports everything as missing — correct behavior.)

- [ ] **Step 2: Bake hook (validation only — never generation)** — in the editor-side bake entry point, add a one-line reminder (no auto-generation: baking must stay fast and side-effect-free with respect to source files):

```csharp
            GeneratedAccessorValidator.Validate(); // logs warning listing stale fields, no-op when clean
```

**Implementer: pick the actual editor-side bake entry point** (search for where `Bake(` is invoked from Editor code — Runtime `TreeBaker` can't see editor code) and add the call there.

- [ ] **Step 3: Manual check** — change a field type in some method (e.g. add a new public float field to any `Enemy_*` method), run Validate: it lists the field. Run Generate: it's clean again. Revert the experimental change or keep and commit with regeneration.

- [ ] **Step 4: Commit**

```powershell
git add Assets/BehaviourTree/Editor/Codegen Assets/BehaviourTree/Editor
git commit -m "feat: generated-accessor validator + bake-time staleness reminder"
```

### Task 4.2: Build-preprocess auto-generation (the hard guarantee)

Every build ships current accessors, even on a fresh clone or after uncommitted method changes. Generation is editor-side, before script compilation — it never touches bake or play.

**Files:**
- Modify: `Assets/BehaviourTree/Editor/Codegen/BindingAccessorGenerator.cs` (skip write when unchanged)
- Create: `Assets/BehaviourTree/Editor/Codegen/GenerateAccessorsBuildPreprocessor.cs`

- [ ] **Step 1: Skip-if-identical in the generator**

In `BindingAccessorGenerator.Generate()`, avoid touching the file (and triggering a pointless recompile) when nothing changed. Replace the write block:

```csharp
            Directory.CreateDirectory(OutputFolder);
            if (!File.Exists(AsmdefFile))
                File.WriteAllText(AsmdefFile, AsmdefContent);
            File.WriteAllText(OutputFile, BuildFile(methodFields));
            AssetDatabase.Refresh();
```

with:

```csharp
            Directory.CreateDirectory(OutputFolder);
            if (!File.Exists(AsmdefFile))
                File.WriteAllText(AsmdefFile, AsmdefContent);

            string content = BuildFile(methodFields);
            if (File.Exists(OutputFile) && File.ReadAllText(OutputFile) == content)
            {
                Debug.Log($"[BindingAccessorGenerator] Unchanged ({methodFields.Count} method types) — no rewrite.");
                return;
            }

            File.WriteAllText(OutputFile, content);
            AssetDatabase.Refresh();
```

- [ ] **Step 2: Implement the preprocessor**

```csharp
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BehaviourTree.EditorTools.Codegen
{
    /// <summary>
    /// Hard guarantee that every build ships current, AOT-safe binding accessors:
    /// regenerates GeneratedBindingAccessors.cs before script compilation.
    /// Editor-side only; costs seconds per build, zero during iteration.
    /// </summary>
    public sealed class GenerateAccessorsBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[GenerateAccessorsBuildPreprocessor] Regenerating binding accessors for build...");
            BindingAccessorGenerator.Generate();
        }
    }
}
```

- [ ] **Step 3: Manual check** — make a development build (`File → Build And Run` or Build window). Expected console line: `[GenerateAccessorsBuildPreprocessor] Regenerating binding accessors for build...` followed by either the generator's `Wrote ...` or `Unchanged ...` line; build completes.

- [ ] **Step 4: Commit**

```powershell
git add Assets/BehaviourTree/Editor/Codegen
git commit -m "feat: regenerate binding accessors in IPreprocessBuildWithReport (AOT build guarantee)"
```

**Manual checkpoint:** user confirms the build-preprocess line appears in a build.

---

## Phase 5 — Tracked bindings (`[GenerateBindingAccessors]`)

Field bindings covered per-type; tracked bindings are per (component type, member). Opt-in attribute keeps the generated surface bounded.

### Task 5.1: Attribute + tracked registry + generator support

**Files:**
- Create: `Assets/BehaviourTree/Runtime/Bindings/GenerateBindingAccessorsAttribute.cs`
- Create: `Assets/BehaviourTree/Runtime/Bindings/TrackedAccessorRegistry.cs`
- Modify: `Assets/BehaviourTree/Editor/Codegen/AccessorEmitter.cs` (member push emitter)
- Modify: `Assets/BehaviourTree/Editor/Codegen/BindingAccessorGenerator.cs` (scan attribute, emit tracked section)
- Modify: `Assets/BehaviourTree/Runtime/Execution/Runners/BehaviourTreeRunnerBase.cs` (registry-first in `CompileTrackedBindingDelegate`)
- Test: `Assets/BehaviourTree/Tests/EditMode/TrackedAccessorRegistryTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class TrackedAccessorRegistryTests
    {
        private class StubComp : MonoBehaviour { public float health; public string title; }

        [TearDown]
        public void TearDown() => TrackedAccessorRegistry.Clear();

        [Test]
        public void TryGet_Miss_ReturnsFalse()
        {
            Assert.IsFalse(TrackedAccessorRegistry.TryGet(typeof(StubComp), "health", typeof(float), out _));
        }

        [Test]
        public void Register_Hit_PushesValue()
        {
            TrackedAccessorRegistry.Register(typeof(StubComp), "health", typeof(float),
                (c, bb, slot) => bb.SetFloat(slot, ((StubComp)c).health));

            bool found = TrackedAccessorRegistry.TryGet(typeof(StubComp), "health", typeof(float),
                out TrackedAccessorRegistry.PushAccessor push);

            Assert.IsTrue(found);

            var def = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.sharedVariables.Add(new BlackboardVariable<float>("health", 1, 0f));
            var compGo = new GameObject("comp");
            var bbGo = new GameObject("bb");
            try
            {
                var bb = bbGo.AddComponent<BlackBoard>();
                bb.Initialize(def);
                StubComp comp = compGo.AddComponent<StubComp>();
                comp.health = 55f;

                push(comp, bb, 0);
                Assert.AreEqual(55f, bb.GetFloat(0));
            }
            finally
            {
                Object.DestroyImmediate(compGo);
                Object.DestroyImmediate(bbGo);
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void StaleMemberType_Misses()
        {
            TrackedAccessorRegistry.Register(typeof(StubComp), "health", typeof(int),
                (c, bb, slot) => { });
            Assert.IsFalse(TrackedAccessorRegistry.TryGet(typeof(StubComp), "health", typeof(float), out _));
        }
    }
}
```

- [ ] **Step 2: Run — verify FAIL.**

- [ ] **Step 3: Implement the attribute + registry**

`GenerateBindingAccessorsAttribute.cs`:

```csharp
using System;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Opt-in marker for the binding accessor generator: public instance fields and
    /// properties of this Component type get generated typed push accessors for
    /// tracked bindings. Without it, tracked bindings use the Expression fallback.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GenerateBindingAccessorsAttribute : Attribute { }
}
```

`TrackedAccessorRegistry.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Registry for generated tracked-binding push accessors, keyed by
    /// (component type, member name). Same staleness contract as
    /// GeneratedAccessorRegistry: member-type mismatch → miss → Expression fallback.
    /// </summary>
    public static class TrackedAccessorRegistry
    {
        public delegate void PushAccessor(Component component, IBlackBoardAccess bb, int slot);

        private struct Entry
        {
            public Type memberType;
            public PushAccessor push;
        }

        private static readonly Dictionary<(Type compType, string memberName), Entry> entries = new();

        public static int Count => entries.Count;

        public static void Clear() => entries.Clear();

        public static void Register(Type compType, string memberName, Type memberType, PushAccessor push)
        {
            entries[(compType, memberName)] = new Entry { memberType = memberType, push = push };
        }

        public static bool TryGet(Type compType, string memberName, Type memberType, out PushAccessor push)
        {
            if (entries.TryGetValue((compType, memberName), out Entry e) && e.memberType == memberType)
            {
                push = e.push;
                return true;
            }

            push = null;
            return false;
        }
    }
}
```

- [ ] **Step 4: Emitter support** — add to `AccessorEmitter`:

```csharp
        /// <summary>
        /// Emits a TrackedAccessorRegistry registration for one component member
        /// (field or property with getter). Returns null for member types without a
        /// typed accessor (those stay on the Expression fallback by design).
        /// </summary>
        public static string EmitTrackedRegistration(Type compType, MemberInfo member)
        {
            Type memberType = member is FieldInfo f ? f.FieldType
                : member is PropertyInfo p ? p.PropertyType
                : null;
            if (memberType == null) return null;

            MethodInfo setter = TypedAccessorMap.GetSetter(memberType);
            if (setter == null) return null; // boxed fallback covers object/custom types

            string compTypeName = FormatTypeName(compType);
            string read = $"(({compTypeName})c).{member.Name}";
            string body = memberType.IsEnum
                ? $"bb.{setter.Name}(slot, (int){read});"
                : $"bb.{setter.Name}(slot, {read});";

            return $"            TR(typeof({compTypeName}), \"{member.Name}\", typeof({FormatTypeName(memberType)}),\n" +
                   $"                (c, bb, slot) => {body}\n";
        }
```

- [ ] **Step 5: Generator support** — in `BindingAccessorGenerator.Generate()`, after the method scan, scan for attributed component types and append a tracked section to `BuildFile`:

```csharp
            // Tracked bindings: opt-in via [GenerateBindingAccessors] on the component.
            var tracked = new List<(Type type, MemberInfo member)>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }

                foreach (Type t in types)
                {
                    if (t == null || !typeof(Component).IsAssignableFrom(t)) continue;
                    if (t.GetCustomAttribute<GenerateBindingAccessorsAttribute>() == null) continue;

                    const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                    foreach (FieldInfo f in t.GetFields(Flags))
                        tracked.Add((t, f));
                    foreach (PropertyInfo p in t.GetProperties(Flags))
                        if (p.CanRead && p.GetIndexParameters().Length == 0)
                            tracked.Add((t, p));
                }
            }

            tracked.Sort((a, b) => string.CompareOrdinal(a.type.FullName + "." + a.member.Name,
                                                          b.type.FullName + "." + b.member.Name));
```

Pass `tracked` into `BuildFile` (new optional parameter, default empty) and emit before the closing of `RegisterAll`:

```csharp
            sb.Append("            TrackedAccessorRegistry.Clear();\n");
            foreach ((Type type, MemberInfo member) in trackedMembers)
            {
                string registration = AccessorEmitter.EmitTrackedRegistration(type, member);
                if (registration != null)
                    sb.Append(registration);
            }
```

plus the helper next to `R`:

```csharp
            sb.Append("        private static void TR(Type compType, string memberName, Type memberType,\n");
            sb.Append("            TrackedAccessorRegistry.PushAccessor push)\n");
            sb.Append("            => TrackedAccessorRegistry.Register(compType, memberName, memberType, push);\n");
```

(`BuildFile`'s signature grows an optional `IReadOnlyList<(Type type, MemberInfo member)> trackedMembers = null` — treat null as empty. Update the Phase-3 test call site accordingly; it still passes since the parameter is optional.)

- [ ] **Step 6: Registry-first in `CompileTrackedBindingDelegate`** — in [BehaviourTreeRunnerBase.cs](file:///d:/Dev/TreeCommanderTest/Assets/BehaviourTree/Runtime/Execution/Runners/BehaviourTreeRunnerBase.cs), insert before the Expression typed branch (right after `memberAccess` is built and `memberType` is computed):

```csharp
                // Generated accessor wins (AOT-safe static code, no Expression at all).
                if (TrackedAccessorRegistry.TryGet(comp.GetType(), binding.memberName, memberType,
                        out TrackedAccessorRegistry.PushAccessor generatedPush))
                {
                    Component capturedComp = comp;
                    binding.typedPushDelegate = (bb, slot) => generatedPush(capturedComp, bb, slot);
                    return;
                }
```

The existing `TypedAccessorMap`-based Expression branch and boxed fallback remain as fallbacks.

- [ ] **Step 7: Mark the project's tracked component types** — add `[GenerateBindingAccessors]` to the component(s) used by tracked bindings in the demo scenes (check `EnemyManager` and any others surfaced by `TrackedVariablesView`). Regenerate via the menu, verify the generated file contains `TR(typeof(...EnemyManager...), "health", ...)` entries.

- [ ] **Step 8: Run ALL EditMode tests — verify PASS. Playmode smoke:** tracked-binding scene; values still push per frame.

- [ ] **Step 9: Commit**

```powershell
git add Assets/BehaviourTree/Runtime/Bindings Assets/BehaviourTree/Editor/Codegen Assets/BehaviourTree/Generated Assets/BehaviourTree/Runtime/Execution/Runners Assets/BehaviourTree/Tests Assets/ExposureDemo
git commit -m "feat: generated tracked-binding accessors via [GenerateBindingAccessors]"
```

**Manual checkpoint:** user verifies the tracked scene + inspects the `TR(...)` entries.

---

## Phase 6 — IL2CPP validation + docs

### Task 6.1: IL2CPP build smoke (manual, user-driven)

- [ ] **Step 1:** `Edit → Project Settings → Player → Scripting Backend: IL2CPP` (keep Mono for editor iteration if preferred — this is a one-off validation).
- [ ] **Step 2:** Build a development player of a demo scene (agent + commander) for the desktop target.
- [ ] **Step 3:** Run it. Expected: trees tick, no `ExecutionEngineException`, no missing-instantiation errors, blackboard values update.
- [ ] **Step 4:** (Optional, decisive) temporarily delete `Assets/BehaviourTree/Generated/GeneratedBindingAccessors.cs` in a throwaway branch and build again → observe the old behavior (interpreter fallback / AOT errors). Restore. This demonstrates exactly what the codegen fixed.

### Task 6.2: Docs

**Files:**
- Modify: `Assets/BehaviourTree/Docs/Compiled-Delegates-Reasoning.md`
- Modify: `Assets/BehaviourTree/Docs/BlackboardSystemOverview.md` (§1.4/1.5 — mention generated accessors as the production bind path)

- [ ] **Step 1:** In `Compiled-Delegates-Reasoning.md`, add an update note: `Expression.Compile` is now the editor-only fallback; production bindings use generated static accessors (`BindingAccessorGenerator` → `GeneratedAccessorRegistry`/`TrackedAccessorRegistry`). The "What gets deleted" list is now accurate for the DOTS step — the deletion candidates only serve editor fallback.
- [ ] **Step 2:** In `BlackboardSystemOverview.md`, add a short subsection under the storage/binding docs: generated accessors as the production path, regeneration via menu, validator.
- [ ] **Step 3: Commit**

```powershell
git add Assets/BehaviourTree/Docs
git commit -m "docs: generated binding accessors as production path; Expression.Compile as editor fallback"
```

---

## Final verification checklist

- [ ] All EditMode tests green (registry, emitter, generator, tracked registry + full suite).
- [ ] `Behaviour Tree → Validate Binding Accessors` reports zero missing/stale.
- [ ] Playmode: agent scene, commander/squad scene, tracked-binding scene — all behave identically to before; no `[FieldBinding]` warnings.
- [ ] Profiler spot-check: zero per-tick allocs from binding reads/writes (already true from the typed-accessor path; generated code keeps it) and zero `Expression.Compile` calls at startup (check the editor log/Profiler during tree init).
- [ ] IL2CPP development build runs a demo scene without `ExecutionEngineException`.
- [ ] `GeneratedBindingAccessors.cs` committed and diff-stable across regenerations (sorted output).

## Risks / notes for the reviewer

- **SharedVar field additions are the staleness vector.** Any new/renamed/retyped public field on a `NodeMethod` without regeneration → fallback warning. The validator + bake hook exist to catch this; consider wiring generation into your pre-commit or bake workflow if it bites often.
- **Field type change with same name** makes the *generated assembly fail to compile* (e.g. float → int changes what `bb.GetFloat(slot)` can assign to). That's loud, not silent — regenerate and the error disappears. This is intentional: wrong-type generated code should never run.
- **`RuntimeInitializeOnLoadMethod(SubsystemRegistration)`** re-registers on every play (and on domain reload in-editor when playing). Edit-time tools must call `RegisterAll()` reflectively (the validator does).
- **Generic instantiations in generated code** (`bb.Get<Transform>(slot)`, `bb.Get<MyStruct>(slot)`) are statically compiled — present in the AOT image by construction. This is the core IL2CPP fix.
- **Tracked bindings without the attribute** silently keep the Expression fallback — when adding a new tracked component type, mark it `[GenerateBindingAccessors]` and regenerate.
- **Not deleted (yet):** `CompileAccessors`, `CompileTrackedBindingDelegate`, `Expression.Compile` — they remain the editor-iteration bridge. Deletion belongs to the DOTS plan.
