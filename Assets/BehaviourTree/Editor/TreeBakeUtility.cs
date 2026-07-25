using System.Collections.Generic;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Single place that turns authoring tree assets into baked RuntimeBehaviourTreeAssets
    /// on disk. Build artifacts live at Assets/BehaviourTree/Resources/BakedTrees/{guid}.asset
    /// so player builds can load them by authoring GUID without AssetDatabase.
    /// </summary>
    public static class TreeBakeUtility
    {
        public static readonly string BakedTreesFolder =
            "Assets/BehaviourTree/Resources/" + RuntimeAssetHelper.BakedTreesResourcesPath;

        public static string GetGuid(BehaviourTreeAssetBase authoringAsset)
        {
            string path = AssetDatabase.GetAssetPath(authoringAsset);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
        }

        /// <summary>Bakes one authoring tree to {folder}/{authoringGuid}.asset, overwriting deterministically.</summary>
        public static RuntimeBehaviourTreeAsset BakeToDisk(
            BehaviourTreeAssetBase authoringAsset, string folder = null)
        {
            folder ??= BakedTreesFolder;

            string guid = GetGuid(authoringAsset);
            if (guid == null)
            {
                Debug.LogError($"[TreeBakeUtility] '{authoringAsset.name}' is not persisted — save it before baking.");
                return null;
            }

            RuntimeBehaviourTreeAsset baked = RuntimeAssetHelper.BakeInto(authoringAsset, guid, transient: false);

            EnsureFolder(folder);
            string assetPath = $"{folder}/{guid}.asset";
            AssetDatabase.DeleteAsset(assetPath); // no-op when absent; guarantees clean overwrite
            AssetDatabase.CreateAsset(baked, assetPath);
            if (baked.blackboardDefinition != null)
            {
                baked.blackboardDefinition.name = baked.name + "_BB_Definition";
                AssetDatabase.AddObjectToAsset(baked.blackboardDefinition, baked);
            }
            AssetDatabase.SaveAssets();

            if (AssetDatabase.LoadAssetAtPath<RuntimeBehaviourTreeAsset>(assetPath) == null)
            {
                Debug.LogError($"[TreeBakeUtility] Failed to persist baked asset at {assetPath}.");
                return null;
            }
            return baked;
        }

        /// <summary>Bakes every authoring tree in the project; deletes orphaned baked assets.</summary>
        public static int BakeAllAuthoringTrees()
        {
            string[] guids = AssetDatabase.FindAssets("t:BaseEditorTreeAsset");
            var liveGuids = new HashSet<string>();
            int bakedCount = 0;

            foreach (string guid in guids)
            {
                BaseEditorTreeAsset asset = AssetDatabase.LoadAssetAtPath<BaseEditorTreeAsset>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                if (BakeToDisk(asset) != null)
                {
                    liveGuids.Add(guid);
                    bakedCount++;
                }
            }

            if (AssetDatabase.IsValidFolder(BakedTreesFolder))
            {
                foreach (string fileGuid in AssetDatabase.FindAssets(
                             "t:RuntimeBehaviourTreeAsset", new[] { BakedTreesFolder }))
                {
                    string orphanPath = AssetDatabase.GUIDToAssetPath(fileGuid);
                    // Files are keyed by the AUTHORING guid in their filename — the
                    // .meta guid of the baked file itself is a different, unrelated guid.
                    string authoringGuid = System.IO.Path.GetFileNameWithoutExtension(orphanPath);
                    if (!liveGuids.Contains(authoringGuid))
                        AssetDatabase.DeleteAsset(orphanPath);
                }
            }

            Debug.Log($"[TreeBakeUtility] Baked {bakedCount} tree(s) into {BakedTreesFolder}.");
            return bakedCount;
        }

        [MenuItem("BehaviourTree/Bake All Trees For Build")]
        private static void BakeAllMenu()
        {
            BackfillRunnerGuids();
            BakeAllAuthoringTrees();
        }

        /// <summary>
        /// Writes authoringAssetGuid on every runner prefab from its assigned authoring asset.
        /// Covers prefabs that were never opened since the field was added (OnValidate timing).
        /// Returns the number of prefabs modified.
        /// </summary>
        public static int BackfillRunnerGuids()
        {
            int touched = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                bool dirty = false;
                foreach (BehaviourTreeRunnerBase runner in
                         prefab.GetComponentsInChildren<BehaviourTreeRunnerBase>(true))
                {
                    var so = new SerializedObject(runner);
                    SerializedProperty authoring = so.FindProperty("authoringAsset");
                    SerializedProperty guidProp = so.FindProperty("authoringAssetGuid");
                    if (authoring == null || guidProp == null) continue;

                    string expected = string.Empty;
                    if (authoring.objectReferenceValue != null)
                    {
                        string assetPath = AssetDatabase.GetAssetPath(authoring.objectReferenceValue);
                        expected = AssetDatabase.AssetPathToGUID(assetPath);
                    }

                    if (guidProp.stringValue != expected)
                    {
                        guidProp.stringValue = expected;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(prefab);
                    touched++;
                }
            }

            if (touched > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[TreeBakeUtility] Backfilled authoringAssetGuid on {touched} prefab(s).");
            }
            return touched;
        }

        private static void EnsureFolder(string folder)
        {
            string[] segments = folder.Split('/');
            string current = segments[0]; // "Assets"
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
