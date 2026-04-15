using System.Collections;
using System.Collections.Generic;
using BreathCasino.Core;
using UnityEngine;

namespace BreathCasino.Gameplay
{
    public class BCCardDisplay : MonoBehaviour
    {
        public static readonly Vector3 DefaultCardScale = new(0.07f, 0.0028f, 0.11f);

        private static readonly Dictionary<int, Material> SharedMaterialCache = new();

        [SerializeField] private int weight = 5;
        [SerializeField] private string cardName = "Card";
        [SerializeField] private CardType cardType = CardType.Resource;
        [SerializeField] private SpecialEffectType specialEffect = SpecialEffectType.None;

        private bool _isEnemyCard;
        private bool _cardHidden;

        public int Weight => weight;
        public string CardName => cardName;
        public CardType CardType => cardType;
        public bool IsThreat => cardType == CardType.Threat;
        public SpecialEffectType SpecialEffect => specialEffect;
        public bool IsCardHidden => _cardHidden;
        public bool IsEnemyCard => _isEnemyCard;

        public void SetEnemyCard(bool isEnemy)
        {
            _isEnemyCard = isEnemy;
            UpdateVisuals();
        }

        public void SetCardHidden(bool hidden)
        {
            _cardHidden = hidden;
            UpdateVisuals();
        }

        public void SetCardData(CardData data)
        {
            weight = Mathf.Clamp(data.weight, 0, 8);
            cardName = string.IsNullOrEmpty(data.cardName) ? "Card" : data.cardName;
            cardType = data.cardType;
            specialEffect = data.specialEffect;
            UpdateVisuals();
        }

        public void SetWeight(int w)
        {
            weight = Mathf.Clamp(w, 2, 8);
            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            ApplyCardColor();
            CardNumberDisplay numbers = GetComponent<CardNumberDisplay>();
            if (numbers != null)
            {
                numbers.Refresh();
            }
        }

        private void ApplyCardColor()
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                return;
            }

            renderer.sharedMaterial = GetSharedMaterial(shader, GetCardColor());
        }

        public Color GetCardColor()
        {
            return cardType switch
            {
                CardType.Resource => new Color(0.55f, 0.55f, 0.55f),
                CardType.Threat => new Color(0.9f, 0.25f, 0.25f),
                CardType.Special => new Color(0.25f, 0.5f, 0.95f),
                _ => new Color(0.55f, 0.55f, 0.55f)
            };
        }

        public void PlaceInSlot(Transform slot)
        {
            if (slot == null)
            {
                return;
            }

            transform.SetParent(slot, false);
            transform.localPosition = new Vector3(0f, 0.06f, 0f);
            transform.localRotation = Quaternion.identity;
            ApplyDefaultScale(transform);
        }

        public void MoveToSlot(Transform slot, Vector3 localPos, Quaternion localRot, float duration)
        {
            if (slot == null)
            {
                return;
            }

            transform.SetParent(slot, false);
            ApplyDefaultScale(transform);

            if (duration > 0f)
            {
                StartCoroutine(AnimateToPosition(localPos, localRot, duration));
            }
            else
            {
                transform.localPosition = localPos;
                transform.localRotation = localRot;
            }
        }

        private IEnumerator AnimateToPosition(Vector3 targetPos, Quaternion targetRot, float duration)
        {
            Vector3 startPos = transform.localPosition;
            Quaternion startRot = transform.localRotation;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                transform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            transform.localPosition = targetPos;
            transform.localRotation = targetRot;
        }

        public static void ApplyDefaultScale(Transform target)
        {
            if (target == null)
            {
                return;
            }

            Vector3 lossy = target.parent != null ? target.parent.lossyScale : Vector3.one;
            float safeX = Mathf.Abs(lossy.x) > 0.0001f ? lossy.x : 1f;
            float safeY = Mathf.Abs(lossy.y) > 0.0001f ? lossy.y : 1f;
            float safeZ = Mathf.Abs(lossy.z) > 0.0001f ? lossy.z : 1f;

            target.localScale = new Vector3(
                DefaultCardScale.x / safeX,
                DefaultCardScale.y / safeY,
                DefaultCardScale.z / safeZ);
        }

        private static Material GetSharedMaterial(Shader shader, Color color)
        {
            Color32 color32 = color;
            int key = (color32.r << 24) | (color32.g << 16) | (color32.b << 8) | color32.a;
            if (SharedMaterialCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Material material = new(shader)
            {
                color = color,
                name = $"BCCardShared_{key:X8}",
                hideFlags = HideFlags.DontSave
            };
            SharedMaterialCache[key] = material;
            return material;
        }
    }
}
