using UnityEngine;
#pragma warning disable CS0649
using BreathCasino.Core;
using BreathCasino.Gameplay;
using BreathCasino.Rendering;
using UnityEngine.InputSystem;

namespace BreathCasino.Core
{
    public class BCCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform tableCenter;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Transform playerHandHolder;
        [SerializeField] private Transform playerMainSlot;
        [SerializeField] private Transform enemyMainSlot;

        [Header("Camera Placeholders (опционально)")]
        [Tooltip("Если задано — камера использует position и rotation пустышки. Например, rotation X=-90 у пустышки даст камере X=-90. Создаются через Setup Helper → Create Camera Placeholders.")]
        [SerializeField] private Transform cardsPlaceholder;
        [SerializeField] private Transform playerSlotsPlaceholder;
        [SerializeField] private Transform tableSlotsPlaceholder;
        [SerializeField] private Transform enemySlotsPlaceholder;

        [Header("First Person")]
        [SerializeField] private Vector3 eyeOffset = new(0f, 0.42f, 0.05f);
        [SerializeField] private float cursorSensitivity = 0.4f;
        [SerializeField] private float maxYaw = 40f;
        [SerializeField] private float maxPitch = 30f;

        [Header("State Positions (камера всегда со стороны игрока)")]
        [Tooltip("Cards: позиция для вида на карты в руке. Смотрим на handHolder.")]
        [SerializeField] private Vector3 cardsViewOffset = new(0f, 0.02f, 0.34f);
        [SerializeField] private Vector3 playerSlotsViewOffset = new(0f, 0.16f, 0.62f);
        [SerializeField] private Vector3 tableSlotsViewOffset = new(0f, 0.62f, 0.1f);
        [Tooltip("EnemySlots: позиция между игроком и столом, смотрим НА врага.")]
        [SerializeField] private Vector3 enemySlotsViewOffset = new(0f, 0.62f, -0.55f);

        [Header("Smoothing")]
        [SerializeField] private float smoothTime = 0.12f;
        [SerializeField] private float stateTransitionTime = 0.15f;
        [Tooltip("Блок между переключениями (сек).")]
        [SerializeField] private float switchCooldownSeconds = 1f;

        [Header("Camera Effects")]
        [SerializeField] private float shotShakeIntensity = 0.15f;
        [SerializeField] private float shotShakeDuration = 0.2f;
        [SerializeField] private float damageShakeIntensity = 0.25f;
        [SerializeField] private float damageShakeDuration = 0.3f;
        [SerializeField] private float deathEffectDuration = 1.5f;
        [SerializeField] private float shakeFrequency = 25f;

        private CameraState _state = CameraState.Free;
        private float _currentYaw;
        private float _currentPitch;
        private float _yawVelocity;
        private float _pitchVelocity;
        private Vector3 _positionVelocity;
        private Quaternion _rotationVelocity;
        private float _stateTransitionProgress = 1f;
        private Vector3 _stateStartPos;
        private Quaternion _stateStartRot;
        private Vector3 _cachedTargetPos;
        private Quaternion _cachedTargetRot;
        private CameraState _stateTarget;
        private float _switchBlockUntil;

        // Camera shake state
        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeTimer;
        private Vector3 _shakeOffset;

        // Death effect state
        private bool _deathEffectActive;
        private float _deathEffectTimer;
        private Vector3 _deathEffectStartPos;
        private Quaternion _deathEffectStartRot;
        private bool _inputLocked;
        private bool _playerHandVisible = true;

        public CameraState State => _state;
        public CameraState TargetState => _stateTarget;

        public void SetInputLocked(bool isLocked)
        {
            _inputLocked = isLocked;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        /// <summary>
        /// Вычисляет pitch камеры (градусы) по нормализованной Y-позиции курсора.
        /// cursorY01 = 0 — низ экрана, 1 — верх. Результат: выше курсор → выше pitch.
        /// </summary>
        public static float ComputeFreeLookPitch(float cursorY01, float sensitivity, float maxPitch)
        {
            float mouseY = cursorY01 - 0.5f;
            float raw = 15f + mouseY * sensitivity * maxPitch * 2f;
            return Mathf.Clamp(raw, 15f - maxPitch * 0.6f, 15f + maxPitch * 0.6f);
        }

        public void SetTableCenter(Transform center) => tableCenter = center;
        public void SetPlayerRoot(Transform player) => playerRoot = player;
        public void SetPlayerHandHolder(Transform h) => playerHandHolder = h;
        public void SetPlayerMainSlot(Transform s) => playerMainSlot = s;
        public void SetEnemyMainSlot(Transform s) => enemyMainSlot = s;

        /// <summary>
        /// Вызывается при выстреле из пистолета — короткий recoil shake.
        /// </summary>
        public void ShakeOnShot()
        {
            TriggerShake(shotShakeIntensity, shotShakeDuration);
            BCCameraGrainFeature.TriggerShotPulse();
        }

        /// <summary>
        /// Вызывается при получении урона — средний shake.
        /// </summary>
        public void ShakeOnDamage()
        {
            TriggerShake(damageShakeIntensity, damageShakeDuration);
            BCCameraGrainFeature.TriggerDamagePulse();
        }

        /// <summary>
        /// Вызывается при смерти игрока или врага — драматический эффект.
        /// </summary>
        public void EffectOnDeath()
        {
            _deathEffectActive = true;
            _deathEffectTimer = 0f;
            _deathEffectStartPos = transform.position;
            _deathEffectStartRot = transform.rotation;
            BCCameraGrainFeature.TriggerDeathPulse();
        }

        public void TriggerLastBreathEffect()
        {
            TriggerShake(damageShakeIntensity * 1.15f, damageShakeDuration * 1.35f);
            BCCameraGrainFeature.TriggerLastBreathPulse();
        }

        public void TriggerPhaseEffect(GamePhase phase)
        {
            BCCameraGrainFeature.TriggerPhasePulse(phase);
        }

        public void SetSurvivalState(bool isCriticalOxygen, bool isLastBreath)
        {
            BCCameraGrainFeature.SetSurvivalState(isCriticalOxygen, isLastBreath);
        }

        public void ResetDynamicEffects()
        {
            _shakeIntensity = 0f;
            _shakeDuration = 0f;
            _shakeTimer = 0f;
            _shakeOffset = Vector3.zero;
            _deathEffectActive = false;
            _deathEffectTimer = 0f;
            BCCameraGrainFeature.ResetRuntime();
        }

        private void TriggerShake(float intensity, float duration)
        {
            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeTimer = 0f;
        }

        public void SwitchToCards()
        {
            if (_state == CameraState.Cards) return;
            StartTransition(CameraState.Cards);
        }

        public void SwitchToFree()
        {
            if (_state == CameraState.Free) return;
            ResetFreeCameraVelocity();
            StartTransition(CameraState.Free);
        }

        public void SwitchToPlayerSlots()
        {
            if (_state == CameraState.PlayerSlots) return;
            StartTransition(CameraState.PlayerSlots);
        }

        private void StartTransition(CameraState target)
        {
            _stateStartPos = transform.position;
            _stateStartRot = transform.rotation;
            _cachedTargetPos = GetStatePosition(target);
            _cachedTargetRot = GetStateRotation(target);
            _stateTarget = target;
            _stateTransitionProgress = 0f;
            _switchBlockUntil = Time.time + switchCooldownSeconds;
            BCCameraGrainFeature.TriggerTransitionPulse(0.65f);
        }

        private bool IsSwitchBlocked()
        {
            return Time.time < _switchBlockUntil || _stateTransitionProgress < 1f;
        }

        private void ResetFreeCameraVelocity()
        {
            _yawVelocity = 0f;
            _pitchVelocity = 0f;
            _positionVelocity = Vector3.zero;
        }

        private void Start()
        {
            if (tableCenter == null)
            {
                tableCenter = new GameObject("TableCenter").transform;
                tableCenter.position = new Vector3(0f, 1f, 0f);
            }

            if (playerRoot != null)
            {
                transform.position = playerRoot.TransformPoint(eyeOffset);
                transform.rotation = playerRoot.rotation;
            }

            _currentYaw = 0f;
            _currentPitch = 15f;
            _state = CameraState.Free;
            _stateTarget = CameraState.Free;
            _stateTransitionProgress = 1f;
            ResetDynamicEffects();
            UpdatePlayerHandVisibility();
        }

        private void LateUpdate()
        {
            if (playerRoot == null || tableCenter == null) return;

            if (!_inputLocked)
            {
                HandleInput();
            }

            // Handle death effect
            if (_deathEffectActive)
            {
                UpdateDeathEffect();
                return;
            }

            if (_stateTransitionProgress < 1f)
            {
                _stateTransitionProgress = Mathf.Min(1f, _stateTransitionProgress + Time.deltaTime / stateTransitionTime);
                float t = _stateTransitionProgress * _stateTransitionProgress * (3f - 2f * _stateTransitionProgress);
                transform.position = Vector3.Lerp(_stateStartPos, _cachedTargetPos, t);
                transform.rotation = Quaternion.Slerp(_stateStartRot, _cachedTargetRot, t);
                if (_stateTransitionProgress >= 1f)
                {
                    _state = _stateTarget;
                    if (_state == CameraState.Free)
                        ResetFreeCameraVelocity();
                }
                ApplyShake();
                UpdatePlayerHandVisibility();
                return;
            }

            _state = _stateTarget;

            if (_state == CameraState.Free && !_inputLocked)
            {
                UpdateFreeCamera();
            }

            ApplyShake();
            UpdatePlayerHandVisibility();
        }

        private void HandleInput()
        {
            if (IsSwitchBlocked()) return;

            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kb == null) return;

            bool sDown = kb.sKey.wasPressedThisFrame || (mouse != null && mouse.scroll.y.ReadValue() < -0.5f);
            bool wDown = kb.wKey.wasPressedThisFrame || (mouse != null && mouse.scroll.y.ReadValue() > 0.5f);

            // S / Scroll Down: Free→Cards, PlayerSlots→Free, EnemySlots→PlayerSlots. В Cards — S ничего не делает.
            if (sDown)
            {
                if (_state == CameraState.Free)
                {
                    SwitchToCards();
                }
                else if (_state == CameraState.PlayerSlots)
                {
                    PlayerSelectionTracker.ClearSelection();
                    SwitchToFree();
                }
                else if (_state == CameraState.EnemySlots)
                {
                    StartTransition(CameraState.PlayerSlots);
                }
                // Cards: S не реагирует
            }

            // W / Scroll Up: Cards→Free, Free→PlayerSlots, PlayerSlots→EnemySlots.
            if (wDown)
            {
                if (_state == CameraState.Cards)
                {
                    SwitchToFree();
                }
                else if (_state == CameraState.Free)
                {
                    StartTransition(CameraState.PlayerSlots);
                }
                else if (_state == CameraState.PlayerSlots)
                {
                    StartTransition(CameraState.EnemySlots);
                }
                // EnemySlots: W не реагирует (конец цепочки)
            }
        }

        private void UpdateFreeCamera()
        {
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(UnityEngine.Input.mousePosition.x, UnityEngine.Input.mousePosition.y);
            float mouseX = mousePos.x / Screen.width - 0.5f;
            // Инвертируем: курсор вверх → камера смотрит вверх (отрицательный pitch)
            float mouseY = -(mousePos.y / Screen.height - 0.5f);

            float targetYaw = Mathf.Clamp(mouseX * cursorSensitivity * maxYaw * 2f, -maxYaw, maxYaw);
            float targetPitch = Mathf.Clamp(15f + mouseY * cursorSensitivity * maxPitch * 2f, 15f - maxPitch * 0.6f, 15f + maxPitch * 0.6f);

            _currentYaw = Mathf.SmoothDamp(_currentYaw, targetYaw, ref _yawVelocity, smoothTime);
            _currentPitch = Mathf.SmoothDamp(_currentPitch, targetPitch, ref _pitchVelocity, smoothTime);

            Vector3 basePos = playerRoot.TransformPoint(eyeOffset);
            Quaternion baseRot = playerRoot.rotation * Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            transform.position = Vector3.SmoothDamp(transform.position, basePos, ref _positionVelocity, smoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, baseRot, 1f - Mathf.Exp(-15f * Time.deltaTime));
        }

        private void ApplyShake()
        {
            if (_shakeTimer < _shakeDuration)
            {
                _shakeTimer += Time.deltaTime;
                float progress = _shakeTimer / _shakeDuration;
                float decay = 1f - progress;

                // Perlin noise для плавного shake
                float x = Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) * 2f - 1f;
                float y = Mathf.PerlinNoise(0f, Time.time * shakeFrequency) * 2f - 1f;
                float z = Mathf.PerlinNoise(Time.time * shakeFrequency, Time.time * shakeFrequency) * 2f - 1f;

                _shakeOffset = new Vector3(x, y, z) * _shakeIntensity * decay;
                transform.position += _shakeOffset;
            }
            else
            {
                _shakeOffset = Vector3.zero;
            }
        }

        private void UpdateDeathEffect()
        {
            _deathEffectTimer += Time.deltaTime;
            float t = _deathEffectTimer / deathEffectDuration;

            if (t >= 1f)
            {
                _deathEffectActive = false;
                return;
            }

            // Медленный наклон камеры и zoom out
            float tiltAngle = Mathf.Lerp(0f, -15f, t);
            float zoomOut = Mathf.Lerp(0f, 0.3f, t);

            Vector3 targetPos = _deathEffectStartPos + Vector3.back * zoomOut;
            Quaternion targetRot = _deathEffectStartRot * Quaternion.Euler(0f, 0f, tiltAngle);

            transform.position = Vector3.Lerp(_deathEffectStartPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(_deathEffectStartRot, targetRot, t);
        }

        private Vector3 GetStatePosition(CameraState state)
        {
            switch (state)
            {
                case CameraState.PlayerSlots when playerSlotsPlaceholder != null:
                    return playerSlotsPlaceholder.position;
                case CameraState.TableSlots when tableSlotsPlaceholder != null:
                    return tableSlotsPlaceholder.position;
                case CameraState.EnemySlots when enemySlotsPlaceholder != null:
                    return enemySlotsPlaceholder.position;
            }

            if (playerRoot == null || tableCenter == null) return transform.position;
            Vector3 basePos = playerRoot.position;
            return state switch
            {
                CameraState.Cards => GetCardsStatePosition(),
                CameraState.PlayerSlots => playerMainSlot != null ? playerMainSlot.TransformPoint(playerSlotsViewOffset) : basePos + playerSlotsViewOffset,
                CameraState.TableSlots => tableCenter.position + tableSlotsViewOffset,
                CameraState.EnemySlots => tableCenter.position + enemySlotsViewOffset,
                _ => playerRoot.TransformPoint(eyeOffset)
            };
        }

        private Quaternion GetStateRotation(CameraState state)
        {
            switch (state)
            {
                case CameraState.PlayerSlots when playerSlotsPlaceholder != null:
                    return playerSlotsPlaceholder.rotation;
                case CameraState.TableSlots when tableSlotsPlaceholder != null:
                    return tableSlotsPlaceholder.rotation;
                case CameraState.EnemySlots when enemySlotsPlaceholder != null:
                    return enemySlotsPlaceholder.rotation;
            }

            Vector3 lookAt = tableCenter.position + Vector3.up * 0.3f;
            Vector3 camPos;
            switch (state)
            {
                case CameraState.Cards:
                    camPos = GetCardsStatePosition();
                    Vector3 cardsLookAt = playerHandHolder != null
                        ? playerHandHolder.position + playerRoot.forward * 0.08f
                        : playerRoot.position + playerRoot.forward * 0.5f;
                    return Quaternion.LookRotation(cardsLookAt - camPos);
                case CameraState.PlayerSlots:
                    camPos = playerMainSlot != null ? playerMainSlot.TransformPoint(playerSlotsViewOffset) : playerRoot.position + playerSlotsViewOffset;
                    return Quaternion.LookRotation(lookAt - camPos);
                case CameraState.TableSlots:
                    camPos = tableCenter.position + tableSlotsViewOffset;
                    return Quaternion.LookRotation(lookAt - camPos);
                case CameraState.EnemySlots:
                    camPos = tableCenter.position + enemySlotsViewOffset;
                    Vector3 enemyLookAt = enemyMainSlot != null ? enemyMainSlot.position : tableCenter.position + Vector3.forward * 1.5f;
                    return Quaternion.LookRotation(enemyLookAt - camPos);
                default:
                    return transform.rotation;
            }
        }

        /// <summary>
        /// Возвращает следующее состояние камеры по кругу: Free → Cards → PlayerSlots → TableSlots → EnemySlots → Free...
        /// </summary>
        public static CameraState NextState(CameraState currentState)
        {
            return currentState switch
            {
                CameraState.Free => CameraState.Cards,
                CameraState.Cards => CameraState.PlayerSlots,
                CameraState.PlayerSlots => CameraState.TableSlots,
                CameraState.TableSlots => CameraState.EnemySlots,
                CameraState.EnemySlots => CameraState.Free,
                _ => CameraState.Free
            };
        }

        private Vector3 GetCardsStatePosition()
        {
            if (playerHandHolder != null && playerRoot != null)
            {
                return playerHandHolder.position - playerRoot.forward * 0.28f + playerRoot.up * 0.16f;
            }

            if (cardsPlaceholder != null)
            {
                return cardsPlaceholder.position;
            }

            return playerRoot != null ? playerRoot.TransformPoint(cardsViewOffset) : transform.position;
        }

        private void UpdatePlayerHandVisibility()
        {
            bool shouldShow = _state == CameraState.Cards || _stateTarget == CameraState.Cards || _stateTransitionProgress < 1f && (_state == CameraState.Cards || _stateTarget == CameraState.Cards);
            if (_playerHandVisible == shouldShow)
            {
                return;
            }

            _playerHandVisible = shouldShow;
            SetHolderVisibility(playerHandHolder, shouldShow);
        }

        private static void SetHolderVisibility(Transform holder, bool visible)
        {
            if (holder == null)
            {
                return;
            }

            Renderer[] renderers = holder.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = visible;
            }

            Collider[] colliders = holder.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = visible;
            }
        }
    }
}
#pragma warning restore CS0649
