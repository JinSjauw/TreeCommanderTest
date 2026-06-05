using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyManager))]
public class EnemyManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EnemyManager manager = (EnemyManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug Controls", EditorStyles.boldLabel);

        GUI.enabled = Application.isPlaying;
        if (GUILayout.Button("Spawn Enemy"))
        {
            manager.SpawnEnemy();
        }
        GUI.enabled = true;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Spawn buttons only work in Play mode.", MessageType.Info);
        }
    }
}
