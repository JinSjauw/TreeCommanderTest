using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BehaviourTree.EditorTools.Codegen
{
    /// <summary>
    /// Hard guarantee that every build ships current, AOT-safe binding accessors:
    /// regenerates GeneratedBindingAccessors.cs before script compilation.
    /// Editor-side only; costs seconds per build, zero during iteration.
    /// </summary>
    public sealed class GenerateAccessorsBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[GenerateAccessorsBuildPreprocessor] Regenerating binding accessors for build...");
            BindingAccessorGenerator.Generate();
        }
    }
}
