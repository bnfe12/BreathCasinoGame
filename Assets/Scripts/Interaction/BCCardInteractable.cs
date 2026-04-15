using UnityEngine;
using BreathCasino.Core;
using BreathCasino.Systems;

namespace BreathCasino.Gameplay
{
    [RequireComponent(typeof(BCCardDisplay))]
    public class BCCardInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private Color highlightColor = new(1f, 0.9f, 0.5f);

        private BCCardDisplay _display;
        private Renderer _renderer;
        private Material _instanceMaterial;
        private bool _selected;
        private bool _hovered;

        public bool CanInteract
        {
            get
            {
                if (_display == null) return false;
                return !_display.IsEnemyCard;
            }
        }

        private void Awake()
        {
            _display = GetComponent<BCCardDisplay>();
            _renderer = GetComponentInChildren<Renderer>();
        }

        public void OnHoverEnter()
        {
            _hovered = true;
            BCAudioManager.Instance?.PlayUIHover();
            ApplyVisualState();
            ShowTooltip();
        }

        public void OnHoverExit()
        {
            _hovered = false;
            ApplyVisualState();
            BCTooltipDisplay.Hide();
        }

        public void OnClick()
        {
            if (_display == null) return;
            GameManager gm = FindFirstObjectByType<GameManager>();

            if (gm != null && gm.TryTakePendingCards(_display))
            {
                BCAudioManager.Instance?.PlayUIClick();
                return;
            }

            bool isMain = _display.CardType != CardType.Special;
            if (!isMain)
            {
                if (gm != null && gm.IsSpecialsProhibited)
                {
                    BCAudioManager.Instance?.PlayUIInvalid();
                    return;
                }
            }

            BCAudioManager.Instance?.PlayUIClick();

            bool selected = isMain
                ? PlayerSelectionTracker.ToggleSelectedMain(_display)
                : PlayerSelectionTracker.ToggleSelectedSpecial(_display);

            SetSelectedVisual(selected);

            if (isMain)
            {
                BCInteractionRaycaster raycaster = FindFirstObjectByType<BCInteractionRaycaster>();
                raycaster?.NotifyCardSelectedForPlacement();
            }
        }

        public void SetSelectedVisual(bool selected)
        {
            _selected = selected;
            ApplyVisualState();
        }

        public void Deselect()
        {
            SetSelectedVisual(false);
        }

        private void ApplyVisualState()
        {
            if (_renderer == null || _display == null) return;

            EnsureInstanceMaterial();

            Color baseColor = _display.GetCardColor();
            if (_selected)
                _instanceMaterial.color = Color.Lerp(baseColor, highlightColor, 0.55f);
            else if (_hovered)
                _instanceMaterial.color = Color.Lerp(baseColor, highlightColor, 0.3f);
            else
                _instanceMaterial.color = baseColor;
        }

        private void EnsureInstanceMaterial()
        {
            if (_instanceMaterial != null) return;
            if (_renderer.sharedMaterial != null)
                _instanceMaterial = _renderer.material;
            else
                _instanceMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            _renderer.material = _instanceMaterial;
        }

        private void ShowTooltip()
        {
            if (_display == null) return;

            System.Text.StringBuilder tooltip = new System.Text.StringBuilder();
            tooltip.AppendLine($"<b>{_display.CardName}</b>");
            tooltip.AppendLine();

            string cardTypeText = _display.CardType switch
            {
                CardType.Resource => "Resource Card (Defense)",
                CardType.Threat => "Threat Card (Attack)",
                CardType.Special => "Special Card",
                _ => "Unknown"
            };
            tooltip.AppendLine($"Type: {cardTypeText}");

            if (_display.CardType != CardType.Special)
            {
                tooltip.AppendLine($"Weight: {_display.Weight}");

                if (_display.IsThreat)
                {
                    tooltip.AppendLine();
                    tooltip.AppendLine("THREAT: Must be blocked with");
                    tooltip.AppendLine("non-Threat card of equal or");
                    tooltip.AppendLine("greater weight, or attacker");
                    tooltip.AppendLine("gets the gun immediately!");
                }
            }
            else
            {
                tooltip.AppendLine();
                tooltip.AppendLine("Special Effect:");
                tooltip.AppendLine(GetSpecialEffectDescription(_display));
            }

            BCTooltipDisplay.Show(tooltip.ToString());
        }

        private string GetSpecialEffectDescription(BCCardDisplay card)
        {
            string symbol = card.CardName;

            return symbol switch
            {
                "C" or "Cancel" => "Cancel: Negates opponent's\nspecial card effect",
                "S" or "Steal" => "Steal: If you win, take one\nof opponent's cards",
                "x2" or "Duplicate" => "Duplicate: Doubles the\nweight of your main card",
                "<>" or "Exchange" => "Exchange: Swap main cards\nwith opponent before duel",
                "0" or "Block" => "Block: Opponent cannot use\nspecial cards next turn",
                "!" or "Prohibit" => "Prohibit: No special cards\nallowed for rest of round",
                _ => "Unknown special effect"
            };
        }

        private void OnDestroy()
        {
            if (_instanceMaterial != null)
                Destroy(_instanceMaterial);
        }
    }
}
