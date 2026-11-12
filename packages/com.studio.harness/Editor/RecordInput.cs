using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace Harness.Editor
{
    /// <summary>
    /// Harness → Record Input: while in Play Mode, records everything you do with real devices into
    /// scenarios/recordings/&lt;time&gt;.inputtrace. A scenario step {"action":"replay","file":"recordings/x.inputtrace"}
    /// then replays it in the built game overnight. Re-record after big control or level changes.
    /// </summary>
    public static class RecordInput
    {
        static InputEventTrace trace;
        const string Menu = "Harness/Record Input (Play Mode)";

        [MenuItem(Menu)]
        static void Toggle()
        {
            if (trace != null) { Stop(); return; }
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[Harness] enter Play Mode first"); return; }
            trace = new InputEventTrace(1024 * 1024, growBuffer: true);
            trace.Enable();
            EditorApplication.playModeStateChanged += OnPlayMode;
            Debug.Log("[Harness] recording input… choose the menu again (or exit Play Mode) to save");
        }

        [MenuItem(Menu, true)]
        static bool Validate() { UnityEditor.Menu.SetChecked(Menu, trace != null); return true; }

        static void OnPlayMode(PlayModeStateChange s) { if (s == PlayModeStateChange.ExitingPlayMode) Stop(); }

        static void Stop()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "scenarios", "recordings"));
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, $"{DateTime.Now:yyyyMMdd-HHmmss}.inputtrace");
            trace.Disable();
            trace.WriteTo(path);
            trace.Dispose();
            trace = null;
            Debug.Log($"[Harness] saved {path}");
        }
    }
}
