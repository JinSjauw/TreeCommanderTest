# Graph Editor Stability Refactor — Rundown

**Branch:** `refactor/EditorStability` · **Date:** 2026-07-25 · **Plan:** `docs/superpowers/plans/2026-07-25-graph-editor-stability-refactor.md`

22 commits in 3 phases, fixing 25 findings from the UI-drawing review of the behaviour graph editor (plus 1 crash found by the new tests). For each change: **why** it was needed, **what** it solves, and its **limitations / edge cases**.

---

## Phase 1 — Critical: hangs, leaks, crashes, desync

| Change | Why it had to happen | What it solves | Limitations / edge cases |
|---|---|---|---|
| Cycle-guard visited set (`ac3b60e`) | `WouldCreateCycle` walked parent pointers with no termination guarantee. One corrupt asset with a pre-existing cycle = **infinite loop inside `GetCompatiblePorts`, which runs during every edge drag → hard editor hang**, force-kill required | Editor stays responsive on corrupt graphs; regression test with 5 s timeout | When a cycle is detected mid-walk it returns "no cycle" — connection rules may be wrong *for that already-corrupt subgraph* (acceptable degradation; graph was already broken). The single-capacity exemption (a cycle through a single-capacity input is broken by edge replacement) was preserved as-is |
| Destroy `NodeSearchProvider` (`6f9c735`) | `CreateInstance` per graph view, **never destroyed**, and a **static event** (`MethodRegistry.OnRegistryRebuilt`) rooted the leaked instance + its Texture2D forever — every window rebuild leaked one | `Dispose()` now calls `Shutdown()` + `DestroyImmediate`; `OnDestroy` belt-and-braces unsubscribes | If the editor *crashes* without `OnDisable`, one instance still leaks (unavoidable in Unity). Verified by test counting `FindObjectsOfTypeAll` |
| Idempotent event lifecycle (`ada0bf7`) | Static `Undo.undoRedoPerformed +=` in the constructor, removed only in `Dispose()` — any window UI rebuild without a matching dispose stacked **duplicate handlers on a static event** → leaked GraphViews + multiple full `PopulateView` rebuilds per undo. Also `Cleanup()`/`Unbind()` were dead code during rebuilds (events detached while `DeleteElements` ran) | `eventsSubscribed` flag makes subscribe/unsubscribe idempotent; node/note cleanup now runs explicitly before every full clear | A crash between window creation and `OnDisable` can still orphan one subscription (Unity lifecycle reality). Guard is per-instance — instances leaked *before* this fix are not retroactively cleaned |
| Proxy prune key fix (`c8fdd3f`) | `proxyNodeViews` keyed by runtime GUID (`"anchor/child"`) but pruning compared/removed by plain node GUID — **the prune literally never matched**, stale debug proxies accumulated | Pruning now works on the dictionary's actual key space | Only prunes during `SetupDebugProxies` (once per populate); proxies leaving scope *mid-play-session* without a repopulate still linger until exit. Manual verification only — no automated test |
| Null-guard batch (`791dd0a`) | `HandleEdgeCreation` dereferenced `edge.output.node`/`tree` raw (NRE on unusual drops); `EditorWindow.focusedWindow` can be null → NRE opening the search window; `GraphEditorTheme.instance` returned null if the asset was deleted → NREs in **every drawing path** (node colors, icons, badges, tint) | Three crash classes eliminated; theme falls back to an in-memory default palette | Theme fallback means a deleted theme asset shows **default colors** until domain reload recreates the asset (correct-but-different visuals, no warning shown) |
| Play-mode structural-edit block (`7a2d66a`) | Worst desync found: in play mode, deletions removed views but not model data — `nodeViewDict` kept pointing at detached elements, model/view diverged; meanwhile edge creation *was* allowed and mutated the asset | Structural edits (delete/create edge) are blocked at the change level in play mode — GraphView simply doesn't apply them; view and model can't diverge | **UX gap: no feedback** when a delete is blocked — the key just "does nothing." Moves are still allowed and still write to the asset (reverted on play exit, but they create undo entries during play). The `isPlaying` check in `HandleEdgeCreation` is now redundant (defense in depth, kept) |
| Test asmdef reference (`b49e057`) | New tests couldn't compile — test assembly didn't reference `BehaviourTree.Editor` | Test infrastructure works | None |

---

## Phase 2 — High impact: per-frame cost and undo flood

| Change | Why | What it solves | Limitations |
|---|---|---|---|
| `SetDebugState` dirty-check + dict reuse (`5813e47`) | **Dominant per-frame cost**: at editor update rate (~100–200 Hz) in play mode, *every node* got `ClearClassList()` + `AddToClassList()` every tick even when unchanged → full USS restyle of the whole graph per frame, plus a new GUID dictionary allocated per call | Class lists only touched on actual state change; dictionary reused; unused `isActive` param removed | Cache is per-view-instance (fresh on rebuild — correct). If external code mutated the border classes directly, the cache would suppress re-apply (no such code exists). **Needs a play-mode visual check** |
| Single warning evaluation (`679b152`) | `RefreshWarningIcon` ran `Evaluate()` then `BuildWarningTooltip()` ran it *again* — double list allocations per node per icon refresh, and icons refresh after every graph change | One evaluation per refresh; new list-based overload; old signature kept for other callers | `Evaluate` itself still allocates a list per node per refresh — further reduction would need caching against node state (out of scope) |
| O(1) parent lookup (`02b1c21`) | `FindParentNodeView` did `GraphView.edges.ToList()` — **copied the entire edge list for every condition node on every icon refresh** → O(N·E) allocation churn | Reads the node's own input port connections — O(1), zero alloc | Returns the *first* parent edge only; single-capacity inputs guarantee ≤1 anyway |
| Position persistence at drag end (`c615a25`) | GraphView calls `SetPosition` **per mouse-move**; the override ran `Undo.RecordObject` + `SetDirty` per event → undo-stack flood, constant asset dirtying during drags | Persistence moved to `UpdatePresenterPosition` (fires once when the drag finishes — same hook `GraphNote` already relied on); one undo entry per drag; snap behavior unchanged | **If the editor crashes mid-drag, position is lost** (was: persisted continuously). `NodeSO.graphPosition` is stale *during* the drag — sort keys therefore read live view positions (`GetViewX`), with `graphPosition` as fallback. Relies on GraphView calling `UpdatePresenterPosition` at drag end (framework contract; a Unity version change here would silently stop persistence — watch on upgrades) |
| Scoped move sorting (`915b064`) | Every move event re-sorted (and dirtied) **every composite in the tree** | Only the moved node and its parent re-sort | Multi-selection drags iterate per element — fine. A move affecting a *grandparent's* ordering isn't re-sorted (order numbers only depend on the direct parent — correct) |
| `Debug.Log` removal (`add7fac`) | Leftover log spammed the console on every populate (selection change, undo, play transitions) | Console noise + string-format cost gone | None |

---

## Phase 3 — Medium: robustness and redundancy

| Change | Why | What it solves | Limitations |
|---|---|---|---|
| Port stylesheet hoisted (`e6815d0`) | Every port instance added the same USS asset to itself — hundreds of duplicate sheet references walked during style resolution | One sheet on the graph root; USS cascades | **Visual check pending** — if any rule relied on being scoped to the port element itself (e.g., `:hover` on the sheet host), behavior could differ subtly |
| Single selection notify (`bb534f5`) | MouseDown *and* `OnSelected` both invoked `OnNodeSelected` → inspector rebuilt twice per click | One notification per click | **Behavior change**: re-clicking an already-selected node no longer re-refreshes the inspector (was accidental, arguably a feature) |
| Unified conflicting-edge removal (`6d787c8`) | Two copies of single-capacity edge replacement had **drifted** (one hid order numbers, one didn't), and `TryConnectPorts` fired `onGraphDataChanged` twice per connect (auto-save/compile ran twice) | One shared `RemoveConflictingEdges`; suppression counter → exactly one notification per `TryConnectPorts` | **Drag-replace (`OnDrop`) still fires two notifications** when replacing an occupied port (one for removal, one for creation) — semantically defensible, but not coalesced |
| Geometry callback dedupe + fresh port list (`3c035c6`) | `RegisterCallback` per `PopulateView` stacked duplicates → multiple `FrameAll` calls. `GetCompatiblePorts` returned a **shared mutable list** cleared per call — mutation-during-enumeration risk if any caller retained it | Unregister-before-register; fresh list per call (matches stock GraphView) | Per-call list allocation is the stock-behavior tradeoff (correctness over micro-alloc) |
| Title field keeps USS classes (`22da6f6`) | `ClearClassList()` on the field and its text input **stripped Unity's functional classes** — fragile across Unity versions, broke focus/hover styling hooks | Built-in classes preserved; styling via `#GraphTitleInput` / `#GraphTitleTextField` selectors | **Visual check pending** — default base-field styles now apply; if the custom look regressed, the fix is higher-specificity USS selectors, not class clearing |
| Populate-failure hardening (`00d5dfc`) | A mid-populate exception left a **half-built graph** and logged only `ex.Message` | Resets to a clean empty view, nulls stale state, logs full stack trace | The underlying cause of a populate failure still needs fixing per-case; this just fails safe |
| Runner cache invalidation + proxy capacity (`25d6efd`) | Static runner cache only invalidated on scene open → new/renamed runners invisible in the dropdown. Replacement proxy edges connected into single-capacity inputs without checking occupancy | Cache invalidates on `hierarchyChanged`; `connected` guard before rewiring | `hierarchyChanged` fires often → `FindObjectsByType` may run more frequently than before (correctness/perf tradeoff, bounded by when `RefreshTitle` runs). Dropdown matches runners **by GameObject name** — two same-named runners still collide (pre-existing) |
| Tooltip null guard (`b6be96a`) | Found *by the new test*: `GetTooltipInternal` threw `ArgumentNullException` for null `methodName` — any corrupt/legacy node asset would crash the node view constructor during `PopulateView` | Default "Unknown" tooltip instead of a crash | Nodes with null method names now render with a generic tooltip — the underlying corrupt asset still needs repair; nothing surfaces that to the user |

---

## Current edge cases / failure points (consolidated)

**Not yet verified:**
1. **The EditMode suite has never run green as a whole** — batch CLI runs were blocked by environment issues (scene-backup modal, lock contention). Only a manual run of the cycle test happened (failed → fixed via `b6be96a` → awaiting re-run). The other 3 tests are unexecuted.
2. **Visual checks pending**: port styling, title bar, play-mode debug borders, one-undo-per-drag. These were compile-verified only.

**Known remaining weaknesses (accepted / out of scope):**
3. **Full-graph rebuild on every undo/redo and every tree switch** — biggest remaining inefficiency; a diff-based refresh is a separate project.
4. **`transition-property: all`** on `node-body` — now harmless (no more per-frame class churn), but still amplifies any future style-churn regression.
5. **Play-mode delete gives zero feedback** — blocked edits are silent; a status hint would be a UX improvement.
6. **Unity-upgrade sensitivity**: position persistence depends on GraphView's `UpdatePresenterPosition` contract; title styling depends on internal element names (`unity-text-input`).
7. **Crash-only leak paths remain**: anything relying on `OnDisable`/`Dispose` can still leak one instance on a hard editor crash (unfixable by design).
8. **No user-facing surfacing of corrupt data**: null-method-name nodes and pre-existing cycles now fail *safe* (generic tooltip / responsive UI) but *silently*.
9. **Mid-play-session proxy scope changes** only prune on repopulate; same-named runners still indistinguishable in the dropdown; drag-replace still emits two change notifications.
10. `CreateGUI` subscribes `playModeStateChanged` while `OnDisable` unsubscribes — a Unity lifecycle quirk re-running `CreateGUI` without `OnDisable` could double-subscribe (not addressed; no observed repro).

**Pre-existing, untouched:** 3 modified assets and the untracked plan doc remain uncommitted; branch hasn't been pushed or merged to `main`.
