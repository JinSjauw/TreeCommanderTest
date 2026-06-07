using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DeathHandler))]
public class DeathHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DeathHandler handler = (DeathHandler)target;

        EditorGUILayout.Space();

        if (GUILayout.Button("Die") && EditorApplication.isPlaying && handler.gameObject.activeSelf == true)
        {
            handler.SpawnCorpse();
        }
    }
}
