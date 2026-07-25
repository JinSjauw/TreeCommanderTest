using System.Collections.Generic;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    /// <summary>
    /// Regression: in player builds SquadBindingGroup.treeAsset always deserializes
    /// null (editor-assembly asset), so EnsureResolved must match on treeAssetGuid
    /// without requiring treeAsset to be non-null.
    /// </summary>
    public class SquadInstanceBuildMatchingTests
    {
        private GameObject squadGo;
        private GameObject treeGo;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(squadGo);
            Object.DestroyImmediate(treeGo);
        }

        [Test]
        public void EnsureResolved_MatchesByGuid_WhenTreeAssetIsNull()
        {
            const string guid = "build-test-guid";

            SquadDefinition def = ScriptableObject.CreateInstance<SquadDefinition>();
            def.blackboardDefinition = ScriptableObject.CreateInstance<BlackboardDefinition>();
            def.blackboardDefinition.EnsureVariable("LeaderIndex", typeof(int), stride: 1, isSquadData: false, isSystemVariable: true);
            def.bindingGroups.Add(new SquadBindingGroup
            {
                treeAsset = null, // ← player-build condition
                treeAssetGuid = guid,
                bindings = new List<VariableBinding>
                {
                    new VariableBinding
                    {
                        squadVariableName = "LeaderIndex",
                        treeVariableName = "LeaderIndex",
                        direction = BindingDirection.FromSquad
                    }
                }
            });

            BlackboardDefinition treeDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            treeDef.EnsureVariable("LeaderIndex", typeof(int), stride: 1, isSquadData: false, isSystemVariable: true);
            treeDef.sourceTreeGuid = guid;

            squadGo = new GameObject("squad");
            SquadInstance squad = squadGo.AddComponent<SquadInstance>();
            squad.Initialize(def, maxAgents: 1);
            squad.BlackBoard.SetBoxedRaw(0, 3);

            treeGo = new GameObject("tree");
            BlackBoard treeBB = treeGo.AddComponent<BlackBoard>();
            treeBB.Initialize(treeDef);

            squad.EnsureResolved(treeDef);
            squad.CopyToBB(treeBB, treeDef);

            Assert.AreEqual(3, treeBB.GetBoxedRaw(0));

            Object.DestroyImmediate(def.blackboardDefinition);
            Object.DestroyImmediate(def);
            Object.DestroyImmediate(treeDef);
        }
    }
}
