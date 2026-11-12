using UnityEngine;

namespace Harness
{
    /// <summary>Minimal "game" for harness selftests: moves with HarnessInput, reports metrics. Not for real gameplay.</summary>
    public class SelfTestMover : MonoBehaviour
    {
        public float speed = 5f;

        void Update()
        {
            var v = new Vector3(HarnessInput.Axis("steer"), 0, HarnessInput.Axis("throttle")) * speed;
            transform.position += v * Time.deltaTime;
            HarnessMetrics.Report("selftest.speed", v.magnitude);
            HarnessMetrics.Report("selftest.z", transform.position.z);
        }
    }
}
