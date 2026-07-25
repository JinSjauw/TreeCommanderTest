using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using BehaviourTree.Core;
using BehaviourTree.Editor;
using BehaviourTree.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class BehaviourTreeEditor : EditorWindow
{
    private BehaviourTreeEditorGraphView treeGraphView;

    private InspectorView inspectorView;
    private BlackBoardView blackBoardView;
    private TrackedVariablesView trackedVariablesView;
    private SquadTabView squadTabView;
    private CommanderTabView commanderTabView;
    private ToolbarMenu assetBarMenu;
    private TabView tabView;
    private Tab inspectorTab;
    private Tab commanderTabInstance;
    private TreeSearchProvider treeSearchProvider;

    public static BlackboardDefinition currentBlackboardDef { get; private set; }
    public static BaseEditorTreeAsset currentTree { get; private set; }
    public static BehaviourTreeRunnerBase currentRunner { get; private set; }

    private static List<BaseEditorTreeAsset> recentOpenedTrees = new List<BaseEditorTreeAsset>();
    private const int maxRecentTrees = 5;

    public static bool selectionIsLocked;
    public static int lockBypassDepth;

    /// <summary>Temporary: log tree-switch phase timings to the Console.</summary>
    internal const bool ProfileTreeSwitch = true;

    [MenuItem("BehaviourTree/Open Behaviour Tree Graph", priority = 29)]
    public static void OpenWindow()
    {
        currentTree = null;
        currentBlackboardDef = null;
        currentRunner = null;
        BehaviourTreeEditor wnd = GetWindow<BehaviourTreeEditor>();
        wnd.titleContent = new GUIContent("Behaviour Tree Editor");
    }

    [OnOpenAsset]
    public static bool OnOpenAsset(int instanceID, int line)
    {
        if (Selection.activeObject is BaseEditorTreeAsset)
        {
            OpenWindow();
            return true;
        }
        return false;
    }

    private void OnTemplateApplied(BlackboardDefinition target)
    {
        // Refresh the blackboard view if it's showing the definition that was just modified
        if (target != null && target == currentBlackboardDef && blackBoardView != null)
            blackBoardView.BuildBlackboardView(target);
    }

    public void CreateGUI()
    {
        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.EditorUxml);

        // Null check for UXML asset
        if (visualTree == null)
        {
            Debug.LogError("Failed to load BehaviourTreeEditor.uxml");
            return;
        }

        VisualElement root = visualTree.CloneTree();
        root.style.flexGrow = 1; // Fix the thin strip

        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(BehaviourTreeEditorPaths.EditorUss);

        // Null check for USS stylesheet
        if (styleSheet != null)
        {
            root.styleSheets.Add(styleSheet);
        }
        else
        {
            Debug.LogWarning("Failed to load BehaviourTreeEditor.uss");
        }

        rootVisualElement.Add(root);

        treeGraphView = root.Q<BehaviourTreeEditorGraphView>();
        inspectorView = root.Q<InspectorView>();
        blackBoardView = root.Q<BlackBoardView>();
        trackedVariablesView = root.Q<TrackedVariablesView>();
        squadTabView = root.Q<SquadTabView>();
        assetBarMenu = root.Q<ToolbarMenu>("AssetBarMenu");
        tabView = root.Q<TabView>("TabView");
        inspectorTab = tabView?.Q<Tab>("InspectorTab");
        commanderTabView = new CommanderTabView();
        commanderTabInstance = new Tab("Commander") { name = "CommanderTab", style = { flexGrow = 1 } };
        commanderTabInstance.AddToClassList("tab-container-styling");
        commanderTabView.AddToClassList("tab-styling");
        commanderTabInstance.Add(commanderTabView);

        if (treeGraphView == null)
        {
            Debug.LogError("Could not find BehaviourTreeEditorGraphView in UXML");
        }
        else
        {
            treeGraphView.OnCreateNewTreeRequested = HandleCreateNewTreeFromContext;
            treeGraphView.OnCyclePrevious = CycleToPreviousRecentTree;
            treeGraphView.OnCycleNext = CycleToNextRecentTree;
        }

        if (inspectorView == null)
        {
            Debug.LogError("Could not find InspectorView in UXML");
        }

        if (blackBoardView == null)
        {
            Debug.LogError("Could not find BlackBoardView in UXML");
        }

        if (assetBarMenu == null)
        {
            Debug.LogError("Could not find AssetBarMenu in UXML");
        }
        else
        {
            BuildAssetBarMenu();
        }

        // Add Config Sources toolbar button
        ToolbarButton configSourcesBtn = new ToolbarButton();
        configSourcesBtn.text = "Config Sources";
        configSourcesBtn.tooltip = "Manage ScriptableObject config sources for SO-field node parameters";
        configSourcesBtn.clicked += () =>
        {
            if (currentTree != null)
                ConfigSourcesWindow.Show(currentTree);
        };
        if (assetBarMenu?.parent != null)
            assetBarMenu.parent.Add(configSourcesBtn);

        OnSelectionChange();

        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        if (EditorApplication.isPlaying)
        {
            EditorApplication.update -= PollDebugState;
            EditorApplication.update += PollDebugState;
        }
    }

    private void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        switch (change)
        {
            case PlayModeStateChange.EnteredPlayMode:
                EditorApplication.update -= PollDebugState;
                EditorApplication.update += PollDebugState;
                break;
            case PlayModeStateChange.ExitingPlayMode:
                EditorApplication.update -= PollDebugState;
                treeGraphView?.ClearRuntimeDebugProxies();
                break;
            case PlayModeStateChange.EnteredEditMode:
                OnSelectionChange();
                break;
        }
    }

    private void SyncTree(DropdownMenuAction action)
    {
        if (currentTree == null) return;
        currentTree.SyncNodesListFromAssets();
    }

    private void OnEnable()
    {
        EditorApplication.projectChanged += OnProjectChanged;
        BlackboardTemplate.Applied += OnTemplateApplied;
    }

    private void BuildAssetBarMenu()
    {
        if (assetBarMenu == null) return;

        var menu = assetBarMenu.menu;
        menu.ClearItems();

        menu.AppendAction("Create New Tree/Agent Tree", _ => CreateTreeAsset<AgentTreeAsset>());
        menu.AppendAction("Create New Tree/Commander Tree", _ => CreateTreeAsset<CommanderTreeAsset>());
        menu.AppendSeparator();

        // Populate Open Tree submenu with recent AgentTreeAsset/CommanderTreeAsset files (last modified, limited to 15)
        const int maxRecentEntries = 5;
        string[] guids = AssetDatabase.FindAssets("t:AgentTreeAsset t:CommanderTreeAsset");

        var recentTrees = guids
            .Select(guid => new
            {
                Path = AssetDatabase.GUIDToAssetPath(guid),
                Asset = AssetDatabase.LoadAssetAtPath<BaseEditorTreeAsset>(AssetDatabase.GUIDToAssetPath(guid))
            })
            .Where(t => t.Asset != null)
            .Select(t => new
            {
                t.Asset,
                LastWrite = File.GetLastWriteTime(t.Path)
            })
            .OrderByDescending(t => t.LastWrite)
            .Take(maxRecentEntries);

        foreach (var entry in recentTrees)
        {
            BaseEditorTreeAsset capturedAsset = entry.Asset;
            menu.AppendAction("Open Tree/" + capturedAsset.name, _ =>
            {
                Selection.activeObject = capturedAsset;
                AssetDatabase.OpenAsset(capturedAsset);
            });
        }

        menu.AppendSeparator("Open Tree/");
        menu.AppendAction("Open Tree/Browse...", BrowseOpenTree);

        menu.AppendSeparator();
        menu.AppendAction("Apply Template...", OpenApplyTemplateDialog);
        menu.AppendAction("Bake Tree", BakeTree);
        menu.AppendAction("Save Tree", SaveTree);
        menu.AppendAction("Sync Tree", SyncTree);

        // ── Squad entries ──
        menu.AppendSeparator();
        {
            string[] squadGuids = AssetDatabase.FindAssets("t:SquadDefinition");
            System.Collections.Generic.List<SquadDefinition> recentSquads = squadGuids
                .Select(g => AssetDatabase.LoadAssetAtPath<SquadDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                .OrderByDescending(s => File.GetLastWriteTime(AssetDatabase.GetAssetPath(s)))
                .Take(5)
                .ToList();

            foreach (SquadDefinition squad in recentSquads)
            {
                SquadDefinition captured = squad;
                menu.AppendAction("Open Squad/" + captured.name, _ => OpenSquadEditor(captured));
            }
            menu.AppendSeparator("Open Squad/");
            menu.AppendAction("Open Squad/Browse...", BrowseOpenSquad);
            menu.AppendAction("Create New Squad", CreateNewSquad);
        }
    }

    private static void OpenSquadEditor(SquadDefinition squad)
    {
        SquadDefinitionEditor wnd = GetWindow<SquadDefinitionEditor>();
        wnd.titleContent = new GUIContent("Squad Editor");
        wnd.LoadSquad(squad);
    }

    private void BrowseOpenSquad(DropdownMenuAction action)
    {
        SquadDefinitionEditor wnd = GetWindow<SquadDefinitionEditor>();
        wnd.titleContent = new GUIContent("Squad Editor");

        string path = EditorUtility.OpenFilePanel("Open Squad Definition", "Assets", "asset");
        if (string.IsNullOrEmpty(path)) return;

        string projectRelative = "Assets" + path.Replace("\\", "/")
            .Replace(Application.dataPath.Replace("\\", "/"), "");

        SquadDefinition squad = AssetDatabase.LoadAssetAtPath<SquadDefinition>(projectRelative);
        if (squad != null)
            wnd.LoadSquad(squad);
    }

    private void CreateNewSquad(DropdownMenuAction action)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Squad Definition", "NewSquad", "asset",
            "Create a new SquadDefinition");

        if (string.IsNullOrEmpty(path)) return;

        SquadDefinition squad = CreateInstance<SquadDefinition>();
        squad.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(squad, path);
        EditorUtility.SetDirty(squad);
        AssetDatabase.SaveAssets();

        OpenSquadEditor(squad);
    }

    private void BrowseOpenTree(DropdownMenuAction action)
    {
        if (treeSearchProvider == null)
            treeSearchProvider = ScriptableObject.CreateInstance<TreeSearchProvider>();

        Rect bounds = assetBarMenu.worldBound;
        Vector2 screenPos = new Vector2(position.x + bounds.x + bounds.width, position.y + bounds.y + bounds.height);
        SearchWindow.Open(new SearchWindowContext(screenPos), treeSearchProvider);
    }

    private void CreateTreeAsset<T>() where T : BaseEditorTreeAsset
    {
        string treeTypeName = typeof(T) == typeof(CommanderTreeAsset) ? "Commander Tree" : "Agent Tree";
        string defaultName = typeof(T) == typeof(CommanderTreeAsset) ? "NewCommanderTree" : "NewTree";
        string path = EditorUtility.SaveFilePanelInProject($"Create {treeTypeName}", defaultName, "asset", $"Create a new {treeTypeName}");
        if (string.IsNullOrEmpty(path)) return;

        T treeAsset = CreateInstance<T>();
        treeAsset.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(treeAsset, path);

        treeAsset.nodesList = new System.Collections.Generic.List<BehaviourNode>();
        treeAsset.CreateBlackBoard();
        EditorUtility.SetDirty(treeAsset);
        AssetDatabase.SaveAssets();

        Selection.activeObject = treeAsset;
        BuildAssetBarMenu();
        OnSelectionChange();
    }

    /// <summary>
    /// Context-aware handler for the graph view's "Create New Tree" context menu.
    /// If a GameObject is selected (without a TreeRunner), adds the appropriate
    /// runner component and assigns the new tree. If a runner is selected without
    /// a tree asset, assigns the new tree to it. Otherwise just opens the new tree.
    /// </summary>
    private void HandleCreateNewTreeFromContext(bool isCommander)
    {
        // Determine type
        System.Type assetType = isCommander ? typeof(CommanderTreeAsset) : typeof(AgentTreeAsset);
        string treeTypeName = isCommander ? "Commander Tree" : "Agent Tree";
        string defaultName = isCommander ? "NewCommanderTree" : "NewTree";
        System.Type runnerType = isCommander ? typeof(CommanderTreeRunner) : typeof(AgentTreeRunner);

        string path = EditorUtility.SaveFilePanelInProject($"Create {treeTypeName}", defaultName, "asset", $"Create a new {treeTypeName}");
        if (string.IsNullOrEmpty(path)) return;

        BaseEditorTreeAsset treeAsset = (BaseEditorTreeAsset)CreateInstance(assetType);
        treeAsset.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(treeAsset, path);

        treeAsset.nodesList = new System.Collections.Generic.List<BehaviourNode>();
        treeAsset.CreateBlackBoard();
        EditorUtility.SetDirty(treeAsset);
        AssetDatabase.SaveAssets();

        // ── Context-aware assignment ──────────────────────────
        GameObject selectedGO = Selection.activeGameObject;

        if (selectedGO != null)
        {
            BehaviourTreeRunnerBase existingRunner = selectedGO.GetComponent<BehaviourTreeRunnerBase>();

            if (existingRunner == null)
            {
                // GameObject selected but no runner — add the appropriate runner component
                BehaviourTreeRunnerBase newRunner = (BehaviourTreeRunnerBase)selectedGO.AddComponent(runnerType);
                // Assign the authoring asset via serialized field
                SerializedObject so = new SerializedObject(newRunner);
                SerializedProperty authoringProp = so.FindProperty("authoringAsset");
                if (authoringProp != null)
                {
                    authoringProp.objectReferenceValue = treeAsset;
                    so.ApplyModifiedProperties();
                }
                currentRunner = newRunner;
                Debug.Log($"[BehaviourTreeEditor] Added {runnerType.Name} to '{selectedGO.name}' and assigned '{treeAsset.name}'.");
            }
            else if (existingRunner.GetSourceTree() == null)
            {
                // Runner exists but has no tree — assign the new tree
                SerializedObject so = new SerializedObject(existingRunner);
                SerializedProperty authoringProp = so.FindProperty("authoringAsset");
                if (authoringProp != null)
                {
                    authoringProp.objectReferenceValue = treeAsset;
                    so.ApplyModifiedProperties();
                }
                currentRunner = existingRunner;
                Debug.Log($"[BehaviourTreeEditor] Assigned '{treeAsset.name}' to existing {existingRunner.GetType().Name} on '{selectedGO.name}'.");
            }
        }
        else if (currentRunner != null && currentRunner.GetSourceTree() == null)
        {
            // Runner selected (via previous selection) but no tree asset
            SerializedObject so = new SerializedObject(currentRunner);
            SerializedProperty authoringProp = so.FindProperty("authoringAsset");
            if (authoringProp != null)
            {
                authoringProp.objectReferenceValue = treeAsset;
                so.ApplyModifiedProperties();
            }
            Debug.Log($"[BehaviourTreeEditor] Assigned '{treeAsset.name}' to existing {currentRunner.GetType().Name}.");
        }

        Selection.activeObject = treeAsset;
        BuildAssetBarMenu();
        OnSelectionChange();
    }

    private void SaveTree(DropdownMenuAction action)
    {
        if (currentTree == null) return;
        EditorUtility.SetDirty(currentTree);
        AssetDatabase.SaveAssets();
    }

    private void PollDebugState()
    {
        if (BuildPipeline.isBuildingPlayer) return;
        treeGraphView?.RefreshDebugVisuals(currentRunner);
    }

    private BaseEditorTreeAsset OnSelectTree()
    {
        // When the user directly selects a tree asset in the Project window,
        // prioritise it over any GameObject runner that may also be selected.
        if (Selection.activeObject is BaseEditorTreeAsset treeAsset)
        {
            currentRunner = null;
            trackedVariablesView?.Refresh(null);
            return treeAsset;
        }

        GameObject selected = Selection.activeGameObject;

        if (selected != null && selected.TryGetComponent(out BehaviourTreeRunnerBase runner))
        {
            currentRunner = runner;
            trackedVariablesView?.Refresh(runner);
            return runner.GetSourceTree() as BaseEditorTreeAsset;
        }

        trackedVariablesView?.Refresh(null);
        return null;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
        BlackboardTemplate.Applied -= OnTemplateApplied;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= PollDebugState;

        ClearGraph();
    }

    private void OpenApplyTemplateDialog(DropdownMenuAction action)
    {
        if (currentBlackboardDef == null)
        {
            Debug.LogWarning("[BehaviourTreeEditor] No blackboard definition available — open a tree asset first.");
            return;
        }

        TemplateApplyWindow.OpenForTarget(currentBlackboardDef);
    }

    private void BakeTree(DropdownMenuAction dropdownMenuAction)
    {
        if (currentTree == null || currentBlackboardDef == null) return;

        Debug.Log("Baking Tree!");

        RuntimeBehaviourTreeAsset runtimeAsset = CreateInstance<RuntimeBehaviourTreeAsset>();
        runtimeAsset.name = currentTree.name + "_Runtime";
        runtimeAsset.sourceTree = currentTree;

        runtimeAsset.blackboardDefinition = TreeBaker.BakeTree(currentTree.root, currentTree,
        ref runtimeAsset.runtimeNodeData,
        ref runtimeAsset.runtimeFieldData,
        ref runtimeAsset.fieldTypeNames,
        ref runtimeAsset.boxedConstants,
        ref runtimeAsset.runtimeNodeGuids,
        out runtimeAsset.maxTreeDepth);

        string assetPath = $"Assets/{runtimeAsset.name}.asset";
        AssetDatabase.CreateAsset(runtimeAsset, assetPath);
        if (runtimeAsset.blackboardDefinition != null)
        {
            runtimeAsset.blackboardDefinition.name = runtimeAsset.name + "_BB_Definition";
            AssetDatabase.AddObjectToAsset(runtimeAsset.blackboardDefinition, runtimeAsset);
        }
        AssetDatabase.SaveAssets();
    }

    private bool selectionChangePending;

    private void OnSelectionChange()
    {
        // Evaluated synchronously so the lock/bypass state is read at event time.
        if (selectionIsLocked && currentTree != null && lockBypassDepth == 0) return;

        // Unity frequently fires OnSelectionChange twice for one user action —
        // coalesce into a single deferred pass.
        if (selectionChangePending) return;
        selectionChangePending = true;

        EditorApplication.delayCall += () =>
        {
            selectionChangePending = false;
            if (treeGraphView?.panel == null) return; // window closed before the deferred call ran
            OnSelectionChangeInternal();
        };
    }

    private void OnSelectionChangeInternal()
    {
        BaseEditorTreeAsset selectedAsset = OnSelectTree();

        if (selectedAsset == null) return;

        // Reset proxy flag when a different tree is selected
        if (selectedAsset != currentTree && treeGraphView != null) treeGraphView.DebugProxiesAreSetup = false;

        if (selectedAsset == currentTree)
        {
            // Reconfigure tabs — runner type may have changed (agent ↔ commander)
            // without the tree asset changing
            ConfigureTabsForTreeType();

            // If the runner is stale (destroyed, deselected, or scene changed),
            // fall through to repopulate and reset state.
            bool runnerIsStale = currentRunner == null;

            // In play mode, allow repopulation to set up debug proxies
            bool needsPlayModeRepop = EditorApplication.isPlaying
                && treeGraphView != null
                && !treeGraphView.DebugProxiesAreSetup;

            if (!runnerIsStale && !needsPlayModeRepop)
            {
                treeGraphView?.RefreshTitle();
                return;
            }
        }

        currentTree = selectedAsset;

        // Refresh tracked variables view — the binding group depends on currentTree
        if (selectedAsset != null)
        {
            var swTabs = System.Diagnostics.Stopwatch.StartNew();
            trackedVariablesView?.Refresh(currentRunner);
            squadTabView?.Refresh(currentTree);
            commanderTabView?.Refresh(currentTree);
            swTabs.Stop();
            if (ProfileTreeSwitch) Debug.Log($"[TreeSwitch] Tab refreshes: {swTabs.ElapsedMilliseconds} ms");
        }

        // Null check for tree asset before using it
        if (currentTree == null)
        {
            currentBlackboardDef = null;
            treeGraphView?.ClearView();
            return;
        }

        if (!AssetDatabase.CanOpenAssetInEditor(currentTree.GetEntityId()))
        {
            return;
        }

        if (currentTree.blackboardDefinition == null && currentTree.CommanderBlackboardDefinition == null)
        {
            currentTree.CreateBlackBoard();
        }

        currentBlackboardDef = null;
        currentBlackboardDef = currentTree?.blackboardDefinition;

        // Configure tabs based on tree type
        ConfigureTabsForTreeType();

        // Null check for graph view before using it
        if (treeGraphView != null)
        {
            try
            {
                inspectorView?.ClearView();
                treeGraphView.OnNodeSelected = OnNodeSelectionChanged;
                var swPopulate = System.Diagnostics.Stopwatch.StartNew();
                treeGraphView.PopulateView(currentTree);
                swPopulate.Stop();
                if (ProfileTreeSwitch) Debug.Log($"[TreeSwitch] PopulateView: {swPopulate.ElapsedMilliseconds} ms");
                RecordTreeOpened(currentTree);
                if (blackBoardView != null)
                    blackBoardView.IsSquadContext = currentTree is CommanderTreeAsset;
                BlackboardDefinition bbDef = currentTree is CommanderTreeAsset
                    ? currentTree.CommanderBlackboardDefinition
                    : currentTree.blackboardDefinition;
                var swBb = System.Diagnostics.Stopwatch.StartNew();
                blackBoardView.BuildBlackboardView(bbDef, force: false);
                swBb.Stop();
                if (ProfileTreeSwitch) Debug.Log($"[TreeSwitch] BuildBlackboardView: {swBb.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                currentTree = null;
                currentBlackboardDef = null;
                treeGraphView.ClearView();
            }
        }
    }

    private void ConfigureTabsForTreeType()
    {
        if (tabView == null || commanderTabInstance == null) return;

        // Commander tab visible if:
        // - Runner is CommanderTreeRunner (play mode), OR
        // - Tree asset is CommanderTreeAsset (editor mode, no runner selected)
        bool isCommander = currentRunner is CommanderTreeRunner
            || (currentRunner == null && currentTree is CommanderTreeAsset);
        bool tabInView = commanderTabInstance.parent == tabView;

        if (isCommander && !tabInView)
            tabView.Add(commanderTabInstance);
        else if (!isCommander && tabInView)
            tabView.Remove(commanderTabInstance);
    }

    private void OnProjectChanged()
    {
        BuildAssetBarMenu();

        if (currentTree == null) return;

        if (!AssetDatabase.Contains(currentTree))
        {
            currentTree = null;
            currentBlackboardDef = null;
            currentRunner = null;
            treeGraphView?.ClearView();
            return;
        }

        treeGraphView?.RefreshTitle();
    }

    private void OnNodeSelectionChanged(BehaviourNodeView nodeView)
    {
        inspectorView.UpdateSelection(nodeView);
        if (tabView != null && inspectorTab != null)
            tabView.activeTab = inspectorTab;
    }

    private void ClearGraph()
    {
        currentTree = null;
        currentBlackboardDef = null;
        currentRunner = null;

        if (treeGraphView != null)
        {
            treeGraphView.OnNodeSelected = null;
            treeGraphView.ClearView();
            treeGraphView.Dispose();
        }

        if (treeSearchProvider != null)
        {
            DestroyImmediate(treeSearchProvider);
            treeSearchProvider = null;
        }
    }

    private static void RecordTreeOpened(BaseEditorTreeAsset treeAsset)
    {
        if (treeAsset == null) return;
        recentOpenedTrees.Remove(treeAsset);
        recentOpenedTrees.Add(treeAsset);
        while (recentOpenedTrees.Count > maxRecentTrees)
            recentOpenedTrees.RemoveAt(0);
    }

    private static int GetCurrentTreeRecentIndex()
    {
        if (currentTree == null) return -1;
        return recentOpenedTrees.IndexOf(currentTree);
    }

    private static void CycleToRecentTree(int direction)
    {
        if (recentOpenedTrees.Count < 2) return;
        int idx = GetCurrentTreeRecentIndex();
        if (idx < 0) return;

        idx = (idx + direction + recentOpenedTrees.Count) % recentOpenedTrees.Count;
        var target = recentOpenedTrees[idx];
        if (target == currentTree) return;

        lockBypassDepth++;
        try
        {
            Selection.activeObject = target;
            AssetDatabase.OpenAsset(target);
        }
        finally
        {
            lockBypassDepth--;
        }
    }

    private static void CycleToPreviousRecentTree()
    {
        CycleToRecentTree(-1);
    }

    private static void CycleToNextRecentTree()
    {
        CycleToRecentTree(1);
    }
}
