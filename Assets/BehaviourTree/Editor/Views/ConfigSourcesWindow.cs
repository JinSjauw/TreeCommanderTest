using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Small editor window for managing the ScriptableObject config sources
    /// attached to a behaviour tree asset. Nodes with ScriptableObjectConstant
    /// parameters can select fields from these SOs.
    /// </summary>
    public class ConfigSourcesWindow : EditorWindow
    {
        private BaseEditorTreeAsset _treeAsset;
        private SerializedObject _serializedAsset;
        private SerializedProperty _availableConfigsProp;
        private Vector2 _scrollPos;

        public static void Show(BaseEditorTreeAsset treeAsset)
        {
            if (treeAsset == null) return;
            var wnd = GetWindow<ConfigSourcesWindow>(true, "Config Sources", true);
            wnd._treeAsset = treeAsset;
            wnd._serializedAsset = new SerializedObject(treeAsset);
            wnd._availableConfigsProp = wnd._serializedAsset.FindProperty("availableConfigs");
            wnd.minSize = new Vector2(280, 150);
            wnd.Show();
        }

        private void OnGUI()
        {
            if (_treeAsset == null || _serializedAsset == null)
            {
                EditorGUILayout.HelpBox("No tree asset selected. Open via the Behaviour Tree editor toolbar.", MessageType.Warning);
                return;
            }

            _serializedAsset.Update();

            EditorGUILayout.LabelField($"Config Sources for: {_treeAsset.name}", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (_availableConfigsProp == null)
            {
                _availableConfigsProp = _serializedAsset.FindProperty("availableConfigs");
                if (_availableConfigsProp == null)
                {
                    EditorGUILayout.HelpBox("Field 'availableConfigs' not found on asset.", MessageType.Error);
                    return;
                }
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            int removeIndex = -1;
            for (int i = 0; i < _availableConfigsProp.arraySize; i++)
            {
                SerializedProperty element = _availableConfigsProp.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(element, GUIContent.none);
                if (GUILayout.Button("×", GUILayout.Width(22)))
                    removeIndex = i;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Config", GUILayout.Height(40)))
            {
                _availableConfigsProp.arraySize++;
            }
            EditorGUILayout.EndHorizontal();

            _serializedAsset.ApplyModifiedProperties();

            if (removeIndex >= 0)
            {
                _availableConfigsProp.DeleteArrayElementAtIndex(removeIndex);
                _serializedAsset.ApplyModifiedProperties();
            }

            // Accept drag-and-drop
            Rect dropArea = GUILayoutUtility.GetLastRect();
            Event evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (Object obj in DragAndDrop.objectReferences)
                    {
                        if (obj is ScriptableObject so)
                        {
                            _availableConfigsProp.arraySize++;
                            _availableConfigsProp.GetArrayElementAtIndex(_availableConfigsProp.arraySize - 1).objectReferenceValue = so;
                        }
                    }
                    _serializedAsset.ApplyModifiedProperties();
                }
                evt.Use();
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(_treeAsset);
            }
        }

        private void OnDestroy()
        {
            if (_serializedAsset != null)
            {
                _serializedAsset.ApplyModifiedProperties();
                _serializedAsset.Dispose();
                _serializedAsset = null;
            }
        }
    }
}
