using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Spring3D))]
public class Spring3DEditor : Editor
{
    private float _impulseX = 1f;
    private float _impulseY = 1f;
    private float _impulseZ = 1f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to test impulses and view live values.", MessageType.Info);
            return;
        }

        Spring3D spring = (Spring3D)target;

        // --- Live values ---
        EditorGUILayout.LabelField("Live Values", EditorStyles.boldLabel);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.Vector3Field("Base Position", spring.BaseLocalPosition);
        EditorGUILayout.Vector3Field("Current Offset", spring.CurrentOffset);
        EditorGUILayout.Vector3Field("Target Offset", spring.TargetOffset);
        EditorGUILayout.Vector3Field("Velocity", spring.CurrentVelocity);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(6);

        // --- Per-axis impulse ---
        EditorGUILayout.LabelField("Impulse Per Axis (on offset)", EditorStyles.boldLabel);
        DrawAxisRow("X", ref _impulseX, spring.AddImpulseX, spring.SnapXTo, spring.SetTargetOffsetXZero);
        DrawAxisRow("Y", ref _impulseY, spring.AddImpulseY, spring.SnapYTo, spring.SetTargetOffsetYZero);
        DrawAxisRow("Z", ref _impulseZ, spring.AddImpulseZ, spring.SnapZTo, spring.SetTargetOffsetZZero);

        EditorGUILayout.Space(6);

        // --- Snap all ---
        EditorGUILayout.LabelField("Global", EditorStyles.boldLabel);
        if (GUILayout.Button("Snap All Offsets to 0"))
            spring.SnapToEquilibrium();
    }

    private static void DrawAxisRow(
        string label,
        ref float impulseValue,
        System.Action<float> applyImpulse,
        System.Action<float> snapTo,
        System.Action setTargetZero)
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(label, GUILayout.Width(14));
        impulseValue = EditorGUILayout.FloatField(impulseValue, GUILayout.Width(50));

        if (GUILayout.Button("Impulse +", GUILayout.Width(75)))
            applyImpulse(impulseValue);
        if (GUILayout.Button("Impulse -", GUILayout.Width(75)))
            applyImpulse(-impulseValue);
        if (GUILayout.Button("Snap 0", GUILayout.Width(55)))
            snapTo(0f);
        if (GUILayout.Button("→0", GUILayout.Width(35)))
            setTargetZero();

        EditorGUILayout.EndHorizontal();
    }
}
