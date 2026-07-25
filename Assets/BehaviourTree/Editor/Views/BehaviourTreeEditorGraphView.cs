using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine;
using BehaviourTree.Core;
using BehaviourTree.Runtime;

namespace BehaviourTree.Editor
{
    [UxmlElement("BTGraphView")]
    public partial class BehaviourTreeEditorGraphView : GraphView
    {
        static BehaviourTreeEditorGraphView()
        {
            EditorSceneManager.sceneOpened += (_, _) => runnersCacheValid = false;
        }

        private static List<BehaviourTreeRunnerBase> cachedRunners;
        private static bool runnersCacheValid;

        // Callback for when graph changes (hook up export logic here)
        public Action<BehaviourTreeEditorGraphView> onGraphDataChanged;
        public Action<BehaviourNodeView> OnNodeSelected;

        /// <summary>
        /// Called when the user requests creating a new tree from the context menu.
        /// Parameter: true for CommanderTree, false for AgentTree.
        /// </summary>
        public Action<bool> OnCreateNewTreeRequested;
        public Action OnCyclePrevious;
        public Action OnCycleNext;
        public Toggle lockToggle;
        
        private BaseEditorTreeAsset tree;
        private Dictionary<string, BehaviourNodeView> nodeViewDict;
        private NodeSearchProvider searchWindow;
        private TextField graphTitleLabel;
        private Label graphTitleBadge;
        private GridBackground gridBackground;
        private VisualElement backgroundTint;
        private CopyPasteHandler copyPasteHandler;
        private BtEdgeConnectorListener edgeConnectorListener;
        private RuntimeDebugManager runtimeDebugManager;
        private SubtreeExtractor subtreeExtractor;
        private List<Port> compatiblePortsCache = new List<Port>();
        private bool debugProxiesAreSetup;
        private DropdownField runnerDropdown;
        private List<BehaviourTreeRunnerBase> availableRunners = new List<BehaviourTreeRunnerBase>();
        private bool refreshingRunnerDropdown;
        private bool eventsSubscribed;

        private void SubscribeEvents()
        {
            if (eventsSubscribed) return;
            graphViewChanged += OnGraphViewChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!eventsSubscribed) return;
            graphViewChanged -= OnGraphViewChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            eventsSubscribed = false;
        }

        public bool HasTree => tree != null;
        public bool DebugProxiesAreSetup
        {
            get => debugProxiesAreSetup;
            set => debugProxiesAreSetup = value;
        }

        public BehaviourTreeEditorGraphView()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(BehaviourTreeEditorPaths.EditorUss);
            styleSheets.Add(styleSheet);

            // Port styles live once on the graph root — USS cascades to all port instances.
            var portStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(BehaviourTreeEditorPaths.BehaviourPortUss);
            if (portStyleSheet != null)
                styleSheets.Add(portStyleSheet);

            nodeViewDict = new Dictionary<string, BehaviourNodeView>();
            runtimeDebugManager = new RuntimeDebugManager(this);
            subtreeExtractor = new SubtreeExtractor(this);

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            // Standard manipulators for pan, select, box-select
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            AddGrid();
            AddGraphTitle();
            EnsureSearchWindow();
            edgeConnectorListener = new BtEdgeConnectorListener(this);
            
            //Setup copy/paste callbacks
            copyPasteHandler = new CopyPasteHandler(this);

            serializeGraphElements = SerializeGraphSelection;
            unserializeAndPaste = UnSerializeAndPaste;

            //Listen for graph changes to trigger auto-save/compile
            SubscribeEvents();
        }

        private void UnSerializeAndPaste(string operationName, string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            // Restore the static clipboard from the string
            copyPasteHandler.DeserializeClipboard(data);

            // Paste the nodes using the handler’s existing logic
            copyPasteHandler.PasteNodes(tree);
        }

        private string SerializeGraphSelection(IEnumerable<GraphElement> elements)
        {
            copyPasteHandler.CopySelectedNodes();

            return copyPasteHandler.SerializeClipboard();
        }

        private void AddGrid()
        {
            // Tinted background layer — sits behind the grid lines
            backgroundTint = new VisualElement
            {
                name = "GraphBackgroundTint"
            };
            backgroundTint.StretchToParentSize();
            backgroundTint.pickingMode = PickingMode.Ignore;
            Insert(0, backgroundTint);

            // Grid background (transparent, grid lines only)
            gridBackground = new GridBackground();
            Insert(1, gridBackground);
            gridBackground.StretchToParentSize();
        }

        private void AddGraphTitle()
        {
            VisualElement titleContainer = new VisualElement
            {
                name = "GraphTitle",
                style = { flexDirection = FlexDirection.Column, alignItems = Align.FlexStart }
            };

            // ── Title bar row (badge + text field + dropdown) ─
            VisualTreeAsset titleBarAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.GraphTitleBarUxml);
            VisualElement titleRow = titleBarAsset.CloneTree();
            graphTitleBadge = titleRow.Q<Label>("GraphTitleBadge");
            graphTitleLabel = titleRow.Q<TextField>("GraphTitleTextField");
            runnerDropdown = titleRow.Q<DropdownField>("RunnerDropdown");

            // Style the inner input element of the text field
            graphTitleLabel.ClearClassList();
            VisualElement input = graphTitleLabel.Q<VisualElement>("unity-text-input");
            input.name = "GraphTitleInput";
            input.ClearClassList();

            graphTitleLabel.RegisterValueChangedCallback(OnGraphTitleChanged);

            runnerDropdown.RegisterValueChangedCallback(OnRunnerDropdownChanged);

            // ── Cycle buttons row (◀ ▶) ─────────────────────
            VisualTreeAsset controlsAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.GraphTitleControlsUxml);
            VisualElement controlsRow = controlsAsset.CloneTree();

            // Insert lock toggle from its own UXML at the front
            VisualTreeAsset lockToggleAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.LockToggleUxml);
            VisualElement lockToggleElement = lockToggleAsset.CloneTree();
            lockToggle = lockToggleElement.Q<Toggle>("LockToggle");

            // Load lock icon sprites
            var lockedIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BehaviourTree/Editor/UITextures/locked-icon.asset");
            var unlockedIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BehaviourTree/Editor/UITextures/unlocked-icon.asset");

            void UpdateLockIcon(bool locked)
            {
                Sprite icon = locked ? lockedIcon : unlockedIcon;
                if (icon != null)
                    lockToggle.style.backgroundImage = new StyleBackground(Background.FromSprite(icon));
            }

            lockToggle.RegisterValueChangedCallback(evt =>
            {
                BehaviourTreeEditor.selectionIsLocked = evt.newValue;
                UpdateLockIcon(evt.newValue);
            });
            UpdateLockIcon(lockToggle.value);
            controlsRow.Insert(0, lockToggleElement);

            Button prevBtn = controlsRow.Q<Button>("CyclePrevBtn");
            prevBtn.clicked += () => OnCyclePrevious?.Invoke();

            Button nextBtn = controlsRow.Q<Button>("CycleNextBtn");
            nextBtn.clicked += () => OnCycleNext?.Invoke();

            titleContainer.Add(titleRow);
            titleContainer.Add(controlsRow);
            Add(titleContainer);
        }

        public void EnsureSearchWindow()
        {
            if(searchWindow == null)
            {
                searchWindow = ScriptableObject.CreateInstance<NodeSearchProvider>();

                searchWindow.Initialize(this);
            }

            nodeCreationRequest = context =>
            {
                OpenNodeSearchAtScreenPosition(context.screenMousePosition);
            };
        }

        private void OpenNodeSearchAtScreenPosition(Vector2 screenPosition)
        {
            EnsureSearchWindow();
            if (tree == null) return;
            if (EditorWindow.focusedWindow == null) return;

            Rect windowRect = EditorWindow.focusedWindow.position;
            Vector2 localPos = this.ChangeCoordinatesTo(contentViewContainer,
                this.WorldToLocal(screenPosition - new Vector2(windowRect.x, windowRect.y)));

            searchWindow.SetCreationPosition(localPos);
            searchWindow.ClearPendingConnection();
            OpenSearchWindow(screenPosition);
        }

        private void OpenSearchWindow(Vector2 mousePosition)
        {
            EnsureSearchWindow();
            if (tree == null) return;

            // Invalidate cached method lists if tree type changed
            bool typeChanged = (searchWindow.currentTreeAsset is CommanderTreeAsset) != (tree is CommanderTreeAsset);
            searchWindow.currentTreeAsset = tree;
            if (typeChanged)
                searchWindow.InvalidateCache();

            SearchWindow.Open(new SearchWindowContext(mousePosition), searchWindow);
        }

        private void SetupEdgeConnectors(BehaviourNodeView nodeView)
        {
            if (nodeView?.input != null)
            {
                var inputEC = new EdgeConnector<Edge>(edgeConnectorListener);
                nodeView.input.AddManipulator(inputEC);
                if (nodeView.input is BehaviourPort bp)
                    bp.SetEdgeConnector(inputEC);
            }

            if (nodeView?.output != null)
            {
                var outputEC = new EdgeConnector<Edge>(edgeConnectorListener);
                nodeView.output.AddManipulator(outputEC);
                if (nodeView.output is BehaviourPort bp)
                    bp.SetEdgeConnector(outputEC);
            }
        }

        private void OpenSearchWindowForEdgeDrop(Port startPort, Vector2 graphMousePosition)
        {
            if (startPort == null) return;
            EnsureSearchWindow();
            if (searchWindow == null) return;
            if (tree == null) return;
            if (EditorWindow.focusedWindow == null) return;

            Vector2 screenPos = GUIUtility.GUIToScreenPoint(graphMousePosition);
            Rect windowRect = EditorWindow.focusedWindow.position;
            Vector2 windowMouse = screenPos - new Vector2(windowRect.x, windowRect.y);
            Vector2 localPos = this.ChangeCoordinatesTo(contentViewContainer, this.WorldToLocal(windowMouse));

            searchWindow.SetCreationPosition(localPos);
            searchWindow.SetPendingConnection(startPort);
            OpenSearchWindow(screenPos);
        }

        public void ClearView()
        {
            tree = null;
            debugProxiesAreSetup = false;
            graphTitleLabel.SetValueWithoutNotify("Behaviour Tree");
            if (graphTitleBadge != null)
                graphTitleBadge.style.display = DisplayStyle.None;
            if (backgroundTint != null)
                backgroundTint.style.backgroundColor = GraphEditorTheme.instance.graphBgAgent;

            UnsubscribeEvents();
            try
            {
                CleanupGraphElements();
                DeleteElements(graphElements);
                nodeViewDict.Clear();
            }
            finally
            {
                SubscribeEvents();
            }

            RefreshRunnerDropdown();
        }

        private void CleanupGraphElements()
        {
            foreach (GraphElement element in graphElements.ToList())
            {
                if (element is BehaviourNodeView nodeView) nodeView.Cleanup();
                else if (element is GraphNote note) note.Unbind();
            }
        }

        public void Dispose()
        {
            UnsubscribeEvents();

            if (graphTitleLabel != null)
                graphTitleLabel.UnregisterValueChangedCallback(OnGraphTitleChanged);

            if (runnerDropdown != null)
                runnerDropdown.UnregisterValueChangedCallback(OnRunnerDropdownChanged);

            if (searchWindow != null)
            {
                searchWindow.Shutdown();
                UnityEngine.Object.DestroyImmediate(searchWindow);
                searchWindow = null;
            }
        }

        public bool TryConnectPorts(Port from, Port to)
        {
            if (from == null || to == null) return false;
            if (from.direction == to.direction) return false;

            Port output = from.direction == Direction.Output ? from : to;
            Port input = from.direction == Direction.Input ? from : to;

            if (!GetCompatiblePorts(output, null).Contains(input)) return false;

            BehaviourNodeView parentView = output.node as BehaviourNodeView;
            BehaviourNodeView childView = input.node as BehaviourNodeView;

            if (parentView == null || childView == null) return false;
            if (tree == null) return false;

            changeNotificationSuppression++;
            try { RemoveConflictingEdges(output, input); }
            finally { changeNotificationSuppression--; }

            tree.AddChild(parentView.NodeSO, childView.NodeSO);
            parentView.SortChildren();
            EditorUtility.SetDirty(parentView.NodeSO);

            Edge edge = output.ConnectTo(input);
            AddElement(edge);

            NotifyGraphDataChanged();
            return true;
        }

        private int changeNotificationSuppression;

        private void NotifyGraphDataChanged()
        {
            if (changeNotificationSuppression > 0) return;
            onGraphDataChanged?.Invoke(this);
        }

        private void RemoveConflictingEdges(Port output, Port input, Edge keepEdge = null)
        {
            List<GraphElement> toRemove = new List<GraphElement>();

            if (input.capacity == Port.Capacity.Single)
                foreach (Edge e in input.connections)
                    if (e != keepEdge) toRemove.Add(e);

            if (output.capacity == Port.Capacity.Single)
                foreach (Edge e in output.connections)
                    if (e != keepEdge) toRemove.Add(e);

            foreach (GraphElement ge in toRemove)
                if (ge is Edge e && e.input?.node is BehaviourNodeView child)
                    child.HideOrderNumber();

            if (toRemove.Count > 0)
                DeleteElements(toRemove);
        }

        private void OnUndoRedo()
        {
            if(tree == null) return;
            
            if (tree.NeedsNodesListSync())
                tree.SyncNodesListFromAssets();

            PopulateView(tree);
        }

        // Simple cycle detection: can't connect if 'target' is an ancestor of 'source'.
        // The visited set also protects against hand-corrupted graphs that already contain
        // a cycle — without it this walk loops forever and hangs the editor.
        private bool WouldCreateCycle(BehaviourNodeView source, BehaviourNodeView target)
        {
            var visited = new HashSet<BehaviourNodeView>();
            BehaviourNodeView current = target;
            while (current != null && visited.Add(current))
            {
                if(current.input == null) return false;
                if (current == source && current.input.capacity != Port.Capacity.Single) return true;
                current = GetParent(current);
            }
            return false;
        }

        private static BehaviourNodeView GetParent(BehaviourNodeView node)
        {
            Port inputPort = node.input;
            if (inputPort == null) return null;
            foreach (Edge edge in inputPort.connections)
            {
                if (edge.output?.node is BehaviourNodeView parent)
                    return parent;
            }
            return null;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (tree == null) return change;

            if (EditorApplication.isPlaying)
            {
                // Play mode: the view mirrors the running tree. Block structural edits
                // entirely so the visual graph and the model cannot desync.
                change.edgesToCreate?.Clear();
                change.elementsToRemove?.Clear();
            }

            if (change.elementsToRemove != null)
                HandleElementRemoval(change.elementsToRemove);

            if (change.edgesToCreate != null)
                HandleEdgeCreation(change.edgesToCreate);

            if (change.movedElements != null)
                HandleElementsMoved(change.movedElements);

            // Defer — edges/nodes aren't parented to the graph yet at this point
            schedule.Execute(() => RefreshAllNodeIcons());

            return change;
        }

        private void HandleElementRemoval(List<GraphElement> elementsToRemove)
        {
            for (int i = 0; i < elementsToRemove.Count; i++)
            {
                if (elementsToRemove[i] is BehaviourNodeView nodeView)
                {
                    nodeView.Cleanup();
                    tree.DeleteNode(nodeView.NodeSO);
                    nodeViewDict.Remove(nodeView.Guid);
                }
                else if (elementsToRemove[i] is Edge edge)
                {
                    BehaviourNodeView parentView = edge.output?.node as BehaviourNodeView;
                    BehaviourNodeView childView = edge.input?.node as BehaviourNodeView;
                    if (parentView != null && childView != null)
                    {
                        tree.RemoveChild(parentView.NodeSO, childView.NodeSO);
                        childView.HideOrderNumber();
                    }
                }
                else if (elementsToRemove[i] is GraphNote graphNote)
                {
                    graphNote.Unbind();
                    if (graphNote.Data != null)
                        tree.editorNotes.Remove(graphNote.Data);
                    EditorUtility.SetDirty(tree);
                }
            }

            NotifyGraphDataChanged();
        }

        private void HandleEdgeCreation(List<Edge> edgesToCreate)
        {
            if (EditorApplication.isPlaying) return;

            for (int i = 0; i < edgesToCreate.Count; i++)
            {
                Edge edge = edgesToCreate[i];
                if (edge?.output?.node is not BehaviourNodeView parentView) continue;
                if (edge?.input?.node is not BehaviourNodeView childView) continue;

                tree.AddChild(parentView.NodeSO, childView.NodeSO);
                parentView.SortChildren();
                EditorUtility.SetDirty(parentView.NodeSO);
            }

            NotifyGraphDataChanged();
        }

        private void HandleElementsMoved(List<GraphElement> movedElements)
        {
            foreach (GraphElement element in movedElements)
            {
                if (element is BehaviourNodeView nodeView)
                {
                    nodeView.SortChildren();
                    GetParent(nodeView)?.SortChildren();
                }
                else if (element is GraphNote note)
                {
                    note.PersistLayout();
                    if (tree != null) EditorUtility.SetDirty(tree);
                }
            }
        }

        private GraphNote CreateNoteFromData(EditorNoteData data)
        {
            GraphNote note = new GraphNote();
            note.Bind(data, () => EditorUtility.SetDirty(tree));
            return note;
        }

        public BehaviourNodeView CreateNodeView(BehaviourNode node)
        {
            BehaviourNodeView nodeView = BuildNodeView(node);
            RegisterNodeView(nodeView);
            return nodeView;
        }

        private BehaviourNodeView BuildNodeView(BehaviourNode node)
        {
            BehaviourNodeView nodeView = new BehaviourNodeView(node);
            nodeView.OnNodeSelected = OnNodeSelected;
            nodeView.GraphView = this;
            nodeView.layer = 0;
            return nodeView;
        }

        private void RegisterNodeView(BehaviourNodeView nodeView)
        {
            AddElement(nodeView);
            SetupEdgeConnectors(nodeView);

            if (!nodeViewDict.TryAdd(nodeView.Guid, nodeView))
                Debug.LogWarning($"Duplicate GUID in graph view: {nodeView.Guid}");
        }

        public BehaviourNodeView FindNodeView(BehaviourNode node) 
        {
            if(nodeViewDict.TryGetValue(node.guid, out BehaviourNodeView nodeView)) 
            {
                return nodeView;
            }
            
            return null;
        }

        public BehaviourNodeView CreateCompositeNode(string methodName, Vector2 position)
        {
            CompositeNode node = (CompositeNode)tree.CreateNode(typeof(CompositeNode));

            BehaviourNodeType compositeType = MethodRegistry.GetCategory(methodName);
            if (compositeType != BehaviourNodeType.COMPOSITE)
                compositeType = BehaviourNodeType.COMPOSITE;

            node.SetCompositeType(compositeType);
            node.name = methodName;
            node.methodName = methodName;
            node.nodeName = string.Empty;
            node.graphPosition = position;
            tree.RegisterNode(node);
            return CreateNodeView(node);
        }
        
        public BehaviourNodeView CreateLeafNode(string methodName, Vector2 position, BehaviourNodeType leafType = BehaviourNodeType.ACTION)
        {
            LeafNode node = (LeafNode)tree.CreateNode(typeof(LeafNode));
            //Undo.RecordObject(node, "(BTree) Configure Node");
            node.SetLeafType(leafType);
            node.methodName = methodName;
            node.name = !string.IsNullOrEmpty(methodName) ? methodName : "Untitled";
            node.nodeName = string.Empty;
            node.graphPosition = position;
            tree.RegisterNode(node);

            return CreateNodeView(node);
        }

        public BehaviourNodeView CreateDecoratorNode(string methodName, Vector2 position)
        {
            DecoratorNode node = (DecoratorNode)tree.CreateNode(typeof(DecoratorNode));
            //Undo.RecordObject(node, "(BTree) Configure Node");
            node.methodName = methodName;
            node.name = !string.IsNullOrEmpty(methodName) ? methodName : "Untitled";
            node.nodeName = string.Empty;
            node.graphPosition = position;
            tree.RegisterNode(node);
            
            return CreateNodeView(node);
        }

        public BehaviourNodeView CreateSubtreeNode(Vector2 position)
        {
            SubtreeNode node = (SubtreeNode)tree.CreateNode(typeof(SubtreeNode));
            node.name = "SUBTREE";
            node.nodeName = string.Empty;
            node.graphPosition = position;
            tree.RegisterNode(node);

            return CreateNodeView(node);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            compatiblePortsCache.Clear();

            foreach (Port port in ports)
            {
                if (IsSelfOrSameNode(port, startPort)) continue;
                if (IsWrongDirection(port, startPort)) continue;

                if (startPort.node is BehaviourNodeView startNode && port.node is BehaviourNodeView endNode)
                {
                    if (IsRootAsChild(endNode, port)) continue;
                    if (IsLeafNodeParenting(startNode, port)) continue;
                    if (IsChild(endNode, startNode)) continue;
                    if (WouldCreateCycle(startNode, endNode)) continue;
                }

                compatiblePortsCache.Add(port);
            }

            return compatiblePortsCache;
        }

        private static bool IsSelfOrSameNode(Port port, Port startPort)
        {
            return port == startPort || port.node == startPort.node;
        }

        private static bool IsWrongDirection(Port port, Port startPort)
        {
            return port.direction == startPort.direction;
        }

        private static bool IsRootAsChild(BehaviourNodeView endNode, Port port)
        {
            return endNode.NodeSO.NodeType == BehaviourNodeType.ROOT
                && port.direction == Direction.Input;
        }

        private static bool IsLeafNodeParenting(BehaviourNodeView startNode, Port port)
        {
            if (port.direction != Direction.Input) return false;

            BehaviourNodeType nodeType = startNode.NodeSO.NodeType;
            return nodeType == BehaviourNodeType.ACTION
                || nodeType == BehaviourNodeType.CONDITION
                || nodeType == BehaviourNodeType.SUBTREE;
        }

        private static bool IsChild(BehaviourNodeView endNode, BehaviourNodeView startNode)
        {
            return endNode.NodeSO.children.Contains(startNode.NodeSO);
        }

        //Build dropdown menu
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            // ── Create New Tree (always available) ─────────────────────
            evt.menu.AppendAction("Create New Tree/Agent Tree", _ =>
            {
                OnCreateNewTreeRequested?.Invoke(false);
            });
            evt.menu.AppendAction("Create New Tree/Commander Tree", _ =>
            {
                OnCreateNewTreeRequested?.Invoke(true);
            });
            evt.menu.AppendSeparator();

            Vector2 screenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            evt.menu.AppendAction("Create Node", _ =>
            {
                OpenNodeSearchAtScreenPosition(screenPosition);
            }, _ => tree == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);

            evt.menu.AppendAction("Subtree/Extract Selection To Subtree", _ =>
            {
                subtreeExtractor.Extract(selection, tree, PopulateView);
            }, _ => subtreeExtractor.CanExtract(selection, tree) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction("Subtree/Copy Selection Into Subtree", _ =>
            {
                subtreeExtractor.Extract(selection, tree, PopulateView, deleteOriginals: false);
            }, _ => subtreeExtractor.CanExtract(selection, tree) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            Vector2 mouseLocal = evt.localMousePosition;
            evt.menu.AppendAction("Add Note", _ =>
            {
                Vector2 localPos = this.ChangeCoordinatesTo(contentViewContainer, mouseLocal);
                EditorNoteData noteData = new EditorNoteData
                {
                    position = localPos,
                };
                tree.editorNotes.Add(noteData);
                GraphNote note = CreateNoteFromData(noteData);
                AddElement(note);
                EditorUtility.SetDirty(tree);
            }, _ => tree == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
        }

        public void PopulateView(BaseEditorTreeAsset tree)
        {
            if (!tree)
            {
                Debug.LogWarning("No treeAsset selected");
                return;
            }

            InitTree(tree);
            ApplyGridTint(tree);
            ClearAndRebuildViews();
            EnsureRootNodeExists();
            CleanupAndCreateViews();
            CleanupAndWireEdges();

            // Initialize child order number labels
            foreach (BehaviourNodeView nodeView in nodeViewDict.Values)
                nodeView.SortChildren();

            // Icons depend on edges being wired (parent traversal)
            RefreshAllNodeIcons();

            if (tree.editorNotes != null)
            {
                foreach (EditorNoteData noteData in tree.editorNotes)
                {
                    GraphNote note = CreateNoteFromData(noteData);
                    AddElement(note);
                }
            }

            if (EditorApplication.isPlaying && !debugProxiesAreSetup)
            {
                BehaviourTreeRunnerBase runner = BehaviourTreeEditor.currentRunner;
                if (runner != null)
                {
                    runtimeDebugManager.SetupDebugProxies(runner, nodeViewDict);
                    debugProxiesAreSetup = true;
                    // Proxy nodes need their abort/warning icons refreshed since they were created after initial icon pass
                    RefreshAllNodeIcons();
                }
            }

            RegisterCallback<GeometryChangedEvent>(OnGeometryChangedForFrameAll);
        }

        private void OnGeometryChangedForFrameAll(GeometryChangedEvent evt)
        {
            UnregisterCallback<GeometryChangedEvent>(OnGeometryChangedForFrameAll);
            FrameAll();
        }

        private void InitTree(BaseEditorTreeAsset tree)
        {
            this.tree = tree;
            RefreshTitle();

            if (tree.nodesList == null)
                tree.nodesList = new List<BehaviourNode>();
        }

        private void ApplyGridTint(BaseEditorTreeAsset treeAsset)
        {
            if (backgroundTint == null) return;
            if (treeAsset is CommanderTreeAsset)
                backgroundTint.style.backgroundColor = GraphEditorTheme.instance.graphBgCommander;
            else
                backgroundTint.style.backgroundColor = GraphEditorTheme.instance.graphBgAgent;
        }

        public void RefreshTitle()
        {
            if (graphTitleLabel == null) return;

            string treeName = tree != null ? tree.name : null;

            if (treeName == null)
            {
                graphTitleLabel.SetValueWithoutNotify("Behaviour Tree");
            }
            else
            {
                graphTitleLabel.SetValueWithoutNotify(treeName);
            }

            if (graphTitleBadge != null)
            {
                if (tree == null)
                {
                    graphTitleBadge.style.display = DisplayStyle.None;
                }
                else if (tree is CommanderTreeAsset)
                {
                    graphTitleBadge.text = "[C]";
                    graphTitleBadge.style.color = GraphEditorTheme.instance.badgeCommander;
                    graphTitleBadge.style.display = DisplayStyle.Flex;
                }
                else
                {
                    graphTitleBadge.text = "[A]";
                    graphTitleBadge.style.color = GraphEditorTheme.instance.badgeAgent;
                    graphTitleBadge.style.display = DisplayStyle.Flex;
                }
            }

            RefreshRunnerDropdown();
        }

        private void RefreshRunnerDropdown()
        {
            if (runnerDropdown == null) return;
            refreshingRunnerDropdown = true;

            if (!runnersCacheValid || cachedRunners == null)
            {
                cachedRunners = new List<BehaviourTreeRunnerBase>();
                cachedRunners.AddRange(UnityEngine.Object.FindObjectsByType<BehaviourTreeRunnerBase>(FindObjectsInactive.Include, FindObjectsSortMode.None));
                runnersCacheValid = true;
            }

            // Remove destroyed entries from cache (e.g. objects destroyed by entering play mode)
            cachedRunners.RemoveAll(r => r == null);

            availableRunners.Clear();
            availableRunners.AddRange(cachedRunners);
            runnerDropdown.choices = availableRunners.Select(r => r.gameObject.name).ToList();

            string goName = BehaviourTreeEditor.currentRunner?.gameObject?.name;
            if (!string.IsNullOrEmpty(goName) && availableRunners.Any(r => r != null && r.gameObject.name == goName))
                runnerDropdown.value = goName;
            else
                runnerDropdown.value = "";

            refreshingRunnerDropdown = false;
        }

        private void OnGraphTitleChanged(ChangeEvent<string> evt)
        {
            if (tree == null)
            {
                graphTitleLabel.SetValueWithoutNotify("Behaviour Tree");
                return;
            }

            string newName = evt.newValue?.Trim();

            if (string.IsNullOrEmpty(newName) || newName == tree.name)
            {
                RefreshTitle();
                return;
            }

            if (EditorApplication.isPlaying)
            {
                RefreshTitle();
                return;
            }

            string path = AssetDatabase.GetAssetPath(tree);
            if (string.IsNullOrEmpty(path))
            {
                RefreshTitle();
                return;
            }

            string err = AssetDatabase.RenameAsset(path, newName);
            if (!string.IsNullOrEmpty(err))
            {
                Debug.LogError(err);
                RefreshTitle();
                return;
            }

            AssetDatabase.SaveAssets();
            RefreshTitle();
        }

        private void OnRunnerDropdownChanged(ChangeEvent<string> evt)
        {
            if (refreshingRunnerDropdown) return;
            var runner = availableRunners.FirstOrDefault(r => r != null && r.gameObject.name == evt.newValue);
            if (runner != null)
            {
                BehaviourTreeEditor.lockBypassDepth++;
                try { Selection.activeGameObject = runner.gameObject; }
                finally { BehaviourTreeEditor.lockBypassDepth--; }
            }
        }

        public void RefreshAllNodeIcons()
        {
            foreach (VisualElement child in graphElements.ToList())
            {
                if (child is BehaviourNodeView nodeView)
                    nodeView.RefreshNodeIcons();
            }
        }

        private void ClearAndRebuildViews()
        {
            debugProxiesAreSetup = false;
            UnsubscribeEvents();
            try
            {
                CleanupGraphElements();
                DeleteElements(graphElements);
                nodeViewDict.Clear();
                runtimeDebugManager.ClearCaches();
            }
            finally { SubscribeEvents(); }
        }

        private void EnsureRootNodeExists()
        {
            if (tree.root != null) return;

            tree.root = tree.CreateNode(typeof(RootNode));
            tree.root.name = "ROOT";
            tree.RegisterNode(tree.root);
        }

        private void CleanupAndCreateViews()
        {
            for (int i = tree.nodesList.Count - 1; i >= 0; i--)
            {
                if (tree.nodesList[i] == null)
                {
                    tree.nodesList.RemoveAt(i);
                    continue;
                }
                CreateNodeView(tree.nodesList[i]);
            }
        }

        private void CleanupAndWireEdges()
        {
            for (int i = 0; i < tree.nodesList.Count; i++)
            {
                BehaviourNode node = tree.nodesList[i];

                for (int j = node.children.Count - 1; j >= 0; j--)
                {
                    if (node.children[j] == null)
                        node.children.RemoveAt(j);
                }

                for (int j = 0; j < node.children.Count; j++)
                {
                    BehaviourNode child = node.children[j];

                    BehaviourNodeView parentView = FindNodeView(node);
                    if (parentView == null && node != null)
                        parentView = CreateNodeView(node);

                    BehaviourNodeView childView = FindNodeView(child);
                    if (childView == null && child != null)
                        childView = CreateNodeView(child);

                    if (parentView == null || childView == null)
                    {
                        Debug.LogWarning($"Failed to connect edge: {node.guid} → {child?.guid}");
                        continue;
                    }

                    if (parentView.output == null || childView.input == null)
                    {
                        Debug.LogWarning($"Port missing: {node.NodeType} → {node.children[j].NodeType}");
                        continue;
                    }

                    Edge edge = parentView.output.ConnectTo(childView.input);
                    AddElement(edge);
                }
            }
        }
    
        public void RefreshDebugVisuals(BehaviourTreeRunnerBase runner)
        {
            runtimeDebugManager.RefreshDebugVisuals(runner, nodeViewDict);
        }

        public void ClearRuntimeDebugProxies()
        {
            debugProxiesAreSetup = false;
            runtimeDebugManager.RemoveAllProxies();
            foreach (BehaviourNodeView nodeView in nodeViewDict.Values)
            {
                nodeView?.SetDebugState(NodeState.NONE);
            }
        }

        // public void SetupRuntimeDebugProxies(BehaviourTreeRunnerBase runner)
        // {
        //     runtimeDebugManager.SetupDebugProxies(runner, nodeViewDict);    
        // }


        private sealed class BtEdgeConnectorListener : IEdgeConnectorListener
        {
            private readonly BehaviourTreeEditorGraphView graphView;

            public BtEdgeConnectorListener(BehaviourTreeEditorGraphView graphView)
            {
                this.graphView = graphView;
            }
            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                Port startPort = edge?.output ?? edge?.input;

                edge?.output?.Disconnect(edge);
                edge?.input?.Disconnect(edge);
                edge?.RemoveFromHierarchy();

                if (startPort != null && startPort.direction == Direction.Output)
                    graphView.OpenSearchWindowForEdgeDrop(startPort, position);
            }

            public void OnDrop(GraphView graphView, Edge edge)
            {
                if (graphView == null || edge == null) return;
                if (edge.input == null || edge.output == null) return;

                this.graphView.RemoveConflictingEdges(edge.output, edge.input, edge);

                edge.input.Connect(edge);
                edge.output.Connect(edge);

                var edgesToCreate = new List<Edge> { edge };
                var graphViewChange = new GraphViewChange { edgesToCreate = edgesToCreate };
                graphViewChange = graphView.graphViewChanged?.Invoke(graphViewChange) ?? graphViewChange;

                if (graphViewChange.edgesToCreate != null)
                {
                    for (int i = 0; i < graphViewChange.edgesToCreate.Count; i++)
                        graphView.AddElement(graphViewChange.edgesToCreate[i]);
                }
            }
        }
    }
}
