using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Harness.Editor
{
    /// <summary>
    /// Unity -batchmode -quit -projectPath game -buildTarget StandaloneWindows64
    ///       -executeMethod Harness.Editor.HarnessBuild.BuildPlayer -harnessBuildPath Builds/Player/MyGame.exe
    /// </summary>
    public static class HarnessBuild
    {
        public static void BuildPlayer()
        {
            var path = Arg("-harnessBuildPath") ?? "Builds/Player/Game";
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) Fail("No enabled scenes in Build Settings.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = EditorUserBuildSettings.activeBuildTarget,
                options = Arg("-release") != null ? BuildOptions.None : BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[Harness] build {s.result} in {s.totalTime.TotalSeconds:0}s, {s.totalSize / 1048576f:0.0} MB, {s.totalErrors} errors, {s.totalWarnings} warnings");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Fast gate without a full build: compiles + opens every build scene and checks for missing scripts.</summary>
        public static void ValidateScenes()
        {
            int bad = 0;
            foreach (var sc in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = EditorSceneManager.OpenScene(sc.path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (n > 0) { bad += n; Debug.LogError($"[Harness] missing script on {sc.path}:{t.name}"); }
                }
            }
            EditorApplication.Exit(bad == 0 ? 0 : 1);
        }

        static string Arg(string flag)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == flag) return a[i + 1];
            return a.Contains(flag) ? "" : null;
        }

        static void Fail(string msg) { Debug.LogError("[Harness] " + msg); EditorApplication.Exit(1); }
    }
}
