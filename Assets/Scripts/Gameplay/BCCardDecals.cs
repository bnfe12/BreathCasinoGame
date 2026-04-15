using UnityEngine;
using BreathCasino.Core;
using TMPro;

namespace BreathCasino.Gameplay
{
    /// <summary>
    /// Управляет маркировками на карте (цифры в углах).
    /// Использует TextMeshPro для отображения цифр/символов в углах карты.
    /// Это альтернатива Canvas системе для более производительного способа.
    /// </summary>
    public class BCCardDecals : MonoBehaviour
    {
        [Header("Decal Settings")]
        [SerializeField] private float fontSize = 24f;
        [SerializeField] private float decalHeight = 0.011f; // Высота над поверхностью карты
        
        private TextMeshPro[] _cornerTexts;
        private string _currentMarking;
        private Color _currentColor;
        
        public void Initialize()
        {
            CreateCornerTexts();
        }

        public void SetMarkings(string marking, Color color)
        {
            _currentMarking = marking;
            _currentColor = color;

            if (_cornerTexts == null || _cornerTexts.Length != 4)
            {
                CreateCornerTexts();
            }

            // Обновляем текст во всех углах
            for (int i = 0; i < _cornerTexts.Length; i++)
            {
                if (_cornerTexts[i] == null) continue;

                _cornerTexts[i].text = marking;
                _cornerTexts[i].color = color;

                // Особые углы перевернуты на 180°
                if (i >= 2)  // BottomLeft, BottomRight
                {
                    _cornerTexts[i].transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                }
            }
            
            Debug.Log($"[CardDecals] Set markings: {marking}, color: {color}");
        }

        private void CreateCornerTexts()
        {
            _cornerTexts = new TextMeshPro[4];
            
            Vector3[] positions = new Vector3[]
            {
                new Vector3(-0.065f, decalHeight, 0.095f),   // TopLeft
                new Vector3(0.065f, decalHeight, 0.095f),    // TopRight
                new Vector3(-0.065f, decalHeight, -0.095f),  // BottomLeft
                new Vector3(0.065f, decalHeight, -0.095f)    // BottomRight
            };

            string[] names = new string[] { "TopLeft", "TopRight", "BottomLeft", "BottomRight" };

            for (int i = 0; i < 4; i++)
            {
                GameObject textObj = new GameObject($"Marking_{names[i]}");
                textObj.transform.SetParent(transform, false);
                textObj.transform.localPosition = positions[i];
                textObj.transform.localScale = Vector3.one * 0.001f;

                TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
                tmp.text = "";
                tmp.fontSize = fontSize;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;

                RectTransform rectTransform = textObj.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = new Vector2(100, 100);
                }

                _cornerTexts[i] = tmp;
            }
        }

        public void Clear()
        {
            if (_cornerTexts == null) return;
            
            foreach (var text in _cornerTexts)
            {
                if (text != null && text.gameObject != null)
                {
                    Destroy(text.gameObject);
                }
            }
            
            _cornerTexts = null;
        }
    }
}