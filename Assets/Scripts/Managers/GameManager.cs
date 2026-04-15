using System.Collections;
using BreathCasino.Core;
using BreathCasino.Gameplay;
using BreathCasino.Rendering;
using BreathCasino.Systems;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BreathCasino.Core
{
    public partial class GameManager : MonoBehaviour
    {
        private const string LogPrefix = "[BreathCasino]";
        [Header("Timing")]
        [SerializeField] private float phaseDelay = 0.8f;
        [SerializeField] private float roundTransitionDelay = 1.25f;
        [SerializeField] private float duelResultViewTime = 2.5f;

        [Header("Mini-round presentation")]
        [SerializeField] private float bulletRevealItemStagger = 0.08f;
        [SerializeField] private float bulletRevealExpandDuration = 0.28f;
        [SerializeField] private float bulletRevealHoldDuration = 0.65f;
        [SerializeField] private float gunBlackoutDuration = 0.24f;
        [SerializeField] private float gunBlackoutHoldDuration = 0.52f;
        [SerializeField] private float gunSettleDelay = 0.35f;
        [SerializeField] private AudioClip bulletRevealClip;
        [SerializeField] private AudioClip gunDrumSpinClip;
        [SerializeField] private AudioClip gunPlaceClip;

        [Header("Mode")]
        [SerializeField] private GameMode gameMode = GameMode.Player;
        private bool AutoSubmitPlayerActions => gameMode == GameMode.Spectator;

        public GameMode CurrentMode => gameMode;

        [Header("Round Defaults")]
        [SerializeField] private RoundConfig[] rounds =
        {
            new RoundConfig { playerHp = 2, enemyHp = 2, oxygenSeconds = 60f, minBullets = 2, maxBullets = 3, allowExplosive = false },
            new RoundConfig { playerHp = 3, enemyHp = 3, oxygenSeconds = 50f, minBullets = 3, maxBullets = 5, allowExplosive = false },
            new RoundConfig { playerHp = 4, enemyHp = 4, oxygenSeconds = 40f, minBullets = 4, maxBullets = 6, allowExplosive = true }
        };

        private readonly List<BulletType> _chamber = new();
        private readonly List<GameObject> _bulletVisuals = new();
        private readonly List<BCCardDisplay> _playerSubmittedMainScratch = new(2);
        private static readonly Dictionary<int, Material> BulletMaterialCache = new();

        private SceneBootstrap _scene;
        private TicketManager _ticketManager;
        private BCCardManager _cardManager;
        private SceneValidator _validator;
        private BCHandAnimator _handAnimator;
        private EnemyAI _enemyAI;          // pure class, создаётся через new — не MonoBehaviour
        private BCAudioManager _audioManager;
        private BCCameraController _cameraController;
        private BCCardSupplyStack _playerSupplyStack;
        private BCCardSupplyStack _enemySupplyStack;
        private BCDealMechanism _dealMechanism;
        private Coroutine _loopRoutine;
        private GamePhase _phase = GamePhase.Waiting;
        private string _lastEvent = "Quick Setup ready.";

        /// <summary>Событие смены фазы — слушают AudioManager и другие системы.</summary>
        public event System.Action<GamePhase> OnPhaseChanged;

        private int _currentRoundIndex;
        private int _currentMiniRound;
        private int _playerHp;
        private int _enemyHp;
        private int _playerMaxHp;
        private int _enemyMaxHp;
        private float _currentOxygen;
        private bool _lastBreathUsed;
        private bool _o2CriticalSoundPlayed;
        private bool _initialized;
        private bool _transitionScheduled;
        private bool _prohibitSpecialsNextMiniRound;
        private int _prohibitSpecialsMinRoundsRemaining;
        private bool _waitingForPlayerSubmit;
        private bool _waitingForPlayerCardPickup;
        private bool _showDebugActions = true;
        private bool _playerDecisionIsDefense;
        private bool _playerTicketAccessOpen;
        private bool _enemyTicketAccessOpen;
        // Карты, размещённые игроком: индекс 0 = Slot1, 1 = Slot2. null — слот пустой.
        private readonly BCCardDisplay[] _playerSlotCards = new BCCardDisplay[2];
        private BCCardDisplay PlayerSubmittedSpecialCard
        {
            get
            {
                for (int i = 0; i < _playerSlotCards.Length; i++)
                {
                    BCCardDisplay card = GetTrackedPlayerSlotCard(i);
                    if (card != null && card.CardType == CardType.Special)
                    {
                        return card;
                    }
                }

                return null;
            }
        }
        private bool _waitingForPlayerShot;
        private bool _waitingForPlayerGunPickup;
        private bool _playerShootAtSelf;
        private Side _currentAttacker;
        private Side _currentDefender;
        private Side _nextCardPhaseAttacker = Side.Player;
        private BCLeverMechanism _playerLever;
        private BCLeverMechanism _enemyLever;
        private BCEnemyDialogueController _enemyDialogue;
        private Vector3 _gunAuthoredWorldScale = Vector3.one;
        private Transform _gunStartParent;
        private Vector3 _gunStartLocalPosition;
        private Quaternion _gunStartLocalRotation = Quaternion.identity;
        private Vector3 _gunStartLocalScale = Vector3.one;
        private bool _skipNextBulletPresentation;
        public BCDealMechanism DealMechanism => _dealMechanism;

        private Transform GetPlayerSlotTransform(int slotIndex)
        {
            if (_scene == null)
            {
                return null;
            }

            return slotIndex == 0 ? _scene.playerMainSlot : _scene.playerSpecialSlot;
        }

        private BCCardSlot GetPlayerSlot(int slotIndex)
        {
            Transform slotTransform = GetPlayerSlotTransform(slotIndex);
            return slotTransform != null ? slotTransform.GetComponent<BCCardSlot>() : null;
        }

        private BCCardDisplay GetTrackedPlayerSlotCard(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _playerSlotCards.Length)
            {
                return null;
            }

            Transform slotTransform = GetPlayerSlotTransform(slotIndex);
            BCCardSlot slot = slotTransform != null ? slotTransform.GetComponent<BCCardSlot>() : null;
            Transform anchor = slot != null ? slot.CardAnchor : slotTransform;
            BCCardDisplay tracked = _playerSlotCards[slotIndex];

            if (tracked != null)
            {
                if (tracked.transform == null || (anchor != null && tracked.transform.parent != anchor))
                {
                    tracked = null;
                }
            }

            if (tracked == null && anchor != null && anchor.childCount > 0)
            {
                tracked = anchor.GetChild(0).GetComponent<BCCardDisplay>();
            }

            _playerSlotCards[slotIndex] = tracked;
            return tracked;
        }

        public void Initialize(SceneBootstrap scene, TicketManager ticketManager, BCCardManager cardManager, SceneValidator validator, BCHandAnimator handAnimator = null)
        {
            _scene = scene;
            _ticketManager = ticketManager;
            _cardManager = cardManager;
            _validator = validator;
            _handAnimator = handAnimator;
            _enemyAI = new EnemyAI();   // pure class, не MonoBehaviour
            _audioManager = FindFirstObjectByType<BCAudioManager>();
            _cameraController = FindFirstObjectByType<BCCameraController>();
            _playerSupplyStack = scene != null && scene.DealMechanism != null && scene.DealMechanism.PlayerCardSocket != null
                ? scene.DealMechanism.PlayerCardSocket.GetComponent<BCCardSupplyStack>()
                : null;
            _enemySupplyStack = scene != null && scene.DealMechanism != null && scene.DealMechanism.EnemyCardSocket != null
                ? scene.DealMechanism.EnemyCardSocket.GetComponent<BCCardSupplyStack>()
                : null;
            _dealMechanism = scene != null ? scene.DealMechanism : FindFirstObjectByType<BCDealMechanism>();
            _playerLever = scene != null ? scene.PlayerLever : null;
            _enemyLever = scene != null ? scene.EnemyLever : null;
            _enemyDialogue = scene != null ? scene.EnemyDialogueController : null;
            if (_scene != null && _scene.gunRoot != null)
            {
                Transform gun = _scene.gunRoot;
                _gunAuthoredWorldScale = gun.lossyScale;
                _gunStartParent = gun.parent;
                _gunStartLocalPosition = gun.localPosition;
                _gunStartLocalRotation = gun.localRotation;
                _gunStartLocalScale = gun.localScale;
            }
            _cardManager?.ConfigureDealStacks(_playerSupplyStack, _enemySupplyStack);
            _initialized = true;
            _cameraController?.ResetDynamicEffects();

            if (_ticketManager != null)
            {
                _ticketManager.Initialize(this);
                _ticketManager.OnTicketsChanged -= HandleTicketsChanged;
                _ticketManager.OnTicketsChanged += HandleTicketsChanged;
            }
        }

        public void BeginGame()
        {
            if (!_initialized)
            {
                return;
            }

            _lastBreathUsed = false;
            _currentRoundIndex = 0;
            _currentMiniRound = 0;
            _nextCardPhaseAttacker = Side.Player;
            _transitionScheduled = false;
            _playerDecisionIsDefense = false;
            _playerTicketAccessOpen = false;
            _enemyTicketAccessOpen = false;
            _ticketManager?.ResetAll();
            ClearPlayerSubmissionState();
            _handAnimator?.LowerBothHands();
            _cameraController?.ResetDynamicEffects();
            Debug.Log($"{LogPrefix} BeginGame");
            StartRound(_currentRoundIndex);
        }

        public void BeginTestGame() => BeginGame();

        public void SetIntroPresentationConsumed(bool consumed)
        {
            _skipNextBulletPresentation = consumed;
        }

        public void Heal(Side side, int amount)
        {
            if (side == Side.Player)
            {
                _playerHp = Mathf.Min(_playerHp + amount, _playerMaxHp);
            }
            else
            {
                _enemyHp = Mathf.Min(_enemyHp + amount, _enemyMaxHp);
            }
            _audioManager?.PlayHPHeal();
        }

        public int GetHp(Side side) => side == Side.Player ? _playerHp : _enemyHp;

        /// <summary>Обмен 1 HP на новые карты. Требуется HP > 1.</summary>
        public bool TrySpendHpForCards(Side side)
        {
            int hp = side == Side.Player ? _playerHp : _enemyHp;
            if (hp <= 1) return false;
            if (side == Side.Player) _playerHp--; else _enemyHp--;
            _cardManager?.RefillDeckFromLife(6);
            return true;
        }

        public string PeekCurrentBullet()
        {
            return _chamber.Count > 0 ? _chamber[0].ToString() : "Empty";
        }

        public void VoidCurrentBullet(Side side)
        {
            if (_chamber.Count == 0)
            {
                RefreshHud($"{side} used VoidTransaction, but the chamber was already empty.");
                return;
            }

            BulletType removed = _chamber[0];
            _chamber.RemoveAt(0);
            RenderChamberVisuals();
            Debug.Log($"{LogPrefix} VoidTransaction: {side} removed {removed}");
            RefreshHud($"{side} used VoidTransaction and removed {removed}.");
        }

        public void ShuffleChamber(Side side)
        {
            for (int i = _chamber.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_chamber[i], _chamber[j]) = (_chamber[j], _chamber[i]);
            }

            RenderChamberVisuals();
            Debug.Log($"{LogPrefix} Shuffle: {side}");
            RefreshHud($"{side} used Shuffle.");
        }

        public void AddOxygen(float amount)
        {
            if (amount <= 0 || _phase == GamePhase.Waiting || _phase == GamePhase.GameOver)
            {
                return;
            }

            _currentOxygen += amount;
            _audioManager?.PlayO2Refill();

            // Сброс флага критического O2 если поднялись выше 20s
            if (_currentOxygen > 20f)
            {
                _o2CriticalSoundPlayed = false;
            }

            Debug.Log($"{LogPrefix} O2 +{amount}s → {_currentOxygen:0.0}s");
            RefreshDynamicCameraState();
            RefreshHud();
        }

        public void ConfirmPlayerShot()
        {
            if (_waitingForPlayerShot)
            {
                _waitingForPlayerShot = false;
            }
        }

        public void ConfirmPlayerGunPickup()
        {
            if (_waitingForPlayerGunPickup)
            {
                _waitingForPlayerGunPickup = false;
            }
        }

        public bool IsWaitingForPlayerShot => _waitingForPlayerShot;
        public bool IsWaitingForPlayerGunPickup => _waitingForPlayerGunPickup;
        public bool IsWaitingForPlayerCardPickup => _waitingForPlayerCardPickup;
        public bool IsPlayerTicketAccessOpen => _playerTicketAccessOpen;
        public bool AreTicketsEnabledForCurrentRound => _currentRoundIndex >= 1;

        public bool CanPlayerPullLever()
        {
            if (!_initialized || gameMode != GameMode.Player || _phase == GamePhase.Waiting || _phase == GamePhase.GameOver)
            {
                return false;
            }

            if (_waitingForPlayerGunPickup || _waitingForPlayerShot)
            {
                return false;
            }

            if (_ticketManager != null && _ticketManager.IsTicketUseInProgress)
            {
                return false;
            }

            if (_playerTicketAccessOpen)
            {
                return true;
            }

            return _waitingForPlayerSubmit &&
                   AreTicketsEnabledForCurrentRound &&
                   _ticketManager != null &&
                   _ticketManager.HasUsableTicket(Side.Player);
        }

        public bool CanPlayerHoldLeverToSkip()
        {
            return _initialized &&
                   gameMode == GameMode.Player &&
                   _waitingForPlayerSubmit &&
                   _playerDecisionIsDefense &&
                   !_playerTicketAccessOpen;
        }

        public bool CanPlayerAccessTicketStack()
        {
            return _playerTicketAccessOpen &&
                   !_waitingForPlayerShot &&
                   !_waitingForPlayerGunPickup &&
                   (_ticketManager == null || !_ticketManager.IsTicketUseInProgress);
        }

        public bool TryTakePendingCards(BCCardDisplay card)
        {
            if (!_waitingForPlayerCardPickup || card == null || card.IsEnemyCard || _cardManager == null)
            {
                return false;
            }

            if (!_cardManager.IsPendingCard(card))
            {
                return false;
            }

            if (_cardManager.TakePendingCards(Side.Player) <= 0)
            {
                return false;
            }

            _waitingForPlayerCardPickup = false;
            RefreshHud("Player took the round card stack.");
            return true;
        }

        public void HandlePlayerLeverPull()
        {
            if (!CanPlayerPullLever())
            {
                RefreshHud(AreTicketsEnabledForCurrentRound
                    ? "No ticket action is available right now."
                    : "Tickets only unlock from round 2.");
                return;
            }

            StartCoroutine(TogglePlayerTicketAccessCoroutine());
        }

        public void HandlePlayerLeverHoldSkip()
        {
            if (!CanPlayerHoldLeverToSkip())
            {
                RefreshHud("Lever hold skip only works while defending.");
                return;
            }

            ReturnPlayerStagedCardsToHand();
            _waitingForPlayerSubmit = false;
            PlayerSelectionTracker.ClearSelection();
            RefreshHud("Player skipped the defense by pulling the lever.");
            _enemyDialogue?.Speak("phase_defense");
            Debug.Log($"{LogPrefix} Player skipped defense through the lever.");
        }

        public void SubmitPlayerCard(BCCardDisplay card, bool isSpecial)
        {
            if (!_waitingForPlayerSubmit || card == null) return;
            if (isSpecial)
            {
                PlayerSelectionTracker.ToggleSelectedSpecial(card);
            }
            else
            {
                PlayerSelectionTracker.ToggleSelectedMain(card);
            }

            BCCardSlot targetSlot = GetFirstFreePlayerSlot();
            if (targetSlot != null)
            {
                SubmitPlayerSelectionToSlot(targetSlot);
            }
        }

        public void SubmitPlayerSelectionToSlot(BCCardSlot slot)
        {
            if (!_waitingForPlayerSubmit || slot == null || slot.Side != Side.Player) return;

            // Определяем индекс слота (0 или 1)
            int slotIndex = slot.SlotKind == SlotKind.Slot1 ? 0 : 1;

            // Слот уже занят
            if (GetTrackedPlayerSlotCard(slotIndex) != null)
            {
                RefreshHud($"Slot {slotIndex + 1} already has a card.");
                return;
            }

            BCCardDisplay selected = PlayerSelectionTracker.SelectedSpecialCard
                ?? PlayerSelectionTracker.SelectedMainCard;

            if (selected == null)
            {
                RefreshHud("Select a card first, then click a slot.");
                return;
            }

            if (!StageCardToSlot(selected, slotIndex, slot))
                return;

            RefreshHud(GetSubmissionStatusText());
        }

        private bool StageCardToSlot(BCCardDisplay card, int slotIndex, BCCardSlot slot)
        {
            if (card == null || slot == null) return false;

            // Проверяем правила из MECHANICS.md:
            // - Спецкарту нельзя класть без обычной карты хотя бы в одном слоте
            BCCardDisplay slot0Card = GetTrackedPlayerSlotCard(0);
            BCCardDisplay slot1Card = GetTrackedPlayerSlotCard(1);
            CardType? slot0Type = slot0Card != null ? slot0Card.CardType : (CardType?)null;
            CardType? slot1Type = slot1Card != null ? slot1Card.CardType : (CardType?)null;

            if (!SlotRules.CanPlaceCard(card.CardType, slot0Type, slot1Type, slotIndex))
            {
                if (card.CardType == CardType.Special)
                    RefreshHud("Place a normal card first, then play a special card.");
                return false;
            }

            // Защищающийся не может класть Threat карту
            if (_phase == GamePhase.Defense && _currentDefender == Side.Player && card.IsThreat)
            {
                RefreshHud("Defense: only Resource cards allowed.");
                PlayerSelectionTracker.ClearSelection();
                return false;
            }

            if (_prohibitSpecialsNextMiniRound && card.CardType == CardType.Special)
            {
                RefreshHud("Special cards prohibited this mini-round.");
                return false;
            }

            if (_phase == GamePhase.Attack &&
                _currentAttacker == Side.Player &&
                SlotRules.CreatesInvalidThreatAttackCombination(card.CardType, slot0Type, slot1Type))
            {
                RefreshHud("Threat attacks can only be paired with a special card.");
                PlayerSelectionTracker.ClearSelection();
                return false;
            }

            _playerSlotCards[slotIndex] = card;
            Transform anchor = slot.CardAnchor != null ? slot.CardAnchor : slot.transform;
            card.MoveToSlot(anchor, Vector3.zero, Quaternion.identity, 0.22f);
            PlayerSelectionTracker.ClearSelection();
            _cardManager?.RelayoutHands();
            return true;
        }

        private void ClearPlayerSubmissionState()
        {
            _playerSlotCards[0] = null;
            _playerSlotCards[1] = null;
            _waitingForPlayerSubmit = false;
            _waitingForPlayerCardPickup = false;
            PlayerSelectionTracker.ClearSelection();
        }

        private int FillPlayerSubmittedMainCards(List<BCCardDisplay> destination)
        {
            if (destination == null)
            {
                return 0;
            }

            destination.Clear();
            for (int i = 0; i < _playerSlotCards.Length; i++)
            {
                BCCardDisplay card = GetTrackedPlayerSlotCard(i);
                if (card != null && card.CardType != CardType.Special)
                {
                    destination.Add(card);
                }
            }

            return destination.Count;
        }

        private string GetSubmissionStatusText()
        {
            FillPlayerSubmittedMainCards(_playerSubmittedMainScratch);
            var special = PlayerSubmittedSpecialCard;

            if (_playerSubmittedMainScratch.Count == 0 && special == null)
                return _playerDecisionIsDefense
                    ? "Select a defense card, click a slot, or hold the lever to skip. Press SPACE to confirm."
                    : "Select a card, then click a slot. Press SPACE to confirm.";

            int totalWeight = GetCardsWeight(_playerSubmittedMainScratch);
            string specialText = special != null ? $" + special {special.CardName}" : string.Empty;
            return $"Staged weight: {totalWeight}{specialText}. Press SPACE to confirm.";
        }

        private bool IsInvalidPlayerAttack(IReadOnlyList<BCCardDisplay> stagedMainCards)
        {
            if (_phase != GamePhase.Attack || _currentAttacker != Side.Player || stagedMainCards == null)
            {
                return false;
            }

            int mainCount = 0;
            bool hasThreat = false;

            for (int i = 0; i < stagedMainCards.Count; i++)
            {
                BCCardDisplay card = stagedMainCards[i];
                if (card == null || card.CardType == CardType.Special)
                {
                    continue;
                }

                mainCount++;
                hasThreat |= card.IsThreat;
            }

            return mainCount > 1 && hasThreat;
        }

        private void TickOxygen()
        {
            if (_phase == GamePhase.Waiting || _phase == GamePhase.GameOver || _phase == GamePhase.RoundOver)
            {
                return;
            }

            _currentOxygen = Mathf.Max(0f, _currentOxygen - Time.deltaTime);

            // Критический уровень O2 (<20s)
            if (_currentOxygen < 20f && _currentOxygen > 0f && !_o2CriticalSoundPlayed)
            {
                _o2CriticalSoundPlayed = true;
                _audioManager?.PlayO2Critical();
            }

            if (_currentOxygen > 0f)
            {
                RefreshDynamicCameraState();
                RefreshHud();
                return;
            }

            if (!_lastBreathUsed)
            {
                _lastBreathUsed = true;
                int hpBeforeLastBreath = _playerHp;
                _playerHp = Mathf.Max(0, _playerHp - 1);
                _currentOxygen = GameConstants.LAST_BREATH_DURATION;
                _audioManager?.PlayHPDamage();
                _audioManager?.PlayO2LastBreath();
                _cameraController?.TriggerLastBreathEffect();
                _enemyDialogue?.Speak("player_last_breath");
                RefreshDynamicCameraState();
                Debug.Log($"{LogPrefix} LastBreath triggered | HP {hpBeforeLastBreath} -> {_playerHp} | Enemy HP stays {_enemyHp} | O2 -> {_currentOxygen:0.0}s");

                if (_playerHp <= 0)
                {
                    GameOver(Side.Enemy, "Oxygen depleted on last breath.");
                    return;
                }

                RefreshHud($"Last breath triggered. Player lost 1 HP and has {_playerHp}/{_playerMaxHp} HP.");
                return;
            }

            Debug.Log($"{LogPrefix} Oxygen depleted (last breath failed)");
            GameOver(Side.Enemy, "Oxygen depleted during last breath.");
        }

        private void StartRound(int roundIndex)
        {
            RoundConfig config = rounds[Mathf.Clamp(roundIndex, 0, rounds.Length - 1)];
            _currentRoundIndex = roundIndex;
            _currentMiniRound = 0;
            _playerHp = config.playerHp;
            _enemyHp = config.enemyHp;
            _playerMaxHp = config.playerHp;
            _enemyMaxHp = config.enemyHp;
            _currentOxygen = config.oxygenSeconds;
            _nextCardPhaseAttacker = Side.Player;
            _transitionScheduled = false;
            _playerTicketAccessOpen = false;
            _enemyTicketAccessOpen = false;
            _playerDecisionIsDefense = false;
            ClearPlayerSubmissionState();
            _cameraController?.ResetDynamicEffects();

            _cardManager?.ClearAllHands();
            _cardManager?.DealHands(_scene.playerHandHolder, _scene.playerSpecialHolder, _scene.enemyHandHolder, _scene.enemySpecialHolder, _currentRoundIndex, true);
            ClearBulletVisuals();
            RestoreGunToSceneStartPose();
            _dealMechanism?.Snap(false);
            Debug.Log($"{LogPrefix} StartRound {roundIndex + 1} | HP P:{config.playerHp} E:{config.enemyHp} | O2:{config.oxygenSeconds}s");
            _enemyDialogue?.Speak("round_start");
            RefreshDynamicCameraState();
            RefreshHud($"Round {roundIndex + 1} started.");
            RunLoop(RunMiniRoundSequence());
        }

        public bool IsSpecialsProhibited => _prohibitSpecialsNextMiniRound;

        private IEnumerator RunMiniRoundSequence()
        {
            if (_scene == null)
            {
                Debug.LogError($"{LogPrefix} RunMiniRoundSequence: _scene is null. Cannot deal cards.");
                yield break;
            }

            _currentMiniRound++;
            _prohibitSpecialsNextMiniRound = _prohibitSpecialsMinRoundsRemaining > 0;
            if (_prohibitSpecialsMinRoundsRemaining > 0) _prohibitSpecialsMinRoundsRemaining--;
            ClearCombatSlots();
            _handAnimator?.LowerBothHands();
            _cardManager?.RefillHandsToRoundLimit(
                _currentRoundIndex,
                _scene.playerHandHolder,
                _scene.playerSpecialHolder,
                _scene.enemyHandHolder,
                _scene.enemySpecialHolder);
            GenerateChamber();
            if (_skipNextBulletPresentation)
            {
                _skipNextBulletPresentation = false;
                ClearBulletVisuals();
            }
            else
            {
                yield return RunBulletPresentationSequence();
            }

            Debug.Log($"{LogPrefix} MiniRound {_currentMiniRound} | Chamber: {DescribeChamber()} | Cards dealt");

            yield return EnsurePendingCardPickups();

            SetPhase(GamePhase.Dealing, "Dealing cards.");
            yield return new WaitForSeconds(phaseDelay);

            yield return RunDuelLoop();
        }

        private IEnumerator RunDuelLoop()
        {
            if (_scene == null)
            {
                Debug.LogError($"{LogPrefix} RunDuelLoop: _scene is null.");
                yield break;
            }

            Side currentAttacker = _nextCardPhaseAttacker;

            while (!_transitionScheduled && _phase != GamePhase.GameOver)
            {
                // Если у одной из сторон полностью закончились основные карты —
                // эта сторона получает новую тройку карт текущего раунда.
                if (_cardManager != null &&
                    (_cardManager.PlayerMainCards.Count == 0 ||
                     _cardManager.EnemyMainCards.Count == 0))
                {
                    Debug.Log($"{LogPrefix} Empty hand detected during duel loop — refilling only empty sides.");
                    _cardManager.RefillHandsToRoundLimit(
                        _currentRoundIndex,
                        _scene.playerHandHolder,
                        _scene.playerSpecialHolder,
                        _scene.enemyHandHolder,
                        _scene.enemySpecialHolder);
                    yield return EnsurePendingCardPickups();
                }

                SetPhase(GamePhase.Attack, "Attack phase.");

                Side attacker = currentAttacker;
                Side defender = Opponent(attacker);
                _currentAttacker = attacker;
                _currentDefender = defender;
                Transform attackSlot = attacker == Side.Player ? _scene.playerMainSlot : _scene.enemyMainSlot;
                Transform defenseSlot = defender == Side.Player ? _scene.playerMainSlot : _scene.enemyMainSlot;

                _handAnimator?.RaiseHandForSideAnimated(attacker);

                if (attacker == Side.Enemy)
                {
                    yield return TryEnemyTicketUseOpportunity("enemy_attack", 0.18f);
                }

                if (gameMode == GameMode.Player && attacker == Side.Player)
                {
                    _playerDecisionIsDefense = false;
                    ClearPlayerSubmissionState();
                    _waitingForPlayerSubmit = true;
                    RefreshHud("Attack phase...");
                    if (!AutoSubmitPlayerActions)
                    {
                        while (_waitingForPlayerSubmit) yield return null;
                    }
                    else
                    {
                        yield return new WaitForSeconds(0.2f);
                        _waitingForPlayerSubmit = false;
                    }
                }

                FillPlayerSubmittedMainCards(_playerSubmittedMainScratch);
                List<BreathCasino.Gameplay.BCCardDisplay> attCards = attacker == Side.Player && _playerSubmittedMainScratch.Count > 0
                    ? new List<BreathCasino.Gameplay.BCCardDisplay>(_playerSubmittedMainScratch)
                    : GetAIAttackCards(attacker);
                BCCardDisplay attSpecial = attacker == Side.Player ? PlayerSubmittedSpecialCard : GetAISpecialCard(attacker);

                if (attacker != Side.Player)
                {
                    yield return new WaitForSeconds(Random.Range(0.6f, 1.2f));
                    MoveCardsToCombatSlot(attCards, attackSlot);
                    if (attSpecial != null && _scene.enemySpecialSlot != null)
                        MoveSpecialToSlot(attSpecial, _scene.enemySpecialSlot);
                }

                yield return new WaitForSeconds(phaseDelay);

                SetPhase(GamePhase.Defense, "Defense phase.");
                _playerDecisionIsDefense = false;
                if (gameMode == GameMode.Player && defender == Side.Player)
                {
                    _playerDecisionIsDefense = true;
                    _handAnimator?.RaiseHandForSideAnimated(Side.Player);
                    ClearPlayerSubmissionState();
                    _waitingForPlayerSubmit = true;
                    RefreshHud("Defense phase...");
                    if (!AutoSubmitPlayerActions)
                    {
                        while (_waitingForPlayerSubmit) yield return null;
                    }
                    else
                    {
                        yield return new WaitForSeconds(0.2f);
                        _waitingForPlayerSubmit = false;
                    }
                }

                int attackWeight = GetCardsWeight(attCards);
                if (defender == Side.Enemy)
                {
                    yield return TryEnemyTicketUseOpportunity("enemy_defense", 0.28f);
                }
                FillPlayerSubmittedMainCards(_playerSubmittedMainScratch);
                List<BreathCasino.Gameplay.BCCardDisplay> defCards = defender == Side.Player && _playerSubmittedMainScratch.Count > 0
                    ? new List<BreathCasino.Gameplay.BCCardDisplay>(_playerSubmittedMainScratch)
                    : GetAIDefenseCards(defender, attackWeight);

                if (defender != Side.Player)
                {
                    defCards.RemoveAll(c => c != null && c.IsThreat);
                    if (defCards.Count == 0)
                    {
                        _enemyLever?.CompleteHold();
                    }
                }

                BCCardDisplay defSpecial = defender == Side.Player ? PlayerSubmittedSpecialCard : GetAISpecialCard(defender);

                if (defender != Side.Player)
                {
                    yield return new WaitForSeconds(Random.Range(0.6f, 1.2f));
                    MoveCardsToCombatSlot(defCards, defenseSlot);
                    if (defSpecial != null && _scene.enemySpecialSlot != null)
                        MoveSpecialToSlot(defSpecial, _scene.enemySpecialSlot);
                }

                yield return new WaitForSeconds(phaseDelay);

                SetPhase(GamePhase.Resolution, "Resolving duel.");
                _handAnimator?.LowerBothHandsAnimated();

                Side? winner = ResolveDuelByWeights(attacker, defender, attCards, defCards, attSpecial, defSpecial);
                Debug.Log($"{LogPrefix} Duel | Attacker:{attacker} Defender:{defender} Winner:{winner?.ToString() ?? "Draw"}");
                yield return new WaitForSeconds(duelResultViewTime);

                DiscardCards(attCards, attSpecial);
                DiscardCards(defCards, defSpecial);
                ClearPlayerSubmissionState();

                if (winner.HasValue)
                {
                    _nextCardPhaseAttacker = DetermineNextCardPhaseAttacker(attacker, defender, winner);
                    _audioManager?.PlayTurnChange();
                    yield return AwardTicketsAfterDuel(attacker, defender, winner.Value, attCards, defCards);
                    if (ShouldStartShooting(winner.Value == attacker, HasThreat(attCards)))
                    {
                        currentAttacker = attacker;
                        MoveGunToTable();
                        RefreshHud($"{winner.Value} won the duel. Click the gun to pick it up, RMB toggles self/opponent, SPACE or click gun again to shoot.");
                        yield return new WaitForSeconds(phaseDelay);
                        SetPhase(GamePhase.Shooting, $"{winner.Value} prepares to shoot.");
                        yield return StartCoroutine(ResolveShotCoroutine(winner.Value));
                    }
                    else
                    {
                        currentAttacker = winner.Value == attacker ? attacker : defender;
                        MoveGunToTable();
                        RefreshHud(winner.Value == defender
                            ? "Defender won. Attack was blocked. Gun stays on table."
                            : "Attacker won by weight without a Threat card. Gun stays on table.");
                    }
                }
                else
                {
                    _nextCardPhaseAttacker = DetermineNextCardPhaseAttacker(attacker, defender, winner: null);
                    _audioManager?.PlayTurnChange();
                    currentAttacker = defender;
                    MoveGunToTable();
                    RefreshHud("Duel tied. Cards discarded.");
                }

                if (_transitionScheduled || _phase == GamePhase.GameOver)
                {
                    yield break;
                }

                MoveGunToTable();
                if (_chamber.Count == 0)
                {
                    yield return new WaitForSeconds(phaseDelay);
                    yield return RunMiniRoundSequence();
                    yield break;
                }

                yield return new WaitForSeconds(phaseDelay * 0.75f);
            }
        }

        /// <summary>Выстрел. Для игрока — ждёт выбора цели (RMB + SPACE/клик). Для AI — автоматически.</summary>
        private IEnumerator ResolveShotCoroutine(Side shooter)
        {
            if (_chamber.Count == 0)
            {
                RefreshHud($"{shooter} tried to shoot, but the chamber was empty.");
                yield break;
            }

            BulletType bullet = _chamber[0];
            Side target;

            if (shooter == Side.Player && gameMode == GameMode.Player)
            {
                MoveGunToTable();
                // Пропускаем один кадр — иначе SPACE от предыдущего хода подхватывается сразу
                yield return null;
                _waitingForPlayerGunPickup = true;
                RefreshHud("Click the gun to pick it up. SPACE also picks it up.");
                while (_waitingForPlayerGunPickup) yield return null;

                MoveGunToHolder(shooter);
                _audioManager?.PlayGunPickup();
                // Ещё один кадр перед ожиданием выстрела
                yield return null;
                _waitingForPlayerShot = true;
                _playerShootAtSelf = false;
                RefreshHud("Gun picked up. RMB = toggle target (self/opponent). SPACE or click gun to shoot.");
                while (_waitingForPlayerShot) yield return null;
                target = _playerShootAtSelf ? Side.Player : Side.Enemy;
            }
            else
            {
                // AI: показываем пистолет в руке перед выстрелом
                MoveGunToHolder(shooter);
                _audioManager?.PlayGunPickup();
                yield return new WaitForSeconds(1.05f);
                target = ChooseTarget(shooter, bullet);
            }

            _chamber.RemoveAt(0);
            RenderChamberVisuals();
            int baseDamage = GetDamage(bullet);
            int multiplier = baseDamage > 0 ? _ticketManager.ConsumeDamageMultiplier(shooter) : 1;
            int totalDamage = baseDamage * multiplier;

            // Camera shake on shot
            _cameraController?.ShakeOnShot();
            _audioManager?.PlayGunShot(bullet);

            if (totalDamage > 0)
            {
                ApplyDamage(target, totalDamage);
                if (target == Side.Enemy)
                {
                    BCCameraGrainFeature.TriggerDamagePulse(0.45f);
                }
            }

            bool keepTurn = bullet == BulletType.Blank && target == shooter;
            _nextCardPhaseAttacker = keepTurn ? shooter : Opponent(shooter);
            if (!keepTurn) _audioManager?.PlayTurnChange();

            // O2:
            // - damage into opponent: +30
            // - all other outcomes: +10
            if (totalDamage > 0 && target != shooter)
                AddOxygen(30f);
            else
                AddOxygen(10f);

            if (totalDamage > 0 && !_transitionScheduled)
            {
                _enemyDialogue?.Speak("post_shot_reflection");
            }

            string damageText = totalDamage > 0 ? totalDamage.ToString() : "0";
            Debug.Log($"{LogPrefix} Shot | {shooter} -> {target} | Bullet:{bullet} Damage:{totalDamage} | keepTurn:{keepTurn}");
            RefreshHud($"{shooter} fired {bullet} at {target} for {damageText} damage." + (keepTurn ? " Blank on self — keep turn!" : ""));

            if (keepTurn)
            {
                MoveGunToHolder(shooter);
                SetPhase(GamePhase.Shooting, $"{shooter} keeps the gun (blank on self).");
                yield return StartCoroutine(ResolveShotCoroutine(shooter));
            }
            else
            {
                yield return new WaitForSeconds(0.2f);
                _audioManager?.PlayGunReturn();
                MoveGunToTable();
            }
        }

        private void ApplyDamage(Side target, int amount)
        {
            if (target == Side.Player)
            {
                _playerHp -= amount;
                _audioManager?.PlayHPDamage();
                _cameraController?.ShakeOnDamage();
                RefreshDynamicCameraState();

                if (_playerHp > 0)
                {
                    return;
                }

                if (!_lastBreathUsed)
                {
                    _lastBreathUsed = true;
                    _playerHp = 1;
                    _currentOxygen = GameConstants.LAST_BREATH_DURATION;
                    _audioManager?.PlayO2LastBreath();
                    _cameraController?.TriggerLastBreathEffect();
                    _lastEvent = BCLocalization.LocalizeRuntimeText("Player triggered last breath after lethal damage.");
                    RefreshDynamicCameraState();
                    return;
                }

                _cameraController?.EffectOnDeath();
                RefreshDynamicCameraState();
                GameOver(Side.Enemy, "Player was defeated.");
                return;
            }

            _enemyHp -= amount;
            _audioManager?.PlayHPDamage();

            if (_enemyHp > 0)
            {
                return;
            }

            if (_currentRoundIndex >= rounds.Length - 1)
            {
                GameOver(Side.Player, "Enemy defeated in final round.");
                return;
            }

            _transitionScheduled = true;
            Debug.Log($"{LogPrefix} RoundOver | Enemy defeated, advancing to round {_currentRoundIndex + 2}");
            SetPhase(GamePhase.RoundOver, $"Enemy defeated. Advancing to round {_currentRoundIndex + 2}.");
            StartCoroutine(AdvanceToNextRound());
        }

        private IEnumerator AdvanceToNextRound()
        {
            yield return new WaitForSeconds(roundTransitionDelay);
            StartRound(_currentRoundIndex + 1);
        }

        private void GameOver(Side winner, string reason)
        {
            _transitionScheduled = true;
            Debug.Log($"{LogPrefix} GameOver | Winner:{winner} | {reason}");
            SetPhase(GamePhase.GameOver, $"Game over. Winner: {winner}. {reason}");
            _enemyDialogue?.Speak("game_over");
            RefreshDynamicCameraState();
            MoveGunToTable();
            StartCoroutine(RestartGameAfterDelay(3f));
        }

        private IEnumerator RestartGameAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Debug.Log($"{LogPrefix} Restarting game...");
            BeginGame();
        }

        private void SetPhase(GamePhase phase, string eventText)
        {
            _phase = phase;
            OnPhaseChanged?.Invoke(phase);
            _cameraController?.TriggerPhaseEffect(phase);
            if (phase == GamePhase.Attack)
            {
                _enemyDialogue?.Speak("phase_attack");
            }
            else if (phase == GamePhase.Defense)
            {
                _enemyDialogue?.Speak("phase_defense");
            }
            Debug.Log($"{LogPrefix} Phase -> {phase}");
            RefreshDynamicCameraState();
            RefreshHud(eventText);
        }

        private void RunLoop(IEnumerator routine)
        {
            if (_loopRoutine != null)
            {
                StopCoroutine(_loopRoutine);
            }

            _loopRoutine = StartCoroutine(routine);
        }

        private void GenerateChamber()
        {
            _chamber.Clear();

            RoundConfig config = rounds[Mathf.Clamp(_currentRoundIndex, 0, rounds.Length - 1)];
            int count = Random.Range(config.minBullets, config.maxBullets + 1);

            // Гарантируем минимум 1 Blank (синий) и минимум 1 Live (красный) в каждом мини-раунде
            _chamber.Add(BulletType.Blank);
            _chamber.Add(BulletType.Live);

            int guaranteedCount = 2;
            int explosiveIndex = -1;
            if (config.allowExplosive && count > guaranteedCount && Random.value < 0.5f)
            {
                explosiveIndex = Random.Range(guaranteedCount, count);
            }

            // Остальные патроны генерируем случайно (начинаем с i=2, т.к. два уже добавлены)
            for (int i = guaranteedCount; i < count; i++)
            {
                BulletType bullet = i == explosiveIndex
                    ? BulletType.Explosive
                    : (Random.value < 0.45f ? BulletType.Live : BulletType.Blank);

                _chamber.Add(bullet);
            }

            ShuffleChamberInternal();
            Debug.Log($"{LogPrefix} Chamber generated: {DescribeChamber()}");
        }

        private IEnumerator RunBulletPresentationSequence()
        {
            RenderChamberVisuals(hiddenAtSpawn: true);
            MoveGunToTable();

            SetPhase(GamePhase.BulletReveal, $"Mini-round {_currentMiniRound}: bullets revealed.");

            if (bulletRevealClip != null)
            {
                _audioManager?.PlayCustomClip(bulletRevealClip, 0.9f);
            }

            yield return AnimateBulletVisualsIn();
            yield return new WaitForSeconds(bulletRevealHoldDuration);

            if (_scene != null)
            {
                yield return _scene.AnimateRoomBlackout(true, gunBlackoutDuration);
            }

            if (gunDrumSpinClip != null)
            {
                _audioManager?.PlayCustomClip(gunDrumSpinClip, 1f);
            }

            yield return new WaitForSeconds(gunBlackoutHoldDuration * 0.45f);
            MoveGunToTable();

            if (gunPlaceClip != null)
            {
                _audioManager?.PlayCustomClip(gunPlaceClip, 1f);
            }
            else
            {
                _audioManager?.PlayGunReturn();
            }

            yield return new WaitForSeconds(gunBlackoutHoldDuration * 0.55f);

            if (_scene != null)
            {
                yield return _scene.AnimateRoomBlackout(false, gunBlackoutDuration);
            }

            yield return new WaitForSeconds(gunSettleDelay);
        }

        private void ShuffleChamberInternal()
        {
            for (int i = _chamber.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_chamber[i], _chamber[j]) = (_chamber[j], _chamber[i]);
            }
        }

        private IEnumerator AnimateBulletVisualsIn()
        {
            if (_bulletVisuals.Count == 0)
            {
                yield return new WaitForSeconds(phaseDelay * 0.5f);
                yield break;
            }

            float elapsed = 0f;
            float totalDuration = Mathf.Max(
                bulletRevealExpandDuration,
                bulletRevealExpandDuration + ((_bulletVisuals.Count - 1) * Mathf.Max(0f, bulletRevealItemStagger)));

            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;

                for (int i = 0; i < _bulletVisuals.Count; i++)
                {
                    GameObject bullet = _bulletVisuals[i];
                    if (bullet == null)
                    {
                        continue;
                    }

                    float localElapsed = Mathf.Clamp01((elapsed - (i * bulletRevealItemStagger)) / Mathf.Max(0.01f, bulletRevealExpandDuration));
                    float eased = Mathf.SmoothStep(0f, 1f, localElapsed);
                    bullet.transform.localScale = Vector3.LerpUnclamped(Vector3.zero, new Vector3(0.08f, 0.18f, 0.08f), eased);
                }

                yield return null;
            }

            for (int i = 0; i < _bulletVisuals.Count; i++)
            {
                GameObject bullet = _bulletVisuals[i];
                if (bullet != null)
                {
                    bullet.transform.localScale = new Vector3(0.08f, 0.18f, 0.08f);
                }
            }
        }

        private void RenderChamberVisuals(bool hiddenAtSpawn = false)
        {
            ClearBulletVisuals();
            if (_scene == null || _scene.bulletSpot == null)
            {
                return;
            }

            for (int i = 0; i < _chamber.Count; i++)
            {
                GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                bullet.name = $"Bullet_{i + 1}_{_chamber[i]}";
                bullet.transform.SetParent(_scene.bulletSpot, false);
                bullet.transform.localScale = hiddenAtSpawn ? Vector3.zero : new Vector3(0.08f, 0.18f, 0.08f);
                bullet.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                bullet.transform.localPosition = new Vector3(i * 0.18f, 0f, 0f);

                Renderer renderer = bullet.GetComponent<Renderer>();
                renderer.sharedMaterial = GetSharedBulletMaterial(GetBulletColor(_chamber[i]));

                Collider collider = bullet.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                }

                _bulletVisuals.Add(bullet);
            }
        }

        private void ClearBulletVisuals()
        {
            for (int i = 0; i < _bulletVisuals.Count; i++)
            {
                if (_bulletVisuals[i] != null)
                {
                    Destroy(_bulletVisuals[i]);
                }
            }

            _bulletVisuals.Clear();
        }

        private void MoveGunToHolder(Side side)
        {
            if (_scene == null || _scene.gunRoot == null)
            {
                return;
            }

            Transform target = side == Side.Player ? _scene.playerWeaponHolder : _scene.enemyWeaponHolder;
            if (target == null)
            {
                return;
            }

            DockGun(target);
        }

        private void RestoreGunToSceneStartPose()
        {
            if (_scene == null || _scene.gunRoot == null)
            {
                return;
            }

            Transform gun = _scene.gunRoot;
            gun.SetParent(_gunStartParent, false);
            gun.localPosition = _gunStartLocalPosition;
            gun.localRotation = _gunStartLocalRotation;
            gun.localScale = _gunStartLocalScale;
        }

        private void MoveGunToTable()
        {
            if (_scene == null || _scene.gunRoot == null)
            {
                return;
            }

            Transform gunDock = _dealMechanism != null && _dealMechanism.GunRestAnchor != null
                ? _dealMechanism.GunRestAnchor
                : _scene.gunSpot;

            if (gunDock == null)
            {
                return;
            }

            DockGun(gunDock);
        }

        private void DockGun(Transform target)
        {
            if (_scene == null || _scene.gunRoot == null || target == null)
            {
                return;
            }

            Transform gun = _scene.gunRoot;
            gun.SetParent(target, false);
            gun.localPosition = Vector3.zero;
            gun.localRotation = Quaternion.identity;
            ApplyAuthoredWorldScale(gun, _gunAuthoredWorldScale);
        }

        private static void ApplyAuthoredWorldScale(Transform subject, Vector3 worldScale)
        {
            if (subject == null)
            {
                return;
            }

            Transform parent = subject.parent;
            if (parent == null)
            {
                subject.localScale = worldScale;
                return;
            }

            Vector3 parentScale = parent.lossyScale;
            subject.localScale = new Vector3(
                SafeDivide(worldScale.x, parentScale.x),
                SafeDivide(worldScale.y, parentScale.y),
                SafeDivide(worldScale.z, parentScale.z));
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
        }

        private Side? ResolveDuelByWeights(Side attacker, Side defender, IReadOnlyList<BreathCasino.Gameplay.BCCardDisplay> attCards, IReadOnlyList<BreathCasino.Gameplay.BCCardDisplay> defCards, BCCardDisplay attSpecial = null, BCCardDisplay defSpecial = null)
        {
            if (_cardManager == null)
            {
                return Random.value < 0.5f ? attacker : defender;
            }

            bool attSpecialCanceled = false;
            bool defSpecialCanceled = false;

            bool attHasBlock = attSpecial != null && attSpecial.SpecialEffect == SpecialEffectType.Block;
            bool defHasBlock = defSpecial != null && defSpecial.SpecialEffect == SpecialEffectType.Block;
            if (attHasBlock || defHasBlock)
            {
                attSpecialCanceled = true;
                defSpecialCanceled = true;
                Debug.Log($"{LogPrefix} Block active - both specials negated");
            }

            if (!attSpecialCanceled && !defSpecialCanceled)
            {
                // Cancel: отменяет спецкарту противника
            if (attSpecial != null && attSpecial.SpecialEffect == SpecialEffectType.Cancel)
            {
                defSpecialCanceled = true;
                _audioManager?.PlaySpecialCancel();
                Debug.Log($"{LogPrefix} Attacker used Cancel - defender's special is negated");
            }
            if (defSpecial != null && defSpecial.SpecialEffect == SpecialEffectType.Cancel)
            {
                attSpecialCanceled = true;
                _audioManager?.PlaySpecialCancel();
                Debug.Log($"{LogPrefix} Defender used Cancel - attacker's special is negated");
            }
            }

            // Exchange: меняем основные карты местами (до подсчета весов)
            IReadOnlyList<BreathCasino.Gameplay.BCCardDisplay> originalAttCards = attCards;
            IReadOnlyList<BreathCasino.Gameplay.BCCardDisplay> originalDefCards = defCards;
            
            if (attSpecial != null && !attSpecialCanceled && attSpecial.SpecialEffect == SpecialEffectType.Exchange)
            {
                (attCards, defCards) = (defCards, attCards);
                _audioManager?.PlaySpecialExchange();
                Debug.Log($"{LogPrefix} Attacker used Exchange - cards swapped!");
            }
            else if (defSpecial != null && !defSpecialCanceled && defSpecial.SpecialEffect == SpecialEffectType.Exchange)
            {
                (attCards, defCards) = (defCards, attCards);
                _audioManager?.PlaySpecialExchange();
                Debug.Log($"{LogPrefix} Defender used Exchange - cards swapped!");
            }

            int attW = GetCardsWeight(attCards);
            int defW = GetCardsWeight(defCards);

            // Duplicate: удваивает вес основной карты
            if (attSpecial != null && !attSpecialCanceled && attSpecial.SpecialEffect == SpecialEffectType.Duplicate)
            {
                attW *= 2;
                _audioManager?.PlaySpecialDuplicate();
                Debug.Log($"{LogPrefix} Attacker used Duplicate - weight doubled to {attW}");
            }
            if (defSpecial != null && !defSpecialCanceled && defSpecial.SpecialEffect == SpecialEffectType.Duplicate)
            {
                defW *= 2;
                _audioManager?.PlaySpecialDuplicate();
                Debug.Log($"{LogPrefix} Defender used Duplicate - weight doubled to {defW}");
            }

            bool attackHasThreat = HasThreat(attCards);
            bool defenseHasThreat = HasThreat(defCards);

            Debug.Log($"{LogPrefix} Duel weights | Att:{attW} (Threat:{attackHasThreat}) Def:{defW} (Threat:{defenseHasThreat}) | Specials: {attSpecial?.CardName ?? "-"} / {defSpecial?.CardName ?? "-"}");

            // Определяем победителя
            Side? winner = null;

            if (attackHasThreat)
            {
                if (defenseHasThreat)
                {
                    winner = attacker;
                }
                else if (defW > attW)
                {
                    winner = defender;
                }
                else if (attW > defW)
                {
                    winner = attacker;
                }
            }
            else
            {
                if (attW > defW)
                {
                    winner = attacker;
                }
                else if (defW > attW)
                {
                    winner = defender;
                }
            }

            if (winner.HasValue)
            {
                BCCardDisplay winnerSpecial = winner == attacker ? attSpecial : defSpecial;
                Side loser = winner == attacker ? defender : attacker;
                bool winnerSpecialEffective = winnerSpecial != null && !((winner == attacker && attSpecialCanceled) || (winner == defender && defSpecialCanceled));

                if (winnerSpecialEffective)
                {
                    if (winnerSpecial.SpecialEffect == SpecialEffectType.Prohibit)
                    {
                        _prohibitSpecialsMinRoundsRemaining = 1;
                        Debug.Log($"{LogPrefix} {winner} used Prohibit - no special cards next mini-round");
                        RefreshHud($"{winner} used Prohibit - no special cards next mini-round!");
                    }
                    else if (winnerSpecial.SpecialEffect == SpecialEffectType.Steal)
                    {
                        var loserPlayed = new List<BreathCasino.Gameplay.BCCardDisplay>();
                        // Используем originalAttCards/originalDefCards — attCards/defCards могли быть свапнуты Exchange
                        if (loser == attacker) { loserPlayed.AddRange(originalAttCards); if (attSpecial != null) loserPlayed.Add(attSpecial); }
                        else { loserPlayed.AddRange(originalDefCards); if (defSpecial != null) loserPlayed.Add(defSpecial); }
                        BCCardDisplay stolen = _cardManager.StealRandomCard(loser, winner.Value, loserPlayed);
                        if (stolen != null)
                        {
                            _audioManager?.PlaySpecialSteal();
                            Debug.Log($"{LogPrefix} {winner} used Steal - took {stolen.CardName} from {loser}");
                            RefreshHud($"{winner} stole a card from {loser}!");
                        }
                        else
                        {
                            Debug.Log($"{LogPrefix} {winner} used Steal - but {loser} had no cards");
                        }
                    }
                }
            }

            return winner;
        }

        private BCCardDisplay GetAISpecialCard(Side side)
        {
            if (side != Side.Enemy || _enemyAI == null || _cardManager == null || IsSpecialsProhibited)
                return null;
            var list = _cardManager.EnemySpecialCards;
            if (list == null || list.Count == 0) return null;
            return _enemyAI.ChooseSpecialCard(list);
        }

        private void MoveSpecialToSlot(BCCardDisplay special, Transform slotRoot)
        {
            if (special == null || slotRoot == null) return;
            special.SetCardHidden(false);
            var slot = slotRoot.GetComponent<BCCardSlot>();
            special.MoveToSlot(slot != null ? slot.transform : slotRoot, Vector3.zero, Quaternion.identity, 0.22f);
        }

        private List<BreathCasino.Gameplay.BCCardDisplay> GetAIAttackCards(Side side)
        {
            if (side != Side.Enemy || _enemyAI == null || _cardManager == null)
                return GetRandomMainCards(side);

            var list = _cardManager.EnemyMainCards;
            if (list == null || list.Count == 0)
            {
                Debug.Log("[GameManager] AI attack: EnemyMainCards empty — skip.");
                return new List<BreathCasino.Gameplay.BCCardDisplay>();
            }

            return _enemyAI.ChooseAttackCards(list);
        }

        private List<BreathCasino.Gameplay.BCCardDisplay> GetAIDefenseCards(Side side, int attackWeight)
        {
            if (side != Side.Enemy || _enemyAI == null || _cardManager == null)
            {
                // Нет AI — случайная карта, пусть идёт как есть (не убиваем результат страховкой)
                return GetRandomMainCards(side);
            }

            var list = _cardManager.EnemyMainCards;
            if (list == null || list.Count == 0) return new List<BreathCasino.Gameplay.BCCardDisplay>();

            // EnemyAI.ChooseDefenseCards сам возвращает пустой список если покрытия нет —
            // дополнительная "страховка" не нужна и ломает корректно найденные комбинации.
            return _enemyAI.ChooseDefenseCards(list, attackWeight);
        }

        private List<BreathCasino.Gameplay.BCCardDisplay> GetRandomMainCards(Side side)
        {
            List<BreathCasino.Gameplay.BCCardDisplay> result = new();
            if (_cardManager == null) return result;

            var list = side == Side.Player ? _cardManager.PlayerMainCards : _cardManager.EnemyMainCards;
            if (list == null || list.Count == 0) return result;

            BCCardDisplay card = list[Random.Range(0, list.Count)];
            if (card != null)
            {
                result.Add(card);
            }

            return result;
        }

        private void MoveCardsToCombatSlot(IReadOnlyList<BreathCasino.Gameplay.BCCardDisplay> cards, Transform slotTransform)
        {
            if (cards == null || slotTransform == null) return;

            BCCardSlot slot = slotTransform.GetComponent<BCCardSlot>();
            Transform anchor = slot != null ? slot.CardAnchor : slotTransform;
            ClearSlotChildren(slotTransform);

            int baseIndex = 0;

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null) continue;

                cards[i].SetCardHidden(false);

                if (slot != null)
                {
                    int total = baseIndex + cards.Count;
                    cards[i].MoveToSlot(anchor, slot.GetCardLocalPosition(baseIndex + i, total), slot.GetCardLocalRotation(baseIndex + i, total), 0.22f);
                }
                else
                {
                    cards[i].PlaceInSlot(anchor);
                }
            }
        }

        private static int GetCardsWeight(IReadOnlyList<BreathCasino.Gameplay.BCCardDisplay> cards)
        {
            if (cards == null || cards.Count == 0) return 0;

            int total = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                    total += cards[i].Weight;
            }

            return total;
        }

        private static bool HasThreat(IReadOnlyList<BreathCasino.Gameplay.BCCardDisplay> cards)
        {
            if (cards == null) return false;

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null && cards[i].IsThreat)
                    return true;
            }

            return false;
        }

        private bool IsInvalidPlayerDefense(IReadOnlyList<BreathCasino.Gameplay.BCCardDisplay> stagedMainCards)
        {
            if (_phase != GamePhase.Defense || _currentDefender != Side.Player)
                return false;

            int enemyAttackWeight = GetEnemyAttackWeight();
            if (enemyAttackWeight <= 0)
                return false;

            return GetCardsWeight(stagedMainCards) < enemyAttackWeight;
        }

        private void ReturnPlayerStagedCardsToHand()
        {
            for (int i = 0; i < _playerSlotCards.Length; i++)
            {
                BCCardDisplay card = GetTrackedPlayerSlotCard(i);
                if (card == null)
                    continue;

                Transform handHolder = card.CardType == CardType.Special ? _scene?.playerSpecialHolder : _scene?.playerHandHolder;
                if (handHolder != null)
                {
                    card.transform.SetParent(handHolder, false);
                }

                _playerSlotCards[i] = null;
            }

            PlayerSelectionTracker.ClearSelection();
            _cardManager?.RelayoutHands();
        }

        private BCCardSlot GetFirstFreePlayerSlot()
        {
            if (_scene == null)
            {
                return null;
            }

            if (GetTrackedPlayerSlotCard(0) == null && _scene.playerMainSlot != null)
            {
                return _scene.playerMainSlot.GetComponent<BCCardSlot>();
            }

            if (GetTrackedPlayerSlotCard(1) == null && _scene.playerSpecialSlot != null)
            {
                return _scene.playerSpecialSlot.GetComponent<BCCardSlot>();
            }

            return null;
        }

        private IEnumerator EnsurePendingCardPickups()
        {
            if (_cardManager == null && _ticketManager == null)
            {
                yield break;
            }

            bool playerPending = HasPendingTablePickup(Side.Player);
            bool enemyPending = HasPendingTablePickup(Side.Enemy);

            if (!playerPending && !enemyPending)
            {
                if (!IsAnyTicketAccessOpen())
                {
                    _dealMechanism?.Snap(false);
                }
                yield break;
            }

            if (_dealMechanism != null)
            {
                yield return StartCoroutine(_dealMechanism.Animate(true));
                yield return new WaitForSeconds(0.08f);
            }

            if (enemyPending)
            {
                StartCoroutine(AutoTakeEnemyPendingCards());
                StartCoroutine(AutoTakeEnemyPendingTickets());
            }

            if (playerPending)
            {
                _waitingForPlayerCardPickup = true;
                RefreshHud("Click your issued cards or ticket on the mechanism. SPACE also works.");

                while (_waitingForPlayerCardPickup && HasPendingTablePickup(Side.Player))
                {
                    yield return null;
                }
            }

            while (HasPendingTablePickup(Side.Enemy))
            {
                yield return null;
            }

            if (_dealMechanism != null && !IsAnyTicketAccessOpen())
            {
                yield return StartCoroutine(_dealMechanism.Animate(false));
            }
        }

        private IEnumerator AutoTakeEnemyPendingCards()
        {
            yield return new WaitForSeconds(Random.Range(0.45f, 0.9f));

            if (_cardManager == null)
            {
                yield break;
            }

            int moved = _cardManager.TakePendingCards(Side.Enemy);
            if (moved > 0)
            {
                Debug.Log($"{LogPrefix} Enemy took {moved} cards from the round stack.");
            }
        }

        private IEnumerator AutoTakeEnemyPendingTickets()
        {
            yield return new WaitForSeconds(Random.Range(0.55f, 1.0f));

            if (_ticketManager == null)
            {
                yield break;
            }

            while (_ticketManager.AutoTakePendingTicket(Side.Enemy))
            {
                yield return new WaitForSeconds(Random.Range(0.15f, 0.35f));
            }
        }

        private bool HasPendingTablePickup(Side side)
        {
            bool pendingCards = _cardManager != null && _cardManager.HasPendingPickup(side);
            bool pendingTickets = _ticketManager != null && _ticketManager.HasPendingPickup(side);
            return pendingCards || pendingTickets;
        }

        private bool IsAnyTicketAccessOpen()
        {
            return _playerTicketAccessOpen || _enemyTicketAccessOpen;
        }

        private IEnumerator TogglePlayerTicketAccessCoroutine()
        {
            if (_playerTicketAccessOpen)
            {
                yield return CloseTicketAccessForSide(Side.Player);
                yield break;
            }

            yield return OpenTicketAccessForSide(Side.Player, true);
        }

        private IEnumerator OpenTicketAccessForSide(Side side, bool playerInitiated)
        {
            if (!AreTicketsEnabledForCurrentRound || _ticketManager == null || !_ticketManager.HasUsableTicket(side))
            {
                if (side == Side.Player)
                {
                    RefreshHud(AreTicketsEnabledForCurrentRound
                        ? "No usable tickets are available on your side."
                        : "Tickets only unlock from round 2.");
                }
                yield break;
            }

            if (side == Side.Player)
            {
                _playerTicketAccessOpen = true;
                _playerLever?.PlayPull();
                RefreshHud("Ticket shelf opened. Click a ticket, then pull the lever again to retract.");
                _enemyDialogue?.Speak("player_open_ticket");
            }
            else
            {
                _enemyTicketAccessOpen = true;
                _enemyLever?.PlayPull();
                _enemyDialogue?.Speak("enemy_use_ticket");
            }

            if (_dealMechanism != null && !_dealMechanism.IsRaised)
            {
                yield return StartCoroutine(_dealMechanism.Animate(true));
            }

            if (side == Side.Enemy)
            {
                yield return new WaitForSeconds(Random.Range(0.45f, 0.9f));
                yield return StartCoroutine(_ticketManager.UseStoredTicketFromAccessCoroutine(Side.Enemy));
                RefreshHud("Enemy used a ticket from the shelf.");

                yield return new WaitForSeconds(0.35f);
                yield return CloseTicketAccessForSide(Side.Enemy);
            }
            else if (playerInitiated && _ticketManager.HasUsableTicket(Side.Enemy))
            {
                StartCoroutine(TryEnemyTicketResponseToPlayer());
            }
        }

        private IEnumerator CloseTicketAccessForSide(Side side)
        {
            if (side == Side.Player)
            {
                if (_ticketManager != null && _ticketManager.HasTicketInHand)
                {
                    _ticketManager.ReturnTicketToStack();
                }

                _playerTicketAccessOpen = false;
                _playerLever?.PlayPull();
                RefreshHud("Ticket shelf retracted.");
            }
            else
            {
                _enemyTicketAccessOpen = false;
                _enemyLever?.PlayPull();
            }

            yield return new WaitForSeconds(0.08f);

            if (_dealMechanism != null &&
                _dealMechanism.IsRaised &&
                !HasPendingTablePickup(Side.Player) &&
                !HasPendingTablePickup(Side.Enemy) &&
                !IsAnyTicketAccessOpen())
            {
                yield return StartCoroutine(_dealMechanism.Animate(false));
            }
        }

        private IEnumerator TryEnemyTicketResponseToPlayer()
        {
            if (_enemyTicketAccessOpen || _ticketManager == null || !_ticketManager.HasUsableTicket(Side.Enemy))
            {
                yield break;
            }

            float chance = 0.35f;
            if (_enemyHp <= 1)
            {
                chance += 0.20f;
            }

            if (_phase == GamePhase.Defense)
            {
                chance += 0.10f;
            }

            if (Random.value > chance)
            {
                yield break;
            }

            yield return new WaitForSeconds(Random.Range(0.45f, 1.0f));
            yield return OpenTicketAccessForSide(Side.Enemy, false);
        }

        private IEnumerator ToggleEnemyTicketAccessForDebug()
        {
            if (_enemyTicketAccessOpen)
            {
                yield return CloseTicketAccessForSide(Side.Enemy);
                yield break;
            }

            yield return OpenTicketAccessForSide(Side.Enemy, false);
        }

        private IEnumerator TryEnemyTicketUseOpportunity(string dialogueKey, float chance)
        {
            if (!AreTicketsEnabledForCurrentRound ||
                _ticketManager == null ||
                !_ticketManager.HasUsableTicket(Side.Enemy) ||
                _enemyTicketAccessOpen ||
                Random.value > chance)
            {
                yield break;
            }

            _enemyDialogue?.Speak(dialogueKey);
            yield return OpenTicketAccessForSide(Side.Enemy, false);
        }

        private IEnumerator AwardTicketsAfterDuel(
            Side attacker,
            Side defender,
            Side winner,
            IReadOnlyList<BCCardDisplay> attackCards,
            IReadOnlyList<BCCardDisplay> defenseCards)
        {
            if (!AreTicketsEnabledForCurrentRound || _ticketManager == null)
            {
                yield break;
            }

            bool normalAttack = !HasThreat(attackCards);
            bool defenderWon = winner == defender;
            int attackWeight = GetCardsWeight(attackCards);
            int defenseWeight = GetCardsWeight(defenseCards);
            bool overweightDefense = defenderWon && normalAttack && defenseWeight > attackWeight;

            if (!overweightDefense)
            {
                yield break;
            }

            _ticketManager.GiveRandomTicket(defender, false);
            RefreshHud($"{defender} earned a ticket by defending with an overweight response.");
            yield return EnsurePendingCardPickups();
        }

        private int GetEnemyAttackWeight()
        {
            if (_scene?.enemyMainSlot == null)
                return 0;

            BCCardSlot enemySlot = _scene.enemyMainSlot.GetComponent<BCCardSlot>();
            if (enemySlot == null || !enemySlot.HasCard || enemySlot.CurrentCard == null)
                return 0;

            BCCardDisplay enemyCard = enemySlot.CurrentCard;
            return enemyCard.IsThreat ? enemyCard.Weight : 0;
        }

        private void DiscardCards(IReadOnlyList<BreathCasino.Gameplay.BCCardDisplay> cards, BCCardDisplay specialCard)
        {
            if (cards != null)
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    _cardManager?.DiscardCard(cards[i]);
                }
            }

            if (specialCard != null)
            {
                _cardManager?.DiscardCard(specialCard);
            }
        }

        /// <summary>Очищает слоты от сыгранных карт перед новым мини-раундом (карты уже были видимы всю дуэльную серию).</summary>
        private void ClearCombatSlots()
        {
            if (_scene == null) return;
            ClearSlotChildren(_scene.playerMainSlot);
            ClearSlotChildren(_scene.enemyMainSlot);
            ClearSlotChildren(_scene.playerSpecialSlot);
            ClearSlotChildren(_scene.enemySpecialSlot);
            _playerSlotCards[0] = null;
            _playerSlotCards[1] = null;
        }

        private static void ClearSlotChildren(Transform slotRoot)
        {
            if (slotRoot == null) return;
            var slot = slotRoot.GetComponent<BCCardSlot>();
            Transform anchor = slot != null ? slot.CardAnchor : slotRoot;
            if (anchor == null) return;
            int n = anchor.childCount;
            for (int i = n - 1; i >= 0; i--)
            {
                Transform child = anchor.GetChild(i);
                if (child != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private static Side DetermineNextCardPhaseAttacker(Side attacker, Side defender, Side? winner)
        {
            if (!winner.HasValue)
            {
                return defender;
            }

            return winner.Value == attacker ? attacker : defender;
        }

        private static bool ShouldStartShooting(bool attackerWon, bool attackerHasThreat)
        {
            return attackerWon && attackerHasThreat;
        }

        private static Side Opponent(Side side)
        {
            return side == Side.Player ? Side.Enemy : Side.Player;
        }

        private Side ChooseTarget(Side shooter, BulletType bullet)
        {
            if (_enemyAI != null)
                return _enemyAI.ChooseShotTarget(_chamber);

            // Fallback без AI: blank → случайно, live → в противника
            if (bullet == BulletType.Blank)
                return Random.value < 0.55f ? shooter : Opponent(shooter);

            return Opponent(shooter);
        }

        private static int GetDamage(BulletType bullet)
        {
            return bullet switch
            {
                BulletType.Blank => 0,
                BulletType.Live => 1,
                BulletType.Explosive => 2,
                _ => 0
            };
        }

        private static Color GetBulletColor(BulletType bullet)
        {
            return bullet switch
            {
                BulletType.Blank => new Color(0.25f, 0.55f, 1f),
                BulletType.Live => new Color(0.9f, 0.2f, 0.2f),
                BulletType.Explosive => new Color(1f, 0.55f, 0.1f),
                _ => Color.white
            };
        }

        private string DescribeChamber()
        {
            if (_chamber.Count == 0)
            {
                return "Empty";
            }

            StringBuilder builder = new();
            for (int i = 0; i < _chamber.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" | ");
                }

                builder.Append(_chamber[i]);
            }

            return builder.ToString();
        }

        private static Material GetSharedBulletMaterial(Color color)
        {
            int key = color.GetHashCode();
            if (BulletMaterialCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new(shader);
            material.color = color;
            BulletMaterialCache[key] = material;
            return material;
        }

    }
}
