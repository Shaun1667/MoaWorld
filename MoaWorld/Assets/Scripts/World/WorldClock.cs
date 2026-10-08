using System;
using UnityEngine;

namespace MoaWorld
{
    // World time and the day/night cycle. Day and night each last GameConfig.dayNightPhaseMinutes.
    // Drives the sun light, ambient light and sky exposure.
    public class WorldClock : MonoBehaviour
    {
        private const float NewWorldStartProgress = 0.25f; // a new world starts mid-morning
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        [SerializeField] private Light sun;
        [SerializeField] private float sunYaw = 230f;

        [Header("Day")]
        [SerializeField] private Color dayLightColor = new Color(1f, 0.96f, 0.88f);
        [SerializeField] private float dayLightMin = 0.4f;
        [SerializeField] private float dayLightMax = 2f;
        [SerializeField] private float dayAmbient = 1f;
        [SerializeField] private float daySkyMin = 0.5f;
        [SerializeField] private float daySkyMax = 1.3f;

        [Header("Night")]
        [SerializeField] private Color nightLightColor = new Color(0.55f, 0.65f, 1f);
        [SerializeField] private float nightLight = 0.15f;
        [SerializeField] private float nightAmbient = 0.35f;
        [SerializeField] private float nightSky = 0.12f;

        private Material originalSkybox;
        private Material skyboxInstance;
        private bool wasNight;

        public static WorldClock Instance { get; private set; }

        // Seconds since the world was created. Saved with the world once saving exists.
        public double WorldTime { get; private set; }

        public bool IsNight { get; private set; }
        public float PhaseProgress { get; private set; }
        public float PhaseRemainingSeconds => (1f - PhaseProgress) * PhaseSeconds;

        private static float PhaseSeconds => GameConfig.Instance.dayNightPhaseMinutes * 60f;

        public event Action<bool> PhaseChanged;

        private void Awake()
        {
            Instance = this;
            WorldTime = PhaseSeconds * NewWorldStartProgress;

            // Work on a copy so play mode never edits the skybox asset.
            originalSkybox = RenderSettings.skybox;
            if (originalSkybox != null)
            {
                skyboxInstance = new Material(originalSkybox);
                RenderSettings.skybox = skyboxInstance;
            }

            UpdatePhase();
            wasNight = IsNight;
            ApplyLighting();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            if (skyboxInstance != null)
            {
                RenderSettings.skybox = originalSkybox;
                Destroy(skyboxInstance);
            }
        }

        private void Update()
        {
            WorldTime += Time.deltaTime;
            UpdatePhase();
            if (IsNight != wasNight)
            {
                wasNight = IsNight;
                PhaseChanged?.Invoke(IsNight);
            }
            ApplyLighting();
        }

        private void UpdatePhase()
        {
            double phases = WorldTime / PhaseSeconds;
            long phaseIndex = (long)Math.Floor(phases);
            IsNight = phaseIndex % 2 == 1;
            PhaseProgress = (float)(phases - phaseIndex);
        }

        private void ApplyLighting()
        {
            // Sun (or moon) rises at the start of each phase and sets at the end.
            float arc = Mathf.Sin(PhaseProgress * Mathf.PI);
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(5f, 175f, PhaseProgress), sunYaw, 0f);
                sun.color = IsNight ? nightLightColor : dayLightColor;
                sun.intensity = IsNight ? nightLight : Mathf.Lerp(dayLightMin, dayLightMax, arc);
            }

            RenderSettings.ambientIntensity = IsNight ? nightAmbient : Mathf.Lerp(nightAmbient, dayAmbient, Mathf.Sqrt(arc));
            if (skyboxInstance != null && skyboxInstance.HasProperty(ExposureId))
            {
                skyboxInstance.SetFloat(ExposureId, IsNight ? nightSky : Mathf.Lerp(daySkyMin, daySkyMax, arc));
            }
        }

        // For testing and for loading a saved world.
        public void SetWorldTime(double seconds)
        {
            WorldTime = seconds;
            UpdatePhase();
        }
    }
}
