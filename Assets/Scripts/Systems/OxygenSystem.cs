using System;
using BreathCasino.Core;
using UnityEngine;

namespace BreathCasino.Systems
{
    /// <summary>
    /// Управляет системой кислорода (O2) игрока.
    /// O2 таймер считается вниз в течение раунда, не сбрасывается между мини-раундами.
    /// Когда O2 достигает 0, срабатывает Last Breath.
    /// </summary>
    public class OxygenSystem : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float warningThreshold = 15f; // Когда показывать предупреждение

        private float _currentOxygen;
        private float _maxOxygen;
        private bool _isActive;
        private bool _lastBreathTriggered;
        private float _lastBreathDuration = 15f;
        private bool _isInLastBreath;

        public float CurrentOxygen => _currentOxygen;
        public float MaxOxygen => _maxOxygen;
        public bool IsActive => _isActive;
        public bool IsInLastBreath => _isInLastBreath;
        public bool LastBreathTriggered => _lastBreathTriggered;

        public event Action OnOxygenChanged;
        public event Action OnLastBreathTriggered;

        /// <summary>
        /// Начать новый раунд с максимальным кислородом.
        /// </summary>
        public void StartRound(float maxOxygen)
        {
            _maxOxygen = maxOxygen;
            _currentOxygen = maxOxygen;
            _isActive = true;
            _lastBreathTriggered = false;
            _isInLastBreath = false;
            OnOxygenChanged?.Invoke();
            Debug.Log($"[OxygenSystem] Round started: {_maxOxygen}s");
        }

        /// <summary>
        /// Остановить отсчет O2 (конец раунда).
        /// </summary>
        public void StopRound()
        {
            _isActive = false;
            Debug.Log($"[OxygenSystem] Round stopped");
        }

        /// <summary>
        /// Добавить кислород за действие игрока.
        /// </summary>
        public void AddOxygen(float amount)
        {
            if (!_isActive || amount <= 0) return;
            
            _currentOxygen = Mathf.Min(_currentOxygen + amount, _maxOxygen);
            OnOxygenChanged?.Invoke();
            Debug.Log($"[OxygenSystem] +{amount}s O2 → {_currentOxygen:0.0}s");
        }

        /// <summary>
        /// Обновить таймер (вызывается каждый frame).
        /// </summary>
        public void Update()
        {
            if (!_isActive) return;

            _currentOxygen = Mathf.Max(0f, _currentOxygen - Time.deltaTime);
            OnOxygenChanged?.Invoke();

            // Проверка Last Breath
            if (_currentOxygen <= 0f && !_lastBreathTriggered && !_isInLastBreath)
            {
                TriggerLastBreath();
            }
        }

        /// <summary>
        /// Проверить нужно ли показать предупреждение.
        /// </summary>
        public bool ShouldShowWarning()
        {
            return _isActive && _currentOxygen <= warningThreshold && _currentOxygen > 0f;
        }

        /// <summary>
        /// Получить статус в процентах (для UI).
        /// </summary>
        public float GetOxygenPercent()
        {
            return _maxOxygen > 0 ? _currentOxygen / _maxOxygen : 0f;
        }

        /// <summary>
        /// Формат для отображения.
        /// </summary>
        public string GetDisplayText()
        {
            if (_isInLastBreath)
                return $"{_currentOxygen:0.0}s (LAST BREATH)";
            return $"{_currentOxygen:0.0}s / {_maxOxygen:0.0}s";
        }

        private void TriggerLastBreath()
        {
            _lastBreathTriggered = true;
            _isInLastBreath = true;
            _currentOxygen = _lastBreathDuration;
            OnLastBreathTriggered?.Invoke();
            Debug.Log($"[OxygenSystem] Last Breath triggered! {_lastBreathDuration}s remaining");
        }

        public void EndLastBreath()
        {
            if (_isInLastBreath)
            {
                _isInLastBreath = false;
                Debug.Log($"[OxygenSystem] Last Breath ended");
            }
        }

        public bool IsLastBreathActive()
        {
            return _isInLastBreath;
        }

        public void Reset()
        {
            _currentOxygen = 0;
            _maxOxygen = 0;
            _isActive = false;
            _lastBreathTriggered = false;
            _isInLastBreath = false;
            Debug.Log($"[OxygenSystem] Reset");
        }
    }
}