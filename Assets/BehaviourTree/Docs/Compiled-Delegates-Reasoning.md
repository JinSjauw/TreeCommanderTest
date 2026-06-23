# Compiled Delegates for FieldBinding and TrackedBindings

## Why we did it

The blackboard storage is a single type-erased `object[]`. When a C# field of type `float` needs to
read from (or a component property needs to push into) this array, boxing and unboxing happens.
For **FieldBinding**, the existing path used `Expression.Compile()` to create typed delegates that
avoid the boxing via `IBlackBoardAccess.Get<T>()` / `Set<T>()`. For **TrackedBindings**, the
`PushTrackedBindings()` loop called `PropertyInfo.GetValue()` / `FieldInfo.GetValue()` every frame —
a per-frame reflection call per binding.

We replaced the per-frame reflection in `PushTrackedBindings()` with a `Func<object>` delegate
compiled once at resolve time, using the same Expression-tree pattern as `FieldBinding.CompileAccessors`.

## Performance gain

| Path | Per-frame cost per binding |
|---|---|
| `PropertyInfo.GetValue()` (before) | Reflection invoke — metadata walk + argument packing |
| `FieldInfo.GetValue()` (before) | Same as above |
| `Func<object>` delegate (after) | Direct typed invoke — equivalent to a virtual call |

The delegate still boxes the return value into `object` because the storage is an `object[]`. The
performance win is **eliminating the per-frame reflection call**, not eliminating boxing. Boxing
will be addressed when the storage itself becomes typed.

## Why it is temporary

Both `FieldBinding.CompileAccessors()` and the tracked bindings `Func<object>` delegate exist
because we are bridging **typed C# fields** to **type-erased `object[]` storage**. That bridge
needs either runtime code generation (`Expression.Compile`) or per-frame reflection. Neither is
a permanent design.

### Future path: typed arrays → DOTS NativeArray<T>

1. **Step 1 — Typed arrays per supported type.**
   Replace the single `object[]` with one array per common type:

   ```
   object[] storage      →    float[]     _floats
                               int[]       _ints
                               bool[]      _bools
                               Vector3[]   _vectors3
                               Transform[] _transforms
                               ...
                               object[]    _fallback   (rare types only)
   ```

   A FieldBinding for `float health` no longer needs `GetBoxed(slot)` or an Expression delegate.
   It reads directly: `this.health = storage._floats[localSlot]`. Zero boxing, zero reflection,
   zero generated code — just an array index.

2. **Step 2 — DOTS NativeArray<T>.**
   When the project moves to DOTS, the typed arrays become `NativeArray<T>` backed by
   unmanaged memory. The access pattern is identical:

   ```
   float[] _floats      →    NativeArray<float> _floats
   int[]   _ints        →    NativeArray<int>   _ints
   ```

   The BT evaluator becomes a Burst-compiled job. Node methods become structs implementing
   `INodeMethod`. The blackboard is just a `DynamicBuffer<T>` or `NativeArray<T>` per type.

3. **What gets deleted.**
   - `Expression.Compile()` — all of it, everywhere
   - `CompileTrackedBindingDelegate()` — the entire method
   - `FieldBinding.CompileAccessors()` — the entire method
   - `Func<object> readDelegate` — no delegates needed
   - `ManagedBlackboardStorage.CanWriteBoxed()` — no type-checking needed
   - `IBlackBoardAccess.GetBoxed()` / `SetBoxed()` — replaced by typed indexers

### Why we didn't skip to typed arrays immediately

The boxed `object[]` path is demonstrably fine for the current project scale (<50 bindings,
<20 BB reads per tick). The reflection path was the actual measurable cost. Typed arrays
will be implemented when DOTS is on the immediate roadmap, not before — the cost of
maintaining two storage backends isn't justified yet.
