using UnityEngine;
using BreathCasino.Core;
using BreathCasino.Systems;

namespace BreathCasino.Gameplay
{
    public class BCGunInteractable : MonoBehaviour, IInteractable
    {
        public bool CanInteract
        {
            get
            {
                var gm = FindFirstObjectByType<GameManager>();
                return gm != null && (gm.IsWaitingForPlayerGunPickup || gm.IsWaitingForPlayerShot);
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
            var gm = FindFirstObjectByType<GameManager>();
            if (gm == null) return;

            if (gm.IsWaitingForPlayerGunPickup)
            {
                BCAudioManager.Instance?.PlayUIClick();
                gm.ConfirmPlayerGunPickup();
                return;
            }

            if (gm.IsWaitingForPlayerShot)
            {
                BCAudioManager.Instance?.PlayUIClick();
                gm.ConfirmPlayerShot();
            }
        }

        private void ShowTooltip()
        {
            System.Text.StringBuilder tooltip = new System.Text.StringBuilder();
            
            tooltip.AppendLine("<b>Revolver</b>");
            tooltip.AppendLine();
            tooltip.AppendLine("Type: Weapon");
            tooltip.AppendLine();
            tooltip.AppendLine("Win a duel to gain control");
            tooltip.AppendLine("of the gun and shoot.");
            tooltip.AppendLine();
            tooltip.AppendLine("Bullet Types:");
            tooltip.AppendLine("• Blank: No damage");
            tooltip.AppendLine("• Live: 1 damage");
            tooltip.AppendLine("• Explosive: 2 damage (Round 3)");
            tooltip.AppendLine();
            tooltip.AppendLine("Shoot yourself with Blank");
            tooltip.AppendLine("to keep your turn!");

            BCTooltipDisplay.Show(tooltip.ToString());
        }
    }
}