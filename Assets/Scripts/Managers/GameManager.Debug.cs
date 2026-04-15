using BreathCasino.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BreathCasino.Core
{
    public partial class GameManager
    {
        public void RefreshHud(string eventLine = null)
        {
            if (!_initialized || _scene == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(eventLine))
            {
                _lastEvent = BCLocalization.LocalizeRuntimeText(eventLine);
            }

            HudSnapshot snapshot = new(
                gameMode,
                _phase,
                _currentRoundIndex + 1,
                rounds.Length,
                _currentMiniRound,
                _playerHp,
                _playerMaxHp,
                _enemyHp,
                _enemyMaxHp,
                _currentOxygen,
                DescribeChamber(),
                _ticketManager?.DescribeTickets(Side.Player) ?? "0",
                _ticketManager?.DescribeTickets(Side.Enemy) ?? "0",
                _lastEvent);

            bool hasValidationErrors = _validator != null && !_validator.LastValidationPassed;
            _scene.SetStatus(HudFormatter.Build(snapshot, hasValidationErrors));
            _scene.SetDebugActions(_showDebugActions ? DebugActionsLegendBuilder.Build() : string.Empty);
        }

        private bool IsCriticalOxygenState()
        {
            if (_phase == GamePhase.Waiting || _phase == GamePhase.GameOver || _phase == GamePhase.RoundOver)
            {
                return false;
            }

            return _currentOxygen > 0f && _currentOxygen < GameConstants.O2_CRITICAL_THRESHOLD;
        }

        private void RefreshDynamicCameraState()
        {
            bool lastBreathActive = _lastBreathUsed && _playerHp <= 1 && _phase != GamePhase.GameOver;
            _cameraController?.SetSurvivalState(IsCriticalOxygenState(), lastBreathActive);
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            TickOxygen();
            HandleDebugInput();
            UpdateGunAim();
        }

        private void UpdateGunAim()
        {
            if (!_waitingForPlayerShot || _scene == null || _scene.gunRoot == null)
            {
                return;
            }

            Transform gun = _scene.gunRoot;
            Transform lookAt = _playerShootAtSelf
                ? (_scene.playerRoot != null ? _scene.playerRoot : gun)
                : (_scene.enemyRoot != null ? _scene.enemyRoot : gun);

            Vector3 targetPos = lookAt != null ? lookAt.position + Vector3.up * 0.5f : gun.position + gun.forward * 2f;
            gun.LookAt(targetPos);
        }

        private void HandleTicketsChanged()
        {
            RefreshHud();
        }

        private void HandleDebugInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                RefreshHud("Tickets are now used through the lever and shelf interaction.");
            }

            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                RefreshHud("Enemy tickets are now driven by enemy lever logic.");
            }

            if (keyboard.tKey.wasPressedThisFrame)
            {
                if (AreTicketsEnabledForCurrentRound)
                {
                    _ticketManager.GiveRandomTicket(Side.Player);
                    StartCoroutine(EnsurePendingCardPickups());
                    Debug.Log($"{LogPrefix} Ticket given: Player");
                }
                else
                {
                    RefreshHud("Tickets are disabled in round 1.");
                }
            }

            if (keyboard.yKey.wasPressedThisFrame)
            {
                if (AreTicketsEnabledForCurrentRound)
                {
                    _ticketManager.GiveRandomTicket(Side.Enemy);
                    StartCoroutine(EnsurePendingCardPickups());
                    Debug.Log($"{LogPrefix} Ticket given: Enemy");
                }
                else
                {
                    RefreshHud("Tickets are disabled in round 1.");
                }
            }

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                _showDebugActions = !_showDebugActions;
                RefreshHud(_showDebugActions ? "Debug hotkeys shown." : "Debug hotkeys hidden.");
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                Debug.Log($"{LogPrefix} Restart");
                    BeginGame();
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                gameMode = gameMode == GameMode.Spectator ? GameMode.Player : GameMode.Spectator;
                Debug.Log($"{LogPrefix} Mode -> {gameMode}");
            }

            if (keyboard.mKey.wasPressedThisFrame && _dealMechanism != null)
            {
                StartCoroutine(_dealMechanism.Animate(!_dealMechanism.IsRaised));
                RefreshHud(_dealMechanism.IsRaised ? "Mechanism lowering." : "Mechanism raising.");
                return;
            }

            if (keyboard.hKey.wasPressedThisFrame)
            {
                _enemyDialogue?.Speak("phase_attack");
                RefreshHud("Enemy dialogue test triggered.");
                return;
            }

            if (keyboard.jKey.wasPressedThisFrame)
            {
                _cameraController?.ShakeOnDamage();
                RefreshHud("Player hit haze test triggered.");
                return;
            }

            if (keyboard.gKey.wasPressedThisFrame)
            {
                MoveGunToTable();
                RefreshHud("Gun re-docked on the mechanism.");
                return;
            }

            if (keyboard.pKey.wasPressedThisFrame)
            {
                bool tookCards = _cardManager != null && _cardManager.TakePendingCards(Side.Player) > 0;
                bool tookTicket = _ticketManager != null && _ticketManager.TakePendingTicketToHand();
                RefreshHud(tookCards || tookTicket ? "Pending player items taken." : "No pending player items.");
                return;
            }

            if (keyboard.oKey.wasPressedThisFrame)
            {
                StartCoroutine(TogglePlayerTicketAccessCoroutine());
                RefreshHud("Player ticket shelf toggle requested.");
                return;
            }

            if (keyboard.iKey.wasPressedThisFrame)
            {
                StartCoroutine(ToggleEnemyTicketAccessForDebug());
                RefreshHud("Enemy ticket shelf toggle requested.");
                return;
            }

            if (_waitingForPlayerShot)
            {
                HandlePlayerShotDebugInput(keyboard);
                return;
            }

            if (_waitingForPlayerGunPickup)
            {
                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    _waitingForPlayerGunPickup = false;
                }
                return;
            }

            if (_waitingForPlayerCardPickup)
            {
                HandlePlayerPickupDebugInput(keyboard);
                return;
            }

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                JumpToRoundForDebug(1);
                return;
            }

            if (keyboard.f3Key.wasPressedThisFrame)
            {
                JumpToRoundForDebug(2);
                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame && _waitingForPlayerSubmit)
            {
                HandlePlayerSubmitDebugInput();
            }
        }

        private void HandlePlayerShotDebugInput(Keyboard keyboard)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                _playerShootAtSelf = !_playerShootAtSelf;
                RefreshHud($"Target: {(_playerShootAtSelf ? "SELF" : "OPPONENT")}. SPACE or click gun to shoot.");
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                _waitingForPlayerShot = false;
            }
        }

        private void HandlePlayerPickupDebugInput(Keyboard keyboard)
        {
            if (!keyboard.spaceKey.wasPressedThisFrame)
            {
                return;
            }

            bool tookCards = _cardManager != null && _cardManager.TakePendingCards(Side.Player) > 0;
            bool tookTicket = _ticketManager != null && _ticketManager.TakePendingTicketToHand();

            if (tookCards || tookTicket)
            {
                if (!HasPendingTablePickup(Side.Player))
                {
                    _waitingForPlayerCardPickup = false;
                }

                RefreshHud("Player took the issued items from the mechanism.");
            }
        }

        private void HandlePlayerSubmitDebugInput()
        {
            FillPlayerSubmittedMainCards(_playerSubmittedMainScratch);
            var mainCards = _playerSubmittedMainScratch;
            var special = PlayerSubmittedSpecialCard;

            if (mainCards.Count > 0 || special != null)
            {
                if (IsInvalidPlayerAttack(mainCards))
                {
                    ReturnPlayerStagedCardsToHand();
                    RefreshHud("Threat attacks can only be paired with a special card.");
                    Debug.Log($"{LogPrefix} Player attack rejected — Threat cannot be combined with another main card.");
                }
                else if (IsInvalidPlayerDefense(mainCards))
                {
                    ReturnPlayerStagedCardsToHand();
                    RefreshHud("Defense is too weak. Cards returned to hand.");
                    Debug.Log($"{LogPrefix} Player defense rejected — staged weight below enemy attack.");
                }
                else
                {
                    _waitingForPlayerSubmit = false;
                    PlayerSelectionTracker.ClearSelection();
                    Debug.Log($"{LogPrefix} Player submitted staged cards");
                }

                return;
            }

            if (PlayerSelectionTracker.SelectedCards.Count > 0)
            {
                foreach (var card in PlayerSelectionTracker.SelectedCards)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        if (GetTrackedPlayerSlotCard(i) != null)
                        {
                            continue;
                        }

                        Transform slotTransform = i == 0 ? _scene?.playerMainSlot : _scene?.playerSpecialSlot;
                        if (slotTransform == null)
                        {
                            continue;
                        }

                        BCCardSlot slot = slotTransform.GetComponent<BCCardSlot>();
                        if (slot != null)
                        {
                            StageCardToSlot(card, i, slot);
                        }

                        break;
                    }
                }

                _waitingForPlayerSubmit = false;
                PlayerSelectionTracker.ClearSelection();
                Debug.Log($"{LogPrefix} Player submitted selected cards");
                return;
            }

            _waitingForPlayerSubmit = false;
            PlayerSelectionTracker.ClearSelection();
            RefreshHud("Player skipped the card phase.");
            Debug.Log($"{LogPrefix} Player submitted no cards — treated as skip.");
        }

        private void JumpToRoundForDebug(int roundIndex)
        {
            if (roundIndex < 0 || roundIndex >= rounds.Length)
            {
                return;
            }

            if (_loopRoutine != null)
            {
                StopCoroutine(_loopRoutine);
                _loopRoutine = null;
            }

            _currentRoundIndex = roundIndex;
            _currentMiniRound = 0;
            _nextCardPhaseAttacker = Side.Player;
            _transitionScheduled = false;
            _playerDecisionIsDefense = false;
            _playerTicketAccessOpen = false;
            _enemyTicketAccessOpen = false;
            _lastBreathUsed = false;
            _ticketManager?.ResetAll();
            ClearPlayerSubmissionState();
            _handAnimator?.LowerBothHands();
            _cameraController?.ResetDynamicEffects();
            Debug.Log($"{LogPrefix} Debug jump -> Round {roundIndex + 1}");
            StartRound(roundIndex);
            RefreshHud($"Jumped to round {roundIndex + 1} for debug.");
        }
    }
}
