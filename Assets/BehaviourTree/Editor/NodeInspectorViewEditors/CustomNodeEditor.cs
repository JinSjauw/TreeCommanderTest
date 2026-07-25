using UnityEditor;
using UnityEngine;
using BehaviourTree;
using BehaviourTree.Core;

namespace BehaviourTree.Editor
{
    [CustomEditor(typeof(BehaviourNode), true)]
    public class CustomNodeEditor : UnityEditor.Editor
    {
        public bool nodeNameChangedThisFrame;
        public bool nodeVisualsChangedThisFrame;

        private string lastMethodName;
        private SerializedProperty nodeNameProp;
        private SerializedProperty methodNameProp;
        private SerializedProperty fieldEntriesProp;
        private SerializedProperty childrenProp;
        private SerializedProperty commentProp;
        private SerializedProperty abortTypeProp;
        private AbortType lastAbortType;
        private NodeParamSectionRenderer paramRenderer;

        private void OnEnable()
        {
            if (target == null) return;

            nodeNameProp = serializedObject.FindProperty("nodeName");
            methodNameProp = serializedObject.FindProperty("methodName");
            fieldEntriesProp = serializedObject.FindProperty("fieldEntries");
            childrenProp = serializedObject.FindProperty("children");
            commentProp = serializedObject.FindProperty("comment");
            if (target is CompositeNode) abortTypeProp = serializedObject.FindProperty("abortType");
            if (target is CompositeNode composite) lastAbortType = composite.abortType;
            paramRenderer = new NodeParamSectionRenderer(this, fieldEntriesProp);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUIUtility.labelWidth = NodeParamSectionRenderer.FieldLabelWidth;

            if(target is RootNode) return;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(nodeNameProp, new GUIContent("Node Name"));
            nodeNameChangedThisFrame = EditorGUI.EndChangeCheck();

            if(!(target is LeafNode || target is DecoratorNode || target is CompositeNode))
            {
                DrawDefaultInspector();
                DrawChildrenDebug();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            // Check if method changed and rebuild field entries
            string selectedMethodName = methodNameProp != null ? methodNameProp.stringValue : null;
            bool methodChanged = selectedMethodName != lastMethodName;
            lastMethodName = selectedMethodName;
            EditorGUI.BeginChangeCheck();

            if (commentProp != null)
            {
                EditorGUILayout.LabelField("Comment", EditorStyles.boldLabel);
                commentProp.stringValue = EditorGUILayout.TextArea(commentProp.stringValue, GUILayout.Height(60));
                EditorGUILayout.Space();
            }

            paramRenderer.BuildFieldEntries(selectedMethodName, methodChanged);
            nodeVisualsChangedThisFrame |= paramRenderer.nodeVisualsChangedThisFrame;
            paramRenderer.nodeVisualsChangedThisFrame = false;

            if (abortTypeProp != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Conditional Abort", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(abortTypeProp, new GUIContent("Abort Type"));

                AbortType currentAbort = (AbortType)abortTypeProp.enumValueIndex;
                if (currentAbort != lastAbortType)
                {
                    lastAbortType = currentAbort;
                    nodeVisualsChangedThisFrame = true;
                }

                if (currentAbort != AbortType.None)
                {
                    CompositeNode composite = (CompositeNode)target;
                    if (!NodeWarningEvaluator.HasValidConditionForAbort(composite, currentAbort))
                    {
                        EditorGUILayout.HelpBox(
                            "No reachable Condition node found. Add a Condition node as a " +
                            "descendant for this abort type to take effect.",
                            MessageType.Warning);
                    }
                }
            }

            if(target is CompositeNode)
            {
                DrawChildrenDebug();
            }

            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawChildrenDebug()
        {
            if (childrenProp == null) return;

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(childrenProp, true);
            EditorGUI.EndDisabledGroup();
        }
    }
}
