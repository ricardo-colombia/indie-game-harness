using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Harness.Editor
{
    /// <summary>
    /// Used by `hx selftest --unity`: creates a throwaway scene (camera, light, ground, a "Player" cube driven by
    /// the Input System and read by probes) and puts it first in Build Settings, so build/tests/autotest/capture
    /// can be validated before the real game exists. Never run this on your game project — selftest uses a temp copy.
    /// </summary>
    public static class SelfTest
    {
        public static void CreateSampleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(10, 1, 10);
            var player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.name = "Player";
            player.transform.position = new Vector3(0, 0.5f, 0);
            player.AddComponent<SelfTestMover>();
            Camera.main.transform.SetPositionAndRotation(new Vector3(0, 6, -10), Quaternion.Euler(25, 0, 0));
            const string path = "Assets/HarnessSelfTest.unity";
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != path)).ToArray();
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }
    }
}
