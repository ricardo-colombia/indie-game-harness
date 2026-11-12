using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Harness
{
    [Serializable]
    public class Take
    {
        public string name;
        public string scene;
        public string type = "orbit";     // orbit | dolly | follow | static
        public string target;             // GameObject name to frame/follow
        public float duration = 6f;
        public float radius = 8f, height = 3f, speed = 20f;   // orbit: degrees/sec
        public Vector3 from, to;                              // dolly / static (from)
        public Vector3 offset = new(0, 3, -7);                // follow
        public float fov = 50f;
        public Step[] steps = Array.Empty<Step>();             // gameplay to perform while filming
    }

    [Serializable]
    public class TakeList { public int fps = 30; public Take[] takes = Array.Empty<Take>(); }

    /// <summary>
    /// Harness build: Game.exe -capture marketing/takes/launch.json -harnessOut marketing/out/raw/launch
    /// Locks the frame rate (Time.captureFramerate) so output is smooth regardless of machine speed,
    /// writes PNG sequences per take: <out>/<take>/f_00001.png. harness/marketing/cut_shorts.py turns them into shorts.
    /// </summary>
    public class CameraTakes : MonoBehaviour
    {
        TakeList list;
        string outDir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var path = StepRunner.Arg("-capture");
            if (path == null) return;
            var go = new GameObject("~CameraTakes");
            DontDestroyOnLoad(go);
            var ct = go.AddComponent<CameraTakes>();
            ct.list = JsonUtility.FromJson<TakeList>(File.ReadAllText(path));
            ct.outDir = StepRunner.Arg("-harnessOut") ?? "capture_out";
        }

        IEnumerator Start()
        {
            HarnessMetrics.Enabled = false;
            InputDriver.Init();
            Time.captureFramerate = list.fps;
            foreach (var take in list.takes)
            {
                if (!string.IsNullOrEmpty(take.scene) && SceneManager.GetActiveScene().name != take.scene)
                {
                    var op = SceneManager.LoadSceneAsync(take.scene);
                    while (!op.isDone) yield return null;
                }
                yield return Film(take);
                InputDriver.ReleaseAll();
            }
            Application.Quit(0);
        }

        IEnumerator Film(Take t)
        {
            var dir = Path.Combine(outDir, t.name);
            Directory.CreateDirectory(dir);
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[Harness] no Camera.main"); yield break; }
            var follow = cam.GetComponents<MonoBehaviour>();
            foreach (var b in follow) if (b != null && b.GetType().Name.Contains("Camera")) b.enabled = false; // disable game camera controllers
            cam.fieldOfView = t.fov;
            var target = string.IsNullOrEmpty(t.target) ? null : GameObject.Find(t.target)?.transform;
            if (t.steps.Length > 0) StartCoroutine(StepRunner.Run(t.steps, dir));

            int frames = Mathf.CeilToInt(t.duration * list.fps);
            for (int i = 0; i < frames; i++)
            {
                float u = frames <= 1 ? 1 : i / (float)(frames - 1);
                Vector3 focus = target ? target.position : Vector3.zero;
                switch (t.type)
                {
                    case "orbit":
                        float a = (t.speed * i / list.fps) * Mathf.Deg2Rad;
                        cam.transform.position = focus + new Vector3(Mathf.Cos(a) * t.radius, t.height, Mathf.Sin(a) * t.radius);
                        cam.transform.LookAt(focus);
                        break;
                    case "dolly":
                        cam.transform.position = Vector3.Lerp(t.from, t.to, Mathf.SmoothStep(0, 1, u));
                        if (target) cam.transform.LookAt(focus);
                        break;
                    case "follow":
                        if (target)
                        {
                            var want = target.TransformPoint(t.offset);
                            cam.transform.position = Vector3.Lerp(cam.transform.position, want, 0.15f);
                            cam.transform.LookAt(focus + Vector3.up);
                        }
                        break;
                    default:
                        cam.transform.position = t.from;
                        if (target) cam.transform.LookAt(focus);
                        break;
                }
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"f_{i + 1:00000}.png"));
                yield return null;
            }
            foreach (var b in follow) if (b != null) b.enabled = true;
        }
    }
}
