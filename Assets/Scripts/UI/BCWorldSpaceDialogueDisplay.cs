using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BreathCasino.Gameplay
{
    public class BCWorldSpaceDialogueDisplay : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 worldOffset = new(0f, 1.9f, 0f);
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform bubbleRoot;
        [SerializeField] private Image bubbleImage;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private float fadeSpeed = 8f;

        private Camera _camera;
        private CanvasGroup _canvasGroup;
        private float _visibleUntil;

        public void Initialize(Transform followTarget, Camera worldCamera, bool placeBelow = false)
        {
            target = followTarget;
            _camera = worldCamera != null ? worldCamera : Camera.main;
            worldOffset = placeBelow ? new Vector3(0f, 0.7f, 0f) : new Vector3(0f, 1.9f, 0f);
            EnsureVisuals();
        }

        public void Show(string line, float duration)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            EnsureVisuals();
            dialogueText.text = line;
            _visibleUntil = Time.time + Mathf.Max(0.5f, duration);
        }

        private void Awake()
        {
            EnsureVisuals();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            float alphaTarget = 0f;

            if (_camera != null && bubbleRoot != null)
            {
                Vector3 screenPoint = _camera.WorldToScreenPoint(target.position + worldOffset);
                bool isVisible = screenPoint.z > 0.05f &&
                                 screenPoint.x >= -60f &&
                                 screenPoint.x <= Screen.width + 60f &&
                                 screenPoint.y >= -60f &&
                                 screenPoint.y <= Screen.height + 60f;

                if (isVisible)
                {
                    bubbleRoot.position = screenPoint;
                    alphaTarget = Time.time < _visibleUntil ? 1f : 0f;
                }
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, alphaTarget, Time.deltaTime * fadeSpeed);
            }
        }

        private void EnsureVisuals()
        {
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("DialogueCanvas");
                canvasObject.transform.SetParent(null, false);
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1300;
                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            _canvasGroup = canvas.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            }
            _canvasGroup.blocksRaycasts = false;

            if (bubbleRoot == null)
            {
                GameObject bubbleObject = new GameObject("BubbleRoot");
                bubbleObject.transform.SetParent(canvas.transform, false);
                bubbleRoot = bubbleObject.AddComponent<RectTransform>();
                bubbleRoot.sizeDelta = new Vector2(360f, 128f);
                bubbleRoot.pivot = new Vector2(0.5f, 0.5f);
            }

            if (bubbleImage == null)
            {
                bubbleImage = bubbleRoot.GetComponent<Image>();
                if (bubbleImage == null)
                {
                    bubbleImage = bubbleRoot.gameObject.AddComponent<Image>();
                }
                bubbleImage.color = new Color(0.04f, 0.06f, 0.08f, 0.90f);
                bubbleImage.raycastTarget = false;
            }

            if (dialogueText == null)
            {
                GameObject textObject = new GameObject("DialogueText");
                textObject.transform.SetParent(bubbleRoot, false);
                dialogueText = textObject.AddComponent<TextMeshProUGUI>();
                dialogueText.textWrappingMode = TextWrappingModes.Normal;
                dialogueText.fontSize = 28f;
                dialogueText.color = new Color(0.96f, 0.96f, 0.93f);
                dialogueText.alignment = TextAlignmentOptions.Center;
                RectTransform textRect = dialogueText.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(20f, 16f);
                textRect.offsetMax = new Vector2(-20f, -16f);
                dialogueText.raycastTarget = false;
            }

            _canvasGroup.alpha = 0f;
        }
    }
}
