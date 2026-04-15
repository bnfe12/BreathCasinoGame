using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BreathCasino.Core;
using BreathCasino.Systems;
using UnityEngine;

namespace BreathCasino.Gameplay
{
    public class TicketManager : MonoBehaviour
    {
        private static readonly TicketType[] ActiveTicketTypes =
        {
            TicketType.Inspection,
            TicketType.MedicalRation
        };

        private static readonly HashSet<TicketType> ActiveTicketTypeSet = new(ActiveTicketTypes);

        [SerializeField] private int maxTicketsPerSide = 5;
        [SerializeField] private GameObject ticketPrefab;
        [SerializeField] private BCTicketStack playerTicketStack;
        [SerializeField] private BCTicketStack enemyTicketStack;
        [SerializeField] private Transform playerHandHolder;

        private readonly List<TicketType> _playerTickets = new();
        private readonly List<TicketType> _enemyTickets = new();
        private readonly Dictionary<Side, int> _damageMultipliers = new();
        private readonly Dictionary<Side, int> _pendingPhysicalTickets = new();
        private bool _ticketUseInProgress;

        private GameManager _gameManager;
        private BCCardManager _cardManager;
        private BCAudioManager _audioManager;

        public event Action OnTicketsChanged;
        public bool HasTicketInHand => playerTicketStack != null && playerTicketStack.HasTicketInHand;
        public bool IsTicketUseInProgress => _ticketUseInProgress;

        public void Initialize(
            GameManager gameManager,
            BCTicketStack playerStack,
            BCTicketStack enemyStack,
            Transform handHolder,
            BCCardManager cardManager)
        {
            _gameManager = gameManager;
            playerTicketStack = playerStack;
            enemyTicketStack = enemyStack;
            playerHandHolder = handHolder;
            _cardManager = cardManager;
            _audioManager = BCAudioManager.Instance;
            ResetMultipliers();
            ResetPendingTickets();
            CreateTicketPrefab();
        }

        public void Initialize(GameManager gameManager)
        {
            _gameManager = gameManager;
            _audioManager = BCAudioManager.Instance;
            ResetMultipliers();
            ResetPendingTickets();
        }

        public void ResetAll()
        {
            _playerTickets.Clear();
            _enemyTickets.Clear();
            _ticketUseInProgress = false;
            ResetMultipliers();
            ResetPendingTickets();
            playerTicketStack?.Clear();
            enemyTicketStack?.Clear();
            OnTicketsChanged?.Invoke();
        }

        public void GiveRandomTicket(Side side, bool updateHud = true)
        {
            GiveTicket(side, GetRandomTicket(), updateHud);
        }

        public void GiveTicket(Side side, TicketType ticket, bool updateHud = true)
        {
            if (!IsActiveTicketType(ticket))
            {
                if (updateHud && _gameManager != null)
                {
                    _gameManager.RefreshHud($"{side} tried to receive archived ticket {ticket}. Ignored.");
                }

                Debug.LogWarning($"[TicketManager] Ignored archived ticket grant: {ticket}");
                return;
            }

            bool issuedPhysically = SpawnPhysicalTicket(side, ticket);
            if (issuedPhysically)
            {
                _pendingPhysicalTickets[side] = GetPendingPickupCount(side) + 1;
            }
            _audioManager?.PlayTicketEmerge();

            if (updateHud && _gameManager != null)
            {
                _gameManager.RefreshHud($"{side} received ticket issue: {ticket}");
            }
        }

        public bool UseFirstTicket(Side side)
        {
            List<TicketType> tickets = GetTickets(side);
            if (tickets.Count == 0)
            {
                return false;
            }

            TicketType ticket = tickets[0];
            if (!IsActiveTicketType(ticket))
            {
                tickets.RemoveAt(0);
                OnTicketsChanged?.Invoke();
                _gameManager?.RefreshHud($"{side} discarded archived ticket {ticket}.");
                Debug.LogWarning($"[TicketManager] Discarded archived ticket from hand: {ticket}");
                return false;
            }

            tickets.RemoveAt(0);
            Apply(side, ticket);
            OnTicketsChanged?.Invoke();
            return true;
        }

        public int ConsumeDamageMultiplier(Side side)
        {
            int multiplier = GetDamageMultiplier(side);
            _damageMultipliers[side] = 1;
            return multiplier;
        }

        public int GetDamageMultiplier(Side side)
        {
            return _damageMultipliers.TryGetValue(side, out int value) ? value : 1;
        }

        public string DescribeTickets(Side side)
        {
            List<TicketType> tickets = GetTickets(side);
            if (tickets.Count == 0)
            {
                return "0";
            }

            StringBuilder builder = new();
            for (int i = 0; i < tickets.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(tickets[i]);
            }

            return builder.ToString();
        }

        public bool HasPendingPickup(Side side)
        {
            return GetPendingPickupCount(side) > 0;
        }

        public bool HasUsableTicket(Side side)
        {
            return GetTickets(side).Count > 0 || HasPendingPickup(side);
        }

        public bool TakeTicketToHand()
        {
            if (playerTicketStack == null || playerHandHolder == null || playerTicketStack.HasTicketInHand)
            {
                return false;
            }

            BCTicketDisplay ticket = playerTicketStack.TakeTopTicket(playerHandHolder);
            if (ticket != null)
            {
                ClaimTicketToInventory(Side.Player, ticket);
                HideCards(true);
                Debug.Log("[TicketManager] Ticket taken to hand, cards hidden");
                return true;
            }

            return false;
        }

        public bool TakePendingTicketToHand()
        {
            if (!HasPendingPickup(Side.Player))
            {
                return false;
            }

            return TakeTicketToHand();
        }

        public bool AutoTakePendingTicket(Side side)
        {
            if (side == Side.Player || !HasPendingPickup(side))
            {
                return false;
            }

            BCTicketStack stack = GetTicketStack(side);
            if (stack == null)
            {
                return false;
            }

            BCTicketDisplay ticket = stack.PopTopTicket();
            if (ticket == null)
            {
                return false;
            }

            ClaimTicketToInventory(side, ticket);
            stack.AddTicket(ticket);
            Debug.Log($"[TicketManager] {side} took issued ticket from the mechanism.");
            return true;
        }

        public void ReturnTicketToStack()
        {
            if (playerTicketStack == null || !playerTicketStack.HasTicketInHand)
            {
                return;
            }

            playerTicketStack.ReturnTicketToTop();
            HideCards(false);
            Debug.Log("[TicketManager] Ticket returned to stack, cards shown");
        }

        public void UseTicketInHand()
        {
            if (_ticketUseInProgress || playerTicketStack == null || !playerTicketStack.HasTicketInHand)
            {
                return;
            }

            StartCoroutine(UseTicketInHandRoutine());
        }

        public IEnumerator UseStoredTicketFromAccessCoroutine(Side side)
        {
            if (_ticketUseInProgress)
            {
                yield break;
            }

            List<TicketType> tickets = GetTickets(side);
            if (tickets.Count == 0)
            {
                yield break;
            }

            _ticketUseInProgress = true;
            TicketType ticketType = tickets[0];
            tickets.RemoveAt(0);

            BCTicketDisplay visual = null;
            BCTicketStack stack = GetTicketStack(side);
            if (stack != null)
            {
                visual = stack.PopTopTicket();
            }

            if (visual != null)
            {
                yield return PlayTicketInsertionIfPossible(visual, side);
                Destroy(visual.gameObject);
            }

            Apply(side, ticketType);
            OnTicketsChanged?.Invoke();
            _ticketUseInProgress = false;
        }

        public bool SpawnPhysicalTicket(Side side, TicketType type)
        {
            if (!IsActiveTicketType(type))
            {
                Debug.LogWarning($"[TicketManager] Ignored archived physical ticket spawn: {type}");
                return false;
            }

            BCTicketStack stack = GetTicketStack(side);
            if (stack == null || ticketPrefab == null)
            {
                AddTicketToInventory(side, type);
                OnTicketsChanged?.Invoke();
                return false;
            }

            GameObject ticketObj = Instantiate(ticketPrefab);
            ticketObj.SetActive(true);
            ticketObj.name = $"Ticket_{side}_{type}";

            BCTicketDisplay display = ticketObj.GetComponent<BCTicketDisplay>();
            if (display == null)
            {
                display = ticketObj.AddComponent<BCTicketDisplay>();
            }

            if (display != null)
            {
                display.Initialize(type, type.ToString());
            }

            if (display == null)
            {
                Destroy(ticketObj);
                AddTicketToInventory(side, type);
                OnTicketsChanged?.Invoke();
                return false;
            }

            stack.AddTicket(display);
            Debug.Log($"[TicketManager] Spawned physical ticket for {side}: {type}");
            return true;
        }

        private IEnumerator UseTicketInHandRoutine()
        {
            BCTicketDisplay ticket = playerTicketStack != null ? playerTicketStack.TicketInHand : null;
            if (ticket == null)
            {
                yield break;
            }

            _ticketUseInProgress = true;

            if (!IsActiveTicketType(ticket.TicketType))
            {
                RemoveTicketFromInventory(Side.Player, ticket.TicketType);
                _gameManager?.RefreshHud($"Archived ticket {ticket.TicketType} was discarded.");
                playerTicketStack.RemoveTicketInHand();
                HideCards(false);
                OnTicketsChanged?.Invoke();
                _ticketUseInProgress = false;
                Debug.LogWarning($"[TicketManager] Discarded archived physical ticket: {ticket.TicketType}");
                yield break;
            }

            ClaimTicketToInventory(Side.Player, ticket);
            RemoveTicketFromInventory(Side.Player, ticket.TicketType);
            yield return PlayTicketInsertionIfPossible(ticket, Side.Player);
            Apply(Side.Player, ticket.TicketType);
            playerTicketStack.RemoveTicketInHand();
            OnTicketsChanged?.Invoke();
            HideCards(false);
            _ticketUseInProgress = false;
            Debug.Log($"[TicketManager] Used ticket: {ticket.TicketType}");
        }

        private void Apply(Side side, TicketType ticket)
        {
            if (_gameManager == null)
            {
                return;
            }

            _audioManager?.PlayTicketUse();

            switch (ticket)
            {
                case TicketType.Inspection:
                    _gameManager.RefreshHud($"{side} used Inspection. Next bullet: {_gameManager.PeekCurrentBullet()}");
                    _gameManager.AddOxygen(10f);
                    break;
                case TicketType.MedicalRation:
                    _gameManager.Heal(side, 1);
                    _gameManager.RefreshHud($"{side} used MedicalRation and healed 1 HP.");
                    _gameManager.AddOxygen(10f);
                    break;
                default:
                    Debug.LogWarning($"[TicketManager] Archived ticket {ticket} reached Apply(). Ignored.");
                    _gameManager.RefreshHud($"{side} tried to use archived ticket {ticket}. Ignored.");
                    break;
            }
        }

        private static TicketType GetRandomTicket()
        {
            return ActiveTicketTypes[UnityEngine.Random.Range(0, ActiveTicketTypes.Length)];
        }

        private void AddTicketToInventory(Side side, TicketType ticket)
        {
            List<TicketType> tickets = GetTickets(side);
            if (tickets.Count >= maxTicketsPerSide)
            {
                tickets.RemoveAt(0);
            }

            tickets.Add(ticket);
        }

        private void ClaimTicketToInventory(Side side, BCTicketDisplay ticket)
        {
            if (ticket == null || ticket.ClaimedToInventory)
            {
                return;
            }

            AddTicketToInventory(side, ticket.TicketType);
            ticket.MarkClaimedToInventory();

            int pending = GetPendingPickupCount(side);
            if (pending > 0)
            {
                _pendingPhysicalTickets[side] = pending - 1;
            }

            OnTicketsChanged?.Invoke();
        }

        private bool RemoveTicketFromInventory(Side side, TicketType ticket)
        {
            List<TicketType> tickets = GetTickets(side);
            int index = tickets.IndexOf(ticket);
            if (index < 0)
            {
                return false;
            }

            tickets.RemoveAt(index);
            return true;
        }

        private List<TicketType> GetTickets(Side side)
        {
            return side == Side.Player ? _playerTickets : _enemyTickets;
        }

        private BCTicketStack GetTicketStack(Side side)
        {
            return side == Side.Player ? playerTicketStack : enemyTicketStack;
        }

        private int GetPendingPickupCount(Side side)
        {
            return _pendingPhysicalTickets.TryGetValue(side, out int count) ? count : 0;
        }

        private void CreateTicketPrefab()
        {
            if (ticketPrefab != null)
            {
                return;
            }

            GameObject ticket = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ticket.name = "TicketPrefab";
            ticket.transform.localScale = new Vector3(0.16f, 0.015f, 0.30f);

            DestroyImmediate(ticket.GetComponent<Collider>());

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new(shader);
            mat.color = new Color(0.9f, 0.85f, 0.7f);
            ticket.GetComponent<Renderer>().sharedMaterial = mat;

            ticket.AddComponent<BCTicketDisplay>();
            ticket.AddComponent<BCTicketInteractable>();

            ticketPrefab = ticket;
            ticket.SetActive(false);
        }

        private void HideCards(bool hide)
        {
            if (_cardManager == null)
            {
                return;
            }

            if (_cardManager.PlayerMainCards != null)
            {
                foreach (var card in _cardManager.PlayerMainCards)
                {
                    if (card != null)
                    {
                        card.gameObject.SetActive(!hide);
                    }
                }
            }

            if (_cardManager.PlayerSpecialCards != null)
            {
                foreach (var card in _cardManager.PlayerSpecialCards)
                {
                    if (card != null)
                    {
                        card.gameObject.SetActive(!hide);
                    }
                }
            }
        }

        private void ResetMultipliers()
        {
            _damageMultipliers[Side.Player] = 1;
            _damageMultipliers[Side.Enemy] = 1;
        }

        private void ResetPendingTickets()
        {
            _pendingPhysicalTickets[Side.Player] = 0;
            _pendingPhysicalTickets[Side.Enemy] = 0;
        }

        private IEnumerator PlayTicketInsertionIfPossible(BCTicketDisplay ticket, Side side)
        {
            BCDealMechanism mechanism = _gameManager != null ? _gameManager.DealMechanism : null;
            if (mechanism == null)
            {
                yield break;
            }

            yield return mechanism.AnimateTicketInsertion(ticket, side);
        }

        private static bool IsActiveTicketType(TicketType ticketType)
        {
            return ActiveTicketTypeSet.Contains(ticketType);
        }
    }
}
