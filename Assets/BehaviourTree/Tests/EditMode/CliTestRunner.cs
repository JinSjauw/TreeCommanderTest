using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace BehaviourTree.Tests
{
    /// <summary>
    /// Batch-mode test bootstrap. Unity's -runTests flag silently no-ops in this
    /// project (quits right after import without running anything), so EditMode
    /// tests are driven through the TestRunnerApi instead:
    ///
    /// Unity.exe -batchmode -projectPath &lt;proj&gt; -executeMethod BehaviourTree.Tests.CliTestRunner.RunEditModeTests -logFile &lt;log&gt;
    ///
    /// Writes a plain-text summary to TestResults/editmode-summary.txt and exits
    /// with code 0 (all passed) or 1 (failures).
    /// </summary>
    public static class CliTestRunner
    {
        private const string SummaryPath = "TestResults/editmode-summary.txt";

        public static void RunEditModeTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        private sealed class Callbacks : ICallbacks
        {
            private readonly StringBuilder failures = new StringBuilder();
            private int total, passed, failed, skipped;

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite) return;

                total++;
                switch (result.TestStatus)
                {
                    case TestStatus.Passed:
                        passed++;
                        break;
                    case TestStatus.Failed:
                        failed++;
                        failures.AppendLine($"FAIL: {result.Test.FullName}");
                        failures.AppendLine($"  {result.Message}");
                        break;
                    default:
                        skipped++;
                        break;
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    var summary = new StringBuilder();
                    summary.AppendLine($"RESULT total={total} passed={passed} failed={failed} skipped={skipped}");
                    summary.Append(failures);

                    string dir = Path.GetDirectoryName(SummaryPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(SummaryPath, summary.ToString());

                    Debug.Log($"[CliTestRunner] total={total} passed={passed} failed={failed} skipped={skipped}");
                }
                finally
                {
                    EditorApplication.Exit(failed == 0 ? 0 : 1);
                }
            }
        }
    }
}
