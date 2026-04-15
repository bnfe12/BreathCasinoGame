using System.Collections;
using BreathCasino.Core;
using UnityEngine;

namespace BreathCasino.Gameplay
{
    /// <summary>
    /// Анимация handLow → handHigh для карт игрока и врага.
    /// Равноправие: обе стороны имеют одинаковую логику подъёма/опускания руки.
    /// </summary>
    public class BCHandAnimator : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private Transform playerHandHolder;
        [SerializeField] private Transform playerSpecialHolder;

        [Header("Enemy")]
        [SerializeField] private Transform enemyHandHolder;
        [SerializeField] private Transform enemySpecialHolder;

        [Header("Positions (local)")]
        [Tooltip("Игрок: рука опущена (карты внизу).")]
        [SerializeField] private Vector3 handLowLocal = new(0f, -0.45f, 0.12f);
        [Tooltip("Игрок: рука поднята, карты перед ним.")]
        [SerializeField] private Vector3 handHighLocal = new(0f, -0.08f, 0.44f);
        [Tooltip("Враг: рука опущена (карты внизу). Z>0 = карты перед врагом (к столу).")]
        [SerializeField] private Vector3 enemyHandLowLocal = new(0f, -0.45f, 0.12f);
        [Tooltip("Враг: рука поднята, карты ПЕРЕД врагом. Z>0 = к столу.")]
        [SerializeField] private Vector3 enemyHandHighLocal = new(0f, -0.08f, 0.72f);

        [Header("Animation")]
        [SerializeField] private float transitionDuration = 0.25f;

        public void RaisePlayerHand()
        {
            SetHandPosition(Side.Player, handHighLocal);
        }

        public void LowerPlayerHand()
        {
            SetHandPosition(Side.Player, handLowLocal);
        }

        public void RaiseEnemyHand()
        {
            SetHandPosition(Side.Enemy, enemyHandHighLocal);
        }

        public void LowerEnemyHand()
        {
            SetHandPosition(Side.Enemy, enemyHandLowLocal);
        }

        /// <summary>
        /// Поднять руку стороны, опустить противоположную.
        /// </summary>
        public void RaiseHandForSide(Side side)
        {
            if (side == Side.Player)
            {
                SetHandPosition(Side.Player, handHighLocal);
                SetHandPosition(Side.Enemy, enemyHandLowLocal);
            }
            else
            {
                SetHandPosition(Side.Enemy, enemyHandHighLocal);
                SetHandPosition(Side.Player, handLowLocal);
            }
        }

        /// <summary>
        /// Опустить обе руки.
        /// </summary>
        public void LowerBothHands()
        {
            SetHandPosition(Side.Player, handLowLocal);
            SetHandPosition(Side.Enemy, enemyHandLowLocal);
        }

        public void RaiseHandForSideAnimated(Side side)
        {
            StopAllCoroutines();
            StartCoroutine(AnimateHandForSide(side, true));
        }

        public void LowerBothHandsAnimated()
        {
            StopAllCoroutines();
            StartCoroutine(AnimateLowerBoth());
        }

        private void SetHandPosition(Side side, Vector3 localPos)
        {
            if (side == Side.Player)
            {
                if (playerHandHolder != null) playerHandHolder.localPosition = localPos;
                if (playerSpecialHolder != null) playerSpecialHolder.localPosition = localPos;
            }
            else
            {
                if (enemyHandHolder != null) enemyHandHolder.localPosition = localPos;
                if (enemySpecialHolder != null) enemySpecialHolder.localPosition = localPos;
            }
        }

        private IEnumerator AnimateHandForSide(Side side, bool raise)
        {
            Transform playerMain = playerHandHolder;
            Transform playerSpec = playerSpecialHolder;
            Transform enemyMain = enemyHandHolder;
            Transform enemySpec = enemySpecialHolder;

            Vector3 playerStart = playerMain != null ? playerMain.localPosition : handLowLocal;
            Vector3 enemyStart = enemyMain != null ? enemyMain.localPosition : enemyHandLowLocal;

            Vector3 playerTarget = side == Side.Player ? handHighLocal : handLowLocal;
            Vector3 enemyTarget = side == Side.Enemy ? enemyHandHighLocal : enemyHandLowLocal;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / transitionDuration;
                float smooth = t * t * (3f - 2f * t);

                Vector3 playerPos = Vector3.Lerp(playerStart, playerTarget, smooth);
                Vector3 enemyPos = Vector3.Lerp(enemyStart, enemyTarget, smooth);

                if (playerMain != null) playerMain.localPosition = playerPos;
                if (playerSpec != null) playerSpec.localPosition = playerPos;
                if (enemyMain != null) enemyMain.localPosition = enemyPos;
                if (enemySpec != null) enemySpec.localPosition = enemyPos;

                yield return null;
            }

            SetHandPosition(Side.Player, playerTarget);
            SetHandPosition(Side.Enemy, enemyTarget);
        }

        private IEnumerator AnimateLowerBoth()
        {
            Vector3 playerStart = playerHandHolder != null ? playerHandHolder.localPosition : handHighLocal;
            Vector3 enemyStart = enemyHandHolder != null ? enemyHandHolder.localPosition : enemyHandHighLocal;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / transitionDuration;
                float smooth = t * t * (3f - 2f * t);

                Vector3 pos = Vector3.Lerp(playerStart, handLowLocal, smooth);
                if (playerHandHolder != null) playerHandHolder.localPosition = pos;
                if (playerSpecialHolder != null) playerSpecialHolder.localPosition = pos;

                pos = Vector3.Lerp(enemyStart, enemyHandLowLocal, smooth);
                if (enemyHandHolder != null) enemyHandHolder.localPosition = pos;
                if (enemySpecialHolder != null) enemySpecialHolder.localPosition = pos;

                yield return null;
            }

            LowerBothHands();
        }

        public void SetHolders(Transform playerHand, Transform playerSpec, Transform enemyHand, Transform enemySpec)
        {
            playerHandHolder = playerHand;
            playerSpecialHolder = playerSpec;
            enemyHandHolder = enemyHand;
            enemySpecialHolder = enemySpec;
        }

        /// <summary>
        /// Устанавливает позиции рук врага для карт перед ним (враг повёрнут 180° к столу).
        /// </summary>
        public void SetEnemyHandPositionsForFacingTable(Vector3 low, Vector3 high)
        {
            enemyHandLowLocal = low;
            enemyHandHighLocal = high;
        }
    }
}