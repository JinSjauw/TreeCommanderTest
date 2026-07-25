using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Shared logic for resolving a RuntimeBehaviourTreeAsset from either
    /// an authoring asset (editor bake) or a pre-baked runtime asset (build).
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
        /// Returns a ready-to-use RuntimeBehaviourTreeAsset.
        /// If <paramref name="existing"/> is null, bakes from <paramref name="authoringAsset"/>
        /// in the editor, or logs an error in builds.
        /// If <paramref name="existing"/> is non-null, instantiates a copy.
        /// Returns null when resolution fails; caller should abort initialization.
        /// </summary>
        public static RuntimeBehaviourTreeAsset GetOrBake(
            RuntimeBehaviourTreeAsset existing,
            BehaviourTreeAssetBase authoringAsset,
            string logContext = "AgentTreeRunner")
        {
            if (existing == null)
            {
#if UNITY_EDITOR
                if (authoringAsset != null)
                {
                    RuntimeBehaviourTreeAsset temp = ScriptableObject.CreateInstance<RuntimeBehaviourTreeAsset>();
                    temp.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                    temp.name = authoringAsset.DisplayName + "_Runtime";
                    temp.sourceTree = authoringAsset;
#if UNITY_EDITOR
                    string assetPath = UnityEditor.AssetDatabase.GetAssetPath(authoringAsset);
                    temp.sourceTreeGuid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
#endif

                    temp.blackboardDefinition = TreeBaker.BakeTree(
                        authoringAsset.Root, authoringAsset,
                        ref temp.runtimeNodeData,
                        ref temp.runtimeFieldData,
                        ref temp.fieldTypeNames,
                        ref temp.boxedConstants,
                        ref temp.runtimeNodeGuids,
                        out temp.maxTreeDepth);

                    return temp;
                }

                Debug.LogError($"[{logContext}] Authoring asset is null — cannot bake tree.");
                return null;
#else
                Debug.LogError($"[{logContext}] Runtime asset is null.");
                return null;
#endif
            }

            RuntimeBehaviourTreeAsset instance = Object.Instantiate(existing);
            instance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            return instance;
        }
    }
}
