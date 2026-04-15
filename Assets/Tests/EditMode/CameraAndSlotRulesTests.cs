using System.Reflection;
using NUnit.Framework;
using BreathCasino.Core;
using UnityEngine;

namespace BreathCasino.Tests
{
    public class CameraAndSlotRulesTests
    {
        [Test]
        public void CameraState_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)CameraState.Free);
            Assert.AreEqual(1, (int)CameraState.Cards);
            Assert.AreEqual(2, (int)CameraState.PlayerSlots);
            Assert.AreEqual(3, (int)CameraState.TableSlots);
            Assert.AreEqual(4, (int)CameraState.EnemySlots);
        }

        [Test]
        public void BCCameraController_NextState_Cycles()
        {
            var next1 = BCCameraController.NextState(CameraState.Free);
            var next2 = BCCameraController.NextState(next1);
            var next3 = BCCameraController.NextState(next2);

            Assert.AreNotEqual(CameraState.Free, next1);
            Assert.AreNotEqual(next1, next2);
            Assert.AreNotEqual(next2, next3);
        }

        [Test]
        public void SlotRules_EmptySlots_AllowsResource()
        {
            bool result = SlotRules.CanPlaceCard(
                CardType.Resource, null, null, 0);
            Assert.IsTrue(result);
        }

        [Test]
        public void SlotRules_EmptySlots_AllowsThreat()
        {
            bool result = SlotRules.CanPlaceCard(
                CardType.Threat, null, null, 0);
            Assert.IsTrue(result);
        }

        [Test]
        public void SlotRules_EmptySlots_BlocksSpecial()
        {
            bool result = SlotRules.CanPlaceCard(
                CardType.Special, null, null, 0);
            Assert.IsFalse(result);
        }

        [Test]
        public void SlotRules_WithResource_AllowsSpecial()
        {
            bool result = SlotRules.CanPlaceCard(
                CardType.Special, CardType.Resource, null, 1);
            Assert.IsTrue(result);
        }

        [Test]
        public void SlotRules_WithThreat_AllowsSpecial()
        {
            bool result = SlotRules.CanPlaceCard(
                CardType.Special, CardType.Threat, null, 1);
            Assert.IsTrue(result);
        }

        [Test]
        public void TurnOrder_DrawPassesNextCardPhaseToPreviousDefender()
        {
            MethodInfo method = typeof(GameManager).GetMethod("DetermineNextCardPhaseAttacker", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null, "Expected GameManager turn-order helper to exist.");

            object result = method.Invoke(null, new object[] { Side.Player, Side.Enemy, null });

            Assert.That((Side)result, Is.EqualTo(Side.Enemy));
        }

        [Test]
        public void TurnOrder_DefenderWinPassesNextCardPhaseToDefender()
        {
            MethodInfo method = typeof(GameManager).GetMethod("DetermineNextCardPhaseAttacker", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null, "Expected GameManager turn-order helper to exist.");

            object result = method.Invoke(null, new object[] { Side.Player, Side.Enemy, Side.Enemy });

            Assert.That((Side)result, Is.EqualTo(Side.Enemy));
        }

        [Test]
        public void TurnOrder_AttackerWinKeepsNextCardPhaseOnAttacker()
        {
            MethodInfo method = typeof(GameManager).GetMethod("DetermineNextCardPhaseAttacker", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null, "Expected GameManager turn-order helper to exist.");

            object result = method.Invoke(null, new object[] { Side.Player, Side.Enemy, Side.Player });

            Assert.That((Side)result, Is.EqualTo(Side.Player));
        }

        [Test]
        public void ShootingRules_AttackerWinWithoutThreat_DoesNotStartShooting()
        {
            MethodInfo method = typeof(GameManager).GetMethod("ShouldStartShooting", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null, "Expected GameManager.ShouldStartShooting helper to exist.");

            object result = method.Invoke(null, new object[] { true, false });

            Assert.That((bool)result, Is.False);
        }

        [Test]
        public void ShootingRules_AttackerWinWithThreat_StartsShooting()
        {
            MethodInfo method = typeof(GameManager).GetMethod("ShouldStartShooting", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null, "Expected GameManager.ShouldStartShooting helper to exist.");

            object result = method.Invoke(null, new object[] { true, true });

            Assert.That((bool)result, Is.True);
        }

        [Test]
        public void ComputeFreeLookPitch_CursorUpProducesHigherPitchThanCursorDown()
        {
            float pitchAtTop = BCCameraController.ComputeFreeLookPitch(1f, 0.4f, 30f);
            float pitchAtCenter = BCCameraController.ComputeFreeLookPitch(0.5f, 0.4f, 30f);
            float pitchAtBottom = BCCameraController.ComputeFreeLookPitch(0f, 0.4f, 30f);

            Assert.That(pitchAtTop, Is.GreaterThan(pitchAtCenter));
            Assert.That(pitchAtCenter, Is.GreaterThan(pitchAtBottom));
        }

        [Test]
        public void SlotRules_NormalCardCanBePlacedIntoAnyEmptySlot()
        {
            Assert.That(SlotRules.CanPlaceCard(
                CardType.Resource,
                firstSlotCardType: null,
                secondSlotCardType: null,
                targetSlotIndex: 0), Is.True);

            Assert.That(SlotRules.CanPlaceCard(
                CardType.Threat,
                firstSlotCardType: null,
                secondSlotCardType: null,
                targetSlotIndex: 1), Is.True);
        }

        [Test]
        public void SlotRules_SpecialCardRequiresAtLeastOneNormalCardAlreadyPlaced()
        {
            Assert.That(SlotRules.CanPlaceCard(
                CardType.Special,
                firstSlotCardType: null,
                secondSlotCardType: null,
                targetSlotIndex: 0), Is.False);

            Assert.That(SlotRules.CanPlaceCard(
                CardType.Special,
                firstSlotCardType: CardType.Resource,
                secondSlotCardType: null,
                targetSlotIndex: 1), Is.True);
        }

        [Test]
        public void SlotRules_CannotPlaceIntoOccupiedSlot()
        {
            Assert.That(SlotRules.CanPlaceCard(
                CardType.Resource,
                firstSlotCardType: CardType.Resource,
                secondSlotCardType: null,
                targetSlotIndex: 0), Is.False);
        }
    }
}