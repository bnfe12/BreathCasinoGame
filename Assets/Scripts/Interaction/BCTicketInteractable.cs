using UnityEngine;
using BreathCasino.Core;
using BreathCasino.Systems;

namespace BreathCasino.Gameplay
{
    /// <summary>
    /// Интерактивность для физического билета.
    /// Клик по билету в руке использует его.
    /// </summary>
    public class BCTicketInteractable : MonoBehaviour, IInteractable
    {
        private BCTicketDisplay _ticketDisplay;
        
        public bool CanInteract => _ticketDisplay != null && _ticketDisplay.InHand;

        private void Awake()
        {
            _ticketDisplay = GetComponent<BCTicketDisplay>();
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
            if (CanInteract)
            {
                BCAudioManager.Instance?.PlayUIClick();
                // Билет в руке - используем его
                TicketManager tm = FindFirstObjectByType<TicketManager>();
                if (tm != null)
                {
                    tm.UseTicketInHand();
                }
                Debug.Log($"[BreathCasino] Ticket clicked in hand: {_ticketDisplay.TicketName}");
            }
            else
            {
                BCAudioManager.Instance?.PlayUIInvalid();
            Debug.Log("[BreathCasino] Ticket clicked but not in hand");
            }
        }

        private void ShowTooltip()
        {
            if (_ticketDisplay == null) return;

            System.Text.StringBuilder tooltip = new System.Text.StringBuilder();
            
            string ticketName = _ticketDisplay.TicketType switch
            {
                TicketType.Inspection => "Inspection Voucher",
                TicketType.DoubleDamage => "Double Down Coupon",
                TicketType.MedicalRation => "Medical Ration",
                TicketType.VoidTransaction => "Void Transaction",
                TicketType.Shuffle => "Shuffle Voucher",
                TicketType.LifeForCards => "Life for Cards",
                _ => "Unknown Ticket"
            };
            
            tooltip.AppendLine($"<b>{ticketName}</b>");
            tooltip.AppendLine();
            
            if (_ticketDisplay.InHand)
            {
                tooltip.AppendLine("Status: In Hand");
                tooltip.AppendLine();
                tooltip.AppendLine("Click to use");
                tooltip.AppendLine();
                tooltip.AppendLine("Pull the lever again to close the shelf");
            }
            else
            {
                tooltip.AppendLine("Status: In Stack");
            }
            
            tooltip.AppendLine();
            
            string description = _ticketDisplay.TicketType switch
            {
                TicketType.Inspection => "Peek at current bullet",
                TicketType.DoubleDamage => "Next shot deals x2 damage",
                TicketType.MedicalRation => "Restore 1 HP or +15s oxygen",
                TicketType.VoidTransaction => "Remove bullet without firing",
                TicketType.Shuffle => "Shuffle bullets in chamber",
                TicketType.LifeForCards => "Exchange 1 HP for 6 new cards (when deck empty)",
                _ => "Unknown effect"
            };
            
            tooltip.AppendLine($"Effect: {description}");

            BCTooltipDisplay.Show(tooltip.ToString());
        }
    }
}
