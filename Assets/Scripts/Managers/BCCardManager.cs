#pragma warning disable CS0649
using System.Collections.Generic;
using BreathCasino.Core;
using UnityEngine;

namespace BreathCasino.Gameplay
{
    public class BCCardManager : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject mainCardPrefab;
        [SerializeField] private GameObject specialCardPrefab;

        [Header("Card Rules")]
        [SerializeField] private int startingMainCardsPerSide = 4;
        [SerializeField] private int refillMainCardsWhenEmpty = 3;
        [SerializeField] private int specialCardsOnRoundStart = 1;

        private static readonly int[] StartingMainCardsByRound = { 4, 5, 6 };

        [Header("Fan Layout")]
        [SerializeField] private float fanAngle = 45f;
        [SerializeField] private float cardSpacing = 0.22f;
        [SerializeField] private float specialCardSpacing = 0.18f;
        [SerializeField] private float cardTiltX = -12f;
        [SerializeField] private float mainCardsShiftWhenSpecialsVisible = -0.18f;
        [SerializeField] private float specialCardsShift = 0.18f;

        private readonly List<BCCardDisplay> _playerMainCards = new();
        private readonly List<BCCardDisplay> _playerSpecialCards = new();
        private readonly List<BCCardDisplay> _enemyMainCards = new();
        private readonly List<BCCardDisplay> _enemySpecialCards = new();

        private readonly List<CardData> _mainDeck = new();
        private readonly List<CardData> _mainDiscard = new();

        private int _currentDeckRoundIndex = -1;

        private Transform _playerHandHolder;
        private Transform _playerSpecialHolder;
        private Transform _enemyHandHolder;
        private Transform _enemySpecialHolder;
        private BCCardSupplyStack _playerSupplyStack;
        private BCCardSupplyStack _enemySupplyStack;

        private readonly List<BCCardDisplay> _pendingPlayerCards = new();
        private readonly List<BCCardDisplay> _pendingEnemyCards = new();

        public IReadOnlyList<BCCardDisplay> PlayerMainCards => _playerMainCards;
        public IReadOnlyList<BCCardDisplay> PlayerSpecialCards => _playerSpecialCards;
        public IReadOnlyList<BCCardDisplay> EnemyMainCards => _enemyMainCards;
        public IReadOnlyList<BCCardDisplay> EnemySpecialCards => _enemySpecialCards;

        public bool IsDeckEmpty => _mainDeck.Count == 0;
        public bool HasPendingPlayerPickup => HasPendingPickup(Side.Player);
        public bool HasPendingEnemyPickup => HasPendingPickup(Side.Enemy);

        public void ConfigureDealStacks(BCCardSupplyStack playerSupplyStack, BCCardSupplyStack enemySupplyStack)
        {
            _playerSupplyStack = playerSupplyStack;
            _enemySupplyStack = enemySupplyStack;
        }

        public void DiscardCard(BCCardDisplay card)
        {
            if (card == null) return;

            if (card.CardType != CardType.Special)
            {
                AddToDiscard(new CardData
                {
                    cardName = card.CardName,
                    weight = card.Weight,
                    cardType = card.CardType,
                    specialEffect = SpecialEffectType.None
                });
            }

            _playerMainCards.Remove(card);
            _playerSpecialCards.Remove(card);
            _enemyMainCards.Remove(card);
            _enemySpecialCards.Remove(card);
            _pendingPlayerCards.Remove(card);
            _pendingEnemyCards.Remove(card);

            if (card.gameObject != null)
            {
                Destroy(card.gameObject);
            }

            RelayoutHands();
        }

        public BCCardDisplay StealRandomCard(Side fromSide, Side toSide, IEnumerable<BCCardDisplay> exclude = null)
        {
            List<BCCardDisplay> fromMain = fromSide == Side.Player ? _playerMainCards : _enemyMainCards;
            List<BCCardDisplay> fromSpecial = fromSide == Side.Player ? _playerSpecialCards : _enemySpecialCards;

            Transform toMainHolder = GetMainHolder(toSide);
            Transform toSpecialHolder = GetSpecialHolder(toSide);

            var excludeSet = exclude != null ? new HashSet<BCCardDisplay>(exclude) : null;
            List<BCCardDisplay> pool = new();

            foreach (var card in fromMain)
            {
                if (excludeSet == null || !excludeSet.Contains(card))
                    pool.Add(card);
            }

            foreach (var card in fromSpecial)
            {
                if (excludeSet == null || !excludeSet.Contains(card))
                    pool.Add(card);
            }

            if (pool.Count == 0)
                return null;

            BCCardDisplay stolen = pool[Random.Range(0, pool.Count)];
            bool isMain = stolen.CardType != CardType.Special;

            _pendingPlayerCards.Remove(stolen);
            _pendingEnemyCards.Remove(stolen);

            if (isMain) fromMain.Remove(stolen);
            else fromSpecial.Remove(stolen);

            Transform targetHolder = isMain ? toMainHolder : toSpecialHolder;
            if (targetHolder == null)
                return null;

            stolen.transform.SetParent(targetHolder, false);
            stolen.SetEnemyCard(toSide == Side.Enemy);
            stolen.SetCardHidden(toSide == Side.Enemy);

            if (isMain)
            {
                if (toSide == Side.Player) _playerMainCards.Add(stolen);
                else _enemyMainCards.Add(stolen);
            }
            else
            {
                if (toSide == Side.Player) _playerSpecialCards.Add(stolen);
                else _enemySpecialCards.Add(stolen);
            }

            RelayoutHands();
            return stolen;
        }

        public void ClearAllHands()
        {
            DestroyCards(_playerMainCards);
            DestroyCards(_playerSpecialCards);
            DestroyCards(_enemyMainCards);
            DestroyCards(_enemySpecialCards);
            _pendingPlayerCards.Clear();
            _pendingEnemyCards.Clear();
        }

        public void DealHands(
            Transform playerHandHolder,
            Transform playerSpecialHolder,
            Transform enemyHandHolder,
            Transform enemySpecialHolder,
            int currentRoundIndex = 0,
            bool isFirstMiniRoundOfRound = false)
        {
            ClearAllHands();
            CacheHolders(playerHandHolder, playerSpecialHolder, enemyHandHolder, enemySpecialHolder);
            EnsureDeckForRound(currentRoundIndex);

            int mainCount = GetStartingMainCardsPerSide(currentRoundIndex);
            bool allowSpecials = currentRoundIndex >= 1;

            DealInitialMainHand(_playerMainCards, GetMainHolder(Side.Player), false, mainCount, isFirstMiniRoundOfRound);
            DealInitialMainHand(_enemyMainCards, GetMainHolder(Side.Enemy), true, mainCount, isFirstMiniRoundOfRound);

            if (allowSpecials)
            {
                DealInitialSpecials(_playerSpecialCards, GetSpecialHolder(Side.Player), false, GetSpecialCardsOnRoundStart(currentRoundIndex));
                DealInitialSpecials(_enemySpecialCards, GetSpecialHolder(Side.Enemy), true, GetSpecialCardsOnRoundStart(currentRoundIndex));
            }

            RelayoutHands();
        }

        public void RefillHandsToRoundLimit(
            int currentRoundIndex,
            Transform playerHandHolder = null,
            Transform playerSpecialHolder = null,
            Transform enemyHandHolder = null,
            Transform enemySpecialHolder = null)
        {
            CacheHolders(playerHandHolder, playerSpecialHolder, enemyHandHolder, enemySpecialHolder);
            EnsureDeckForRound(currentRoundIndex);

            int refillCount = GetRefillMainCardsWhenEmpty(currentRoundIndex);

            RefillSideIfEmpty(_playerMainCards, GetMainHolder(Side.Player), false, refillCount);
            RefillSideIfEmpty(_enemyMainCards, GetMainHolder(Side.Enemy), true, refillCount);

            RelayoutHands();
        }

        public void RecreateCurrentRoundDeck()
        {
            if (_currentDeckRoundIndex < 0)
                return;

            RebuildRoundDeck(_currentDeckRoundIndex);
        }

        public void RefillDeckFromLife(int cardCount = 6)
        {
            if (_currentDeckRoundIndex < 0)
                return;

            for (int i = 0; i < cardCount; i++)
            {
                _mainDeck.Add(CreateDeckCardFromRound(_currentDeckRoundIndex, i));
            }

            ShuffleList(_mainDeck);
        }

        public void AddToDiscard(CardData data)
        {
            if (data.cardType != CardType.Special)
            {
                _mainDiscard.Add(data);
            }
        }

        public bool HasPendingPickup(Side side)
        {
            List<BCCardDisplay> pending = GetPendingList(side);
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i] == null)
                {
                    pending.RemoveAt(i);
                }
            }

            return pending.Count > 0;
        }

        public bool IsPendingCard(BCCardDisplay card)
        {
            if (card == null)
            {
                return false;
            }

            return _pendingPlayerCards.Contains(card) || _pendingEnemyCards.Contains(card);
        }

        public int TakePendingCards(Side side)
        {
            List<BCCardDisplay> pending = GetPendingList(side);
            if (pending.Count == 0)
            {
                return 0;
            }

            int moved = 0;
            Transform mainHolder = GetMainHolder(side);
            Transform specialHolder = GetSpecialHolder(side);

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                BCCardDisplay card = pending[i];
                pending.RemoveAt(i);

                if (card == null)
                {
                    continue;
                }

                Transform targetHolder = card.CardType == CardType.Special ? specialHolder : mainHolder;
                if (targetHolder == null)
                {
                    continue;
                }

                card.transform.SetParent(targetHolder, false);
                moved++;
            }

            RelayoutHands();
            return moved;
        }

        public void RelayoutHands()
        {
            RelayoutSide(_playerMainCards, _playerSpecialCards, GetMainHolder(Side.Player), GetSpecialHolder(Side.Player));
            RelayoutSide(_enemyMainCards, _enemySpecialCards, GetMainHolder(Side.Enemy), GetSpecialHolder(Side.Enemy));
        }

        public CardData CreateRandomMainCard(bool allowThreat = true)
        {
            int roundIndex = Mathf.Max(_currentDeckRoundIndex, 0);
            List<CardData> recipe = new();
            BuildRoundMainDeck(roundIndex, recipe);

            if (!allowThreat)
            {
                recipe.RemoveAll(card => card.cardType == CardType.Threat);
            }

            if (recipe.Count == 0)
            {
                return new CardData
                {
                    cardName = "Resource",
                    weight = 2,
                    cardType = CardType.Resource,
                    specialEffect = SpecialEffectType.None
                };
            }

            return recipe[Random.Range(0, recipe.Count)];
        }

        public CardData CreateRandomSpecialCard()
        {
            SpecialEffectType[] effects =
            {
                SpecialEffectType.Cancel,
                SpecialEffectType.Duplicate
            };

            SpecialEffectType effect = effects[Random.Range(0, effects.Length)];
            return new CardData
            {
                cardName = "Special",
                weight = 0,
                cardType = CardType.Special,
                specialEffect = effect
            };
        }

        private void CacheHolders(
            Transform playerHandHolder,
            Transform playerSpecialHolder,
            Transform enemyHandHolder,
            Transform enemySpecialHolder)
        {
            if (playerHandHolder != null) _playerHandHolder = playerHandHolder;
            if (playerSpecialHolder != null) _playerSpecialHolder = playerSpecialHolder;
            if (enemyHandHolder != null) _enemyHandHolder = enemyHandHolder;
            if (enemySpecialHolder != null) _enemySpecialHolder = enemySpecialHolder;
        }

        private Transform GetMainHolder(Side side)
        {
            return side == Side.Player ? _playerHandHolder : _enemyHandHolder;
        }

        private Transform GetSpecialHolder(Side side)
        {
            Transform special = side == Side.Player ? _playerSpecialHolder : _enemySpecialHolder;
            Transform main = GetMainHolder(side);
            return special != null ? special : main;
        }

        private BCCardSupplyStack GetSupplyStack(Side side)
        {
            return side == Side.Player ? _playerSupplyStack : _enemySupplyStack;
        }

        private List<BCCardDisplay> GetPendingList(Side side)
        {
            return side == Side.Player ? _pendingPlayerCards : _pendingEnemyCards;
        }

        private static int GetDeckSize(int roundIndex)
        {
            int[] sizes = { 17, 24, 27 };
            return sizes[Mathf.Clamp(roundIndex, 0, sizes.Length - 1)];
        }

        private int GetStartingMainCardsPerSide(int roundIndex)
        {
            int index = Mathf.Clamp(roundIndex, 0, StartingMainCardsByRound.Length - 1);
            int configuredValue = StartingMainCardsByRound[index];
            return Mathf.Max(1, configuredValue > 0 ? configuredValue : startingMainCardsPerSide);
        }

        private int GetRefillMainCardsWhenEmpty(int roundIndex)
        {
            return Mathf.Max(1, refillMainCardsWhenEmpty);
        }

        private int GetSpecialCardsOnRoundStart(int roundIndex)
        {
            return roundIndex >= 1 ? Mathf.Max(0, specialCardsOnRoundStart) : 0;
        }

        private void EnsureDeckForRound(int roundIndex)
        {
            if (_currentDeckRoundIndex == roundIndex && _mainDeck.Count > 0)
                return;

            RebuildRoundDeck(roundIndex);
        }

        private void RebuildRoundDeck(int roundIndex)
        {
            _currentDeckRoundIndex = roundIndex;
            _mainDeck.Clear();
            _mainDiscard.Clear();
            BuildRoundMainDeck(roundIndex, _mainDeck);
            ShuffleList(_mainDeck);
            Debug.Log($"[CardManager] Built round {roundIndex + 1} deck with {_mainDeck.Count} cards.");
        }

        private void BuildRoundMainDeck(int roundIndex, List<CardData> target)
        {
            target.Clear();

            switch (Mathf.Clamp(roundIndex, 0, 2))
            {
                case 0:
                    AddCards(target, CardType.Resource, 2, 4);
                    AddCards(target, CardType.Resource, 3, 3);
                    AddCards(target, CardType.Resource, 4, 2);
                    AddCards(target, CardType.Threat, 4, 1);
                    AddCards(target, CardType.Resource, 5, 2);
                    AddCards(target, CardType.Resource, 6, 2);
                    AddCards(target, CardType.Threat, 6, 1);
                    AddCards(target, CardType.Resource, 7, 1);
                    AddCards(target, CardType.Threat, 8, 1);
                    break;

                case 1:
                    AddCards(target, CardType.Resource, 2, 5);
                    AddCards(target, CardType.Resource, 3, 4);
                    AddCards(target, CardType.Resource, 4, 3);
                    AddCards(target, CardType.Threat, 4, 2);
                    AddCards(target, CardType.Resource, 5, 3);
                    AddCards(target, CardType.Resource, 6, 2);
                    AddCards(target, CardType.Threat, 6, 2);
                    AddCards(target, CardType.Resource, 7, 1);
                    AddCards(target, CardType.Resource, 8, 1);
                    AddCards(target, CardType.Threat, 8, 1);
                    break;

                default:
                    AddCards(target, CardType.Resource, 2, 5);
                    AddCards(target, CardType.Resource, 3, 4);
                    AddCards(target, CardType.Resource, 4, 3);
                    AddCards(target, CardType.Threat, 4, 2);
                    AddCards(target, CardType.Resource, 5, 3);
                    AddCards(target, CardType.Resource, 6, 3);
                    AddCards(target, CardType.Threat, 6, 2);
                    AddCards(target, CardType.Resource, 7, 2);
                    AddCards(target, CardType.Resource, 8, 1);
                    AddCards(target, CardType.Threat, 8, 2);
                    break;
            }
        }

        private static void AddCards(List<CardData> deck, CardType type, int weight, int count)
        {
            for (int i = 0; i < count; i++)
            {
                deck.Add(new CardData
                {
                    cardName = type == CardType.Threat ? "Threat" : "Resource",
                    weight = weight,
                    cardType = type,
                    specialEffect = SpecialEffectType.None
                });
            }
        }

        private CardData CreateDeckCardFromRound(int roundIndex, int seedIndex)
        {
            List<CardData> recipe = new();
            BuildRoundMainDeck(roundIndex, recipe);
            if (recipe.Count == 0)
            {
                return new CardData
                {
                    cardName = "Resource",
                    weight = 2,
                    cardType = CardType.Resource,
                    specialEffect = SpecialEffectType.None
                };
            }

            return recipe[Mathf.Abs(seedIndex) % recipe.Count];
        }

        private CardData DrawMainCard()
        {
            if (_currentDeckRoundIndex < 0)
                _currentDeckRoundIndex = 0;

            if (_mainDeck.Count == 0)
            {
                RebuildRoundDeck(_currentDeckRoundIndex);
            }

            int lastIndex = _mainDeck.Count - 1;
            CardData data = _mainDeck[lastIndex];
            _mainDeck.RemoveAt(lastIndex);
            return data;
        }

        private void DealInitialMainHand(
            List<BCCardDisplay> targetList,
            Transform holder,
            bool isEnemy,
            int mainCount,
            bool guaranteeThreat)
        {
            if (holder == null) return;

            List<CardData> handData = new();
            for (int i = 0; i < mainCount; i++)
            {
                handData.Add(DrawMainCard());
            }

            if (guaranteeThreat && !HasThreat(handData))
            {
                for (int i = 0; i < handData.Count; i++)
                {
                    if (handData[i].cardType == CardType.Resource && handData[i].weight >= 4)
                    {
                        handData[i] = new CardData
                        {
                            cardName = "Threat",
                            weight = handData[i].weight,
                            cardType = CardType.Threat,
                            specialEffect = SpecialEffectType.None
                        };
                        break;
                    }
                }
            }

            for (int i = 0; i < handData.Count; i++)
            {
                Side side = isEnemy ? Side.Enemy : Side.Player;
                Transform spawnParent = GetSpawnParent(side, holder);
                BCCardDisplay display = SpawnCard(mainCardPrefab, spawnParent, i, handData.Count, isEnemy, true, handData[i]);
                targetList.Add(display);
                RegisterPendingIfNeeded(side, display, holder);
            }

            LayoutPendingStackIfNeeded(isEnemy ? Side.Enemy : Side.Player);
        }

        private void DealInitialSpecials(List<BCCardDisplay> targetList, Transform holder, bool isEnemy, int count)
        {
            if (holder == null || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                Side side = isEnemy ? Side.Enemy : Side.Player;
                Transform spawnParent = GetSpawnParent(side, holder);
                BCCardDisplay display = SpawnCard(specialCardPrefab, spawnParent, i, count, isEnemy, false, default);
                targetList.Add(display);
                RegisterPendingIfNeeded(side, display, holder);
            }

            LayoutPendingStackIfNeeded(isEnemy ? Side.Enemy : Side.Player);
        }

        private void RefillSideIfEmpty(List<BCCardDisplay> targetList, Transform holder, bool isEnemy, int refillCount)
        {
            if (holder == null || targetList.Count > 0)
                return;

            for (int i = 0; i < refillCount; i++)
            {
                CardData data = DrawMainCard();
                Side side = isEnemy ? Side.Enemy : Side.Player;
                Transform spawnParent = GetSpawnParent(side, holder);
                BCCardDisplay display = SpawnCard(mainCardPrefab, spawnParent, i, refillCount, isEnemy, true, data);
                targetList.Add(display);
                RegisterPendingIfNeeded(side, display, holder);
            }

            LayoutPendingStackIfNeeded(isEnemy ? Side.Enemy : Side.Player);
            Debug.Log($"[CardManager] {(isEnemy ? "Enemy" : "Player")} drew {refillCount} cards after empty hand.");
        }

        private Transform GetSpawnParent(Side side, Transform defaultHolder)
        {
            BCCardSupplyStack supplyStack = GetSupplyStack(side);
            return supplyStack != null ? supplyStack.transform : defaultHolder;
        }

        private void RegisterPendingIfNeeded(Side side, BCCardDisplay card, Transform finalHolder)
        {
            if (card == null)
            {
                return;
            }

            BCCardSupplyStack supplyStack = GetSupplyStack(side);
            if (supplyStack == null || supplyStack.transform == finalHolder)
            {
                return;
            }

            GetPendingList(side).Add(card);
        }

        private void LayoutPendingStackIfNeeded(Side side)
        {
            BCCardSupplyStack supplyStack = GetSupplyStack(side);
            if (supplyStack == null)
            {
                return;
            }

            supplyStack.Layout(GetPendingList(side));
        }

        private static bool HasThreat(IReadOnlyList<CardData> cards)
        {
            if (cards == null) return false;

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].cardType == CardType.Threat)
                    return true;
            }

            return false;
        }

        private BCCardDisplay SpawnCard(
            GameObject prefab,
            Transform parent,
            int index,
            int total,
            bool isEnemy,
            bool isMainCard,
            CardData? deckData = null)
        {
            GameObject instance = prefab != null ? Instantiate(prefab, parent) : CreateCardInPlace(parent, isMainCard);
            instance.name = (isEnemy ? "Enemy" : "Player") + "_" + (isMainCard ? "Main" : "Special") + "_" + (index + 1);
            BCCardDisplay.ApplyDefaultScale(instance.transform);

            BCCardDisplay display = instance.GetComponent<BCCardDisplay>();
            if (display == null) display = instance.AddComponent<BCCardDisplay>();
            if (instance.GetComponent<CardNumberDisplay>() == null) instance.AddComponent<CardNumberDisplay>();

            CardData data = deckData.HasValue && isMainCard
                ? deckData.Value
                : (isMainCard ? CreateRandomMainCard() : CreateRandomSpecialCard());

            display.SetCardData(data);
            display.SetEnemyCard(isEnemy);
            display.SetCardHidden(isEnemy);

            if (instance.GetComponent<Collider>() == null)
            {
                BoxCollider bc = instance.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(1.2f, 1.2f, 1.2f);
            }

            if (instance.GetComponent<BCCardInteractable>() == null)
            {
                instance.AddComponent<BCCardInteractable>();
            }

            return display;
        }

        private GameObject CreateCardInPlace(Transform parent, bool isMainCard)
        {
            GameObject card = GameObject.CreatePrimitive(PrimitiveType.Cube);
            card.transform.SetParent(parent, false);
            BCCardDisplay.ApplyDefaultScale(card.transform);

            Collider collider = card.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            BoxCollider box = card.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.2f, 1.2f, 1.2f);

            BCCardDisplay display = card.AddComponent<BCCardDisplay>();
            card.AddComponent<CardNumberDisplay>();
            display.SetCardData(isMainCard ? CreateRandomMainCard() : CreateRandomSpecialCard());
            card.AddComponent<BCCardInteractable>();
            return card;
        }

        private static void DestroyCards(List<BCCardDisplay> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].gameObject != null)
                {
                    Destroy(list[i].gameObject);
                }
            }

            list.Clear();
        }

        private void RelayoutSide(List<BCCardDisplay> mainCards, List<BCCardDisplay> specialCards, Transform mainHolder, Transform specialHolder)
        {
            if (mainHolder == null)
                return;

            bool singleHolder = specialHolder == null || specialHolder == mainHolder;
            if (singleHolder)
            {
                RelayoutCombinedHand(mainCards, specialCards, mainHolder);
                return;
            }

            RelayoutCardGroup(mainCards, mainHolder, false, false);
            RelayoutCardGroup(specialCards, specialHolder, true, true);
        }

        private void RelayoutCombinedHand(List<BCCardDisplay> mainCards, List<BCCardDisplay> specialCards, Transform holder)
        {
            List<BCCardDisplay> visibleMain = GetCardsInHolder(mainCards, holder);
            List<BCCardDisplay> visibleSpecial = GetCardsInHolder(specialCards, holder);

            visibleMain.Sort((a, b) => a.Weight.CompareTo(b.Weight));

            bool hasSpecials = visibleSpecial.Count > 0;
            RelayoutCardGroup(visibleMain, holder, hasSpecials, false);
            RelayoutCardGroup(visibleSpecial, holder, true, true);
        }

        private List<BCCardDisplay> GetCardsInHolder(List<BCCardDisplay> cards, Transform holder)
        {
            List<BCCardDisplay> result = new();
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null && cards[i].transform.parent == holder)
                {
                    result.Add(cards[i]);
                }
            }

            return result;
        }

        private void RelayoutCardGroup(List<BCCardDisplay> cards, Transform holder, bool hasVisibleSpecials, bool isSpecialHand)
        {
            if (holder == null || cards == null || cards.Count == 0)
                return;

            float spacing = isSpecialHand ? specialCardSpacing : cardSpacing;
            float width = (cards.Count - 1) * spacing;
            float centerShift = isSpecialHand
                ? specialCardsShift
                : (hasVisibleSpecials ? mainCardsShiftWhenSpecialsVisible : 0f);
            float angle = isSpecialHand ? fanAngle * 0.45f : fanAngle;

            for (int i = 0; i < cards.Count; i++)
            {
                BCCardDisplay card = cards[i];
                float yaw = cards.Count > 1
                    ? (i - (cards.Count - 1) * 0.5f) / (cards.Count - 1) * angle
                    : 0f;

                card.transform.localPosition = new Vector3((i * spacing - width * 0.5f) + centerShift, 0f, 0f);
                card.transform.localRotation = Quaternion.Euler(cardTiltX, yaw, 0f);
                BCCardDisplay.ApplyDefaultScale(card.transform);
            }
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
#pragma warning restore CS0649
