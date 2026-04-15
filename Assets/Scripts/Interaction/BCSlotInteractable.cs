using UnityEngine;
using BreathCasino.Core;
using BreathCasino.Systems;

namespace BreathCasino.Gameplay
{
    /// <summary>
    /// Клик по слоту — размещение выбранной карты. Для полного контроля (мышь ИЛИ клавиатура).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BCSlotInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool isMainSlot = true;

        private GameManager _gameManager;

        public bool CanInteract => true;

        public void SetMainSlot(bool main) => isMainSlot = main;

        private void Awake()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
        }

        public void OnHoverEnter()
        {
            BCAudioManager.Instance?.PlayUIHover();
            ShowTooltip();
        }
        
        public void OnHoverExit() 
        { 
            BCTooltipDisplay.Hide();
        }

        public void OnClick()
        {
            if (_gameManager == null)
                _gameManager = FindFirstObjectByType<GameManager>();

            if (_gameManager != null && _gameManager.CurrentMode == GameMode.Player)
            {
                BCAudioManager.Instance?.PlayUIClick();
                BCCardSlot slot = GetComponent<BCCardSlot>();
                if (slot != null)
                    _gameManager.SubmitPlayerSelectionToSlot(slot);
                else if ((isMainSlot && PlayerSelectionTracker.SelectedMainCards.Count > 0) || (!isMainSlot && PlayerSelectionTracker.SelectedSpecialCard != null))
                    _gameManager.SubmitPlayerCard(isMainSlot ? PlayerSelectionTracker.SelectedMainCard : PlayerSelectionTracker.SelectedSpecialCard, !isMainSlot);
            }
        }

        private void ShowTooltip()
        {
            BCCardSlot slot = GetComponent<BCCardSlot>();
            if (slot == null) return;

            System.Text.StringBuilder tooltip = new System.Text.StringBuilder();
            
            tooltip.AppendLine($"<b>{(isMainSlot ? "Main Card Slot" : "Special Card Slot")}</b>");
            tooltip.AppendLine();
            
            if (slot.HasCard)
            {
                BCCardDisplay card = slot.CurrentCard;
                tooltip.AppendLine($"Current: {card.CardName}");
                
                if (card.CardType != CardType.Special)
                {
                    tooltip.AppendLine($"Weight: {card.Weight}");
                    if (card.IsThreat)
                        tooltip.AppendLine("(THREAT)");
                }
            }
            else
            {
                tooltip.AppendLine("Status: Empty");
                tooltip.AppendLine();
                tooltip.AppendLine("Click to place selected card");
            }

            BCTooltipDisplay.Show(tooltip.ToString());
        }
    }
}