using System.Collections.Generic;
using BreathCasino.Core;
using UnityEngine;

namespace BreathCasino.Gameplay
{
    /// <summary>
    /// Физический билет - тонкая карта, чуть длиннее обычных карт.
    /// Может быть взят в руку игрока и использован кликом или клавишами.
    /// </summary>
    public class BCTicketDisplay : MonoBehaviour
    {
        private static readonly Dictionary<int, Material> SharedMaterialCache = new();

        [Header("Ticket Data")]
        [SerializeField] private TicketType ticketType = TicketType.Inspection;
        [SerializeField] private string ticketName = "Ticket";

        [Header("Visual")]
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Color ticketColor = new Color(0.9f, 0.85f, 0.7f);
        [SerializeField] private BoxCollider interactionCollider;

        private bool _inHand;
        private bool _claimedToInventory;
        private Transform _originalParent;
        private Vector3 _originalLocalPosition;
        private Quaternion _originalLocalRotation;

        public TicketType TicketType => ticketType;
        public string TicketName => ticketName;
        public bool InHand => _inHand;
        public bool ClaimedToInventory => _claimedToInventory;

        private void Awake()
        {
            EnsureComponents();
        }

        private void EnsureComponents()
        {
            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }

            if (meshRenderer != null)
            {
                Shader shader = meshRenderer.sharedMaterial != null ? meshRenderer.sharedMaterial.shader : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    meshRenderer.sharedMaterial = GetSharedMaterial(shader, ticketColor);
                }
            }

            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<BoxCollider>();
                if (interactionCollider == null)
                {
                    interactionCollider = gameObject.AddComponent<BoxCollider>();
                }
            }

            if (GetComponent<BCTicketInteractable>() == null)
            {
                gameObject.AddComponent<BCTicketInteractable>();
            }

            SyncInteractionState();
        }

        public void Initialize(TicketType type, string name)
        {
            ticketType = type;
            ticketName = name;

            ticketColor = type switch
            {
                TicketType.Inspection => new Color(0.7f, 0.85f, 1f),
                TicketType.DoubleDamage => new Color(1f, 0.7f, 0.7f),
                TicketType.MedicalRation => new Color(0.7f, 1f, 0.7f),
                TicketType.VoidTransaction => new Color(0.9f, 0.85f, 0.7f),
                TicketType.Shuffle => new Color(1f, 0.9f, 0.6f),
                TicketType.LifeForCards => new Color(0.85f, 0.6f, 1f),
                _ => new Color(0.9f, 0.85f, 0.7f)
            };

            EnsureComponents();
        }

        public void TakeToHand(Transform handHolder)
        {
            if (_inHand)
            {
                return;
            }

            _originalParent = transform.parent;
            _originalLocalPosition = transform.localPosition;
            _originalLocalRotation = transform.localRotation;

            transform.SetParent(handHolder, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            _inHand = true;
            SyncInteractionState();
            Debug.Log($"[BCTicket] {ticketName} taken to hand");
        }

        public void ReturnToStack(Transform stackParent, Vector3 localPos, Quaternion localRot)
        {
            if (!_inHand)
            {
                return;
            }

            transform.SetParent(stackParent, false);
            transform.localPosition = localPos;
            transform.localRotation = localRot;

            _inHand = false;
            SyncInteractionState();
            Debug.Log($"[BCTicket] {ticketName} returned to stack");
        }

        public void Use()
        {
            Debug.Log($"[BCTicket] Using {ticketName} ({ticketType})");
        }

        public void MarkClaimedToInventory()
        {
            _claimedToInventory = true;
        }

        private void SyncInteractionState()
        {
            if (interactionCollider != null)
            {
                interactionCollider.enabled = _inHand;
            }
        }

        private static Material GetSharedMaterial(Shader shader, Color color)
        {
            Color32 color32 = color;
            int key = (shader.name.GetHashCode() * 397) ^ ((color32.r << 24) | (color32.g << 16) | (color32.b << 8) | color32.a);
            if (SharedMaterialCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Material material = new(shader)
            {
                color = color,
                name = $"BCTicketShared_{key:X8}",
                hideFlags = HideFlags.DontSave
            };
            SharedMaterialCache[key] = material;
            return material;
        }
    }
}
