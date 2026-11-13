using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Studio.Pacman
{
    /// <summary>
    /// Camera look: a soft bloom so the lanterns and cherries glow, a vignette and a touch of colour grading, plus short kicks
    /// (screen shake, chromatic pulse, zoom punch) for impacts. Everything is created at runtime so the
    /// scene file needs no setup.
    /// </summary>
    public class PacmanPost : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private float bloomThreshold = 0.9f;
        [SerializeField] private float bloomIntensity = 0.85f;
        [SerializeField] private float vignette = 0.32f;

        private Camera cam;
        private Vector3 basePosition;
        private float baseSize;
        private float shake;
        private float shakeTime;
        private float shakeDuration = 0.01f;
        private float punch;
        private float chroma;
        private Bloom bloom;
        private ChromaticAberration chromatic;
        private Vignette vignetteEffect;

        /// <summary>Sets up the volume and the camera effects. Call after the camera is positioned.</summary>
        public static PacmanPost Attach(Camera camera)
        {
            PacmanPost post = camera.gameObject.GetComponent<PacmanPost>();
            if (post == null)
            {
                post = camera.gameObject.AddComponent<PacmanPost>();
            }
            post.Setup(camera);
            return post;
        }

        /// <summary>Changes the resting zoom, for when the screen shape changes.</summary>
        public void SetBaseSize(float size)
        {
            baseSize = size;
        }

        private void Setup(Camera camera)
        {
            cam = camera;
            basePosition = cam.transform.position;
            baseSize = cam.orthographicSize;

            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;

            GameObject host = new GameObject("PostFx");
            host.transform.SetParent(transform, false);
            Volume volume = host.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = bloomThreshold;
            bloom.intensity.value = bloomIntensity;
            bloom.scatter.value = 0.65f;
            bloom.clamp.value = 20f;
            bloom.highQualityFiltering.value = true;

            vignetteEffect = profile.Add<Vignette>(true);
            vignetteEffect.intensity.value = vignette;
            vignetteEffect.smoothness.value = 0.55f;
            vignetteEffect.rounded.value = false;

            ColorAdjustments grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.value = 0.05f;
            grade.contrast.value = 14f;
            grade.saturation.value = 14f;
            grade.colorFilter.value = new Color(1f, 0.96f, 0.88f); // a warm, late-afternoon tint

            FilmGrain grain = profile.Add<FilmGrain>(true);
            grain.type.value = FilmGrainLookup.Thin1;
            grain.intensity.value = 0.18f;
            grain.response.value = 0.7f;

            chromatic = profile.Add<ChromaticAberration>(true);
            chromatic.intensity.value = 0f;
        }

        /// <summary>Shakes the camera. amount is world units at full strength.</summary>
        public void Shake(float amount, float seconds)
        {
            shake = Mathf.Max(shake, amount);
            shakeDuration = Mathf.Max(0.01f, seconds);
            shakeTime = shakeDuration;
        }

        /// <summary>Quick zoom-in that eases back, with a chromatic flash. strength 0..1.</summary>
        public void Punch(float strength)
        {
            punch = Mathf.Max(punch, strength);
            chroma = Mathf.Max(chroma, strength);
        }

        private void LateUpdate()
        {
            if (cam == null)
            {
                return;
            }

            punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 2.2f);
            chroma = Mathf.MoveTowards(chroma, 0f, Time.unscaledDeltaTime * 1.8f);
            cam.orthographicSize = baseSize * (1f - 0.035f * punch * punch);
            chromatic.intensity.value = chroma * 0.6f;
            bloom.intensity.value = bloomIntensity + punch * 0.6f;

            Vector3 offset = Vector3.zero;
            if (shakeTime > 0f)
            {
                shakeTime -= Time.unscaledDeltaTime;
                float falloff = Mathf.Clamp01(shakeTime / shakeDuration);
                offset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * (shake * falloff);
                if (shakeTime <= 0f)
                {
                    shake = 0f;
                }
            }
            cam.transform.position = basePosition + offset;
        }
    }
}
