using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Guarantees every player build ships current baked trees: backfills runner GUIDs,
    /// then bakes all authoring trees into Assets/BehaviourTree/Resources/BakedTrees.
    /// Runs before accessor regeneration (callbackOrder -1) so a stale generated file
    /// never blocks baking.
    /// </summary>
    public sealed class BakeTreesBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1;

        public void OnPreprocessBuild(BuildReport report)
        {
            TreeBakeUtility.BackfillRunnerGuids();
            TreeBakeUtility.BakeAllAuthoringTrees();
        }
    }
}
