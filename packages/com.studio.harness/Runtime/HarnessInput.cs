using System;
using System.Collections.Generic;

namespace Harness
{
    /// <summary>
    /// THE CONTRACT: gameplay code reads input only through HarnessInput, never directly from
    /// the Input System. At startup the game wires LiveAxis/LiveButton to its real input
    /// (see README). In autotest/capture runs the AutoPilot drives these values instead, so the
    /// built game can be played by a script with no human and no editor.
    /// </summary>
    public static class HarnessInput
    {
        static readonly Dictionary<string, float> simulated = new();

        /// <summary>Public so Funplay MCP `execute_code` can drive play mode: Simulated = true; Set("throttle", 1).</summary>
        public static bool Simulated { get; set; }
        public static Func<string, float> LiveAxis = _ => 0f;
        public static Func<string, bool> LiveButton = _ => false;

        public static float Axis(string name) =>
            Simulated ? (simulated.TryGetValue(name, out var v) ? v : 0f) : LiveAxis(name);

        public static bool Button(string name) =>
            Simulated ? simulated.TryGetValue(name, out var v) && v > 0.5f : LiveButton(name);

        public static void Set(string name, float value) => simulated[name] = value;
        public static void ClearAll() => simulated.Clear();
    }

    /// <summary>Named commands a scenario can invoke ("teleport", "give_money", "load_field").
    /// Register them from game code: HarnessHooks.Register("give_money", arg => Wallet.Add(int.Parse(arg)));</summary>
    public static class HarnessHooks
    {
        static readonly Dictionary<string, Action<string>> hooks = new();
        public static void Register(string name, Action<string> fn) => hooks[name] = fn;
        public static bool Invoke(string name, string arg)
        {
            if (!hooks.TryGetValue(name, out var fn)) return false;
            fn(arg);
            return true;
        }
    }
}
