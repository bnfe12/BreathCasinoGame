using UnityEngine;
using System.Collections.Generic;
using BreathCasino.Gameplay;

namespace BreathCasino.Core
{
    /// <summary>
    /// Хранит выбранные карты игрока. Оба слота одинаковые:
    /// - Resource/Threat можно класть в любой слот
    /// - Special можно выбрать только если уже есть хотя бы одна карта в любом слоте (проверяется снаружи при стейджинге)
    /// </summary>
    public static class PlayerSelectionTracker
    {
        private static readonly List<BCCardDisplay> SelectedCardsInternal = new();

        public static IReadOnlyList<BCCardDisplay> SelectedCards => SelectedCardsInternal;

        // Обратная совместимость: код, читающий SelectedMainCards, получает тот же список
        public static IReadOnlyList<BCCardDisplay> SelectedMainCards => SelectedCardsInternal;
        public static BCCardDisplay SelectedMainCard => SelectedCardsInternal.Count > 0 ? SelectedCardsInternal[0] : null;

        // Special-карта — первая Special в выборке (если есть)
        public static BCCardDisplay SelectedSpecialCard
        {
            get
            {
                for (int i = 0; i < SelectedCardsInternal.Count; i++)
                    if (SelectedCardsInternal[i] != null &&
                        SelectedCardsInternal[i].CardType == CardType.Special)
                        return SelectedCardsInternal[i];
                return null;
            }
        }

        public static bool ToggleSelected(BCCardDisplay card)
        {
            if (card == null) return false;

            if (SelectedCardsInternal.Contains(card))
            {
                SelectedCardsInternal.Remove(card);
                return false;
            }

            SelectedCardsInternal.Add(card);
            return true;
        }

        // Обратная совместимость с кодом, вызывающим ToggleSelectedMain/ToggleSelectedSpecial
        public static bool ToggleSelectedMain(BCCardDisplay card) => ToggleSelected(card);
        public static bool ToggleSelectedSpecial(BCCardDisplay card) => ToggleSelected(card);

        public static void ClearSelection()
        {
            for (int i = 0; i < SelectedCardsInternal.Count; i++)
            {
                if (SelectedCardsInternal[i] != null)
                    SelectedCardsInternal[i].GetComponent<BCCardInteractable>()?.SetSelectedVisual(false);
            }
            SelectedCardsInternal.Clear();
        }

        // Обратная совместимость
        public static void ClearMainSelection() => ClearSelection();
        public static void ClearSpecialSelection() => ClearSelection();
    }
}