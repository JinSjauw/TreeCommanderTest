using BehaviourTree.Core;
using BehaviourTree.Editor;
using BehaviourTree.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class RuntimeAssetHelperResolveTests
    {
        private const string TempAuthoringPath = "Assets/BehaviourTree/Tests/EditMode/TempResolveTest.asset";

        [Test]
        public void Resolve_EditorAutobake_SetsSourceTreeGuid()
        {
            AgentTreeAsset authoring = ScriptableObject.CreateInstance<AgentTreeAsset>();
            authoring.root = ScriptableObject.CreateInstance<RootNode>();
            authoring.blackboardDefinition = ScriptableObject.CreateInstance<BlackboardDefinition>();

            AssetDatabase.DeleteAsset(TempAuthoringPath);
            AssetDatabase.CreateAsset(authoring, TempAuthoringPath);
            string guid = AssetDatabase.AssetPathToGUID(TempAuthoringPath);

            try
            {
                RuntimeBehaviourTreeAsset resolved =
                    RuntimeAssetHelper.Resolve(null, authoring, null, "Test");

                Assert.NotNull(resolved);
                Assert.AreEqual(guid, resolved.sourceTreeGuid);
                Assert.NotNull(resolved.runtimeNodeData);
                Object.DestroyImmediate(resolved);
            }
            finally
            {
                AssetDatabase.DeleteAsset(TempAuthoringPath);
            }
        }

        [Test]
        public void Resolve_OverrideWins_AndReturnsIndependentCopy()
        {
            RuntimeBehaviourTreeAsset overrideAsset = ScriptableObject.CreateInstance<RuntimeBehaviourTreeAsset>();
            overrideAsset.sourceTreeGuid = "override-guid";

            RuntimeBehaviourTreeAsset resolved =
                RuntimeAssetHelper.Resolve(overrideAsset, null, "some-other-guid", "Test");

            Assert.NotNull(resolved);
            Assert.AreNotSame(overrideAsset, resolved);
            Assert.AreEqual("override-guid", resolved.sourceTreeGuid);

            Object.DestroyImmediate(overrideAsset);
            Object.DestroyImmediate(resolved);
        }
    }
}
