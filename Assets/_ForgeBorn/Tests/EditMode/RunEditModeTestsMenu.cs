using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ForgeBorn.Tests
{
    /// <summary>
    /// Runs the EditMode tests inside the already-open Editor (a second Unity process cannot open the same project) and writes a
    /// one-line summary plus every failure to the Editor log. Menu: ForgeBorn > Run EditMode Tests.
    /// </summary>
    public static class RunEditModeTestsMenu
    {
        [MenuItem("ForgeBorn/Run EditMode Tests")]
        public static void Run()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "ForgeBorn.EditModeTests" }
            }));
            Debug.Log("[ForgeBorn] EditMode tests started");
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.Test.IsSuite && result.TestStatus == TestStatus.Failed)
                    Debug.LogError("[ForgeBorn] EditMode FAILED: " + result.Test.FullName + " :: " + result.Message);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log("[ForgeBorn] EditMode tests finished: passed=" + result.PassCount + " failed=" + result.FailCount +
                          " skipped=" + result.SkipCount + " inconclusive=" + result.InconclusiveCount);
            }
        }
    }
}
