using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Harness
{
    [Serializable]
    public class Step
    {
        // input   : set axis/button `name` to `value`; if duration > 0, hold then release to 0
        // wait    : wait `duration` seconds (scaled game time)
        // screenshot : save <outDir>/<name>.png
        // hook    : HarnessHooks.Invoke(name, arg)
        // mark    : HarnessMetrics.Report(name, value) — tag phases for later reading
        public string action;
        public string name;
        public float value;
        public float duration;
        public string arg;
    }

    [Serializable]
    public class Scenario
    {
        public string scene;            // scene to load first (optional)
        public float timeScale = 1f;
        public float timeout = 120f;    // real seconds
        public bool failOnError = true;
        public Step[] steps = Array.Empty<Step>();
        // `expect` lives in the same JSON but is evaluated by harness/unity.py, not here.
    }

    public static class StepRunner
    {
        public static IEnumerator Run(Step[] steps, string outDir)
        {
            foreach (var s in steps)
            {
                switch (s.action)
                {
                    case "input":
                        HarnessInput.Set(s.name, s.value);
                        if (s.duration > 0)
                        {
                            yield return new WaitForSeconds(s.duration);
                            HarnessInput.Set(s.name, 0f);
                        }
                        break;
                    case "wait":
                        yield return new WaitForSeconds(s.duration);
                        break;
                    case "screenshot":
                        yield return new WaitForEndOfFrame();
                        ScreenCapture.CaptureScreenshot(Path.Combine(outDir, s.name + ".png"));
                        yield return null;
                        break;
                    case "hook":
                        if (!HarnessHooks.Invoke(s.name, s.arg))
                            Debug.LogError($"[Harness] unknown hook '{s.name}'");
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
