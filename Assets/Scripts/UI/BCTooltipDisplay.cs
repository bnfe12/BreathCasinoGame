using UnityEngine;
using BreathCasino.Core;
using UnityEngine.UI;

namespace BreathCasino.Gameplay
{
    /// <summary>
    /// Отображает tooltip с информацией о предмете при наведении курсора.
    /// Показывает название, тип, вес, способности и другую информацию.
    /// </summary>
    public class BCTooltipDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Canvas tooltipCanvas;
        [SerializeField] private Text tooltipText;
        [SerializeField] private RectTransform tooltipPanel;
        
        [Header("Settings")]
        [SerializeField] private Vector2 offset = new Vector2(15f, -15f);
        [SerializeField] private float fadeSpeed = 8f;
        [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        
        private CanvasGroup _canvasGroup;
        private bool _isVisible;
        private string _currentTooltip = string.Empty;

        private static BCTooltipDisplay _instance;
        public static BCTooltipDisplay Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            EnsureComponents();
        }

        private void EnsureComponents()
        {
            if (tooltipCanvas == null)
            {
                tooltipCanvas = GetComponentInChildren<Canvas>();
                if (tooltipCanvas == null)
                {
                    GameObject canvasObj = new GameObject("TooltipCanvas");
                    canvasObj.transform.SetParent(transform, false);
                    tooltipCanvas = canvasObj.AddComponent<Canvas>();
                    tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    tooltipCanvas.sortingOrder = 1000;
                    
                    canvasObj.AddComponent<CanvasScaler>();
                    canvasObj.AddComponent<GraphicRaycaster>();
                }
            }

            _canvasGroup = tooltipCanvas.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = tooltipCanvas.gameObject.AddComponent<CanvasGroup>();
            }
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;

            if (tooltipPanel == null)
            {
                GameObject panelObj = new GameObject("TooltipPanel");
                panelObj.transform.SetParent(tooltipCanvas.transform, false);
                tooltipPanel = panelObj.AddComponent<RectTransform>();
                tooltipPanel.sizeDelta = new Vector2(300f, 100f);
                
                Image panelImage = panelObj.AddComponent<Image>();
                panelImage.color = backgroundColor;
            }

            if (tooltipText == null)
            {
                GameObject textObj = new GameObject("TooltipText");
                textObj.transform.SetParent(tooltipPanel, false);
                tooltipText = textObj.AddComponent<Text>();
                tooltipText.font = BCRuntimeFontProvider.Get(16);
                tooltipText.fontSize = 14;
                tooltipText.color = Color.white;
                tooltipText.alignment = TextAnchor.UpperLeft;
                
                RectTransform textRect = tooltipText.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10f, 10f);
                textRect.offsetMax = new Vector2(-10f, -10f);
            }
        }

        private void Update()
        {
            if (_isVisible)
            {
                UpdatePosition();
                _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, 1f, Time.deltaTime * fadeSpeed);
            }
            else
            {
                _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, 0f, Time.deltaTime * fadeSpeed);
            }
        }

        private void UpdatePosition()
        {
            if (tooltipPanel == null) return;

            Vector2 mousePos = Input.mousePosition;
            tooltipPanel.position = mousePos + offset;

            // Держим tooltip в пределах экрана
            Vector3[] corners = new Vector3[4];
            tooltipPanel.GetWorldCorners(corners);
            
            float rightEdge = corners[2].x;
            float topEdge = corners[1].y;
            
            if (rightEdge > Screen.width)
            {
                tooltipPanel.position = new Vector2(mousePos.x - tooltipPanel.sizeDelta.x - offset.x, tooltipPanel.position.y);
            }
            
            if (topEdge > Screen.height)
            {
                tooltipPanel.position = new Vector2(tooltipPanel.position.x, mousePos.y - tooltipPanel.sizeDelta.y + offset.y);
            }
        }

        public void ShowTooltip(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                HideTooltip();
                return;
            }

            _currentTooltip = content;
            _isVisible = true;
            
            if (tooltipText != null)
            {
                tooltipText.text = content;
            }

            // Автоматически подгоняем размер панели под текст
            if (tooltipPanel != null && tooltipText != null)
            {
                float preferredHeight = tooltipText.preferredHeight + 20f;
                float preferredWidth = Mathf.Min(tooltipText.preferredWidth + 20f, 400f);
                tooltipPanel.sizeDelta = new Vector2(preferredWidth, preferredHeight);
            }
        }

        public void HideTooltip()
        {
            _isVisible = false;
            _currentTooltip = string.Empty;
        }

        public static void Show(string content)
        {
            if (Instance != null)
            {
                Instance.ShowTooltip(content);
            }
        }

        public static void Hide()
        {
            if (Instance != null)
            {
                Instance.HideTooltip();
            }
        }
    }
}
