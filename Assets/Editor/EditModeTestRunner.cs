using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// EditMode 테스트를 돌리고 결과를 파일로 남기는 개발용 도구(2026-11-28). Unity MCP에서 Test Runner를 직접 부를 수 없어 만들었다.
/// 메뉴 DEPTH 5m > Run EditMode Tests, 결과는 Temp/editmode-tests.txt.
/// </summary>
public static class EditModeTestRunner
{
    /// <summary>결과 파일 경로(프로젝트 기준).</summary>
    public const string ResultPath = "Temp/editmode-tests.txt";

    class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"pass={result.PassCount} fail={result.FailCount} skip={result.SkipCount}");
            void Walk(ITestResultAdaptor a)
            {
                if (!a.HasChildren && a.TestStatus == TestStatus.Failed) sb.AppendLine("FAIL " + a.FullName + " :: " + a.Message);
                foreach (var c in a.Children) Walk(c);
            }
            Walk(result);
            System.IO.File.WriteAllText(ResultPath, sb.ToString());
            Debug.Log("[EditModeTestRunner] " + sb.ToString().Split('\n')[0]);
        }
    }

    /// <summary>EditMode 테스트를 모두 돌린다. 끝나면 결과 파일을 쓴다.</summary>
    [MenuItem("DEPTH 5m/Run EditMode Tests")]
    public static void Run()
    {
        if (System.IO.File.Exists(ResultPath)) System.IO.File.Delete(ResultPath);
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
    }
}
