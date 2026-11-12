using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Harness.Tests
{
    public class SceneSmokeTests
    {
        static string[] BuildScenes => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        [Test]
        public void BuildSettingsHaveScenes() => Assert.IsNotEmpty(BuildScenes, "Add scenes to Build Settings");

        [TestCaseSource(nameof(BuildScenes))]
        public void SceneHasNoMissingScripts(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var missing = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                .Select(t => t.name).ToList();
            Assert.IsEmpty(missing, "Missing scripts on: " + string.Join(", ", missing));
        }

        [Test]
        public void MetricsJsonIsValidShape()
        {
            HarnessMetrics.Reset();
            HarnessMetrics.Report("a", 1); HarnessMetrics.Report("a", 3); HarnessMetrics.Count("b");
            var json = HarnessMetrics.ToJson();
            StringAssert.Contains("\"a\":{\"min\":1,\"max\":3,\"avg\":2", json);
            StringAssert.Contains("\"b\":", json);
        }
    }
}
