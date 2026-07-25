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
                foreach (string old in AssetDatabase.FindAssets(
                             "t:RuntimeBehaviourTreeAsset", new[] { BakedTreesFolder }))
                {
                    if (!liveGuids.Contains(old))
                        AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(old));
                }
            }

            Debug.Log($"[TreeBakeUtility] Baked {bakedCount} tree(s) into {BakedTreesFolder}.");
            return bakedCount;
        }

        [MenuItem("BehaviourTree/Bake All Trees For Build")]
        private static void BakeAllMenu() => BakeAllAuthoringTrees();

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
