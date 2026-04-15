namespace BreathCasino.Core
{
    /// <summary>
    /// Правила размещения карт по слотам согласно MECHANICS.md + CLAUDE.md.
    /// Слоты одинаковые (нейтральные). Спецкарта разрешена только если
    /// в одном из слотов уже лежит обычная карта (Resource или Threat).
    /// </summary>
    public static class SlotRules
    {
        /// <summary>
        /// Проверяет, можно ли положить карту с типом <paramref name="cardType"/>
        /// в слот с индексом <paramref name="targetSlotIndex"/> (0 или 1).
        /// <paramref name="firstSlotCardType"/> — тип карты, уже лежащей в слоте 0 (null = пустой).
        /// <paramref name="secondSlotCardType"/> — тип карты, уже лежащей в слоте 1 (null = пустой).
        /// </summary>
        public static bool CanPlaceCard(
            CardType cardType,
            CardType? firstSlotCardType,
            CardType? secondSlotCardType,
            int targetSlotIndex)
        {
            // Нельзя класть в занятый слот
            if (targetSlotIndex == 0 && firstSlotCardType.HasValue) return false;
            if (targetSlotIndex == 1 && secondSlotCardType.HasValue) return false;

            // Спецкарту можно класть только если уже есть хотя бы одна обычная карта
            if (cardType == CardType.Special)
            {
                bool hasNormalInSlot0 = firstSlotCardType.HasValue
                    && firstSlotCardType.Value != CardType.Special;
                bool hasNormalInSlot1 = secondSlotCardType.HasValue
                    && secondSlotCardType.Value != CardType.Special;
                return hasNormalInSlot0 || hasNormalInSlot1;
            }

            return true;
        }

        public static bool CreatesInvalidThreatAttackCombination(
            CardType cardType,
            CardType? firstSlotCardType,
            CardType? secondSlotCardType)
        {
            if (cardType == CardType.Special)
            {
                return false;
            }

            int mainCardCount = 1;
            bool hasThreat = cardType == CardType.Threat;

            if (firstSlotCardType.HasValue && firstSlotCardType.Value != CardType.Special)
            {
                mainCardCount++;
                hasThreat |= firstSlotCardType.Value == CardType.Threat;
            }

            if (secondSlotCardType.HasValue && secondSlotCardType.Value != CardType.Special)
            {
                mainCardCount++;
                hasThreat |= secondSlotCardType.Value == CardType.Threat;
            }

            return mainCardCount > 1 && hasThreat;
        }
    }
}
