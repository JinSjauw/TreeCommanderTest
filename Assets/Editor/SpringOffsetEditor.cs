using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SpringOffset))]
public class SpringOffsetEditor : Editor
{
    private float _impulseValue = 1f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to test impulses.", MessageType.Info);
            return;
        }

        SpringOffset offset = (SpringOffset)target;

        EditorGUILayout.LabelField("Impulse Test", EditorStyles.boldLabel);

        _impulseValue = EditorGUILayout.FloatField("Impulse Value", _impulseValue);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Impulse"))
        {
            offset.ApplyImpulse(_impulseValue);
        }
        if (GUILayout.Button("Apply Negative"))
        {
            offset.ApplyImpulse(-_impulseValue);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Snap to 0"))
        {
            offset.SnapToTarget(0f);
        }
        if (GUILayout.Button("Set Target to 0"))
        {
            offset.SetTarget(0f);
        }
        EditorGUILayout.EndHorizontal();
    }
}
