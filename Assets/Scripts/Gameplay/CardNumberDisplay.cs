using UnityEngine;
using TMPro;
using BreathCasino.Core;

namespace BreathCasino.Gameplay
{
    /// <summary>
    /// Отображение цифр/символов на карте. Единственный рабочий способ — TextMeshPro в обёртке с inverse scale.
    /// Карта имеет scale (0.14, 0.004, 0.21) — Y=0.004 сжимает любой дочерний объект. Обёртка компенсирует.
    /// </summary>
    [RequireComponent(typeof(BCCardDisplay))]
    public class CardNumberDisplay : MonoBehaviour
    {
        private const string WrapperName = "CardNumbers_Wrapper";
        private const string RootName = "CardNumbers_Root";

        private BCCardDisplay _card;
        private GameObject _wrapper;
        private TextMeshPro[] _texts;

        private void Awake()
        {
            _card = GetComponent<BCCardDisplay>();
            if (_card == null) return;
            EnsureNumbers();
        }

        public void Refresh()
        {
            if (_card == null) _card = GetComponent<BCCardDisplay>();
            if (_card == null) return;

            string marking = _card.IsCardHidden
                ? "?"
                : (_card.CardType == CardType.Special ? GetEffectSymbol(_card.SpecialEffect) : _card.Weight.ToString());

            Color color = _card.IsCardHidden
                ? new Color(0.8f, 0.8f, 0.8f)
                : (_card.CardType == CardType.Special ? new Color(0.95f, 0.98f, 1f) : new Color(0.98f, 0.98f, 0.98f));

            if (_texts == null || _texts.Length != 4)
            {
                DestroyOld();
                CreateNumbers();
            }

            if (_texts != null)
            {
                bool isSpecial = _card.CardType == CardType.Special;
                int fontSize = isSpecial ? 32 : 36;

                for (int i = 0; i < _texts.Length; i++)
                {
                    if (_texts[i] != null)
                    {
                        _texts[i].text = marking;
                        _texts[i].color = color;
                        _texts[i].fontSize = fontSize;

                        if (isSpecial)
                        {
                            _texts[i].gameObject.SetActive(i == 0);
                        }
                        else
                        {
                            _texts[i].gameObject.SetActive(true);
                        }
                    }
                }

                if (isSpecial && _texts[0] != null)
                {
                    _texts[0].transform.localPosition = Vector3.zero;
                    _texts[0].transform.localRotation = Quaternion.identity;
                    _texts[0].transform.localScale = Vector3.one * 0.04f;
                }
            }
        }

        private void EnsureNumbers()
        {
            if (_texts != null && _texts.Length == 4 && _texts[0] != null)
                return;
            DestroyOld();
            CreateNumbers();
        }

        public void DestroyOld()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;
                if (child.name == WrapperName || child.name == RootName ||
                    child.name == "WeightCanvas" || child.name == "CardMarkings" ||
                    child.name == "CardMarkings3D_Wrapper" || child.name == "CardMarkings3D")
                {
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }
            _texts = null;
            _wrapper = null;
        }

        private void CreateNumbers()
        {
            _wrapper = new GameObject(WrapperName);
            _wrapper.transform.SetParent(transform, false);
            _wrapper.transform.localPosition = new Vector3(0f, 0.51f, 0f);
            _wrapper.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _wrapper.transform.localScale = Vector3.one;

            Vector3[] corners = {
                new(-0.06f, 0.09f, 0f),
                new(0.06f, 0.09f, 0f),
                new(-0.06f, -0.09f, 0f),
                new(0.06f, -0.09f, 0f)
            };
            float[] rotZ = { 0f, 0f, 180f, 180f };

            _texts = new TextMeshPro[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"Num_{i}");
                go.transform.SetParent(_wrapper.transform, false);
                go.transform.localPosition = corners[i];
                go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ[i]);
                go.transform.localScale = Vector3.one * 0.05f;

                var tmp = go.AddComponent<TextMeshPro>();
                var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font == null)
                {
                    Debug.LogWarning("[CardNumberDisplay] TMP font not found. Import TMP Essential Resources via Window > TextMeshPro > Import TMP Essential Resources");
                }
                else
                {
                    tmp.font = font;
                }
                tmp.text = "0";
                tmp.fontSize = 36;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.raycastTarget = false;

                _texts[i] = tmp;
            }

            Refresh();
        }

        private static string GetEffectSymbol(SpecialEffectType e)
        {
            return e switch
            {
                SpecialEffectType.Cancel => "C",
                SpecialEffectType.Steal => "S",
                SpecialEffectType.Duplicate => "x2",
                SpecialEffectType.Exchange => "<>",
                SpecialEffectType.Block => "0",
                SpecialEffectType.Prohibit => "!",
                _ => "?"
            };
        }
    }
}