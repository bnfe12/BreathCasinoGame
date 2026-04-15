using System;
using System.Collections;
using System.Collections.Generic;
using BreathCasino.Core;
using BreathCasino.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#pragma warning disable CS0649

namespace BreathCasino.Gameplay
{
    public class BCIntroSequenceController : MonoBehaviour
    {
        [Serializable]
        private class IntroCaption
        {
            public string localizationKey;
            public float holdDuration = 2.3f;
        }

        [Header("Overlay")]
        [SerializeField] private Canvas introCanvas;
        [SerializeField] private Image blackOverlay;
        [SerializeField] private TextMeshProUGUI centerText;
        [SerializeField] private Button skipButton;
        [SerializeField] private TextMeshProUGUI skipButtonText;

        [Header("Sequence")]
        [SerializeField] private float textFadeDuration = 0.55f;
        [SerializeField] private float blackHoldBetweenCaptions = 0.55f;
        [SerializeField] private float captionHoldMultiplier = 1.55f;
        [SerializeField] private float pauseAfterText = 0.8f;
        [SerializeField] private float seatMoveDuration = 2.35f;
        [SerializeField] private float eyeRevealDuration = 1.75f;
        [SerializeField] private float eyeRevealTarget = 1.45f;
        [SerializeField] private float eyeRevealSoftness = 0.16f;
        [SerializeField] private float openingBlackoutDuration = 0.35f;
        [SerializeField] private float openingBlackoutHold = 2f;
        [SerializeField] private float bulletBlackoutDuration = 0.3f;
        [SerializeField] private float bulletBlackoutHold = 0.9f;
        [SerializeField] private float bulletPreviewSettleDelay = 0.18f;
        [SerializeField] private float dialoguePauseMin = 0.15f;
        [SerializeField] private float dialoguePauseMax = 0.55f;
        [SerializeField] private float glyphJumpAmplitude = 0.55f;
        [SerializeField] private float glyphShakeAmplitude = 0.72f;
        [SerializeField] private float glyphRotationAmplitude = 1.15f;
        [SerializeField] private float glyphJumpFrequency = 2.4f;
        [SerializeField] private float glyphShakeFrequency = 15f;
        [SerializeField] private float glyphPhaseOffset = 0.23f;

        [Header("Audio")]
        [SerializeField] private AudioClip textPulseClip;
        [SerializeField] private AudioClip seatMotionClip;
        [SerializeField] private AudioClip eyeOpenClip;

        [Header("Story")]
        [SerializeField] private IntroCaption[] captions =
        {
            new IntroCaption { localizationKey = "intro.caption.1", holdDuration = 2.1f },
            new IntroCaption { localizationKey = "intro.caption.2", holdDuration = 2.8f },
            new IntroCaption { localizationKey = "intro.caption.3", holdDuration = 2.5f },
            new IntroCaption { localizationKey = "intro.caption.4", holdDuration = 2.7f },
            new IntroCaption { localizationKey = "intro.caption.5", holdDuration = 2.4f }
        };

        private readonly List<GameObject> _previewBullets = new();

        private SceneBootstrap _bootstrap;
        private Material _eyeRevealMaterial;
        private bool _isRunning;
        private bool _isCaptionPhase;
        private bool _skipRequested;
        private bool _leverHoldCompleted;
        private Action _onComplete;

        public bool IsRunning => _isRunning;
        public bool IsAwaitingLeverHold { get; private set; }

        public void Initialize(SceneBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
            EnsureUi();
            ClearPreviewBullets();
            SetOverlayActive(false);
        }

        public void PlayIntro(Action onComplete)
        {
            if (_isRunning)
            {
                return;
            }

            _onComplete = onComplete;
            StartCoroutine(PlayIntroRoutine());
        }

        public void CompleteLeverHold()
        {
            if (!IsAwaitingLeverHold)
            {
                return;
            }

            _leverHoldCompleted = true;
        }

        private IEnumerator PlayIntroRoutine()
        {
            _isRunning = true;
            _skipRequested = false;
            _leverHoldCompleted = false;
            IsAwaitingLeverHold = false;

            EnsureUi();
            ClearPreviewBullets();
            SetOverlayActive(true);
            SetSkipVisible(true);
            SetTextAlpha(0f);
            SetBlackAlpha(1f);
            UseEyeRevealMaterial(true);
            SetReveal(0f);
            centerText.text = string.Empty;
            _bootstrap?.SetCameraMenuLock(true);

            Camera camera = _bootstrap != null ? _bootstrap.mainCamera : Camera.main;
            Transform startAnchor = _bootstrap != null ? _bootstrap.IntroCameraStart : null;
            Transform seatAnchor = _bootstrap != null ? _bootstrap.IntroCameraSeat : null;

            if (camera != null && startAnchor != null)
            {
                camera.transform.SetPositionAndRotation(startAnchor.position, startAnchor.rotation);
            }

            _isCaptionPhase = true;
            for (int i = 0; i < captions.Length; i++)
            {
                if (_skipRequested)
                {
                    break;
                }

                IntroCaption caption = captions[i];
                string localizedText = caption != null ? BCLocalization.Get(caption.localizationKey) : string.Empty;
                if (caption == null || string.IsNullOrWhiteSpace(localizedText))
                {
                    continue;
                }

                centerText.text = localizedText;
                PlayOptionalClip(textPulseClip);
                yield return FadeText(0f, 1f, textFadeDuration);
                yield return WaitSkippable(Mathf.Max(caption.holdDuration * captionHoldMultiplier, 0f));
                yield return FadeText(centerText.color.a, 0f, Mathf.Min(textFadeDuration, 0.18f));
                centerText.text = string.Empty;
                yield return WaitSkippable(blackHoldBetweenCaptions);
            }

            _isCaptionPhase = false;
            SetSkipVisible(false);
            centerText.text = string.Empty;
            yield return FadeText(centerText.color.a, 0f, 0.12f);
            yield return WaitSkippable(pauseAfterText);

            if (camera != null && startAnchor != null && seatAnchor != null)
            {
                PlayOptionalClip(seatMotionClip);
                yield return MoveCamera(camera.transform, startAnchor, seatAnchor, seatMoveDuration);
            }

            PlayOptionalClip(eyeOpenClip);
            yield return AnimateReveal(0f, eyeRevealTarget, eyeRevealDuration);
            UseEyeRevealMaterial(false);
            SetBlackAlpha(0f);

            yield return PlayDialogueSet("intro_opening", 2);
            yield return PlayRoomBlackout(openingBlackoutDuration, openingBlackoutHold);

            CreatePreviewBullets();
            yield return new WaitForSeconds(bulletPreviewSettleDelay);
            yield return PlayDialogueSet("intro_bullets", 2);
            yield return PlayRoomBlackout(
                bulletBlackoutDuration,
                bulletBlackoutHold,
                () => ClearPreviewBullets());

            yield return PlayDialogueSet("intro_lever", 2);
            IsAwaitingLeverHold = true;

            while (!_leverHoldCompleted)
            {
                yield return null;
            }

            IsAwaitingLeverHold = false;
            SetOverlayActive(false);
            _bootstrap?.SetCameraMenuLock(false);
            _isRunning = false;
            Action complete = _onComplete;
            _onComplete = null;
            complete?.Invoke();
        }

        private void LateUpdate()
        {
            AnimateCaptionGlyphs();
        }

        private IEnumerator PlayDialogueSet(string key, int maxLines)
        {
            BCEnemyDialogueController dialogue = _bootstrap != null ? _bootstrap.EnemyDialogueController : null;
            if (dialogue == null || !dialogue.TryGetLocalizedSet(key, out string[] lines, out float duration))
            {
                yield break;
            }

            int count = maxLines > 0 ? Mathf.Min(maxLines, lines.Length) : lines.Length;
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                dialogue.SpeakLine(lines[i], duration);
                yield return new WaitForSeconds(duration + UnityEngine.Random.Range(dialoguePauseMin, dialoguePauseMax));
            }
        }

        private IEnumerator PlayRoomBlackout(float duration, float holdDuration, Action onFullBlack = null)
        {
            yield return FadeBlack(blackOverlay != null ? blackOverlay.color.a : 0f, 1f, duration);

            if (_bootstrap != null)
            {
                yield return _bootstrap.AnimateRoomBlackout(true, duration);
            }

            onFullBlack?.Invoke();
            yield return new WaitForSeconds(Mathf.Max(holdDuration, 0f));

            if (_bootstrap != null)
            {
                yield return _bootstrap.AnimateRoomBlackout(false, duration);
            }

            yield return FadeBlack(blackOverlay != null ? blackOverlay.color.a : 1f, 0f, duration);
        }

        private IEnumerator WaitSkippable(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (_isCaptionPhase && _skipRequested)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator FadeText(float from, float to, float duration)
        {
            float elapsed = 0f;
            duration = Mathf.Max(duration, 0.01f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                SetTextAlpha(Mathf.Lerp(from, to, eased));
                yield return null;
            }

            SetTextAlpha(to);
        }

        private IEnumerator MoveCamera(Transform cameraTransform, Transform start, Transform end, float duration)
        {
            float elapsed = 0f;
            duration = Mathf.Max(duration, 0.01f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                cameraTransform.position = Vector3.Lerp(start.position, end.position, eased);
                cameraTransform.rotation = Quaternion.Slerp(start.rotation, end.rotation, eased);
                yield return null;
            }

            cameraTransform.SetPositionAndRotation(end.position, end.rotation);
        }

        private IEnumerator AnimateReveal(float from, float to, float duration)
        {
            float elapsed = 0f;
            duration = Mathf.Max(duration, 0.01f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                SetReveal(Mathf.Lerp(from, to, eased));
                yield return null;
            }

            SetReveal(to);
        }

        private void EnsureUi()
        {
            if (introCanvas == null)
            {
                GameObject canvasObject = new GameObject("IntroSequenceCanvas");
                introCanvas = canvasObject.AddComponent<Canvas>();
                introCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                introCanvas.sortingOrder = 590;
                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            introCanvas.transform.SetParent(null, false);
            introCanvas.transform.localScale = Vector3.one;

            if (blackOverlay == null)
            {
                GameObject overlayObject = new GameObject("IntroBlackOverlay");
                overlayObject.transform.SetParent(introCanvas.transform, false);
                RectTransform rect = overlayObject.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                blackOverlay = overlayObject.AddComponent<Image>();
                blackOverlay.color = Color.black;
                blackOverlay.raycastTarget = false;
            }

            EnsureEyeRevealMaterial();
            UseEyeRevealMaterial(false);

            if (centerText == null)
            {
                GameObject textObject = new GameObject("IntroCenterText");
                textObject.transform.SetParent(introCanvas.transform, false);
                RectTransform rect = textObject.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(980f, 300f);

                centerText = textObject.AddComponent<TextMeshProUGUI>();
                centerText.font = BCRuntimeFontProvider.GetTitleTmpFont(38);
                centerText.fontSize = 38;
                centerText.fontStyle = FontStyles.Bold;
                centerText.alignment = TextAlignmentOptions.Center;
                centerText.textWrappingMode = TextWrappingModes.Normal;
                centerText.overflowMode = TextOverflowModes.Overflow;
                centerText.color = new Color(0.94f, 0.94f, 0.92f, 0f);
                centerText.raycastTarget = false;
            }

            if (skipButton == null)
            {
                GameObject buttonObject = new GameObject("IntroSkipButton");
                buttonObject.transform.SetParent(introCanvas.transform, false);
                RectTransform rect = buttonObject.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-42f, -34f);
                rect.sizeDelta = new Vector2(180f, 52f);

                Image image = buttonObject.AddComponent<Image>();
                image.color = new Color(0.06f, 0.06f, 0.07f, 0.86f);

                skipButton = buttonObject.AddComponent<Button>();
                skipButton.targetGraphic = image;
                skipButton.onClick.AddListener(RequestSkip);

                GameObject textObject = new GameObject("Label");
                textObject.transform.SetParent(buttonObject.transform, false);
                RectTransform textRect = textObject.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10f, 6f);
                textRect.offsetMax = new Vector2(-10f, -6f);

                skipButtonText = textObject.AddComponent<TextMeshProUGUI>();
                skipButtonText.font = BCRuntimeFontProvider.GetTitleTmpFont(24);
                skipButtonText.fontSize = 24f;
                skipButtonText.alignment = TextAlignmentOptions.Center;
                skipButtonText.color = new Color(0.94f, 0.94f, 0.92f);
                skipButtonText.text = BCLocalization.Get("intro.skip");
                skipButtonText.raycastTarget = false;
            }

            if (skipButtonText != null)
            {
                skipButtonText.text = BCLocalization.Get("intro.skip");
            }
        }

        private void RequestSkip()
        {
            if (!_isCaptionPhase)
            {
                return;
            }

            _skipRequested = true;
        }

        private void EnsureEyeRevealMaterial()
        {
            if (_eyeRevealMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find("BreathCasino/UI/EyeReveal");
            if (shader == null)
            {
                return;
            }

            _eyeRevealMaterial = new Material(shader);
            _eyeRevealMaterial.hideFlags = HideFlags.DontSave;
            _eyeRevealMaterial.SetColor("_Color", Color.black);
            _eyeRevealMaterial.SetFloat("_Softness", eyeRevealSoftness);
        }

        private void UseEyeRevealMaterial(bool enabled)
        {
            if (blackOverlay == null)
            {
                return;
            }

            blackOverlay.material = enabled ? _eyeRevealMaterial : null;
        }

        private void SetOverlayActive(bool isActive)
        {
            if (introCanvas != null)
            {
                introCanvas.gameObject.SetActive(isActive);
            }
        }

        private void SetSkipVisible(bool isVisible)
        {
            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(isVisible);
            }
        }

        private void SetBlackAlpha(float alpha)
        {
            if (blackOverlay == null)
            {
                return;
            }

            Color color = blackOverlay.color;
            color.a = alpha;
            blackOverlay.color = color;
        }

        private void SetTextAlpha(float alpha)
        {
            if (centerText == null)
            {
                return;
            }

            Color color = centerText.color;
            color.a = alpha;
            centerText.color = color;
        }

        private void SetReveal(float reveal)
        {
            if (_eyeRevealMaterial != null)
            {
                _eyeRevealMaterial.SetFloat("_Reveal", reveal);
            }
        }

        private void AnimateCaptionGlyphs()
        {
            if (!_isRunning || !_isCaptionPhase || centerText == null || string.IsNullOrWhiteSpace(centerText.text) || centerText.color.a <= 0.001f)
            {
                return;
            }

            centerText.ForceMeshUpdate();
            TMP_TextInfo textInfo = centerText.textInfo;
            float time = Time.unscaledTime;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];
                if (!characterInfo.isVisible)
                {
                    continue;
                }

                int materialIndex = characterInfo.materialReferenceIndex;
                int vertexIndex = characterInfo.vertexIndex;
                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
                Vector3 center = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
                float phase = time + (i * glyphPhaseOffset);
                float driftX = Mathf.Sin(phase * glyphJumpFrequency) * glyphJumpAmplitude;
                float driftY = Mathf.Cos((phase * glyphJumpFrequency * 0.83f) + 0.45f) * (glyphJumpAmplitude * 0.62f);
                float shakeX = Mathf.Sin((phase * glyphShakeFrequency) + (i * 0.37f)) * glyphShakeAmplitude;
                float shakeY = Mathf.Cos((phase * glyphShakeFrequency * 0.91f) + (i * 0.19f)) * (glyphShakeAmplitude * 0.74f);
                float rotation = Mathf.Sin((phase * glyphJumpFrequency * 0.72f) + 0.3f) * glyphRotationAmplitude;
                Quaternion rotationOffset = Quaternion.Euler(0f, 0f, rotation);
                Vector3 offset = new Vector3(driftX + shakeX, driftY + shakeY, 0f);

                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 relative = vertices[vertexIndex + corner] - center;
                    vertices[vertexIndex + corner] = center + (rotationOffset * relative) + offset;
                }
            }

            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
                centerText.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
            }
        }

        private void CreatePreviewBullets()
        {
            ClearPreviewBullets();
            Transform bulletRoot = null;
            if (_bootstrap != null && _bootstrap.DealMechanism != null)
            {
                bulletRoot = _bootstrap.DealMechanism.GunRestAnchor != null
                    ? _bootstrap.DealMechanism.GunRestAnchor
                    : _bootstrap.DealMechanism.LiftRoot;
            }

            if (bulletRoot == null && _bootstrap != null)
            {
                bulletRoot = _bootstrap.bulletSpot;
            }

            if (bulletRoot == null)
            {
                return;
            }

            Color[] colors =
            {
                new Color(0.46f, 0.54f, 0.62f, 1f),
                new Color(0.92f, 0.72f, 0.24f, 1f),
                new Color(0.92f, 0.72f, 0.24f, 1f)
            };

            for (int i = 0; i < colors.Length; i++)
            {
                GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                bullet.name = $"IntroBullet_{i + 1}";
                bullet.transform.SetParent(bulletRoot, false);
                bullet.transform.localScale = new Vector3(0.065f, 0.15f, 0.065f);
                bullet.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                bullet.transform.localPosition = new Vector3((i - 1f) * 0.12f, 0.05f, 0.08f);

                Collider collider = bullet.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                Renderer renderer = bullet.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    Material material = renderer.material;
                    material.color = colors[i];
                }

                _previewBullets.Add(bullet);
            }
        }

        private IEnumerator FadeBlack(float from, float to, float duration)
        {
            if (blackOverlay == null)
            {
                yield break;
            }

            float elapsed = 0f;
            float safeDuration = Mathf.Max(duration, 0.01f);

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                SetBlackAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }

            SetBlackAlpha(to);
        }

        private void ClearPreviewBullets()
        {
            for (int i = 0; i < _previewBullets.Count; i++)
            {
                if (_previewBullets[i] != null)
                {
                    Destroy(_previewBullets[i]);
                }
            }

            _previewBullets.Clear();
        }

        private void PlayOptionalClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            BCAudioManager.Instance?.PlayCustomClip(clip, 1f);
        }
    }
}
#pragma warning restore CS0649
