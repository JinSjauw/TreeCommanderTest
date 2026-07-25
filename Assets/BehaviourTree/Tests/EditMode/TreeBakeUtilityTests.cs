using BehaviourTree.Core;
using BehaviourTree.Editor;
using BehaviourTree.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class TreeBakeUtilityTests
    {
        private const string TempAuthoringPath = "Assets/BehaviourTree/Tests/EditMode/TempBakeTest.asset";

        [Test]
        public void BakeToDisk_WritesSourceTreeGuid_AndPersistsAtGuidPath()
        {
            AgentTreeAsset authoring = ScriptableObject.CreateInstance<AgentTreeAsset>();
            authoring.root = ScriptableObject.CreateInstance<RootNode>();
            authoring.blackboardDefinition = ScriptableObject.CreateInstance<BlackboardDefinition>();
            authoring.blackboardDefinition.EnsureVariable("Health", typeof(float), stride: 1, isSquadData: false, isSystemVariable: false);

            AssetDatabase.DeleteAsset(TempAuthoringPath);
            AssetDatabase.CreateAsset(authoring, TempAuthoringPath);
            string guid = AssetDatabase.AssetPathToGUID(TempAuthoringPath);
            string bakedPath = $"{TreeBakeUtility.BakedTreesFolder}/{guid}.asset";

            try
            {
                RuntimeBehaviourTreeAsset baked = TreeBakeUtility.BakeToDisk(authoring);

                Assert.NotNull(baked);
                Assert.AreEqual(guid, baked.sourceTreeGuid);

                RuntimeBehaviourTreeAsset reloaded =
                    AssetDatabase.LoadAssetAtPath<RuntimeBehaviourTreeAsset>(bakedPath);
                Assert.NotNull(reloaded, "Baked asset must persist at the GUID-keyed path.");
                Assert.AreEqual(guid, reloaded.sourceTreeGuid);
                Assert.NotNull(reloaded.blackboardDefinition);
            }
            finally
            {
                AssetDatabase.DeleteAsset(TempAuthoringPath);
                AssetDatabase.DeleteAsset(bakedPath);
            }
        }
    }
}
