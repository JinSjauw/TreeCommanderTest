using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Shared logic for resolving a RuntimeBehaviourTreeAsset: explicit override,
    /// authoring asset (editor in-memory bake), or a build-baked Resources artifact.
    /// Used by both AgentTreeRunner and CommanderTreeRunner.
    /// </summary>
    public static class RuntimeAssetHelper
    {
        /// <summary>Resources-relative folder (no leading slash) where build-baked trees live.</summary>
        public const string BakedTreesResourcesPath = "BakedTrees";

        /// <summary>
        /// Single bake core shared by editor autobake (transient) and disk baking for builds.
        /// Fills all serialized fields including sourceTreeGuid — the build-time matching key.
        /// </summary>
        public static RuntimeBehaviourTreeAsset BakeInto(
            BehaviourTreeAssetBase authoringAsset, string sourceGuid, bool transient)
        {
            RuntimeBehaviourTreeAsset baked = ScriptableObject.CreateInstance<RuntimeBehaviourTreeAsset>();
            baked.name = authoringAsset.DisplayName + "_Runtime";
            if (transient)
                baked.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
#if UNITY_EDITOR
            baked.sourceTree = authoringAsset;
#endif
            baked.sourceTreeGuid = sourceGuid;
            baked.blackboardDefinition = TreeBaker.BakeTree(
                authoringAsset.Root, authoringAsset,
                ref baked.runtimeNodeData,
                ref baked.runtimeFieldData,
                ref baked.fieldTypeNames,
                ref baked.boxedConstants,
                ref baked.runtimeNodeGuids,
                out baked.maxTreeDepth);
            return baked;
        }

        /// <summary>
        /// Returns a ready-to-use RuntimeBehaviourTreeAsset, in priority order:
        /// explicit override (tests/tools) → editor in-memory autobake → Resources
        /// artifact baked by the build preprocessor (keyed by authoring GUID).
        /// Returns null when resolution fails; caller should abort initialization.
        /// </summary>
        public static RuntimeBehaviourTreeAsset Resolve(
            RuntimeBehaviourTreeAsset overrideAsset,
            BehaviourTreeAssetBase authoringAsset,
            string authoringAssetGuid,
            string logContext = "BehaviourTreeRunner")
        {
            if (overrideAsset != null)
                return PrepareInstance(Object.Instantiate(overrideAsset));

#if UNITY_EDITOR
            if (authoringAsset != null)
            {
                string path = UnityEditor.AssetDatabase.GetAssetPath(authoringAsset);
                string guid = string.IsNullOrEmpty(path) ? null : UnityEditor.AssetDatabase.AssetPathToGUID(path);
                if (guid != null)
                    return BakeInto(authoringAsset, guid, transient: true);
            }
#endif

            if (!string.IsNullOrEmpty(authoringAssetGuid))
            {
                RuntimeBehaviourTreeAsset baked = Resources.Load<RuntimeBehaviourTreeAsset>(
                    BakedTreesResourcesPath + "/" + authoringAssetGuid);
                if (baked != null)
                    return PrepareInstance(Object.Instantiate(baked));
            }

            Debug.LogError($"[{logContext}] No baked tree found (authoring GUID: '{authoringAssetGuid}'). " +
                           "In the editor, assign an authoring asset; in builds, ensure the bake preprocessor ran.");
            return null;
        }

        private static RuntimeBehaviourTreeAsset PrepareInstance(RuntimeBehaviourTreeAsset instance)
        {
            instance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            return instance;
        }
    }
}
