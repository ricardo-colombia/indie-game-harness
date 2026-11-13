using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Harness
{
    /// <summary>
    /// Drives the game through Unity's own Input System — no game code changes. The game keeps using its
    /// InputActions/PlayerInput as usual; we add virtual Keyboard/Mouse/Gamepad devices and write state into them
    /// (the same technique InputTestFixture uses), or replay an InputEventTrace recorded from a real play session.
    /// Requirement for games: use the Input System package (not the legacy Input Manager).
    /// </summary>
    public static class InputDriver
    {
        static readonly List<InputDevice> devices = new();

        public static void Init()
        {
            if (devices.Count > 0) return;
            devices.Add(InputSystem.AddDevice<Keyboard>("HarnessKeyboard"));
            devices.Add(InputSystem.AddDevice<Mouse>("HarnessMouse"));
            devices.Add(InputSystem.AddDevice<Gamepad>("HarnessGamepad"));
            foreach (var d in devices) d.MakeCurrent();
        }

        /// <summary>control: a binding path such as "&lt;Keyboard&gt;/w", "&lt;Gamepad&gt;/rightTrigger", "&lt;Gamepad&gt;/leftStick".</summary>
        public static InputControl Find(string control)
        {
            foreach (var d in devices)
            {
                var c = InputControlPath.TryFindControl(d, control);
                if (c != null) return c;
            }
            Debug.LogError($"[Harness] no virtual control matches '{control}'");
            return null;
        }

        public static void Set(string control, float value) => Write(Find(control), value, Vector2.zero, false);
        public static void Set(string control, Vector2 value) => Write(Find(control), 0, value, true);

        static void Write(InputControl c, float f, Vector2 v, bool isVector)
        {
            if (c == null) return;
            using (StateEvent.From(c.device, out var ptr))
            {
                if (isVector && c is Vector2Control v2) v2.WriteValueIntoEvent(v, ptr);
                else if (c is AxisControl a) a.WriteValueIntoEvent(f, ptr);   // ButtonControl is an AxisControl
                else if (c is Vector2Control v2b) v2b.WriteValueIntoEvent(new Vector2(0, f), ptr);
                else { Debug.LogError($"[Harness] unsupported control type {c.GetType().Name}"); return; }
                InputSystem.QueueEvent(ptr);
            }
        }

        public static void ReleaseAll()
        {
            foreach (var d in devices)
                using (StateEvent.From(d, out var ptr))
                {
                    foreach (var c in d.allControls)
                        if (c is AxisControl a) a.WriteValueIntoEvent(0f, ptr);
                    InputSystem.QueueEvent(ptr);
                }
        }

        /// <summary>Replay a trace saved with "Harness → Record Input" (editor) or -recordInput (player).</summary>
        /// Recorded devices whose layout is exactly the same as one of the harness's virtual devices (Keyboard, Mouse,
        /// Gamepad) are mapped onto it, so a PlayerInput already paired to them keeps receiving input. Other layouts
        /// (e.g. XInputController, whose state format differs) get new instances of their own layout.
        /// frameByFrame: replay one recorded frame per player frame (pair with captureFramerate for repeatable runs).
        public static IEnumerator Replay(string path, bool frameByFrame = false)
        {
            Init();
            var trace = InputEventTrace.LoadFrom(path);
            var ctrl = trace.Replay();
            foreach (var info in trace.deviceInfos)
            {
                InputDevice target = null;
                foreach (var d in devices)
                    if (info.layout == d.layout) { target = d; break; }
                if (target != null) ctrl = ctrl.WithDeviceMappedFromTo(info.deviceId, target.deviceId);
            }
            ctrl = ctrl.WithAllDevicesMappedToNewInstances();   // only affects devices not mapped above
            ctrl = frameByFrame ? ctrl.PlayAllFramesOneByOne() : ctrl.PlayAllEventsAccordingToTimestamps();
            while (!ctrl.finished) yield return null;
            ctrl.Dispose();
            trace.Dispose();
        }
    }

    /// <summary>Player flag: Game.exe -recordInput out.inputtrace  → records the whole session's input on quit.</summary>
    public class InputRecorderBoot : MonoBehaviour
    {
        InputEventTrace trace;
        string path;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var p = StepRunner.Arg("-recordInput");
            if (p == null) return;
            var go = new GameObject("~InputRecorder");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<InputRecorderBoot>();
            r.path = p;
            r.trace = new InputEventTrace(1024 * 1024, growBuffer: true);
            r.trace.Enable();
        }

        void OnApplicationQuit()
        {
            if (trace == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            trace.WriteTo(path);
            trace.Dispose();
        }
    }
}
