using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyManager))]
public class EnemyManagerEditor : Editor
{
    private int squadAgentCount = 3;
    private float squadRadius = 5f;

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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Squad Spawn", EditorStyles.boldLabel);

        if (GUILayout.Button("Spawn Squad"))
        {
            manager.SpawnSquad();
        }

        squadAgentCount = EditorGUILayout.IntField("Agent Count", squadAgentCount);
        squadRadius = EditorGUILayout.FloatField("Radius", squadRadius);

        if (GUILayout.Button("Spawn Squad (Circle Formation)"))
        {
            manager.SpawnSquadInFormation(squadAgentCount, squadRadius);
        }
        GUI.enabled = true;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Spawn buttons only work in Play mode.", MessageType.Info);
        }
    }
}
