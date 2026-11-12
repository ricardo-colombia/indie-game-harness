using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Harness
{
    /// <summary>
    /// Harness build, launched with:  Game.exe -autotest scenarios/x.json -harnessOut out/
    /// Plays the scenario through the Input System, samples probes + frame time, counts errors, writes metrics.json,
    /// quits. Exit 0 = completed without errors; thresholds are judged by harness/unity.py.
    /// </summary>
    public class AutoPilot : MonoBehaviour
    {
        string scenarioPath, outDir;
        Scenario sc;
        int errors;
        bool completed, running;
        float startReal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var path = StepRunner.Arg("-autotest");
            if (path == null) return;
            var go = new GameObject("~AutoPilot");
            DontDestroyOnLoad(go);
            var ap = go.AddComponent<AutoPilot>();
            ap.scenarioPath = path;
            ap.outDir = StepRunner.Arg("-harnessOut") ?? "harness_out";
        }

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            startReal = Time.realtimeSinceStartup;
            sc = JsonUtility.FromJson<Scenario>(File.ReadAllText(scenarioPath));
            StartCoroutine(Watchdog(sc.timeout));
            if (!string.IsNullOrEmpty(sc.scene) && SceneManager.GetActiveScene().name != sc.scene)
            {
                var op = SceneManager.LoadSceneAsync(sc.scene);
                while (!op.isDone) yield return null;
            }
            HarnessMetrics.Report("startup.seconds", Time.realtimeSinceStartup);
            Time.timeScale = sc.timeScale > 0 ? sc.timeScale : 1f;
            running = true;
            yield return StepRunner.Run(sc.steps, outDir, Path.GetDirectoryName(Path.GetFullPath(scenarioPath)));
            completed = true;
            Finish();
        }

        void Update()
        {
            if (!running) return;
            HarnessMetrics.Report("frame.ms", Time.unscaledDeltaTime * 1000f);
            foreach (var p in sc.probes) p.Sample();
        }

        IEnumerator Watchdog(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (!completed)
            {
                Debug.LogError($"[Harness] scenario timeout after {seconds}s");
                Finish();
            }
        }

        void Finish()
        {
            running = false;
            var json = "{\"scenario\":\"" + Path.GetFileNameWithoutExtension(scenarioPath) + "\"," +
                       "\"completed\":" + (completed ? "true" : "false") + "," +
                       "\"errors\":" + errors + "," +
                       "\"realSeconds\":" + (Time.realtimeSinceStartup - startReal).ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                       "\"metrics\":" + HarnessMetrics.ToJson() + "}";
            File.WriteAllText(Path.Combine(outDir, "metrics.json"), json);
            Application.Quit(completed && errors == 0 ? 0 : 1);
        }
    }
}
