using System.Collections.Generic;
using System.Linq;
using BehaviourTree.Core;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Standalone editor window for BlackboardTemplate assets.
    /// Provides a BlackBoardView for editing template variables
    /// and an "Apply Template..." button that opens a modal popup.
    /// </summary>
    public class BlackboardTemplateEditorWindow : EditorWindow
    {
        private BlackboardTemplate currentTemplate;
        private ToolbarMenu templateBarMenu;
        private BlackBoardView templateBlackBoardView;
        private Button createNewTemplateButton;
        private Button browseTemplateButton;
        private Label templateDefinitionNameLabel;
        private Button applyButton;

        [MenuItem("BehaviourTree/Open Template Editor", priority = 31)]
        public static void OpenWindow()
        {
            BlackboardTemplateEditorWindow wnd = GetWindow<BlackboardTemplateEditorWindow>();
            wnd.titleContent = new GUIContent("Blackboard Template Editor");
        }

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            if (Selection.activeObject is BlackboardTemplate template)
            {
                BlackboardTemplateEditorWindow wnd = GetWindow<BlackboardTemplateEditorWindow>();
                wnd.titleContent = new GUIContent("Blackboard Template Editor");
                wnd.LoadTemplate(template);
                return true;
            }
            return false;
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.TemplateEditorUxml);

            if (visualTree == null)
            {
                Debug.LogError("Failed to load BlackboardTemplateEditor.uxml");
                return;
            }

            VisualElement rootVisual = visualTree.CloneTree();
            rootVisual.style.flexGrow = 1;
            rootVisualElement.Add(rootVisual);

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                BehaviourTreeEditorPaths.TemplateEditorUss);
            if (styleSheet != null)
                rootVisual.styleSheets.Add(styleSheet);

            // ── Query elements ──
            templateBlackBoardView = rootVisual.Q<BlackBoardView>("template-blackboard-view");
            createNewTemplateButton = rootVisual.Q<Button>("create-new-template-button");
            browseTemplateButton = rootVisual.Q<Button>("browse-template-button");
            templateBarMenu = rootVisual.Q<ToolbarMenu>("template-bar-menu");
            templateDefinitionNameLabel = rootVisual.Q<Label>("template-definition-name");
            applyButton = rootVisual.Q<Button>("apply-button");

            // ── Wire events ──
            if (createNewTemplateButton != null)
                createNewTemplateButton.clicked += OnCreateNewTemplateClicked;

            if (browseTemplateButton != null)
                browseTemplateButton.clicked += OnBrowseTemplateClicked;

            if (applyButton != null)
                applyButton.clicked += OnApplyClicked;

            BuildTemplateBarMenu();
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        private void OnProjectChanged()
        {
            if (currentTemplate == null)
            {
                ClearUI();
                BuildTemplateBarMenu();
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is BlackboardTemplate template && template != currentTemplate)
                LoadTemplate(template);
        }

        /// <summary>
        /// Loads a BlackboardTemplate into the editor window.
        /// </summary>
        public void LoadTemplate(BlackboardTemplate template)
        {
            currentTemplate = template;
            RefreshUI();
            BuildTemplateBarMenu();
        }

        private void RefreshUI()
        {
            if (currentTemplate == null)
            {
                ClearUI();
                return;
            }

            // Ensure the template has an embedded BlackboardDefinition
            if (currentTemplate.templateDefinition == null)
            {
                BlackboardDefinition bbDef = CreateInstance<BlackboardDefinition>();
                bbDef.name = currentTemplate.name + "_Variables";
                currentTemplate.templateDefinition = bbDef;
                AssetDatabase.AddObjectToAsset(bbDef, currentTemplate);
                EditorUtility.SetDirty(currentTemplate);
                AssetDatabase.SaveAssets();
            }

            // Show variables via BlackBoardView
            if (templateBlackBoardView != null)
            {
                // Enable SquadData toggle in the type-creation popup
                templateBlackBoardView.IsSquadContext = true;
                templateBlackBoardView.BuildBlackboardView(currentTemplate.templateDefinition);
            }

            // Update name label
            if (templateDefinitionNameLabel != null)
                templateDefinitionNameLabel.text = currentTemplate.name;
        }

        private void ClearUI()
        {
            templateBlackBoardView?.BuildBlackboardView(null);
            if (templateDefinitionNameLabel != null)
                templateDefinitionNameLabel.text = "(no template loaded)";
        }

        // ═══════════════════════════════════════════════════════════════
        // Toolbar menu
        // ═══════════════════════════════════════════════════════════════

        private void BuildTemplateBarMenu()
        {
            if (templateBarMenu == null) return;

            DropdownMenu menu = templateBarMenu.menu;
            menu.ClearItems();

            menu.AppendAction("Create New Template", CreateNewTemplate);
            menu.AppendSeparator();

            // Recent templates
            const int maxRecent = 5;
            string[] guids = AssetDatabase.FindAssets("t:BlackboardTemplate");
            List<BlackboardTemplate> recentTemplates = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<BlackboardTemplate>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(t => t != null)
                .OrderByDescending(t => System.IO.File.GetLastWriteTime(AssetDatabase.GetAssetPath(t)))
                .Take(maxRecent)
                .ToList();

            foreach (BlackboardTemplate template in recentTemplates)
            {
                BlackboardTemplate captured = template;
                menu.AppendAction("Open Template/" + captured.name, _ => LoadTemplate(captured));
            }

            menu.AppendSeparator("Open Template/");
            menu.AppendAction("Open Template/Browse...", BrowseOpenTemplate);

            if (currentTemplate != null)
            {
                menu.AppendSeparator();
                menu.AppendAction("Save Template", _ =>
                {
                    EditorUtility.SetDirty(currentTemplate);
                    AssetDatabase.SaveAssets();
                });
            }
        }

        private void CreateNewTemplate(DropdownMenuAction action)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Blackboard Template", "NewBlackboardTemplate", "asset",
                "Create a new Blackboard Template");

            if (string.IsNullOrEmpty(path)) return;

            BlackboardTemplate template = CreateInstance<BlackboardTemplate>();
            template.name = System.IO.Path.GetFileNameWithoutExtension(path);

            // Create embedded BlackboardDefinition
            BlackboardDefinition bbDef = CreateInstance<BlackboardDefinition>();
            bbDef.name = template.name + "_Variables";
            template.templateDefinition = bbDef;

            AssetDatabase.CreateAsset(template, path);
            AssetDatabase.AddObjectToAsset(bbDef, template);
            EditorUtility.SetDirty(template);
            EditorUtility.SetDirty(bbDef);
            AssetDatabase.SaveAssets();

            LoadTemplate(template);
            Selection.activeObject = template;
        }

        private void BrowseOpenTemplate(DropdownMenuAction action)
        {
            if (currentTemplate != null)
                EditorGUIUtility.PingObject(currentTemplate);

            string path = EditorUtility.OpenFilePanel("Open Blackboard Template", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;

            string projectRelative = "Assets" + path.Replace("\\", "/")
                .Replace(Application.dataPath.Replace("\\", "/"), "");

            BlackboardTemplate template = AssetDatabase.LoadAssetAtPath<BlackboardTemplate>(projectRelative);
            if (template != null)
                LoadTemplate(template);
        }

        // ═══════════════════════════════════════════════════════════════
        // Navigation buttons
        // ═══════════════════════════════════════════════════════════════

        private void OnCreateNewTemplateClicked()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Blackboard Template", "NewBlackboardTemplate", "asset",
                "Create a new Blackboard Template");

            if (string.IsNullOrEmpty(path)) return;

            BlackboardTemplate template = CreateInstance<BlackboardTemplate>();
            template.name = System.IO.Path.GetFileNameWithoutExtension(path);

            BlackboardDefinition bbDef = CreateInstance<BlackboardDefinition>();
            bbDef.name = template.name + "_Variables";
            template.templateDefinition = bbDef;

            AssetDatabase.CreateAsset(template, path);
            AssetDatabase.AddObjectToAsset(bbDef, template);

            EditorUtility.SetDirty(template);
            EditorUtility.SetDirty(bbDef);
            AssetDatabase.SaveAssets();

            LoadTemplate(template);
            Selection.activeObject = template;
        }

        private void OnBrowseTemplateClicked()
        {
            TemplateSearchProvider provider = CreateInstance<TemplateSearchProvider>();
            provider.onTemplateSelected = LoadTemplate;
            SearchWindow.Open(new SearchWindowContext(
                GUIUtility.GUIToScreenPoint(browseTemplateButton.worldBound.position)),
                provider);
        }

        // ═══════════════════════════════════════════════════════════════
        // Apply
        // ═══════════════════════════════════════════════════════════════

        private void OnApplyClicked()
        {
            if (currentTemplate == null)
            {
                EditorUtility.DisplayDialog("No Template", "Load a template before applying.", "OK");
                return;
            }

            TemplateApplyWindow.OpenForTemplate(currentTemplate);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Search provider for BlackboardTemplate assets
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Search provider for BlackboardTemplate assets in the project.
    /// </summary>
    public class TemplateSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public System.Action<BlackboardTemplate> onTemplateSelected;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> entries = new List<SearchTreeEntry>();
            entries.Add(new SearchTreeGroupEntry(new GUIContent("Select Template"), 0));

            string[] guids = AssetDatabase.FindAssets("t:BlackboardTemplate");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BlackboardTemplate template = AssetDatabase.LoadAssetAtPath<BlackboardTemplate>(path);
                if (template != null)
                {
                    entries.Add(new SearchTreeEntry(new GUIContent(template.name))
                    {
                        level = 1,
                        userData = template
                    });
                }
            }

            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
        {
            if (SearchTreeEntry.userData is BlackboardTemplate template)
            {
                onTemplateSelected?.Invoke(template);
                return true;
            }
            return false;
        }
    }
}
