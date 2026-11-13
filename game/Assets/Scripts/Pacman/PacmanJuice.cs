using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Cosmetic motion for the maze: a gentle bob for ghost bodies and a slow pulse for power pellets.
    /// Purely visual; it never touches gameplay state.
    /// </summary>
    public class PacmanJuice : MonoBehaviour
    {
        public enum Kind { Bob, Pulse }

        [Tooltip("Bob height in world units (Bob) or scale swing (Pulse).")]
        [SerializeField] private float amplitude = 0.06f;

        [Tooltip("Cycles per second.")]
        [SerializeField] private float rate = 3f;

        [SerializeField] private Kind kind = Kind.Bob;

        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private float phase;

        /// <summary>Attaches the effect to a transform and records its rest pose.</summary>
        public static PacmanJuice Attach(Transform target, Kind kind, float amplitude, float rate, float phase)
        {
            PacmanJuice juice = target.gameObject.AddComponent<PacmanJuice>();
            juice.kind = kind;
            juice.amplitude = amplitude;
            juice.rate = rate;
            juice.phase = phase;
            juice.baseLocalPosition = target.localPosition;
            juice.baseLocalScale = target.localScale;
            return juice;
        }

        private void Update()
        {
            float wave = Mathf.Sin((Time.time * rate + phase) * Mathf.PI * 2f);
            if (kind == Kind.Bob)
            {
                transform.localPosition = baseLocalPosition + Vector3.up * (wave * amplitude);
            }
            else
            {
                transform.localScale = baseLocalScale * (1f + wave * amplitude);
            }
        }
    }
}
