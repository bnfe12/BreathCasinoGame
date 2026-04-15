using BreathCasino.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BreathCasino.Rendering
{
    public sealed class BCCameraGrainFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            [Header("Film Grain")]
            [Range(0f, 0.5f)] public float grainStrength = 0.16f;
            [Range(0.5f, 4f)] public float grainSize = 1.4f;
            [Range(0.5f, 12f)] public float grainSpeed = 3.0f;

            [Header("Vignette")]
            [Range(0f, 1f)] public float vignetteStrength = 0.55f;
            [Range(0.1f, 0.9f)] public float vignetteRadius = 0.45f;
            [Range(0f, 0.5f)] public float vignetteSoftness = 0.30f;

            [Header("Color Grade")]
            [Range(-2f, 0f)] public float exposure = -0.40f;
            [Range(0.5f, 2f)] public float contrast = 1.25f;
            [Range(0f, 1f)] public float saturation = 0.50f;
            public Color shadowTint = new(0.17f, 0.12f, 0.04f, 0f);

            [Header("Object Fringe")]
            [Range(0f, 1f)] public float outlineStrength = 0.46f;
            [Range(0.5f, 4f)] public float outlineThickness = 0.95f;
            [Range(0.0001f, 0.02f)] public float outlineDepthThreshold = 0.0025f;
            [Range(0f, 0.03f)] public float chromaticAberration = 0.0038f;
            [Range(0f, 0.02f)] public float edgeFringeStrength = 0.004f;
            public Color outlineColor = new(0.02f, 0.02f, 0.03f, 1f);
            [Range(0f, 1f)] public float outlineFogFadeStart = 0.08f;
            [Range(0f, 1f)] public float outlineFogFadeEnd = 0.42f;
            [Range(0f, 1f)] public float outlineDistanceThinness = 0.68f;

            [Header("Impact Haze")]
            [Range(0f, 2f)] public float glareStrength = 0.28f;
            [Range(0f, 1f)] public float veilStrength = 0.06f;
            [Range(0.2f, 0.95f)] public float glareThreshold = 0.58f;

            [Header("Flat Shade")]
            [Range(0f, 1f)] public float flatShadeBlend = 0f;
            [Range(2f, 8f)] public float flatShadeSteps = 4f;
            [Range(0f, 1f)] public float shadowCrush = 0f;
        }

        private enum PhaseFxProfile
        {
            None,
            BulletReveal,
            Dealing,
            Attack,
            Defense,
            Resolution,
            Shooting,
            RoundOver,
            GameOver
        }

        private readonly struct RuntimeFrameSettings
        {
            public readonly float grainStrength;
            public readonly float grainSize;
            public readonly float grainSpeed;
            public readonly float vignetteStrength;
            public readonly float vignetteRadius;
            public readonly float vignetteSoftness;
            public readonly float exposure;
            public readonly float contrast;
            public readonly float saturation;
            public readonly Color shadowTint;
            public readonly float blurStrength;
            public readonly float channelSplit;
            public readonly Color pulseTint;
            public readonly float outlineStrength;
            public readonly float outlineThickness;
            public readonly float outlineDepthThreshold;
            public readonly float edgeFringeStrength;
            public readonly Color outlineColor;
            public readonly float glareStrength;
            public readonly float veilStrength;
            public readonly float glareThreshold;
            public readonly float outlineFogFadeStart;
            public readonly float outlineFogFadeEnd;
            public readonly float outlineDistanceThinness;
            public readonly float flatShadeBlend;
            public readonly float flatShadeSteps;
            public readonly float shadowCrush;

            public RuntimeFrameSettings(
                float grainStrength,
                float grainSize,
                float grainSpeed,
                float vignetteStrength,
                float vignetteRadius,
                float vignetteSoftness,
                float exposure,
                float contrast,
                float saturation,
                Color shadowTint,
                float blurStrength,
                float channelSplit,
                Color pulseTint,
                float outlineStrength,
                float outlineThickness,
                float outlineDepthThreshold,
                float edgeFringeStrength,
                Color outlineColor,
                float glareStrength,
                float veilStrength,
                float glareThreshold,
                float outlineFogFadeStart,
                float outlineFogFadeEnd,
                float outlineDistanceThinness,
                float flatShadeBlend,
                float flatShadeSteps,
                float shadowCrush)
            {
                this.grainStrength = grainStrength;
                this.grainSize = grainSize;
                this.grainSpeed = grainSpeed;
                this.vignetteStrength = vignetteStrength;
                this.vignetteRadius = vignetteRadius;
                this.vignetteSoftness = vignetteSoftness;
                this.exposure = exposure;
                this.contrast = contrast;
                this.saturation = saturation;
                this.shadowTint = shadowTint;
                this.blurStrength = blurStrength;
                this.channelSplit = channelSplit;
                this.pulseTint = pulseTint;
                this.outlineStrength = outlineStrength;
                this.outlineThickness = outlineThickness;
                this.outlineDepthThreshold = outlineDepthThreshold;
                this.edgeFringeStrength = edgeFringeStrength;
                this.outlineColor = outlineColor;
                this.glareStrength = glareStrength;
                this.veilStrength = veilStrength;
                this.glareThreshold = glareThreshold;
                this.outlineFogFadeStart = outlineFogFadeStart;
                this.outlineFogFadeEnd = outlineFogFadeEnd;
                this.outlineDistanceThinness = outlineDistanceThinness;
                this.flatShadeBlend = flatShadeBlend;
                this.flatShadeSteps = flatShadeSteps;
                this.shadowCrush = shadowCrush;
            }
        }

        private static float s_flatShadeBlendOverride;
        private static float s_flatShadeStepsOverride = 4f;
        private static float s_shadowCrushOverride;

        private static class RuntimeFx
        {
            private static int s_lastFrame = -1;
            private static RuntimeFrameSettings s_cached;

            private static float s_shotPulse;
            private static float s_damagePulse;
            private static float s_transitionPulse;
            private static float s_lastBreathPulse;
            private static float s_phasePulse;
            private static float s_criticalWeight;
            private static float s_lastBreathWeight;
            private static float s_deathWeight;

            private static bool s_targetCritical;
            private static bool s_targetLastBreath;
            private static bool s_targetDeath;
            private static PhaseFxProfile s_phaseProfile = PhaseFxProfile.None;

            public static void Reset()
            {
                s_lastFrame = -1;
                s_cached = default;
                s_shotPulse = 0f;
                s_damagePulse = 0f;
                s_transitionPulse = 0f;
                s_lastBreathPulse = 0f;
                s_phasePulse = 0f;
                s_criticalWeight = 0f;
                s_lastBreathWeight = 0f;
                s_deathWeight = 0f;
                s_targetCritical = false;
                s_targetLastBreath = false;
                s_targetDeath = false;
                s_phaseProfile = PhaseFxProfile.None;
            }

            public static void TriggerShot(float intensity)
            {
                s_shotPulse = Mathf.Max(s_shotPulse, Mathf.Clamp01(intensity));
            }

            public static void TriggerDamage(float intensity)
            {
                s_damagePulse = Mathf.Max(s_damagePulse, Mathf.Clamp01(intensity));
            }

            public static void TriggerTransition(float intensity)
            {
                s_transitionPulse = Mathf.Max(s_transitionPulse, Mathf.Clamp01(intensity));
            }

            public static void TriggerLastBreath()
            {
                s_lastBreathPulse = Mathf.Max(s_lastBreathPulse, 1f);
                s_damagePulse = Mathf.Max(s_damagePulse, 0.85f);
                s_targetCritical = true;
                s_targetLastBreath = true;
            }

            public static void TriggerDeath()
            {
                s_targetDeath = true;
                s_targetCritical = false;
                s_targetLastBreath = false;
                s_damagePulse = Mathf.Max(s_damagePulse, 1f);
                s_phaseProfile = PhaseFxProfile.GameOver;
                s_phasePulse = Mathf.Max(s_phasePulse, 1f);
            }

            public static void SetSurvivalState(bool critical, bool lastBreath)
            {
                s_targetCritical = critical || lastBreath;
                s_targetLastBreath = lastBreath;

                if (!lastBreath)
                {
                    s_lastBreathPulse = Mathf.Min(s_lastBreathPulse, 0.45f);
                }
            }

            public static void TriggerPhase(GamePhase phase)
            {
                s_phaseProfile = phase switch
                {
                    GamePhase.BulletReveal => PhaseFxProfile.BulletReveal,
                    GamePhase.Dealing => PhaseFxProfile.Dealing,
                    GamePhase.Attack => PhaseFxProfile.Attack,
                    GamePhase.Defense => PhaseFxProfile.Defense,
                    GamePhase.Resolution => PhaseFxProfile.Resolution,
                    GamePhase.Shooting => PhaseFxProfile.Shooting,
                    GamePhase.RoundOver => PhaseFxProfile.RoundOver,
                    GamePhase.GameOver => PhaseFxProfile.GameOver,
                    _ => PhaseFxProfile.None
                };

                if (phase == GamePhase.GameOver)
                {
                    TriggerDeath();
                    return;
                }

                s_phasePulse = Mathf.Max(s_phasePulse, GetPhasePulseStrength(s_phaseProfile));
            }

            public static RuntimeFrameSettings Sample(Settings baseSettings)
            {
                if (s_lastFrame != Time.frameCount)
                {
                    UpdateState();
                    s_lastFrame = Time.frameCount;
                }

                float breathPulse =
                    (Mathf.Sin(Time.unscaledTime * (s_lastBreathWeight > 0.01f ? 7.5f : 4.0f)) * 0.5f + 0.5f) *
                    Mathf.Max(s_criticalWeight * 0.35f, s_lastBreathWeight * 0.9f);

                float phaseBlur = GetPhaseBlur(s_phaseProfile) * s_phasePulse;
                float phaseExposure = GetPhaseExposure(s_phaseProfile) * s_phasePulse;
                float phaseContrast = GetPhaseContrast(s_phaseProfile) * s_phasePulse;
                float phaseGrain = GetPhaseGrain(s_phaseProfile) * s_phasePulse;
                float phaseVignette = GetPhaseVignette(s_phaseProfile) * s_phasePulse;

                Color tint = Color.clear;
                tint += GetPhaseTint(s_phaseProfile) * s_phasePulse;
                tint += new Color(0.55f, 0.08f, 0.05f, 0.18f) * s_damagePulse;
                tint += new Color(0.32f, 0.24f, 0.12f, 0.10f) * s_shotPulse;
                tint += new Color(0.10f, 0.18f, 0.30f, 0.08f) * s_criticalWeight;
                tint += new Color(0.12f, 0.22f, 0.38f, 0.14f) * (s_lastBreathWeight + breathPulse * 0.4f);
                tint += new Color(0.18f, 0.02f, 0.02f, 0.16f) * s_deathWeight;
                tint.a = Mathf.Clamp01(tint.a);
                tint.r = Mathf.Clamp01(tint.r);
                tint.g = Mathf.Clamp01(tint.g);
                tint.b = Mathf.Clamp01(tint.b);

                Color shadowTint = baseSettings.shadowTint;
                shadowTint += new Color(0.10f, -0.01f, -0.01f, 0f) * s_damagePulse;
                shadowTint += new Color(-0.01f, 0.02f, 0.08f, 0f) * (s_criticalWeight + s_lastBreathWeight);
                shadowTint += new Color(0.06f, 0.02f, 0.00f, 0f) * s_phasePulse * (s_phaseProfile == PhaseFxProfile.Attack ? 1f : 0f);

                float grainStrength = Mathf.Clamp(
                    baseSettings.grainStrength +
                    phaseGrain +
                    s_shotPulse * 0.05f +
                    s_damagePulse * 0.08f +
                    s_criticalWeight * 0.04f +
                    s_lastBreathWeight * 0.07f +
                    s_deathWeight * 0.12f,
                    0f,
                    0.5f);

                float vignetteStrength = Mathf.Clamp01(
                    baseSettings.vignetteStrength +
                    phaseVignette +
                    s_transitionPulse * 0.05f +
                    s_damagePulse * 0.18f +
                    s_criticalWeight * 0.10f +
                    s_lastBreathWeight * 0.18f +
                    breathPulse * 0.14f +
                    s_deathWeight * 0.32f);

                float vignetteRadius = Mathf.Clamp(
                    baseSettings.vignetteRadius -
                    s_criticalWeight * 0.04f -
                    s_lastBreathWeight * 0.06f -
                    breathPulse * 0.05f -
                    s_deathWeight * 0.12f,
                    0.1f,
                    0.9f);

                float vignetteSoftness = Mathf.Clamp(
                    baseSettings.vignetteSoftness +
                    s_damagePulse * 0.04f +
                    s_lastBreathWeight * 0.03f +
                    s_deathWeight * 0.08f,
                    0f,
                    0.5f);

                float exposure = Mathf.Clamp(
                    baseSettings.exposure +
                    phaseExposure +
                    s_shotPulse * 0.18f -
                    s_damagePulse * 0.14f -
                    s_criticalWeight * 0.08f -
                    s_lastBreathWeight * 0.10f -
                    breathPulse * 0.08f -
                    s_deathWeight * 0.25f,
                    -2f,
                    0.35f);

                float contrast = Mathf.Clamp(
                    baseSettings.contrast +
                    phaseContrast +
                    s_shotPulse * 0.05f +
                    s_damagePulse * 0.06f +
                    s_deathWeight * 0.12f,
                    0.5f,
                    2f);

                float saturation = Mathf.Clamp01(
                    baseSettings.saturation -
                    s_damagePulse * 0.08f -
                    s_criticalWeight * 0.14f -
                    s_lastBreathWeight * 0.22f -
                    breathPulse * 0.14f -
                    s_deathWeight * 0.55f +
                    (s_phaseProfile == PhaseFxProfile.RoundOver ? 0.08f * s_phasePulse : 0f));

                float blurStrength = Mathf.Clamp01(
                    phaseBlur +
                    s_transitionPulse * 0.12f +
                    s_shotPulse * 0.12f +
                    s_damagePulse * 0.68f +
                    s_criticalWeight * 0.06f +
                    s_lastBreathWeight * 0.18f +
                    breathPulse * 0.18f +
                    s_lastBreathPulse * 0.30f +
                    s_deathWeight * 0.45f);

                float channelSplit = Mathf.Clamp(
                    baseSettings.chromaticAberration +
                    s_shotPulse * 0.004f +
                    s_damagePulse * 0.011f +
                    s_lastBreathWeight * 0.005f +
                    s_lastBreathPulse * 0.008f +
                    s_deathWeight * 0.018f,
                    0f,
                    0.03f);

                float outlineStrength = Mathf.Clamp01(
                    baseSettings.outlineStrength +
                    s_damagePulse * 0.08f +
                    s_shotPulse * 0.05f +
                    s_lastBreathWeight * 0.05f +
                    s_deathWeight * 0.10f);

                float outlineThickness = Mathf.Clamp(
                    baseSettings.outlineThickness +
                    s_damagePulse * 0.20f +
                    s_shotPulse * 0.12f +
                    s_deathWeight * 0.30f,
                    0.5f,
                    4f);

                float outlineDepthThreshold = Mathf.Clamp(
                    baseSettings.outlineDepthThreshold -
                    s_damagePulse * 0.0005f -
                    s_shotPulse * 0.0003f,
                    0.0001f,
                    0.02f);

                float edgeFringeStrength = Mathf.Clamp(
                    baseSettings.edgeFringeStrength +
                    s_damagePulse * 0.0025f +
                    s_shotPulse * 0.0012f +
                    s_lastBreathWeight * 0.0015f +
                    s_deathWeight * 0.0020f,
                    0f,
                    0.02f);

                Color outlineColor = Color.Lerp(
                    baseSettings.outlineColor,
                    new Color(0.06f, 0.01f, 0.01f, 1f),
                    Mathf.Clamp01(s_damagePulse * 0.35f + s_deathWeight * 0.50f));

                float glareStrength = Mathf.Clamp(
                    baseSettings.glareStrength +
                    s_shotPulse * 0.30f +
                    s_damagePulse * 0.62f +
                    s_lastBreathWeight * 0.25f +
                    s_lastBreathPulse * 0.22f +
                    s_deathWeight * 0.40f,
                    0f,
                    2f);

                float veilStrength = Mathf.Clamp01(
                    baseSettings.veilStrength +
                    s_shotPulse * 0.10f +
                    s_damagePulse * 0.28f +
                    s_lastBreathWeight * 0.10f +
                    s_lastBreathPulse * 0.16f +
                    s_deathWeight * 0.22f);

                s_cached = new RuntimeFrameSettings(
                    grainStrength,
                    baseSettings.grainSize,
                    baseSettings.grainSpeed,
                    vignetteStrength,
                    vignetteRadius,
                    vignetteSoftness,
                    exposure,
                    contrast,
                    saturation,
                    shadowTint,
                    blurStrength,
                    channelSplit,
                    tint,
                    outlineStrength,
                    outlineThickness,
                    outlineDepthThreshold,
                    edgeFringeStrength,
                    outlineColor,
                    glareStrength,
                    veilStrength,
                    baseSettings.glareThreshold,
                    baseSettings.outlineFogFadeStart,
                    baseSettings.outlineFogFadeEnd,
                    baseSettings.outlineDistanceThinness,
                    Mathf.Clamp01(baseSettings.flatShadeBlend + s_flatShadeBlendOverride),
                    Mathf.Max(2f, s_flatShadeStepsOverride),
                    Mathf.Clamp01(baseSettings.shadowCrush + s_shadowCrushOverride));

                return s_cached;
            }

            private static void UpdateState()
            {
                float dt = Mathf.Max(Time.unscaledDeltaTime, 1f / 240f);

                s_shotPulse = Mathf.MoveTowards(s_shotPulse, 0f, dt / 0.20f);
                s_damagePulse = Mathf.MoveTowards(s_damagePulse, 0f, dt / 0.18f);
                s_transitionPulse = Mathf.MoveTowards(s_transitionPulse, 0f, dt / 0.35f);
                s_lastBreathPulse = Mathf.MoveTowards(s_lastBreathPulse, 0f, dt / 0.90f);
                s_phasePulse = Mathf.MoveTowards(s_phasePulse, 0f, dt / 0.75f);

                s_criticalWeight = Mathf.MoveTowards(s_criticalWeight, s_targetCritical ? 1f : 0f, dt / 0.85f);
                s_lastBreathWeight = Mathf.MoveTowards(s_lastBreathWeight, s_targetLastBreath ? 1f : 0f, dt / 0.50f);
                s_deathWeight = Mathf.MoveTowards(s_deathWeight, s_targetDeath ? 1f : 0f, dt / 1.10f);

                if (s_phasePulse <= 0.001f && !s_targetDeath)
                {
                    s_phaseProfile = PhaseFxProfile.None;
                }
            }

            private static float GetPhasePulseStrength(PhaseFxProfile profile)
            {
                return profile switch
                {
                    PhaseFxProfile.BulletReveal => 0.75f,
                    PhaseFxProfile.Dealing => 0.35f,
                    PhaseFxProfile.Attack => 0.55f,
                    PhaseFxProfile.Defense => 0.55f,
                    PhaseFxProfile.Resolution => 0.45f,
                    PhaseFxProfile.Shooting => 0.85f,
                    PhaseFxProfile.RoundOver => 0.80f,
                    PhaseFxProfile.GameOver => 1f,
                    _ => 0f
                };
            }

            private static float GetPhaseBlur(PhaseFxProfile profile)
            {
                return profile switch
                {
                    PhaseFxProfile.BulletReveal => 0.05f,
                    PhaseFxProfile.Dealing => 0.03f,
                    PhaseFxProfile.Attack => 0.04f,
                    PhaseFxProfile.Defense => 0.05f,
                    PhaseFxProfile.Resolution => 0.06f,
                    PhaseFxProfile.Shooting => 0.14f,
                    PhaseFxProfile.RoundOver => 0.08f,
                    PhaseFxProfile.GameOver => 0.20f,
                    _ => 0f
                };
            }

            private static float GetPhaseExposure(PhaseFxProfile profile)
            {
                return profile switch
                {
                    PhaseFxProfile.BulletReveal => -0.04f,
                    PhaseFxProfile.Dealing => 0.04f,
                    PhaseFxProfile.Attack => 0.06f,
                    PhaseFxProfile.Defense => -0.02f,
                    PhaseFxProfile.Resolution => 0.02f,
                    PhaseFxProfile.Shooting => -0.05f,
                    PhaseFxProfile.RoundOver => 0.08f,
                    PhaseFxProfile.GameOver => -0.18f,
                    _ => 0f
                };
            }

            private static float GetPhaseContrast(PhaseFxProfile profile)
            {
                return profile switch
                {
                    PhaseFxProfile.Attack => 0.08f,
                    PhaseFxProfile.Defense => 0.04f,
                    PhaseFxProfile.Resolution => 0.06f,
                    PhaseFxProfile.Shooting => 0.10f,
                    PhaseFxProfile.GameOver => 0.12f,
                    _ => 0f
                };
            }

            private static float GetPhaseGrain(PhaseFxProfile profile)
            {
                return profile switch
                {
                    PhaseFxProfile.BulletReveal => 0.03f,
                    PhaseFxProfile.Attack => 0.02f,
                    PhaseFxProfile.Defense => 0.015f,
                    PhaseFxProfile.Shooting => 0.04f,
                    PhaseFxProfile.GameOver => 0.06f,
                    _ => 0f
                };
            }

            private static float GetPhaseVignette(PhaseFxProfile profile)
            {
                return profile switch
                {
                    PhaseFxProfile.BulletReveal => 0.08f,
                    PhaseFxProfile.Attack => 0.04f,
                    PhaseFxProfile.Defense => 0.06f,
                    PhaseFxProfile.Shooting => 0.10f,
                    PhaseFxProfile.GameOver => 0.20f,
                    _ => 0f
                };
            }

            private static Color GetPhaseTint(PhaseFxProfile profile)
            {
                return profile switch
                {
                    PhaseFxProfile.BulletReveal => new Color(0.16f, 0.12f, 0.05f, 0.08f),
                    PhaseFxProfile.Dealing => new Color(0.08f, 0.06f, 0.02f, 0.04f),
                    PhaseFxProfile.Attack => new Color(0.20f, 0.10f, 0.03f, 0.07f),
                    PhaseFxProfile.Defense => new Color(0.04f, 0.10f, 0.20f, 0.07f),
                    PhaseFxProfile.Resolution => new Color(0.12f, 0.12f, 0.12f, 0.05f),
                    PhaseFxProfile.Shooting => new Color(0.22f, 0.08f, 0.04f, 0.10f),
                    PhaseFxProfile.RoundOver => new Color(0.20f, 0.14f, 0.05f, 0.08f),
                    PhaseFxProfile.GameOver => new Color(0.16f, 0.03f, 0.03f, 0.14f),
                    _ => Color.clear
                };
            }
        }

        public static void ResetRuntime() => RuntimeFx.Reset();
        public static void TriggerShotPulse(float intensity = 1f) => RuntimeFx.TriggerShot(intensity);
        public static void TriggerDamagePulse(float intensity = 1f) => RuntimeFx.TriggerDamage(intensity);
        public static void TriggerTransitionPulse(float intensity = 1f) => RuntimeFx.TriggerTransition(intensity);
        public static void TriggerLastBreathPulse() => RuntimeFx.TriggerLastBreath();
        public static void TriggerDeathPulse() => RuntimeFx.TriggerDeath();
        public static void SetSurvivalState(bool critical, bool lastBreath) => RuntimeFx.SetSurvivalState(critical, lastBreath);
        public static void TriggerPhasePulse(GamePhase phase) => RuntimeFx.TriggerPhase(phase);
        public static void SetFlatShadeProfile(bool enabled)
        {
            s_flatShadeBlendOverride = enabled ? 0.74f : 0f;
            s_flatShadeStepsOverride = enabled ? 4.5f : 4f;
            s_shadowCrushOverride = enabled ? 0.12f : 0f;
        }

        public Settings settings = new();

        private BCCameraGrainPass _pass;
        private Material _material;

        public override void Create()
        {
            Shader shader = Shader.Find("BreathCasino/CameraGrain");
            if (shader == null)
            {
                Debug.LogWarning(
                    "[BCCameraGrainFeature] Shader 'BreathCasino/CameraGrain' not found.\n" +
                    "Make sure Assets/Shaders/BCCameraGrain.shader exists and is available to the renderer.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new BCCameraGrainPass();
            _pass.Setup(_material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _material == null)
            {
                return;
            }

            var cam = renderingData.cameraData;
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection)
            {
                return;
            }

            PushMaterialProperties();
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _pass = null;
        }

        private void PushMaterialProperties()
        {
            if (_material == null)
            {
                return;
            }

            RuntimeFrameSettings runtime = RuntimeFx.Sample(settings);

            _material.SetFloat("_GrainStrength", runtime.grainStrength);
            _material.SetFloat("_GrainSize", runtime.grainSize);
            _material.SetFloat("_GrainSpeed", runtime.grainSpeed);
            _material.SetFloat("_VignetteStrength", runtime.vignetteStrength);
            _material.SetFloat("_VignetteRadius", runtime.vignetteRadius);
            _material.SetFloat("_VignetteSoftness", runtime.vignetteSoftness);
            _material.SetFloat("_Exposure", runtime.exposure);
            _material.SetFloat("_Contrast", runtime.contrast);
            _material.SetFloat("_Saturation", runtime.saturation);
            _material.SetColor("_ShadowTint", runtime.shadowTint);
            _material.SetFloat("_BlurStrength", runtime.blurStrength);
            _material.SetFloat("_ChannelSplit", runtime.channelSplit);
            _material.SetColor("_PulseTint", runtime.pulseTint);
            _material.SetFloat("_OutlineStrength", runtime.outlineStrength);
            _material.SetFloat("_OutlineThickness", runtime.outlineThickness);
            _material.SetFloat("_OutlineDepthThreshold", runtime.outlineDepthThreshold);
            _material.SetFloat("_EdgeFringeStrength", runtime.edgeFringeStrength);
            _material.SetColor("_OutlineColor", runtime.outlineColor);
            _material.SetFloat("_GlareStrength", runtime.glareStrength);
            _material.SetFloat("_VeilStrength", runtime.veilStrength);
            _material.SetFloat("_GlareThreshold", runtime.glareThreshold);
            _material.SetFloat("_OutlineFogFadeStart", runtime.outlineFogFadeStart);
            _material.SetFloat("_OutlineFogFadeEnd", runtime.outlineFogFadeEnd);
            _material.SetFloat("_OutlineDistanceThinness", runtime.outlineDistanceThinness);
            _material.SetFloat("_FlatShadeBlend", runtime.flatShadeBlend);
            _material.SetFloat("_FlatShadeSteps", runtime.flatShadeSteps);
            _material.SetFloat("_ShadowCrush", runtime.shadowCrush);
        }
    }
}
