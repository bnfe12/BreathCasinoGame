using System.Collections.Generic;
using BreathCasino.Core;
using BreathCasino.Gameplay;
using UnityEngine;

namespace BreathCasino.Core
{
    /// <summary>
    /// Детерминированный AI противника. НЕ MonoBehaviour — создаётся через new EnemyAI().
    /// Никаких зависимостей от сцены, FindFirstObjectByType, Start/Awake.
    /// Все методы — pure functions над переданными данными.
    /// </summary>
    public sealed class EnemyAI
    {
        // ────────────────────────────────────────────────────────────
        //  АТАКА
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Выбрать карты для атаки.
        /// AI, как и игрок, может суммировать до двух карт.
        /// Приоритет: комбинация с Threat, затем просто максимальный вес.
        /// </summary>
        public List<BCCardDisplay> ChooseAttackCards(
            IReadOnlyList<BCCardDisplay> hand)
        {
            var result = new List<BCCardDisplay>();

            if (hand == null || hand.Count == 0)
            {
                Debug.Log("[EnemyAI] Attack: hand is empty — skip.");
                return result;
            }

            int bestScore = int.MinValue;
            int bestWeight = int.MinValue;
            BCCardDisplay bestA = null;
            BCCardDisplay bestB = null;

            for (int i = 0; i < hand.Count; i++)
            {
                BCCardDisplay first = hand[i];
                if (first == null) continue;

                EvaluateAttackCandidate(first, null);

                for (int j = i + 1; j < hand.Count; j++)
                {
                    BCCardDisplay second = hand[j];
                    if (second == null) continue;
                    EvaluateAttackCandidate(first, second);
                }
            }

            if (bestA != null)
            {
                result.Add(bestA);
                if (bestB != null)
                {
                    result.Add(bestB);
                }

                string label = bestB != null
                    ? $"{bestA.CardName}+{bestB.CardName}"
                    : bestA.CardName;
                Debug.Log($"[EnemyAI] Attack: {label} w={bestWeight} Threat={bestScore > 0}");
            }
            else
            {
                Debug.Log("[EnemyAI] Attack: no valid card in hand — skip.");
            }

            return result;

            void EvaluateAttackCandidate(BCCardDisplay first, BCCardDisplay second)
            {
                if (second != null && (first.IsThreat || second.IsThreat))
                {
                    // Threat attacks may only be paired with specials, not with another main card.
                    return;
                }

                int weight = first.Weight + (second != null ? second.Weight : 0);
                bool hasThreat = first.IsThreat || (second != null && second.IsThreat);
                int score = hasThreat ? 1 : 0;
                int cardCount = second != null ? 2 : 1;
                int bestCardCount = bestB != null ? 2 : (bestA != null ? 1 : int.MaxValue);

                if (score < bestScore)
                {
                    return;
                }

                bool isBetter =
                    score > bestScore ||
                    weight > bestWeight ||
                    (weight == bestWeight && cardCount < bestCardCount);

                if (!isBetter)
                {
                    return;
                }

                bestScore = score;
                bestWeight = weight;
                bestA = first;
                bestB = second;
            }
        }

        // ────────────────────────────────────────────────────────────
        //  ЗАЩИТА
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Выбрать карты для защиты. Только Resource (не Threat).
        /// Шаг 1: минимальная одиночная карта с весом >= attackWeight.
        /// Шаг 2: лучшая пара Resource с суммой >= attackWeight (минимально превышающая).
        /// Шаг 3: нет покрытия → пустой список (AI честно пропускает).
        /// Возвращает 1–2 карты, никогда не возвращает заведомо проигрышный набор.
        /// </summary>
        public List<BCCardDisplay> ChooseDefenseCards(
            IReadOnlyList<BCCardDisplay> hand, int attackWeight)
        {
            var result = new List<BCCardDisplay>();

            if (hand == null || hand.Count == 0)
            {
                Debug.Log($"[EnemyAI] Defense vs {attackWeight}: hand empty — skip.");
                return result;
            }

            // Собираем только Resource карты
            var resources = new List<BCCardDisplay>(hand.Count);
            foreach (var card in hand)
            {
                if (card != null && !card.IsThreat)
                    resources.Add(card);
            }

            if (resources.Count == 0)
            {
                Debug.Log($"[EnemyAI] Defense vs {attackWeight}: no resource cards in hand — skip.");
                return result;
            }

            // ── Шаг 1: минимальная одиночная карта >= attackWeight ──
            BCCardDisplay bestSingle  = null;
            int                 bestSingleW = int.MaxValue;

            foreach (var card in resources)
            {
                if (card.Weight >= attackWeight && card.Weight < bestSingleW)
                {
                    bestSingleW = card.Weight;
                    bestSingle  = card;
                }
            }

            if (bestSingle != null)
            {
                result.Add(bestSingle);
                Debug.Log($"[EnemyAI] Defense vs {attackWeight}: single card w={bestSingle.Weight}");
                return result;
            }

            // ── Шаг 2: лучшая пара с минимальной суммой >= attackWeight ──
            BCCardDisplay pairA   = null;
            BCCardDisplay pairB   = null;
            int                 bestSum = int.MaxValue;

            for (int i = 0; i < resources.Count; i++)
            {
                for (int j = i + 1; j < resources.Count; j++)
                {
                    int sum = resources[i].Weight + resources[j].Weight;
                    if (sum >= attackWeight && sum < bestSum)
                    {
                        bestSum = sum;
                        pairA   = resources[i];
                        pairB   = resources[j];
                    }
                }
            }

            if (pairA != null)
            {
                result.Add(pairA);
                result.Add(pairB);
                Debug.Log($"[EnemyAI] Defense vs {attackWeight}: pair w={pairA.Weight}+{pairB.Weight}={bestSum}");
                return result;
            }

            // ── Шаг 3: нет покрытия ──
            int maxAvail = 0;
            foreach (var c in resources) maxAvail += c.Weight;
            Debug.Log($"[EnemyAI] Defense vs {attackWeight}: cannot cover " +
                      $"(max all resources={maxAvail}) — honest skip.");
            return result;
        }

        // ────────────────────────────────────────────────────────────
        //  СПЕЦ КАРТА
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Выбрать специальную карту. Берём первую доступную.
        /// </summary>
        public BCCardDisplay ChooseSpecialCard(
            IReadOnlyList<BCCardDisplay> specials)
        {
            if (specials == null) return null;

            foreach (var card in specials)
            {
                if (card != null && card.CardType == CardType.Special)
                {
                    Debug.Log($"[EnemyAI] Special: {card.CardName}");
                    return card;
                }
            }

            return null;
        }

        // ────────────────────────────────────────────────────────────
        //  ВЫСТРЕЛ
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Выбрать цель для выстрела.
        /// Если холостых > blankThreshold → стрелять в себя (безопасно).
        /// Иначе → в игрока.
        /// </summary>
        public Side ChooseShotTarget(
            List<BulletType> chamber, float blankThreshold = 0.6f)
        {
            if (chamber == null || chamber.Count == 0)
                return Side.Player;

            int blanks = 0;
            foreach (var b in chamber)
                if (b == BulletType.Blank) blanks++;

            float ratio = (float)blanks / chamber.Count;

            if (ratio > blankThreshold)
            {
                Debug.Log($"[EnemyAI] Shot target: SELF (blanks={ratio:0%} > threshold={blankThreshold:0%})");
                return Side.Enemy; // Enemy = стреляет в себя
            }

            Debug.Log($"[EnemyAI] Shot target: PLAYER (blanks={ratio:0%} <= threshold={blankThreshold:0%})");
            return Side.Player;
        }
    }
}
