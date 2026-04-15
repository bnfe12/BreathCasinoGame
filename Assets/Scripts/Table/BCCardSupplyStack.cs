using System.Collections.Generic;
using BreathCasino.Core;
using UnityEngine;

namespace BreathCasino.Gameplay
{
    public class BCCardSupplyStack : MonoBehaviour
    {
        [SerializeField] private Side side = Side.Player;
        [SerializeField] private float fanWidth = 0.28f;
        [SerializeField] private float fanDepth = 0.035f;
        [SerializeField] private float fanYawAngle = 15f;
        [SerializeField] private float baseLift = 0.01f;
        [SerializeField] private float stackHeightStep = 0.0035f;
        [SerializeField] private float stackYawForEnemy = 180f;

        public Side Side => side;

        public void Configure(Side stackSide)
        {
            side = stackSide;
        }

        public void Layout(IReadOnlyList<BCCardDisplay> cards)
        {
            if (cards == null)
            {
                return;
            }

            int total = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    total++;
                }
            }

            if (total == 0)
            {
                return;
            }

            float center = (total - 1) * 0.5f;
            float horizontalStep = total > 1 ? fanWidth / (total - 1) : 0f;
            int visibleIndex = 0;

            for (int i = 0; i < cards.Count; i++)
            {
                BCCardDisplay card = cards[i];
                if (card == null)
                {
                    continue;
                }

                card.transform.SetParent(transform, false);
                float centeredIndex = visibleIndex - center;
                float normalized = total > 1 ? centeredIndex / center : 0f;
                float localX = total > 1 ? -fanWidth * 0.5f + horizontalStep * visibleIndex : 0f;
                float localZ = -Mathf.Abs(normalized) * fanDepth;
                float localY = baseLift + visibleIndex * stackHeightStep;
                float yaw = normalized * fanYawAngle;

                card.transform.localPosition = new Vector3(localX, localY, localZ);
                card.transform.localRotation = side == Side.Enemy
                    ? Quaternion.Euler(0f, stackYawForEnemy - yaw, 0f)
                    : Quaternion.Euler(0f, yaw, 0f);
                BCCardDisplay.ApplyDefaultScale(card.transform);
                visibleIndex++;
            }
        }
    }
}
