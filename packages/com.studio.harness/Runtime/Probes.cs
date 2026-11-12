using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Harness
{
    /// <summary>
    /// Reads game state from the OUTSIDE by reflection, declared in the scenario JSON — no game code changes:
    ///   {"name": "vehicle.speed", "target": "Player", "component": "Rigidbody", "member": "linearVelocity.magnitude"}
    ///   {"name": "crops.cut",     "target": "Field",  "component": "FieldManager", "member": "CutCount"}
    /// target = GameObject name (or path "Root/Child"); member = dotted chain of public/private fields or properties.
    /// Numbers, bools (1/0) and enums are recorded each frame into HarnessMetrics.
    /// </summary>
    [Serializable]
    public class Probe
    {
        public string name, target, component, member;

        [NonSerialized] Component comp;
        [NonSerialized] MemberInfo[] chain;
        [NonSerialized] bool warned;

        /// <summary>Probe resolution failures, reported in metrics.json so the runner can tell "the game broke"
        /// from "a probe points at a renamed field" (the latter goes to the director, not to failed/).</summary>
        public static readonly List<string> Errors = new();

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public void Sample()
        {
            if (comp == null && !Resolve()) return;
            object v = comp;
            foreach (var m in chain)
            {
                if (v == null) return;
                v = m is PropertyInfo p ? p.GetValue(v) : ((FieldInfo)m).GetValue(v);
            }
            if (v is bool b) HarnessMetrics.Report(name, b ? 1 : 0);
            else if (v is Enum e) HarnessMetrics.Report(name, Convert.ToInt32(e));
            else if (v is IConvertible) HarnessMetrics.Report(name, Convert.ToDouble(v));
            else Fail($"{member} is {v?.GetType().Name}, not a number");
        }

        bool Resolve()
        {
            var go = GameObject.Find(target);
            if (go == null) return false;               // may spawn later; retry next frame
            comp = string.IsNullOrEmpty(component) || component == "Transform" ? go.transform : go.GetComponent(component);
            if (comp == null) { Fail($"no component '{component}' on '{target}'"); return false; }
            var list = new List<MemberInfo>();
            var t = comp.GetType();
            foreach (var part in member.Split('.'))
            {
                MemberInfo m = t.GetProperty(part, Flags) ?? (MemberInfo)t.GetField(part, Flags);
                if (m == null) { Fail($"no member '{part}' on {t.Name}"); comp = null; return false; }
                list.Add(m);
                t = m is PropertyInfo p ? p.PropertyType : ((FieldInfo)m).FieldType;
            }
            chain = list.ToArray();
            return true;
        }

        void Fail(string msg)
        {
            if (warned) return;
            warned = true;
            Errors.Add($"{name}: {msg}");
            Debug.LogError($"[Harness] probe '{name}': {msg}");
        }
    }
}
