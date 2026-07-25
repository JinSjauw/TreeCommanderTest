using System;
using UnityEditor;
using UnityEngine;
using BehaviourTree.Core;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// The single constant-value field renderer. Writes into a NodeFieldEntry's
    /// typed value properties (intValue/floatValue/boolValue/vector*/object refs).
    /// </summary>
    public static class ConstantValueFieldDrawer
    {
        public static void Draw(SerializedProperty entryProp, Type fieldType,
            string label, bool showLabel, float fieldWidth)
        {
            if (fieldType == null)
            {
                EditorGUILayout.HelpBox("No type selected.", MessageType.Warning);
                return;
            }

            if (showLabel)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(label);
            }

            if (fieldType.IsEnum)
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
                Enum current = (Enum)Enum.ToObject(fieldType, prop.intValue);
                Enum next = EditorGUILayout.EnumPopup(current, GUILayout.Width(fieldWidth));
                prop.intValue = Convert.ToInt32(next);
            }
            else if (fieldType == typeof(int) || fieldType == typeof(uint))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
                prop.intValue = EditorGUILayout.IntField(prop.intValue, GUILayout.Width(fieldWidth));
            }
            else if (fieldType == typeof(float))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("floatValue");
                prop.floatValue = EditorGUILayout.FloatField(prop.floatValue, GUILayout.Width(fieldWidth));
            }
            else if (fieldType == typeof(bool))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("boolValue");
                prop.boolValue = GUILayout.Toggle(prop.boolValue, prop.boolValue ? "True" : "False",
                    "Button", GUILayout.Width(fieldWidth));
            }
            else if (fieldType == typeof(Vector2))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("vector2Value");
                prop.vector2Value = EditorGUILayout.Vector2Field(GUIContent.none, prop.vector2Value,
                    GUILayout.Width(fieldWidth));
            }
            else if (fieldType == typeof(Vector3))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("vector3Value");
                prop.vector3Value = EditorGUILayout.Vector3Field(GUIContent.none, prop.vector3Value,
                    GUILayout.Width(fieldWidth));
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                SerializedProperty prop = fieldType == typeof(GameObject)
                    ? entryProp.FindPropertyRelative("gameObjectValue")
                    : entryProp.FindPropertyRelative("transformValue");
                prop.objectReferenceValue = EditorGUILayout.ObjectField(
                    prop.objectReferenceValue, fieldType, true, GUILayout.Width(fieldWidth));
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Type '{TypeDisplayRegistry.instance.GetDisplayName(fieldType)}' is not supported " +
                    "for constant values. Use a variable source instead.",
                    MessageType.Warning);
            }

            if (showLabel)
                EditorGUILayout.EndHorizontal();
        }
    }
}
