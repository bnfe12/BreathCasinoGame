using UnityEngine;
using BreathCasino.Core;
using BreathCasino.Systems;

namespace BreathCasino.Gameplay
{
    /// <summary>
    /// Интерактивность для стопки билетов.
    /// Клик берет верхний билет в руку.
    /// </summary>
    public class BCTicketStackInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private BCTicketStack ticketStack;

        public bool CanInteract
        {
            get
            {
                GameManager gm = FindFirstObjectByType<GameManager>();
                bool accessOpen = gm != null && gm.CanPlayerAccessTicketStack();
                return accessOpen && ticketStack != null && ticketStack.TicketCount > 0 && !ticketStack.HasTicketInHand;
            }
        }

        private void Awake()
        {
            if (ticketStack == null)
            {
                ticketStack = GetComponent<BCTicketStack>();
            }
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
            if (!CanInteract)
            {
                BCAudioManager.Instance?.PlayUIInvalid();
                return;
            }

            BCAudioManager.Instance?.PlayUIClick();
            Debug.Log("[BCTicketStack] Click - taking top ticket");

            // Вызываем метод из TicketManager
            TicketManager tm = FindFirstObjectByType<TicketManager>();
            if (tm != null)
            {
                if (!tm.TakeTicketToHand())
                {
                    BCAudioManager.Instance?.PlayUIInvalid();
                }
            }
        }

        private void ShowTooltip()
        {
            if (ticketStack == null) return;

            System.Text.StringBuilder tooltip = new System.Text.StringBuilder();
            
            tooltip.AppendLine("<b>Ticket Stack</b>");
            tooltip.AppendLine();
            tooltip.AppendLine($"Tickets available: {ticketStack.TicketCount}");
            tooltip.AppendLine();
            
            if (ticketStack.HasTicketInHand)
            {
                tooltip.AppendLine("Already holding a ticket");
                tooltip.AppendLine("Pull the lever again to return it");
            }
            else if (ticketStack.TicketCount > 0)
            {
                tooltip.AppendLine("Click to take top ticket");
                tooltip.AppendLine();
                tooltip.AppendLine("Use: Click the ticket in hand");
                tooltip.AppendLine("Close shelf: pull the lever again");
            }
            else
            {
                tooltip.AppendLine("No tickets available");
            }

            BCTooltipDisplay.Show(tooltip.ToString());
        }
    }
}
