using System.Collections.Generic;
using BreathCasino.Core;
using UnityEngine;

namespace BreathCasino.Gameplay
{
    [ExecuteAlways]
    public class BCCardSlot : MonoBehaviour, IInteractable
    {
        private static readonly Dictionary<int, Material> RimMaterialCache = new();

        [SerializeField] private string slotLabel = "Slot";
        [SerializeField] private Side side = Side.Player;
        [SerializeField] private SlotKind slotKind = SlotKind.Slot1;
        [SerializeField] private float cardHoverHeight = 0.04f;
        [SerializeField] private float cardStackSpacing = 0.04f;
        [SerializeField] private float combatYawSpread = 8f;

        private const string AnchorName = "CardAnchor";
        private const string RimRootName = "SlotRim";
        private const string FrameName = "Frame";

        private Transform _cardAnchor;

        public string SlotLabel => slotLabel;
        public bool IsMainSlot => slotKind == SlotKind.Slot1;
        public Side Side => side;
        public SlotKind SlotKind => slotKind;
        public Transform CardAnchor
        {
            get
            {
                EnsureVisuals(true);
                return _cardAnchor != null ? _cardAnchor : transform;
            }
        }

        public bool HasCard
        {
            get
            {
                if (_cardAnchor == null)
                {
                    return false;
                }

                return _cardAnchor.childCount > 0;
            }
        }

        public BCCardDisplay CurrentCard
        {
            get
            {
                if (_cardAnchor == null || _cardAnchor.childCount == 0)
                {
                    return null;
                }

                return _cardAnchor.GetChild(0).GetComponent<BCCardDisplay>();
            }
        }

        public bool CanInteract => side == Side.Player;

        public void OnHoverEnter()
        {
            if (HasCard && CurrentCard != null)
            {
                System.Text.StringBuilder tooltip = new System.Text.StringBuilder();
                tooltip.AppendLine($"<b>{CurrentCard.CardName}</b>");
                tooltip.AppendLine($"Weight: {CurrentCard.Weight}");
                BCTooltipDisplay.Show(tooltip.ToString());
            }
        }

        public void OnHoverExit()
        {
            BCTooltipDisplay.Hide();
        }

        public void OnClick()
        {
            GameManager gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null)
            {
                gm.SubmitPlayerSelectionToSlot(this);
            }
        }

        public void Configure(Side slotSide, SlotKind kind, string label)
        {
            side = slotSide;
            slotKind = kind;
            slotLabel = label;
            EnsureVisuals(true);
        }

        public Vector3 GetCardLocalPosition(int index, int total)
        {
            if (slotKind == SlotKind.Slot2 || total <= 1)
            {
                return Vector3.zero;
            }

            float width = (total - 1) * cardStackSpacing;
            float x = index * cardStackSpacing - width * 0.5f;
            return new Vector3(x, index * 0.0015f, 0f);
        }

        public Quaternion GetCardLocalRotation(int index, int total)
        {
            if (total <= 1)
            {
                return Quaternion.identity;
            }

            float t = total > 1 ? (index - (total - 1) * 0.5f) / (total - 1) : 0f;
            return Quaternion.Euler(0f, combatYawSpread * t, 0f);
        }

        private void Awake()
        {
            EnsureVisuals(true);
        }

        private void OnEnable()
        {
            EnsureVisuals(true);
        }

        private void OnValidate()
        {
            EnsureVisuals(false);
        }

        private void EnsureVisuals(bool allowCreate)
        {
            if (gameObject.name.Contains("Enemy"))
            {
                side = Side.Enemy;
            }
            else if (gameObject.name.Contains("Player"))
            {
                side = Side.Player;
            }

            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            EnsureAnchor(allowCreate);
            EnsureRim(allowCreate);
        }

        private void EnsureAnchor(bool allowCreate)
        {
            _cardAnchor = transform.Find(AnchorName);
            if (_cardAnchor == null && allowCreate)
            {
                GameObject anchor = new(AnchorName);
                anchor.transform.SetParent(transform, false);
                _cardAnchor = anchor.transform;
            }

            if (_cardAnchor == null)
            {
                return;
            }

            float slotHeight = Mathf.Max(0.008f, transform.localScale.y);
            _cardAnchor.localPosition = new Vector3(0f, (slotHeight * 0.5f) + cardHoverHeight, 0f);
            _cardAnchor.localRotation = Quaternion.identity;
            _cardAnchor.localScale = Vector3.one;
        }

        private void EnsureRim(bool allowCreate)
        {
            Transform rimRoot = transform.Find(RimRootName);
            if (rimRoot == null && allowCreate)
            {
                GameObject go = new(RimRootName);
                go.transform.SetParent(transform, false);
                rimRoot = go.transform;
            }

            if (rimRoot == null)
            {
                return;
            }

            CleanupLegacyRimParts(rimRoot);

            Transform frame = rimRoot.Find(FrameName);
            if (frame == null && allowCreate)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = FrameName;
                Collider collider = go.GetComponent<Collider>();
                if (collider != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(collider);
                    }
                    else
                    {
                        DestroyImmediate(collider);
                    }
                }

                go.transform.SetParent(rimRoot, false);
                frame = go.transform;
            }

            if (frame == null)
            {
                return;
            }

            float slotHeight = Mathf.Max(0.01f, transform.localScale.y);
            frame.localPosition = new Vector3(0f, 0.5f + (0.0015f / slotHeight), 0f);
            frame.localRotation = Quaternion.Euler(90f, 0f, 0f);
            frame.localScale = new Vector3(1.04f, 1.04f, 1f);

            Renderer renderer = frame.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateRimMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void CleanupLegacyRimParts(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name != FrameName)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }
        }

        private Material CreateRimMaterial()
        {
            Shader shader = Shader.Find("BreathCasino/BCSlotRim") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Color frameColor = GetFrameColor();
            Color glowColor = GetGlowColor();
            Color32 frame32 = frameColor;
            Color32 glow32 = glowColor;
            int key = (shader.name.GetHashCode() * 397) ^
                (frame32.r << 24) ^ (frame32.g << 16) ^ (frame32.b << 8) ^ frame32.a ^
                (glow32.r << 20) ^ (glow32.g << 12) ^ (glow32.b << 4) ^ glow32.a;

            if (RimMaterialCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Material material = new(shader)
            {
                name = $"BCSlotRim_{key:X8}",
                hideFlags = HideFlags.DontSave
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
            }

            if (material.HasProperty("_FrameColor"))
            {
                material.SetColor("_FrameColor", frameColor);
            }

            if (material.HasProperty("_GlowColor"))
            {
                material.SetColor("_GlowColor", glowColor);
            }

            if (material.HasProperty("_GlowIntensity"))
            {
                material.SetFloat("_GlowIntensity", 1.15f);
            }

            if (material.HasProperty("_BorderThickness"))
            {
                material.SetFloat("_BorderThickness", 0.08f);
            }

            if (material.HasProperty("_Feather"))
            {
                material.SetFloat("_Feather", 0.035f);
            }

            if (material.HasProperty("_GlowWidth"))
            {
                material.SetFloat("_GlowWidth", 0.16f);
            }

            if (material.HasProperty("_Color"))
            {
                material.color = frameColor;
            }

            RimMaterialCache[key] = material;
            return material;
        }

        private Color GetFrameColor()
        {
            return new Color(0.88f, 0.78f, 0.52f, 0.96f);
        }

        private Color GetGlowColor()
        {
            return new Color(1f, 0.63f, 0.2f, 0.78f);
        }
    }
}
