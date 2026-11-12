using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Harness
{
    [Serializable]
    public class Step
    {
        // press      : control (binding path) = value (default 1); if duration > 0, hold then release
        // set        : control = value, or stick = (x, y); if duration > 0, hold then release
        // replay     : file = recorded .inputtrace (relative to the scenario file)
        // wait       : duration seconds (game time)
        // screenshot : name -> <out>/<name>.png
        // send       : GameObject `target` .SendMessage(name, arg) — setup/debug via the game's own methods
        // mark       : HarnessMetrics.Report(name, value) — tag phases
        public string action, control, name, target, arg, file;
        public float value = 1f, x, y, duration;
    }

    [Serializable]
    public class Scenario
    {
        public string scene;
        public float timeScale = 1f;
        public float timeout = 120f;
        public int captureFramerate;   // > 0: fixed timestep per frame (Time.captureFramerate) and frame-by-frame replay
        public bool failOnError = true;
        public Step[] steps = Array.Empty<Step>();
        public Probe[] probes = Array.Empty<Probe>();
        // `expect` lives in the same JSON; harness/unity.py evaluates it against metrics.json.
    }

    public static class StepRunner
    {
        public static IEnumerator Run(Step[] steps, string outDir, string baseDir = "")
        {
            InputDriver.Init();
            foreach (var s in steps)
            {
                switch (s.action)
                {
                    case "press":
                    case "set":
                        bool stick = s.action == "set" && (s.x != 0 || s.y != 0);
                        if (stick) InputDriver.Set(s.control, new Vector2(s.x, s.y));
                        else InputDriver.Set(s.control, s.value);
                        if (s.duration > 0 || s.action == "press")
                        {
                            if (s.duration <= 0) yield return null;   // a press with no duration = one frame tap
                            if (s.duration > 0) yield return new WaitForSeconds(s.duration);
                            if (stick) InputDriver.Set(s.control, Vector2.zero);
                            else InputDriver.Set(s.control, 0f);
                        }
                        break;
                    case "replay":
                        yield return InputDriver.Replay(Path.Combine(baseDir, s.file), Time.captureFramerate > 0);
                        break;
                    case "wait":
                        yield return new WaitForSeconds(s.duration);
                        break;
                    case "screenshot":
                        yield return new WaitForEndOfFrame();
                        ScreenCapture.CaptureScreenshot(Path.Combine(outDir, s.name + ".png"));
                        yield return null;
                        break;
                    case "send":
                        var go = GameObject.Find(s.target);
                        if (go == null) Debug.LogError($"[Harness] send: no GameObject '{s.target}'");
                        else if (string.IsNullOrEmpty(s.arg)) go.SendMessage(s.name, SendMessageOptions.RequireReceiver);
                        else go.SendMessage(s.name, s.arg, SendMessageOptions.RequireReceiver);
                        break;
                    case "mark":
                        HarnessMetrics.Report(s.name, s.value);
                        break;
                    default:
                        Debug.LogError($"[Harness] unknown step action '{s.action}'");
                        break;
                }
            }
        }

        public static string Arg(string flag)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == flag) return a[i + 1];
            return null;
        }
    }
}
