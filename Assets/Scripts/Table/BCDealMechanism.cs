#pragma warning disable CS0649
using System.Collections;
using System.Collections.Generic;
using BreathCasino.Core;
using BreathCasino.Systems;
using UnityEngine;
using UnityEngine.Serialization;

namespace BreathCasino.Gameplay
{
    public class BCDealMechanism : MonoBehaviour
    {
        [Header("Shared lift block")]
        [SerializeField] private Transform liftRoot;

        [Header("Shelves facing each side")]
        [FormerlySerializedAs("playerDrawerRoot")]
        [SerializeField] private Transform playerShelfRoot;

        [FormerlySerializedAs("enemyDrawerRoot")]
        [SerializeField] private Transform enemyShelfRoot;

        [Header("Sockets")]
        [SerializeField] private Transform playerCardSocket;
        [SerializeField] private Transform enemyCardSocket;
        [HideInInspector, SerializeField] private Transform playerTicketSocket;
        [HideInInspector, SerializeField] private Transform enemyTicketSocket;
        [SerializeField] private Transform gunRestAnchor;
        [SerializeField] private Transform ticketAcceptSocket;
        [SerializeField] private Transform playerTicketAcceptSocket;
        [SerializeField] private Transform enemyTicketAcceptSocket;

        [Header("Visual motion hooks")]
        [SerializeField] private Transform[] gearRoots;
        [SerializeField] private bool autoAdoptShelfVisuals = false;

        [Header("Travel")]
        [SerializeField] private Vector3 liftHiddenOffset = new(0f, -0.22f, 0f);
        [SerializeField] private Vector3 liftRaisedLocalOffset = new(0f, 1.28f, 0f);
        [FormerlySerializedAs("playerDrawerHiddenOffset")]
        [SerializeField] private Vector3 playerShelfHiddenOffset = new(0f, 0f, -0.08f);
        [FormerlySerializedAs("enemyDrawerHiddenOffset")]
        [SerializeField] private Vector3 enemyShelfHiddenOffset = new(0f, 0f, 0.08f);
        [SerializeField] private Vector3 gunHiddenOffset = new(0f, -0.22f, 0f);
        [SerializeField] private float transitionDuration = 1.8f;
        [SerializeField, Range(0.2f, 0.8f)] private float liftPhasePortion = 0.58f;
        [SerializeField] private bool startRaisedOnPlay;
        [SerializeField] private bool useAbsoluteLiftWorldHeights = true;
        [SerializeField] private float hiddenLiftWorldY = 6.17f;
        [SerializeField] private float raisedLiftWorldY = 7.65f;

        [Header("Mechanical motion")]
        [SerializeField] private float horizontalSway = 0.012f;
        [SerializeField] private float forwardSway = 0.010f;
        [SerializeField] private float liftTiltDegrees = 1.8f;
        [SerializeField] private float shelfTiltDegrees = 2.4f;
        [SerializeField] private float settleDuration = 0.28f;
        [SerializeField] private float endJitterAmplitude = 0.0035f;
        [SerializeField] private float wobbleFrequency = 1.7f;
        [SerializeField] private float endJitterFrequency = 10f;

        [Header("Gears")]
        [SerializeField] private float gearDegreesPerCycle = 210f;
        [SerializeField] private float gearJitterDegrees = 7f;

        [Header("Ticket acceptor")]
        [SerializeField] private float ticketInsertDuration = 0.34f;
        [SerializeField] private float ticketInsertDepth = 0.11f;
        [SerializeField] private float ticketInsertPause = 0.06f;

        private const string PlayerShelfMotionRootName = "PlayerShelfMotionRoot";
        private const string EnemyShelfMotionRootName = "EnemyShelfMotionRoot";

        private Vector3 _liftClosedLocalPosition;
        private Vector3 _liftOpenLocalPosition;
        private Vector3 _playerShelfClosedLocalPosition;
        private Vector3 _playerShelfOpenLocalPosition;
        private Vector3 _enemyShelfClosedLocalPosition;
        private Vector3 _enemyShelfOpenLocalPosition;
        private Vector3 _gunClosedLocalPosition;
        private Vector3 _gunOpenLocalPosition;
        private Vector3 _liftClosedWorldPosition;

        private Quaternion _liftBaseLocalRotation;
        private Quaternion _playerShelfBaseLocalRotation;
        private Quaternion _enemyShelfBaseLocalRotation;
        private Quaternion _gunBaseLocalRotation;
        private Quaternion _liftClosedWorldRotation;
        private Quaternion[] _gearBaseLocalRotations;

        private bool _isConfigured;
        private bool _isRaised;

        public bool IsRaised => _isRaised;
        public bool StartRaisedOnPlay => startRaisedOnPlay;
        public Transform LiftRoot => liftRoot != null ? liftRoot : transform;
        public Transform PlayerShelfRoot => playerShelfRoot;
        public Transform EnemyShelfRoot => enemyShelfRoot;
        public Transform PlayerCardSocket => playerCardSocket != null ? playerCardSocket : playerShelfRoot;
        public Transform EnemyCardSocket => enemyCardSocket != null ? enemyCardSocket : enemyShelfRoot;
        public Transform PlayerTicketSocket => PlayerCardSocket;
        public Transform EnemyTicketSocket => EnemyCardSocket;
        public Transform GunRestAnchor => gunRestAnchor;
        public Transform TicketAcceptSocket => ticketAcceptSocket;
        public Transform PlayerTicketAcceptSocket => playerTicketAcceptSocket != null ? playerTicketAcceptSocket : ticketAcceptSocket;
        public Transform EnemyTicketAcceptSocket => enemyTicketAcceptSocket != null ? enemyTicketAcceptSocket : ticketAcceptSocket;
        public IReadOnlyList<Transform> GearRoots => gearRoots;
        public int ConfigurationScore
        {
            get
            {
                int score = 0;
                if (liftRoot != null) score += 5;
                if (playerShelfRoot != null) score += 4;
                if (enemyShelfRoot != null) score += 4;
                if (playerCardSocket != null) score += 3;
                if (enemyCardSocket != null) score += 3;
                if (PlayerTicketSocket != null) score += 2;
                if (EnemyTicketSocket != null) score += 2;
                if (gunRestAnchor != null) score += 2;
                if (playerTicketAcceptSocket != null) score += 2;
                if (enemyTicketAcceptSocket != null) score += 2;
                if (ticketAcceptSocket != null) score += 1;
                if (gearRoots != null) score += gearRoots.Length;
                return score;
            }
        }

        public void EnsureScaffold(
            Transform tableRoot,
            Transform fallbackPlayerSocket,
            Transform fallbackEnemySocket,
            Transform fallbackGunAnchor,
            Transform fallbackTicketAcceptSpot)
        {
            if (playerCardSocket == null)
            {
                playerCardSocket = fallbackPlayerSocket;
            }

            if (enemyCardSocket == null)
            {
                enemyCardSocket = fallbackEnemySocket;
            }

            if (gunRestAnchor == null)
            {
                gunRestAnchor = fallbackGunAnchor;
            }

            if (ticketAcceptSocket == null)
            {
                ticketAcceptSocket = fallbackTicketAcceptSpot;
            }

            Transform scaffoldRoot = tableRoot != null ? tableRoot : transform;
            if (liftRoot == null)
            {
                liftRoot = EnsureChildTransform(scaffoldRoot, "LiftRoot", new Vector3(0f, 0.12f, 0f), Quaternion.identity);
            }

            if (playerShelfRoot == null)
            {
                playerShelfRoot = playerCardSocket != null && playerCardSocket.parent != null
                    ? playerCardSocket.parent
                    : EnsureChildTransform(liftRoot, "PlayerShelfRoot", new Vector3(0f, 0f, 0.22f), Quaternion.identity);
            }

            if (enemyShelfRoot == null)
            {
                enemyShelfRoot = enemyCardSocket != null && enemyCardSocket.parent != null
                    ? enemyCardSocket.parent
                    : EnsureChildTransform(liftRoot, "EnemyShelfRoot", new Vector3(0f, 0f, -0.22f), Quaternion.identity);
            }

            if (playerCardSocket == null)
            {
                playerCardSocket = EnsureChildTransform(playerShelfRoot, "PlayerCardSocket", new Vector3(0f, 0.012f, 0f), Quaternion.identity);
            }

            if (enemyCardSocket == null)
            {
                enemyCardSocket = EnsureChildTransform(enemyShelfRoot, "EnemyCardSocket", new Vector3(0f, 0.012f, 0f), Quaternion.identity);
            }

            if (playerTicketSocket == null)
            {
                playerTicketSocket = playerCardSocket;
            }

            if (enemyTicketSocket == null)
            {
                enemyTicketSocket = enemyCardSocket;
            }

            if (gunRestAnchor == null)
            {
                gunRestAnchor = EnsureChildTransform(liftRoot, "GunRestAnchor", new Vector3(0f, 0.11f, 0f), Quaternion.identity);
            }

            if (playerTicketAcceptSocket == null)
            {
                Quaternion playerAcceptRotation = Quaternion.LookRotation(GetAcceptForward(true), Vector3.up);
                playerTicketAcceptSocket = EnsureChildTransform(playerShelfRoot, "PlayerTicketAcceptSocket", new Vector3(0.18f, 0.015f, 0f), playerAcceptRotation);
            }

            if (enemyTicketAcceptSocket == null)
            {
                Quaternion enemyAcceptRotation = Quaternion.LookRotation(GetAcceptForward(false), Vector3.up);
                enemyTicketAcceptSocket = EnsureChildTransform(enemyShelfRoot, "EnemyTicketAcceptSocket", new Vector3(-0.18f, 0.015f, 0f), enemyAcceptRotation);
            }

            if (ticketAcceptSocket == null)
            {
                ticketAcceptSocket = playerTicketAcceptSocket;
            }

            EnsureShelfMotionRoots();
            NormalizeRuntimeAnchors(fallbackPlayerSocket, fallbackEnemySocket, fallbackGunAnchor, fallbackTicketAcceptSpot);
        }

        public string DescribeHierarchyIssues()
        {
            List<string> issues = new List<string>();

            if (liftRoot == null)
            {
                issues.Add("LiftRoot is missing.");
            }

            if (playerShelfRoot == null)
            {
                issues.Add("PlayerShelfRoot is missing.");
            }

            if (enemyShelfRoot == null)
            {
                issues.Add("EnemyShelfRoot is missing.");
            }

            if (playerShelfRoot != null && enemyShelfRoot != null)
            {
                if (playerShelfRoot.IsChildOf(enemyShelfRoot))
                {
                    issues.Add("PlayerShelfRoot is nested inside EnemyShelfRoot.");
                }

                if (enemyShelfRoot.IsChildOf(playerShelfRoot))
                {
                    issues.Add("EnemyShelfRoot is nested inside PlayerShelfRoot.");
                }
            }

            if (playerCardSocket == null)
            {
                issues.Add("PlayerCardSocket is missing.");
            }
            else if (playerShelfRoot != null && playerCardSocket.parent != playerShelfRoot)
            {
                issues.Add("PlayerCardSocket should be a direct child of PlayerShelfRoot.");
            }

            if (enemyCardSocket == null)
            {
                issues.Add("EnemyCardSocket is missing.");
            }
            else if (enemyShelfRoot != null && enemyCardSocket.parent != enemyShelfRoot)
            {
                issues.Add("EnemyCardSocket should be a direct child of EnemyShelfRoot.");
            }

            return string.Join(" ", issues);
        }

        public void Initialize(Transform fallbackPlayerSocket, Transform fallbackEnemySocket, Transform fallbackGunAnchor)
        {
            if (playerCardSocket == null)
            {
                playerCardSocket = fallbackPlayerSocket;
            }

            if (enemyCardSocket == null)
            {
                enemyCardSocket = fallbackEnemySocket;
            }

            if (gunRestAnchor == null)
            {
                gunRestAnchor = fallbackGunAnchor;
            }

            if (liftRoot == null)
            {
                liftRoot = transform;
            }

            if (playerShelfRoot == null)
            {
                playerShelfRoot = playerCardSocket != null && playerCardSocket.parent != null
                    ? playerCardSocket.parent
                    : playerCardSocket;
            }

            if (enemyShelfRoot == null)
            {
                enemyShelfRoot = enemyCardSocket != null && enemyCardSocket.parent != null
                    ? enemyCardSocket.parent
                    : enemyCardSocket;
            }

            if (playerCardSocket == null)
            {
                playerCardSocket = playerShelfRoot;
            }

            if (enemyCardSocket == null)
            {
                enemyCardSocket = enemyShelfRoot;
            }

            if (playerTicketSocket == null)
            {
                playerTicketSocket = playerCardSocket;
            }

            if (enemyTicketSocket == null)
            {
                enemyTicketSocket = enemyCardSocket;
            }

            if (playerTicketAcceptSocket == null)
            {
                playerTicketAcceptSocket = ticketAcceptSocket;
            }

            if (enemyTicketAcceptSocket == null)
            {
                enemyTicketAcceptSocket = ticketAcceptSocket;
            }

            EnsureShelfMotionRoots();
            NormalizeRuntimeAnchors(fallbackPlayerSocket, fallbackEnemySocket, fallbackGunAnchor, ticketAcceptSocket);
            CacheReferencePose();
            _isConfigured = true;
        }

        public void Snap(bool raised)
        {
            if (!_isConfigured)
            {
                return;
            }

            float weight = raised ? 1f : 0f;
            ApplyPose(weight, weight, 0f, raised ? 1f : -1f, -1f);
            _isRaised = raised;
        }

        public IEnumerator Animate(bool raised)
        {
            if (!_isConfigured || _isRaised == raised)
            {
                yield break;
            }

            if (raised)
            {
                BCAudioManager.Instance?.PlayMechanismRise();
            }
            else
            {
                BCAudioManager.Instance?.PlayMechanismLower();
            }

            float motionDuration = Mathf.Max(transitionDuration, 2.35f);
            float settleTime = Mathf.Max(settleDuration, 0.24f);
            float startWeight = _isRaised ? 1f : 0f;
            float endWeight = raised ? 1f : 0f;
            float direction = raised ? 1f : -1f;
            float elapsed = 0f;

            while (elapsed < motionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(motionDuration, 0.01f));
                EvaluateTravelWeights(raised, t, out float liftWeight, out float shelfWeight);
                liftWeight = Mathf.Lerp(startWeight, endWeight, liftWeight);
                shelfWeight = Mathf.Lerp(startWeight, endWeight, shelfWeight);
                ApplyPose(liftWeight, shelfWeight, t, direction, -1f);
                yield return null;
            }

            float settleElapsed = 0f;
            while (settleElapsed < settleTime)
            {
                settleElapsed += Time.deltaTime;
                float settleT = Mathf.Clamp01(settleElapsed / Mathf.Max(settleTime, 0.01f));
                ApplyPose(endWeight, endWeight, 1f, direction, settleT);
                yield return null;
            }

            ApplyPose(endWeight, endWeight, 1f, direction, 1f);
            _isRaised = raised;
        }

        private void CacheReferencePose()
        {
            if (liftRoot != null)
            {
                _liftClosedLocalPosition = liftRoot.localPosition;
                _liftBaseLocalRotation = liftRoot.localRotation;
                _liftClosedWorldRotation = liftRoot.rotation;
                Vector3 resolvedRaisedOffset = ResolveLiftRaisedOffset();
                _liftOpenLocalPosition = _liftClosedLocalPosition + resolvedRaisedOffset;

                Transform parent = liftRoot.parent;
                _liftClosedWorldPosition = parent != null
                    ? parent.TransformPoint(_liftClosedLocalPosition)
                    : _liftClosedLocalPosition;
            }

            if (playerShelfRoot != null)
            {
                _playerShelfClosedLocalPosition = playerShelfRoot.localPosition;
                _playerShelfOpenLocalPosition = ResolveVisibleLocalPosition(_playerShelfClosedLocalPosition, playerShelfHiddenOffset);
                _playerShelfBaseLocalRotation = playerShelfRoot.localRotation;
            }

            if (enemyShelfRoot != null)
            {
                _enemyShelfClosedLocalPosition = enemyShelfRoot.localPosition;
                _enemyShelfOpenLocalPosition = ResolveVisibleLocalPosition(_enemyShelfClosedLocalPosition, enemyShelfHiddenOffset);
                _enemyShelfBaseLocalRotation = enemyShelfRoot.localRotation;
            }

            if (gunRestAnchor != null)
            {
                _gunClosedLocalPosition = gunRestAnchor.localPosition;
                _gunOpenLocalPosition = ResolveVisibleLocalPosition(_gunClosedLocalPosition, gunHiddenOffset);
                _gunBaseLocalRotation = gunRestAnchor.localRotation;
            }

            if (gearRoots == null)
            {
                _gearBaseLocalRotations = null;
                return;
            }

            if (_gearBaseLocalRotations == null || _gearBaseLocalRotations.Length != gearRoots.Length)
            {
                _gearBaseLocalRotations = new Quaternion[gearRoots.Length];
            }

            for (int i = 0; i < gearRoots.Length; i++)
            {
                if (gearRoots[i] != null)
                {
                    _gearBaseLocalRotations[i] = gearRoots[i].localRotation;
                }
            }
        }

        private void EvaluateTravelWeights(bool opening, float t, out float liftWeight, out float shelfWeight)
        {
            float split = Mathf.Clamp(liftPhasePortion, 0.2f, 0.85f);
            float shelfPhase = Mathf.Max(1f - split, 0.01f);

            if (opening)
            {
                float liftT = Mathf.Clamp01(t / split);
                float shelfT = Mathf.Clamp01((t - split) / shelfPhase);
                liftWeight = EaseInOutCubic(liftT);
                shelfWeight = EaseInOutCubic(shelfT);
                return;
            }

            float closeShelfT = Mathf.Clamp01(t / shelfPhase);
            float lowerLiftT = Mathf.Clamp01((t - shelfPhase) / split);
            shelfWeight = 1f - EaseInOutCubic(closeShelfT);
            liftWeight = 1f - EaseInOutCubic(lowerLiftT);
        }

        private void ApplyPose(float liftWeight, float shelfWeight, float travelAlpha, float directionSign, float settleAlpha)
        {
            float clampedTravel = Mathf.Clamp01(travelAlpha);
            float swayWave = Mathf.Sin(clampedTravel * Mathf.PI * wobbleFrequency);
            float driftWave = Mathf.Cos(clampedTravel * Mathf.PI * (wobbleFrequency * 0.5f) + 0.65f);

            float liftEnvelope = Mathf.Sin(Mathf.Clamp01(liftWeight) * Mathf.PI);

            Vector3 liftOffset = new Vector3(
                horizontalSway * liftEnvelope * swayWave,
                0f,
                forwardSway * liftEnvelope * driftWave * directionSign);

            float liftRoll = liftTiltDegrees * liftEnvelope * driftWave * directionSign;
            float shelfRoll = shelfTiltDegrees * liftEnvelope * swayWave * 0.35f;

            if (settleAlpha >= 0f)
            {
                float damp = 1f - Mathf.Clamp01(settleAlpha);
                float settleWave = Mathf.Sin(settleAlpha * Mathf.PI * endJitterFrequency);
                float settleWaveQuarter = Mathf.Cos(settleAlpha * Mathf.PI * (endJitterFrequency * 0.5f));

                Vector3 settleOffset = new Vector3(
                    endJitterAmplitude * 0.55f * damp * settleWave,
                    endJitterAmplitude * 0.85f * damp * settleWaveQuarter,
                    endJitterAmplitude * damp * settleWave * directionSign);

                liftOffset += settleOffset;
                liftRoll += settleWave * damp * liftTiltDegrees * 0.85f;
                shelfRoll += settleWaveQuarter * damp * shelfTiltDegrees * 0.28f;
            }

            ApplyAnchorBetween(
                liftRoot,
                _liftClosedLocalPosition,
                _liftOpenLocalPosition,
                _liftBaseLocalRotation,
                Quaternion.Euler(0f, 0f, liftRoll),
                liftWeight,
                liftOffset);

            ApplyAnchorBetween(
                playerShelfRoot,
                _playerShelfClosedLocalPosition,
                _playerShelfOpenLocalPosition,
                _playerShelfBaseLocalRotation,
                Quaternion.Euler(shelfRoll * 0.18f, 0f, shelfRoll),
                shelfWeight,
                Vector3.zero);

            ApplyAnchorBetween(
                enemyShelfRoot,
                _enemyShelfClosedLocalPosition,
                _enemyShelfOpenLocalPosition,
                _enemyShelfBaseLocalRotation,
                Quaternion.Euler(-shelfRoll * 0.18f, 0f, -shelfRoll),
                shelfWeight,
                Vector3.zero);

            ApplyAnchorBetween(
                gunRestAnchor,
                _gunClosedLocalPosition,
                _gunOpenLocalPosition,
                _gunBaseLocalRotation,
                Quaternion.Euler(shelfRoll * 0.1f, liftRoll * 0.08f, -shelfRoll * 0.12f),
                liftWeight,
                liftOffset * 0.95f);

            ApplyGears(liftWeight, shelfWeight, clampedTravel, settleAlpha);
        }

        private void ApplyAnchorBetween(
            Transform anchor,
            Vector3 closedLocalPosition,
            Vector3 openLocalPosition,
            Quaternion baseLocalRotation,
            Quaternion motionRotation,
            float weight,
            Vector3 motionOffset)
        {
            if (anchor == null)
            {
                return;
            }

            anchor.localPosition = Vector3.LerpUnclamped(closedLocalPosition, openLocalPosition, weight) + motionOffset;
            anchor.localRotation = baseLocalRotation * motionRotation;
        }

        private void ApplyGears(float liftWeight, float shelfWeight, float travelAlpha, float settleAlpha)
        {
            if (gearRoots == null || _gearBaseLocalRotations == null)
            {
                return;
            }

            float spin = (liftWeight * 0.65f + shelfWeight * 0.35f) * gearDegreesPerCycle;
            spin += Mathf.Sin(travelAlpha * Mathf.PI * wobbleFrequency) * gearJitterDegrees;

            if (settleAlpha >= 0f)
            {
                float damp = 1f - Mathf.Clamp01(settleAlpha);
                spin += Mathf.Sin(settleAlpha * Mathf.PI * endJitterFrequency) * gearJitterDegrees * damp;
            }

            for (int i = 0; i < gearRoots.Length; i++)
            {
                Transform gear = gearRoots[i];
                if (gear == null)
                {
                    continue;
                }

                float direction = (i % 2 == 0) ? 1f : -1f;
                gear.localRotation = _gearBaseLocalRotations[i] * Quaternion.Euler(0f, 0f, spin * direction);
            }
        }

        public Transform GetTicketAcceptSocket(Side side)
        {
            return side == Side.Player ? PlayerTicketAcceptSocket : EnemyTicketAcceptSocket;
        }

        public IEnumerator AnimateTicketInsertion(BCTicketDisplay ticket, Side side)
        {
            if (ticket == null)
            {
                yield break;
            }

            Transform socket = GetTicketAcceptSocket(side);
            if (socket == null)
            {
                yield break;
            }

            Vector3 startPosition = ticket.transform.position;
            Quaternion startRotation = ticket.transform.rotation;
            Vector3 entryPosition = socket.position;
            Quaternion entryRotation = socket.rotation;
            Vector3 insertedPosition = entryPosition + socket.forward * ticketInsertDepth;

            float approachDuration = Mathf.Max(ticketInsertDuration * 0.68f, 0.01f);
            float sinkDuration = Mathf.Max(ticketInsertDuration * 0.32f, 0.01f);

            yield return AnimateTicketSegment(ticket.transform, startPosition, entryPosition, startRotation, entryRotation, approachDuration);
            yield return AnimateTicketSegment(ticket.transform, entryPosition, insertedPosition, entryRotation, entryRotation, sinkDuration);

            if (ticketInsertPause > 0f)
            {
                yield return new WaitForSeconds(ticketInsertPause);
            }
        }

        private static IEnumerator AnimateTicketSegment(
            Transform ticketTransform,
            Vector3 startPosition,
            Vector3 endPosition,
            Quaternion startRotation,
            Quaternion endRotation,
            float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                ticketTransform.position = Vector3.Lerp(startPosition, endPosition, eased);
                ticketTransform.rotation = Quaternion.Slerp(startRotation, endRotation, eased);
                yield return null;
            }

            ticketTransform.position = endPosition;
            ticketTransform.rotation = endRotation;
        }

        private static float EaseOutBack(float t)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            float value = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            return Mathf.Clamp(value, -0.12f, 1.12f);
        }

        private static float EaseInBack(float t)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            float value = c3 * t * t * t - c1 * t * t;
            return Mathf.Clamp(value, -0.12f, 1.12f);
        }

        private static float EaseInOutCubic(float t)
        {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        private Vector3 GetAcceptForward(bool isPlayerSide)
        {
            Vector3 forward = isPlayerSide ? playerShelfHiddenOffset : enemyShelfHiddenOffset;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = isPlayerSide ? Vector3.back : Vector3.forward;
            }

            return forward.normalized;
        }

        private Vector3 ResolveLiftRaisedOffset()
        {
            if (liftRoot == null)
            {
                return liftRaisedLocalOffset;
            }

            if (liftRaisedLocalOffset.sqrMagnitude > 0.0001f)
            {
                return liftRaisedLocalOffset;
            }

            if (!useAbsoluteLiftWorldHeights)
            {
                return -liftHiddenOffset;
            }

            Transform parent = liftRoot.parent;
            Vector3 currentWorldPosition = liftRoot.position;
            Vector3 closedWorldPosition = new Vector3(currentWorldPosition.x, hiddenLiftWorldY, currentWorldPosition.z);
            Vector3 openWorldPosition = new Vector3(currentWorldPosition.x, raisedLiftWorldY, currentWorldPosition.z);

            if (parent == null)
            {
                return openWorldPosition - closedWorldPosition;
            }

            Vector3 closedLocalPosition = parent.InverseTransformPoint(closedWorldPosition);
            Vector3 openLocalPosition = parent.InverseTransformPoint(openWorldPosition);
            return openLocalPosition - closedLocalPosition;
        }

        private static Vector3 ResolveVisibleLocalPosition(Vector3 hiddenLocalPosition, Vector3 hiddenOffset)
        {
            if (hiddenOffset.sqrMagnitude <= 0.0001f)
            {
                return hiddenLocalPosition;
            }

            return hiddenLocalPosition - hiddenOffset;
        }

        private static Transform EnsureChildTransform(Transform parent, string childName, Vector3 localPosition, Quaternion localRotation)
        {
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.Find(childName);
            if (child == null)
            {
                GameObject childObject = new GameObject(childName);
                child = childObject.transform;
                child.SetParent(parent, false);
            }

            child.localPosition = localPosition;
            child.localRotation = localRotation;
            return child;
        }

        private void NormalizeRuntimeAnchors(
            Transform fallbackPlayerSocket,
            Transform fallbackEnemySocket,
            Transform fallbackGunAnchor,
            Transform fallbackTicketAcceptSpot)
        {
            int repairs = 0;

            repairs += EnsureUnifiedIssueAnchor(ref playerCardSocket, playerShelfRoot, "PlayerIssueSocket", new Vector3(0f, 0.012f, 0f), fallbackPlayerSocket);
            repairs += EnsureUnifiedIssueAnchor(ref enemyCardSocket, enemyShelfRoot, "EnemyIssueSocket", new Vector3(0f, 0.012f, 0f), fallbackEnemySocket);

            playerTicketSocket = playerCardSocket;
            enemyTicketSocket = enemyCardSocket;

            if (playerTicketAcceptSocket == playerCardSocket || playerTicketAcceptSocket == playerTicketSocket)
            {
                playerTicketAcceptSocket = null;
            }

            if (enemyTicketAcceptSocket == enemyCardSocket || enemyTicketAcceptSocket == enemyTicketSocket)
            {
                enemyTicketAcceptSocket = null;
            }

            Quaternion playerAcceptRotation = Quaternion.LookRotation(GetAcceptForward(true), Vector3.up);
            Quaternion enemyAcceptRotation = Quaternion.LookRotation(GetAcceptForward(false), Vector3.up);
            repairs += EnsureDedicatedAnchor(ref playerTicketAcceptSocket, playerShelfRoot, "PlayerTicketAcceptSocket", new Vector3(0.18f, 0.015f, 0f), playerAcceptRotation, fallbackTicketAcceptSpot, false, playerCardSocket, playerTicketSocket);
            repairs += EnsureDedicatedAnchor(ref enemyTicketAcceptSocket, enemyShelfRoot, "EnemyTicketAcceptSocket", new Vector3(-0.18f, 0.015f, 0f), enemyAcceptRotation, fallbackTicketAcceptSpot, false, enemyCardSocket, enemyTicketSocket);
            repairs += EnsureDedicatedAnchor(ref gunRestAnchor, liftRoot, "GunRestAnchor", new Vector3(0f, 0.11f, 0f), Quaternion.identity, fallbackGunAnchor, false);

            if (ticketAcceptSocket == null)
            {
                ticketAcceptSocket = playerTicketAcceptSocket;
            }

            if (repairs > 0)
            {
                Debug.Log($"[BCDealMechanism] Auto-repaired {repairs} hierarchy link(s) on {name}.");
            }
        }

        private void EnsureShelfMotionRoots()
        {
            if (liftRoot == null)
            {
                return;
            }

            playerShelfRoot = EnsureShelfMotionRoot(playerShelfRoot, PlayerShelfMotionRootName);
            enemyShelfRoot = EnsureShelfMotionRoot(enemyShelfRoot, EnemyShelfMotionRootName);
            NormalizeMotionRootPose(playerShelfRoot);
            NormalizeMotionRootPose(enemyShelfRoot);
        }

        private Transform EnsureShelfMotionRoot(Transform visualShelfRoot, string motionRootName)
        {
            if (visualShelfRoot == null || liftRoot == null)
            {
                return visualShelfRoot;
            }

            if (visualShelfRoot.parent == liftRoot && visualShelfRoot.name == motionRootName)
            {
                return visualShelfRoot;
            }

            if (visualShelfRoot.parent != null &&
                visualShelfRoot.parent.parent == liftRoot &&
                visualShelfRoot.parent.name == motionRootName)
            {
                return visualShelfRoot.parent;
            }

            Transform motionRoot = liftRoot.Find(motionRootName);
            if (motionRoot == null)
            {
                GameObject motionRootObject = new GameObject(motionRootName);
                motionRoot = motionRootObject.transform;
                motionRoot.SetParent(liftRoot, false);
            }

            motionRoot.position = visualShelfRoot.position;
            motionRoot.rotation = visualShelfRoot.rotation;
            motionRoot.localScale = Vector3.one;

            if (visualShelfRoot.parent != motionRoot)
            {
                visualShelfRoot.SetParent(motionRoot, true);
            }

            return motionRoot;
        }

        private void NormalizeMotionRootPose(Transform motionRoot)
        {
            if (motionRoot == null || liftRoot == null)
            {
                return;
            }

            if (motionRoot.parent != liftRoot)
            {
                motionRoot.SetParent(liftRoot, true);
            }

            if (motionRoot.localScale != Vector3.one)
            {
                motionRoot.localScale = Vector3.one;
            }

            if (Quaternion.Angle(motionRoot.localRotation, Quaternion.identity) > 0.01f)
            {
                motionRoot.localRotation = Quaternion.identity;
            }
        }

        private static int EnsureUnifiedIssueAnchor(
            ref Transform anchor,
            Transform expectedParent,
            string childName,
            Vector3 defaultLocalPosition,
            Transform worldReference)
        {
            if (expectedParent == null)
            {
                return 0;
            }

            if (!NeedsNormalizedIssueAnchor(anchor, expectedParent))
            {
                return 0;
            }

            Transform existing = expectedParent.Find(childName);
            if (existing != null)
            {
                existing.localPosition = defaultLocalPosition;
                existing.localRotation = Quaternion.identity;
                existing.localScale = Vector3.one;
                anchor = existing;
                return 1;
            }

            GameObject anchorObject = new GameObject(childName);
            Transform child = anchorObject.transform;
            child.SetParent(expectedParent, false);
            child.localPosition = defaultLocalPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;

            Transform worldPoseSource = anchor != null ? anchor : worldReference;
            if (worldPoseSource != null)
            {
                child.localPosition = expectedParent.InverseTransformPoint(worldPoseSource.position);
            }

            anchor = child;
            return 1;
        }

        private static bool NeedsNormalizedIssueAnchor(Transform anchor, Transform expectedParent)
        {
            if (anchor == null || expectedParent == null)
            {
                return true;
            }

            if (!anchor.IsChildOf(expectedParent))
            {
                return true;
            }

            Vector3 scale = anchor.localScale;
            bool suspiciousScale = scale.x < 0.25f || scale.y < 0.25f || scale.z < 0.25f || scale.x > 4f || scale.y > 4f || scale.z > 4f;
            if (suspiciousScale)
            {
                return true;
            }

            if (Quaternion.Angle(anchor.localRotation, Quaternion.identity) > 15f)
            {
                return true;
            }

            return false;
        }

        private void TryAutoAdoptShelfVisuals()
        {
            if (!autoAdoptShelfVisuals || liftRoot == null || playerShelfRoot == null || enemyShelfRoot == null)
            {
                return;
            }

            if (CountDirectVisualChildren(playerShelfRoot) > 0 && CountDirectVisualChildren(enemyShelfRoot) > 0)
            {
                return;
            }

            int adoptedPlayer = 0;
            int adoptedEnemy = 0;

            List<Transform> candidates = new List<Transform>();
            for (int i = 0; i < liftRoot.childCount; i++)
            {
                Transform child = liftRoot.GetChild(i);
                if (child == playerShelfRoot || child == enemyShelfRoot)
                {
                    continue;
                }

                if (!IsShelfVisualCandidate(child))
                {
                    continue;
                }

                candidates.Add(child);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                Transform candidate = candidates[i];
                float playerDistance = Vector3.Distance(candidate.position, playerShelfRoot.position);
                float enemyDistance = Vector3.Distance(candidate.position, enemyShelfRoot.position);
                Transform target = playerDistance <= enemyDistance ? playerShelfRoot : enemyShelfRoot;
                candidate.SetParent(target, true);

                if (target == playerShelfRoot)
                {
                    adoptedPlayer++;
                }
                else
                {
                    adoptedEnemy++;
                }
            }

            if (adoptedPlayer > 0 || adoptedEnemy > 0)
            {
                Debug.Log($"[BCDealMechanism] Auto-adopted shelf visuals on {name}. Player:{adoptedPlayer} Enemy:{adoptedEnemy}");
            }
        }

        private static bool IsShelfVisualCandidate(Transform child)
        {
            if (child == null || child.GetComponent<Renderer>() == null)
            {
                return false;
            }

            string nameLower = child.name.ToLowerInvariant();
            if (!nameLower.StartsWith("cube"))
            {
                return false;
            }

            if (nameLower.Contains("socket") || nameLower.Contains("slot") || nameLower.Contains("ticket") || nameLower.Contains("gun"))
            {
                return false;
            }

            return true;
        }

        private static int CountDirectVisualChildren(Transform parent)
        {
            if (parent == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).GetComponent<Renderer>() != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int EnsureDedicatedAnchor(
            ref Transform anchor,
            Transform expectedParent,
            string childName,
            Vector3 defaultLocalPosition,
            Quaternion defaultLocalRotation,
            Transform worldReference,
            bool preserveWorldPosition,
            params Transform[] distinctFrom)
        {
            if (expectedParent == null)
            {
                return 0;
            }

            if (!NeedsDedicatedAnchor(anchor, expectedParent, distinctFrom))
            {
                return 0;
            }

            Transform existing = expectedParent.Find(childName);
            if (existing != null)
            {
                anchor = existing;
                return 0;
            }

            GameObject childObject = new GameObject(childName);
            Transform child = childObject.transform;
            child.SetParent(expectedParent, false);

            if (preserveWorldPosition)
            {
                Vector3 worldPosition;
                Quaternion worldRotation;

                if (anchor != null)
                {
                    worldPosition = anchor.position;
                    worldRotation = anchor.rotation;
                }
                else if (worldReference != null)
                {
                    worldPosition = worldReference.position;
                    worldRotation = worldReference.rotation;
                }
                else
                {
                    worldPosition = expectedParent.TransformPoint(defaultLocalPosition);
                    worldRotation = expectedParent.rotation * defaultLocalRotation;
                }

                child.position = worldPosition;
                child.rotation = worldRotation;
            }
            else
            {
                child.localPosition = defaultLocalPosition;
                child.localRotation = defaultLocalRotation;
            }

            child.localScale = Vector3.one;
            anchor = child;
            return 1;
        }

        private static bool NeedsDedicatedAnchor(Transform anchor, Transform expectedParent, params Transform[] distinctFrom)
        {
            if (anchor == null || expectedParent == null)
            {
                return true;
            }

            if (!anchor.IsChildOf(expectedParent))
            {
                return true;
            }

            if (distinctFrom != null)
            {
                for (int i = 0; i < distinctFrom.Length; i++)
                {
                    if (anchor == distinctFrom[i] && distinctFrom[i] != null)
                    {
                        return true;
                    }
                }
            }

            Vector3 scale = anchor.localScale;
            bool suspiciousScale = scale.x < 0.25f || scale.y < 0.25f || scale.z < 0.25f || scale.x > 4f || scale.y > 4f || scale.z > 4f;
            if (suspiciousScale)
            {
                return true;
            }

            if (Quaternion.Angle(anchor.localRotation, Quaternion.identity) > 15f)
            {
                return true;
            }

            return false;
        }
    }
}
#pragma warning restore CS0649
