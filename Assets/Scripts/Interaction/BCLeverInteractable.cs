using BreathCasino.Core;
using BreathCasino.Gameplay;
using BreathCasino.Systems;
using UnityEngine;

namespace BreathCasino.Gameplay
{
    public class BCLeverInteractable : MonoBehaviour, IHoldInteractable
    {
        [SerializeField] private Side owner = Side.Player;
        [SerializeField] private float holdDuration = 0.42f;
        [SerializeField] private BCLeverMechanism leverMechanism;

        private GameManager _gameManager;
        private SceneBootstrap _bootstrap;

        private bool IsIntroHoldActive => owner == Side.Player && _bootstrap != null && _bootstrap.IsIntroLeverHoldActive;

        public bool CanInteract => owner == Side.Player &&
                                   (IsIntroHoldActive || (_gameManager != null &&
                                   (_gameManager.CanPlayerPullLever() || _gameManager.CanPlayerHoldLeverToSkip())));
        public bool CanHold => owner == Side.Player &&
                               (IsIntroHoldActive || (_gameManager != null && _gameManager.CanPlayerHoldLeverToSkip()));
        public float HoldDuration => holdDuration;

        private void Awake()
        {
            if (leverMechanism == null)
            {
                leverMechanism = GetComponent<BCLeverMechanism>();
            }
        }

        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _bootstrap = FindFirstObjectByType<SceneBootstrap>();
        }

        public void Configure(Side side, BCLeverMechanism mechanism)
        {
            owner = side;
            leverMechanism = mechanism;
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
            if (IsIntroHoldActive)
            {
                BCAudioManager.Instance?.PlayUIInvalid();
                return;
            }

            if (!CanInteract)
            {
                BCAudioManager.Instance?.PlayUIInvalid();
                return;
            }

            BCAudioManager.Instance?.PlayUIClick();
            leverMechanism?.PlayPull();
            _gameManager?.HandlePlayerLeverPull();
        }

        public void OnHoldStart()
        {
            if (!CanHold)
            {
                return;
            }

            BCAudioManager.Instance?.PlayUIHover();
            leverMechanism?.SetHoldProgress(0f);
        }

        public void OnHoldProgress(float progress01)
        {
            if (!CanHold)
            {
                return;
            }

            leverMechanism?.SetHoldProgress(progress01);
        }

        public void OnHoldCancel()
        {
            leverMechanism?.CancelHold();
        }

        public void OnHoldComplete()
        {
            if (!CanHold)
            {
                leverMechanism?.CancelHold();
                return;
            }

            BCAudioManager.Instance?.PlayUIClick();
            leverMechanism?.CompleteHold();

            if (IsIntroHoldActive)
            {
                _bootstrap?.CompleteIntroLeverHold();
                return;
            }

            _gameManager?.HandlePlayerLeverHoldSkip();
        }

        private void ShowTooltip()
        {
            if (owner != Side.Player)
            {
                return;
            }

            if (IsIntroHoldActive)
            {
                BCTooltipDisplay.Show(BCLocalization.Get("intro.lever_hint"));
                return;
            }

            if (_gameManager == null)
            {
                return;
            }

            string title = _gameManager.IsPlayerTicketAccessOpen
                ? BCLocalization.Get("tooltip.lever.retract")
                : BCLocalization.Get("tooltip.lever.open");

            if (_gameManager.CanPlayerHoldLeverToSkip())
            {
                title += "\n\n" + BCLocalization.Get("tooltip.lever.hold_skip");
            }

            BCTooltipDisplay.Show(title);
        }
    }
}
