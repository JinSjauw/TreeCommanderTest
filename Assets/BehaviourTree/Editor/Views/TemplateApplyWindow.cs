using System.Collections.Generic;
using System.Linq;
using BehaviourTree.Core;
using BehaviourTree.Editor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Modal popup for applying a BlackboardTemplate to a target tree's BlackboardDefinition.
    /// Opened either from the template editor (template pre-selected, user picks target tree)
    /// or from the tree editor (target pre-selected, user picks template).
    ///
    /// Target is selected via tree assets (AgentTreeAsset / CommanderTreeAsset) rather than
    /// raw BlackboardDefinitions, so the popup can determine whether to skip squad-data
    /// variables (agent trees) or include them (commander trees).
    /// </summary>
    public class TemplateApplyWindow : EditorWindow
    {
        // ── Selection state ──
        private BlackboardTemplate selectedTemplate;
        private BaseEditorTreeAsset selectedTreeAsset;
        private BlackboardTemplate.ApplyMode applyMode = BlackboardTemplate.ApplyMode.AddMissing;

        // ── Context ──
        private bool templateIsFixed; // true when opened from template editor
        private bool targetIsFixed;   // true when opened from tree editor

        // ═══════════════════════════════════════════════════════════════
        // Entry points
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Opens the popup with a pre-selected template (from template editor).</summary>
        public static void OpenForTemplate(BlackboardTemplate template)
        {
            TemplateApplyWindow window = CreateInstance<TemplateApplyWindow>();
            window.selectedTemplate = template;
            window.templateIsFixed = true;
            window.titleContent = new GUIContent("Apply Template");
            window.minSize = new Vector2(440, 180);
            window.maxSize = new Vector2(440, 180);
            window.ShowUtility();
        }

        /// <summary>Opens the popup with a pre-selected target tree (from tree editor).</summary>
        public static void OpenForTarget(BlackboardDefinition target)
        {
            // Find the tree asset that owns this BlackboardDefinition
            BaseEditorTreeAsset tree = FindTreeAssetForDefinition(target);
            if (tree == null)
            {
                Debug.LogWarning("[TemplateApply] Could not find a tree asset owning the given BlackboardDefinition.");
            }

            TemplateApplyWindow window = CreateInstance<TemplateApplyWindow>();
            window.selectedTreeAsset = tree;
            window.targetIsFixed = true;
            window.titleContent = new GUIContent("Apply Template");
            window.minSize = new Vector2(440, 180);
            window.maxSize = new Vector2(440, 180);
            window.ShowUtility();
        }

        // ═══════════════════════════════════════════════════════════════
        // GUI
        // ═══════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            // ── Template row ──
            if (!templateIsFixed)
                DrawSelectRow("Template", selectedTemplate, SelectTemplate, ClearTemplate);

            // ── Target tree row ──
            if (!targetIsFixed)
                DrawSelectRow("Target Tree", selectedTreeAsset, SelectTargetTree, ClearTargetTree);

            EditorGUILayout.Space(6);

            // ── Info (show what's fixed) ──
            if (templateIsFixed && selectedTemplate != null)
                EditorGUILayout.LabelField($"Template: {selectedTemplate.name}", EditorStyles.boldLabel);

            if (targetIsFixed && selectedTreeAsset != null)
                EditorGUILayout.LabelField($"Target: {selectedTreeAsset.name}", EditorStyles.boldLabel);

            // ── Mode field ──
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Mode", GUILayout.Width(80));
                applyMode = (BlackboardTemplate.ApplyMode)EditorGUILayout.EnumPopup(applyMode);
            }

            EditorGUILayout.Space(10);

            // ── Apply / Cancel ──
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Apply", GUILayout.Width(100), GUILayout.Height(28)))
                    TryApply();

                if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(28)))
                    Close();

                GUILayout.FlexibleSpace();
            }

            // ── Validation warnings ──
            if (selectedTemplate == null)
                EditorGUILayout.HelpBox("Select a template.", MessageType.Warning);
            else if (selectedTreeAsset == null)
                EditorGUILayout.HelpBox("Select a target tree asset.", MessageType.Warning);
        }

        /// <summary>Draws a label + [Select] + [X] row for picking an asset via search window.</summary>
        private void DrawSelectRow<T>(string label, T current, System.Action<Vector2> openSearch, System.Action clear) where T : UnityEngine.Object
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(80));

                if (current != null)
                {
                    EditorGUILayout.LabelField(current.name, EditorStyles.boldLabel);
                    if (GUILayout.Button("X", GUILayout.Width(22), GUILayout.Height(18)))
                        clear();
                }
                else
                {
                    EditorGUILayout.LabelField("(none selected)");
                }

                if (GUILayout.Button("Select...", GUILayout.Width(80), GUILayout.Height(18)))
                {
                    // Anchor the search popup to the button's screen position
                    Rect buttonRect = GUILayoutUtility.GetLastRect();
                    openSearch(GUIUtility.GUIToScreenPoint(buttonRect.position));
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Selection actions
        // ═══════════════════════════════════════════════════════════════

        private void SelectTemplate(Vector2 screenPos)
        {
            TemplateSearchProvider provider = CreateInstance<TemplateSearchProvider>();
            provider.onTemplateSelected = t => selectedTemplate = t;
            SearchWindow.Open(new SearchWindowContext(screenPos), provider);
        }

        private void ClearTemplate()
        {
            selectedTemplate = null;
        }

        private void SelectTargetTree(Vector2 screenPos)
        {
            TreeTargetSearchProvider provider = CreateInstance<TreeTargetSearchProvider>();
            provider.onTreeSelected = t => selectedTreeAsset = t;
            SearchWindow.Open(new SearchWindowContext(screenPos), provider);
        }

        private void ClearTargetTree()
        {
            selectedTreeAsset = null;
        }

        // ═══════════════════════════════════════════════════════════════
        // Apply logic
        // ═══════════════════════════════════════════════════════════════

        private void TryApply()
        {
            if (selectedTemplate == null)
            {
                EditorUtility.DisplayDialog("Missing Template", "Select a template to apply.", "OK");
                return;
            }

            BlackboardDefinition targetBB = ExtractTargetBB();
            if (targetBB == null)
            {
                EditorUtility.DisplayDialog("Missing Target", "Select a target tree asset with a valid BlackboardDefinition.", "OK");
                return;
            }

            bool isAgentTree = selectedTreeAsset is AgentTreeAsset
                || (selectedTreeAsset != null && selectedTreeAsset.CommanderBlackboardDefinition == null);

            Undo.RecordObject(targetBB, $"Apply Template '{selectedTemplate.name}'");
            selectedTemplate.ApplyTo(targetBB, applyMode, skipSquadData: isAgentTree);
            EditorUtility.SetDirty(targetBB);
            AssetDatabase.SaveAssets();

            string squadInfo = isAgentTree ? " (squad data skipped)" : "";
            Debug.Log($"[TemplateApply] Applied '{selectedTemplate.name}' → '{targetBB.name}' ({applyMode}){squadInfo}.");
            Close();
        }

        /// <summary>
        /// Extracts the correct BlackboardDefinition from the selected tree asset.
        /// For commander trees, uses commanderBlackboardDefinition (which has squad-data variables).
        /// For agent trees, uses the standard blackboardDefinition.
        /// </summary>
        private BlackboardDefinition ExtractTargetBB()
        {
            if (selectedTreeAsset == null) return null;

            // CommanderTreeAsset puts both agent and commander vars on the same definition
            // via commanderBlackboardDefinition — use that when available
            return selectedTreeAsset.CommanderBlackboardDefinition ?? selectedTreeAsset.BlackboardDefinition;
        }

        /// <summary>
        /// Finds the first tree asset whose BlackboardDefinition matches the given one.
        /// Used when opening from the tree editor with just a BlackboardDefinition reference.
        /// </summary>
        private static BaseEditorTreeAsset FindTreeAssetForDefinition(BlackboardDefinition target)
        {
            if (target == null) return null;

            string[] guids = AssetDatabase.FindAssets("t:BehaviourTreeAssetBase");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BehaviourTreeAssetBase tree = AssetDatabase.LoadAssetAtPath<BehaviourTreeAssetBase>(path);
                if (tree != null && (tree.BlackboardDefinition == target || tree.CommanderBlackboardDefinition == target))
                    return tree as BaseEditorTreeAsset;
            }
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Search provider for target tree assets
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Search provider that lists both AgentTreeAsset and CommanderTreeAsset
    /// for selecting the target of a template apply operation.
    /// </summary>
    public class TreeTargetSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public System.Action<BaseEditorTreeAsset> onTreeSelected;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> entries = new List<SearchTreeEntry>();
            entries.Add(new SearchTreeGroupEntry(new GUIContent("Select Target Tree"), 0));

            string[] guids = AssetDatabase.FindAssets("t:BaseEditorTreeAsset");
            var trees = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<BaseEditorTreeAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(t => t != null)
                .OrderBy(t => t.name);

            foreach (BaseEditorTreeAsset tree in trees)
            {
                string typeTag = tree is CommanderTreeAsset ? "[Commander]" : "[Agent]";
                entries.Add(new SearchTreeEntry(new GUIContent($"{typeTag} {tree.name}"))
                {
                    level = 1,
                    userData = tree
                });
            }

            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is BaseEditorTreeAsset tree)
            {
                onTreeSelected?.Invoke(tree);
                return true;
            }
            return false;
        }
    }
}
