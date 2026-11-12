using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Harness.Tests
{
    /// <summary>Loads the first build scene and runs ~3 seconds. Unity fails the test on any logged error/exception.</summary>
    public class BootSmokeTest
    {
        [UnityTest]
        public IEnumerator FirstSceneRunsCleanly()
        {
            Assert.Greater(SceneManager.sceneCountInBuildSettings, 0, "No scenes in Build Settings");
            yield return SceneManager.LoadSceneAsync(0);
            float end = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.Greater(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, 0, "Scene has no camera");
        }
    }
}
