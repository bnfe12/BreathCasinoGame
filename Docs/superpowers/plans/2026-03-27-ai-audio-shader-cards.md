# AI / Audio / Shader / Cards Fix — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Полностью переписать EnemyAI, добавить AudioManager, написать URP post-process шейдер зернистого нуара, исправить позицию карт противника кодом, починить дораздачу карт, добавить подробное дебаг-логирование колоды.

**Architecture:** Каждая система — отдельный файл с одной ответственностью. EnemyAI становится чистым детерминированным автоматом без зависимостей от MonoBehaviour-состояния. AudioManager — синглтон, слушает фазы через Action-коллбеки. Шейдер — Renderer Feature поверх URP pipeline, влияет только на финальный кадр. Позиция карт противника фиксируется через принудительную установку localPosition в момент спавна, не через HandAnimator.

**Tech Stack:** Unity 6, URP 17, C# 10, HLSL (URP ShaderLibrary), UnityEngine.InputSystem

---

## Файлы, которые затрагивает план

| Файл | Действие | Ответственность |
|---|---|---|
| `Assets/Scripts/AI/EnemyAI.cs` | **Переписать** | Полный детерминированный AI без MonoBehaviour-зависимостей |
| `Assets/Scripts/Managers/BlockoutAudioManager.cs` | **Создать** | Синглтон аудио: ambient, выстрел, карты, кашель |
| `Assets/Scripts/Managers/BlockoutCardManager.cs` | **Изменить** | Дебаг-лог колоды, дораздача 2 карт при пустой руке |
| `Assets/Shaders/BlockoutCameraGrain.shader` | **Создать** | URP ScriptableRendererFeature + HLSL full-screen pass |
| `Assets/Scripts/Rendering/BlockoutCameraGrainFeature.cs` | **Создать** | ScriptableRendererFeature регистрирующий шейдер в URP |
| `Assets/Scripts/Rendering/BlockoutCameraGrainPass.cs` | **Создать** | ScriptableRenderPass выполняющий grain blit |
| `Assets/Scripts/Bootstrap/SceneBootstrap.cs` | **Изменить** | Принудительная установка localPosition enemyHandHolder без зависимости от HandAnimator |
| `Assets/Scripts/Gameplay/BlockoutHandAnimator.cs` | **Изменить** | LowerBothHands никогда не телепортирует карты в чужой parent |
| `Assets/Scripts/Managers/GameManager.cs` | **Изменить** | Инициализация AudioManager, подключение фазовых хуков |

---

## Task 1: Полный переписать EnemyAI

**Проблема:** Текущий EnemyAI — MonoBehaviour с lazy `FindFirstObjectByType`. Если он не найден в GameManager.Initialize() — вызывается `GetRandomMainCards` (случайная карта), которая возвращает единственную слабую карту. В защите эта карта не покрывает атаку → AI молчит.

**Решение:** Убрать все MonoBehaviour-зависимости. EnemyAI становится чистым `sealed class` (не MonoBehaviour) с детерминированной логикой. GameManager создаёт его через `new EnemyAI()`. Никакого `FindFirstObjectByType`.

**Файлы:**
- Переза��исать: `Assets/Scripts/AI/EnemyAI.cs`
- Изменить: `Assets/Scripts/Managers/GameManager.cs` строки 98, 1205–1241

- [ ] **Шаг 1: Переписать EnemyAI.cs**

```csharp
// Assets/Scripts/AI/EnemyAI.cs
using System.Collections.Generic;
using UnityEngine;

namespace BreathCasino.Blockout
{
    /// <summary>
    /// Детерминированный AI противника. НЕ MonoBehaviour — создаётся через new EnemyAI().
    /// Никаких зависимостей от сцены, FindFirstObjectByType, Start/Awake.
    /// </summary>
    public sealed class EnemyAI
    {
        // ────────────────────────────────────────────────────────────
        //  АТАКА
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Выбрать карту(ы) для атаки.
        /// Приоритет: Threat с наибольшим весом → Resource с наибольшим весом.
        /// Всегда возвращает ровно 1 карту (или пустой список если рука пустая).
        /// </summary>
        public List<BlockoutCardDisplay> ChooseAttackCards(
            IReadOnlyList<BlockoutCardDisplay> hand)
        {
            var result = new List<BlockoutCardDisplay>();
            if (hand == null || hand.Count == 0)
            {
                Debug.Log("[EnemyAI] Attack: hand is empty — skip.");
                return result;
            }

            BlockoutCardDisplay bestThreat   = null;
            BlockoutCardDisplay bestResource = null;

            foreach (var card in hand)
            {
                if (card == null) continue;

                if (card.IsThreat)
                {
                    if (bestThreat == null || card.Weight > bestThreat.Weight)
                        bestThreat = card;
                }
                else
                {
                    if (bestResource == null || card.Weight > bestResource.Weight)
                        bestResource = card;
                }
            }

            // Предпочитаем Threat; если нет — берём лучший Resource
            BlockoutCardDisplay chosen = bestThreat ?? bestResource;
            if (chosen != null)
            {
                result.Add(chosen);
                Debug.Log($"[EnemyAI] Attack: chose {chosen.CardName} w={chosen.Weight} IsThreat={chosen.IsThreat}");
            }
            else
            {
                Debug.Log("[EnemyAI] Attack: no valid card found — skip.");
            }

            return result;
        }

        // ────────────────────────────────────────────────────────────
        //  ЗАЩИТА
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Выбрать карты для защиты. Только Resource (не Threat).
        /// Шаг 1: одна карта с весом >= attackWeight (минимально превышающая).
        /// Шаг 2: лучшая пара Resource с суммой >= attackWeight.
        /// Шаг 3: нет покрытия — пустой список (AI пропускает).
        /// Возвращает 1 или 2 карты, никогда не возвращает заведомо проигрышный результат.
        /// </summary>
        public List<BlockoutCardDisplay> ChooseDefenseCards(
            IReadOnlyList<BlockoutCardDisplay> hand, int attackWeight)
        {
            var result = new List<BlockoutCardDisplay>();
            if (hand == null || hand.Count == 0)
            {
                Debug.Log($"[EnemyAI] Defense vs {attackWeight}: hand empty — skip.");
                return result;
            }

            // Собираем только Resource
            var resources = new List<BlockoutCardDisplay>();
            foreach (var card in hand)
            {
                if (card != null && !card.IsThreat)
                    resources.Add(card);
            }

            if (resources.Count == 0)
            {
                Debug.Log($"[EnemyAI] Defense vs {attackWeight}: no resource cards — skip.");
                return result;
            }

            // Шаг 1: минимальная одиночная карта >= attackWeight
            BlockoutCardDisplay bestSingle   = null;
            int                 bestSingleW  = int.MaxValue;
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

            // Шаг 2: лучшая пара с минимальной суммой >= attackWeight
            BlockoutCardDisplay pairA   = null;
            BlockoutCardDisplay pairB   = null;
            int                 bestSum = int.MaxValue;

            for (int i = 0; i < resources.Count; i++)
            {
                for (int j = i + 1; j < resources.Count; j++)
                {
                    int s = resources[i].Weight + resources[j].Weight;
                    if (s >= attackWeight && s < bestSum)
                    {
                        bestSum = s;
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

            // Шаг 3: нет покрытия
            int maxAvail = 0;
            foreach (var c in resources) maxAvail += c.Weight;
            Debug.Log($"[EnemyAI] Defense vs {attackWeight}: cannot cover (max available={maxAvail}) — skip.");
            return result;
        }

        // ────────────────────────────────────────────────────────────
        //  СПЕЦ КАРТА
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Выбрать специальную карту. Простая стратегия: берём первую доступную.
        /// </summary>
        public BlockoutCardDisplay ChooseSpecialCard(
            IReadOnlyList<BlockoutCardDisplay> specials)
        {
            if (specials == null) return null;
            foreach (var card in specials)
            {
                if (card != null && card.CardType == BlockoutCardType.Special)
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
        /// Если холостых > 60% → стрелять в себя (безопасно).
        /// Иначе → в игрока.
        /// </summary>
        public BlockoutSide ChooseShotTarget(List<BlockoutBulletType> chamber,
            float blankThreshold = 0.6f)
        {
            if (chamber == null || chamber.Count == 0)
                return BlockoutSide.Player;

            int blanks = 0;
            foreach (var b in chamber)
                if (b == BlockoutBulletType.Blank) blanks++;

            float ratio = (float)blanks / chamber.Count;
            if (ratio > blankThreshold)
            {
                Debug.Log($"[EnemyAI] Shot: self (blanks={ratio:0%} > {blankThreshold:0%})");
                return BlockoutSide.Enemy; // Enemy = стреляет в себя
            }

            Debug.Log($"[EnemyAI] Shot: player (blanks={ratio:0%})");
            return BlockoutSide.Player;
        }
    }
}
```

- [ ] **Шаг 2: Обновить GameManager.cs — убрать MonoBehaviour-поиск AI, создать через new**

Найти строку 98:
```csharp
_enemyAI = FindFirstObjectByType<EnemyAI>();
```
Заменить на:
```csharp
_enemyAI = new EnemyAI();
Debug.Log("[GameManager] EnemyAI created (pure class, no MonoBehaviour).");
```

Изменить поле на строке ~39:
```csharp
// было:
private EnemyAI _enemyAI;
// стало (тип тот же, просто EnemyAI теперь не MonoBehaviour):
private EnemyAI _enemyAI;
```
*(Тип не меняется — просто убедиться что `using UnityEngine` не нужен для EnemyAI отдельно.)*

- [ ] **Шаг 3: Обновить GetAIAttackCards в GameManager — убрать playerWeight из ChooseAttackCards**

Найти `GetAIAttackCards` (~строка 1205) и заменить тело:

```csharp
private List<BlockoutCardDisplay> GetAIAttackCards(BlockoutSide side)
{
    if (side != BlockoutSide.Enemy || _enemyAI == null || _cardManager == null)
        return GetRandomMainCards(side);

    var list = _cardManager.EnemyMainCards;
    if (list == null || list.Count == 0)
    {
        Debug.Log("[GameManager] AI attack: EnemyMainCards empty.");
        return new List<BlockoutCardDisplay>();
    }

    return _enemyAI.ChooseAttackCards(list);
}
```

- [ ] **Шаг 4: Проверить GetAIDefenseCards — он уже правильный после прошлого фикса**

Строки 1228–1241 должны выглядеть так (не трогать если уже так):
```csharp
private List<BlockoutCardDisplay> GetAIDefenseCards(BlockoutSide side, int attackWeight)
{
    if (side != BlockoutSide.Enemy || _enemyAI == null || _cardManager == null)
        return GetRandomMainCards(side);

    var list = _cardManager.EnemyMainCards;
    if (list == null || list.Count == 0) return new List<BlockoutCardDisplay>();

    return _enemyAI.ChooseDefenseCards(list, attackWeight);
}
```

- [ ] **Шаг 5: Удалить MonoBehaviour GameObject EnemyAI из сцены**

В Unity Editor: найти объект `EnemyAI` на сцене `BlockoutTest.unity` → удалить компонент `EnemyAI` (он больше не MonoBehaviour). Или оставить GameObject, просто удалить компонент. AI создаётся кодом.

- [ ] **Шаг 6: Build-проверка**

```
dotnet build "Assembly-CSharp.csproj" -c Release 2>&1 | grep -E "error|Error"
```
Ожидаемый результат: `0 Error(s)`

---

## Task 2: AudioManager — фоновый ambient, выстрел, карты, кашель

**Архитектура:** `BlockoutAudioManager` — MonoBehaviour синглтон. Подписывается на `GameManager.OnPhaseChanged` (Action). Рандомный кашель через коллаб. Все `AudioClip` — SerializeField в Inspector (подкладываются .wav/.ogg файлы).

**Файлы:**
- Создать: `Assets/Scripts/Managers/BlockoutAudioManager.cs`
- Изменить: `Assets/Scripts/Managers/GameManager.cs` — добавить `public event Action<BlockoutGamePhase> OnPhaseChanged`
- Изменить: `Assets/Scripts/Bootstrap/SceneBootstrap.cs` — инициализировать AudioManager

- [ ] **Шаг 1: Добавить событие OnPhaseChanged в GameManager**

Найти поле `_phase` (~строка 41) и после объявления добавить:
```csharp
/// <summary>Событие изменения фазы — AudioManager и другие слушатели.</summary>
public event System.Action<BlockoutGamePhase> OnPhaseChanged;
```

Найти метод `SetPhase` (~строка 913) и добавить вызов после `_phase = phase;`:
```csharp
private void SetPhase(BlockoutGamePhase phase, string eventText)
{
    _phase = phase;
    OnPhaseChanged?.Invoke(phase);
    Debug.Log($"{LogPrefix} Phase -> {phase}");
    RefreshHud(eventText);
}
```

- [ ] **Шаг 2: Добавить публичный метод NotifyCardPlayed в GameManager**

Где-то после `ConfirmPlayerGunPickup` добавить:
```csharp
/// <summary>Вызывается карточным менеджером/интерактаблом когда карта физически кладётся на стол.</summary>
public void NotifyCardPlayed() => _audioManager?.PlayCardPlace();

private BlockoutAudioManager _audioManager;
```

И в `Initialize()` после строки `_enemyAI = new EnemyAI();`:
```csharp
_audioManager = Object.FindFirstObjectByType<BlockoutAudioManager>();
```

- [ ] **Шаг 3: Создать BlockoutAudioManager.cs**

```csharp
// Assets/Scripts/Managers/BlockoutAudioManager.cs
using System.Collections;
using UnityEngine;

namespace BreathCasino.Blockout
{
    /// <summary>
    /// Центральный менеджер звука для Breath Casino.
    /// Singleton. Подписывается на GameManager.OnPhaseChanged.
    /// Все AudioClip назначаются через Inspector.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BlockoutAudioManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────
        public static BlockoutAudioManager Instance { get; private set; }

        // ── Inspector: Ambient ───────────────────────────��───────────
        [Header("Ambient")]
        [Tooltip("Фоновый ambient трек. loop=true, воспроизводится сразу.")]
        [SerializeField] private AudioClip ambientClip;
        [SerializeField] [Range(0f, 1f)] private float ambientVolume = 0.25f;

        // ── Inspector: SFX ───────────────────────────────────────────
        [Header("SFX — Gun")]
        [Tooltip("Выстрел живым патроном.")]
        [SerializeField] private AudioClip gunShotLive;
        [Tooltip("Холостой щелчок.")]
        [SerializeField] private AudioClip gunShotBlank;
        [Tooltip("Звук взвода/поднятия пистолета.")]
        [SerializeField] private AudioClip gunCock;
        [Tooltip("Звук возврата пистолета на стол.")]
        [SerializeField] private AudioClip gunPutDown;

        [Header("SFX — Cards")]
        [Tooltip("Карта кладётся на стол.")]
        [SerializeField] private AudioClip cardPlace;
        [Tooltip("Раздача карт (одиночный шлепок).")]
        [SerializeField] private AudioClip cardDeal;
        [Tooltip("Карта возвращается в руку.")]
        [SerializeField] private AudioClip cardReturn;

        [Header("SFX — Drum")]
        [Tooltip("Барабан крутится/открывается.")]
        [SerializeField] private AudioClip drumSpin;
        [Tooltip("Барабан закрывается.")]
        [SerializeField] private AudioClip drumClose;

        [Header("SFX — Atmosphere")]
        [Tooltip("Рандомный кашель — один или несколько клипов.")]
        [SerializeField] private AudioClip[] coughClips;
        [Tooltip("Минимальная пауза между кашлями (секунды).")]
        [SerializeField] private float coughIntervalMin = 18f;
        [Tooltip("Максимальная пауза между кашлями (секунды).")]
        [SerializeField] private float coughIntervalMax = 45f;
        [Tooltip("Громкость кашля.")]
        [SerializeField] [Range(0f, 1f)] private float coughVolume = 0.55f;

        [Header("SFX — UI / Duel")]
        [Tooltip("Победа в дуэли.")]
        [SerializeField] private AudioClip duelWin;
        [Tooltip("Проигрыш/пропуск хода.")]
        [SerializeField] private AudioClip duelLose;

        // ── Внутренние источники ─────────────────────────────────────
        private AudioSource _ambientSource;
        private AudioSource _sfxSource;

        // ────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Ambient source
            _ambientSource = GetComponent<AudioSource>();
            _ambientSource.loop        = true;
            _ambientSource.spatialBlend = 0f;   // 2D
            _ambientSource.volume      = ambientVolume;
            _ambientSource.playOnAwake = false;

            // SFX source (второй AudioSource на том же объекте)
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop        = false;
            _sfxSource.spatialBlend = 0f;
            _sfxSource.volume      = 1f;
            _sfxSource.playOnAwake = false;
        }

        private void Start()
        {
            PlayAmbient();
            StartCoroutine(RandomCoughRoutine());
            SubscribeToGameManager();
        }

        private void OnDestroy()
        {
            UnsubscribeFromGameManager();
        }

        // ────────────────────────────────────────────────────────────
        //  Подписка на GameManager
        // ────────────────────────────────────────────────────────────

        private GameManager _gm;

        private void SubscribeToGameManager()
        {
            _gm = Object.FindFirstObjectByType<GameManager>();
            if (_gm != null)
                _gm.OnPhaseChanged += HandlePhaseChanged;
        }

        private void UnsubscribeFromGameManager()
        {
            if (_gm != null)
                _gm.OnPhaseChanged -= HandlePhaseChanged;
        }

        private void HandlePhaseChanged(BlockoutGamePhase phase)
        {
            switch (phase)
            {
                case BlockoutGamePhase.BulletReveal:
                    Play(_sfxSource, drumSpin);
                    break;

                case BlockoutGamePhase.Dealing:
                    Play(_sfxSource, cardDeal);
                    break;

                case BlockoutGamePhase.Shooting:
                    Play(_sfxSource, gunCock);
                    break;

                case BlockoutGamePhase.Resolution:
                    // без звука — результат отображается визуально
                    break;
            }
        }

        // ────────────────────────────────────────────────────────────
        //  Публичные методы — вызываются GameManager / Interactable
        // ────────────────────────────────────────────────────────────

        public void PlayGunShot(BlockoutBulletType bulletType)
        {
            AudioClip clip = bulletType == BlockoutBulletType.Blank ? gunShotBlank : gunShotLive;
            Play(_sfxSource, clip);
        }

        public void PlayGunPickup()  => Play(_sfxSource, gunCock);
        public void PlayGunReturn()  => Play(_sfxSource, gunPutDown);
        public void PlayCardPlace()  => Play(_sfxSource, cardPlace);
        public void PlayCardReturn() => Play(_sfxSource, cardReturn);
        public void PlayDrumClose()  => Play(_sfxSource, drumClose);
        public void PlayDuelWin()    => Play(_sfxSource, duelWin);
        public void PlayDuelLose()   => Play(_sfxSource, duelLose);

        // ────────────────────────────────────────────────────────────
        //  Рандомный кашель
        // ────────────────────────────────────────────────────────────

        private IEnumerator RandomCoughRoutine()
        {
            while (true)
            {
                float wait = Random.Range(coughIntervalMin, coughIntervalMax);
                yield return new WaitForSeconds(wait);

                if (coughClips != null && coughClips.Length > 0)
                {
                    AudioClip clip = coughClips[Random.Range(0, coughClips.Length)];
                    if (clip != null)
                        _sfxSource.PlayOneShot(clip, coughVolume);
                }
            }
        }

        // ────────────────────────────────────────────────────────────
        //  Helpers
        // ────────────────────────────────────────────────────────────

        private void PlayAmbient()
        {
            if (ambientClip == null) return;
            _ambientSource.clip = ambientClip;
            _ambientSource.Play();
        }

        private static void Play(AudioSource source, AudioClip clip)
        {
            if (source == null || clip == null) return;
            source.PlayOneShot(clip);
        }
    }
}
```

- [ ] **Шаг 4: Добавить `[SerializeField] private BlockoutAudioManager audioManagerRef` в SceneBootstrap**

В `SceneBootstrap.cs` добавить в секцию `[Header("Managers")]`:
```csharp
public BlockoutAudioManager audioManager;
```
*(Назначить в Inspector — перетащить GameObject с компонентом `BlockoutAudioManager`.)*

- [ ] **Шаг 5: Создать GameObject AudioManager в сцене**

В Unity Editor: `GameObject → Create Empty → назвать "AudioManager"` → добавить компонент `BlockoutAudioManager`. Назначить AudioClip'ы в Inspector когда они будут готовы (пока null — ошибок нет, звуков просто не будет).

- [ ] **Шаг 6: Build-проверка**

```
dotnet build "Assembly-CSharp.csproj" -c Release 2>&1 | grep -E " error "
```
Ожидаемый результат: `0 Error(s)`

---

## Task 3: URP Camera Grain Shader (нуар: зерно + виньетка + тёмные тени)

**Как работает в URP:** Не нужен отдельный шейдер на каждый объект сцены. Создаётся `ScriptableRendererFeature` который добавляет full-screen blit-pass после рендера сцены. Шейдер читает `_BlitTexture` (финальный кадр) и пишет модифицированный результат. Влияет только на финальное изображение камеры.

**Файлы:**
- Создать: `Assets/Shaders/BlockoutCameraGrain.shader`
- Создать: `Assets/Scripts/Rendering/BlockoutCameraGrainFeature.cs`
- Создать: `Assets/Scripts/Rendering/BlockoutCameraGrainPass.cs`

- [ ] **Шаг 1: Создать HLSL шейдер `BlockoutCameraGrain.shader`**

```hlsl
// Assets/Shaders/BlockoutCameraGrain.shader
// Full-screen post-process: grain + vignette + dark lift + desaturate.
// Используется только через ScriptableRendererFeature (не назначать на объекты).
Shader "BreathCasino/CameraGrain"
{
    Properties
    {
        // Заполняются через Material.SetXxx из C# — не редактировать вручную
        [HideInInspector] _MainTex ("Source", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "BlockoutGrain"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GRAIN_ON
            #pragma multi_compile_fragment _ _VIGNETTE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // ── Параметры ────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                // Grain
                float _GrainStrength;   // 0..1  (0.18 default)
                float _GrainSize;       // 0.5..4 (1.5 default)
                float _GrainSpeed;      // 0.5..8 (2.0 default)

                // Vignette
                float _VignetteStrength;  // 0..1  (0.55 default)
                float _VignetteRadius;    // 0..1  (0.45 default)
                float _VignetteSoftness;  // 0..1  (0.35 default)

                // Color grade
                float _Exposure;       // -2..0  (-0.4 default, тёмный стиль)
                float _Contrast;       //  0..2  (1.2 default)
                float _Saturation;     //  0..1  (0.55 default — частичное обесцвечивание)
                float4 _ShadowTint;    // RGB тени (0.05,0.08,0.12,0 — синеватый нуар)
            CBUFFER_END

            // ── Утилиты ──────────────────────────────────────────────

            // Простой высокочастотный шум на основе sin/frac
            float Noise(float2 uv, float seed)
            {
                float s = dot(uv, float2(127.1, 311.7)) + seed;
                return frac(sin(s) * 43758.5453);
            }

            float Grain(float2 uv)
            {
                float2 scaled = uv * _GrainSize * _ScreenParams.xy * 0.001;
                float  seed   = floor(_Time.y * _GrainSpeed) * 0.01;
                float  n      = Noise(floor(scaled), seed);
                return (n - 0.5) * 2.0; // -1..1
            }

            float3 ApplyGrade(float3 col)
            {
                // Exposure
                col *= exp2(_Exposure);

                // Contrast вокруг 0.5
                col = (col - 0.5) * _Contrast + 0.5;

                // Saturation
                float lum = dot(col, float3(0.2126, 0.7152, 0.0722));
                col = lerp(float3(lum, lum, lum), col, _Saturation);

                // Shadow tint (добавляем цвет только в тёмных областях)
                float shadow = saturate(1.0 - lum * 2.0);
                col += _ShadowTint.rgb * shadow;

                return saturate(col);
            }

            float Vignette(float2 uv)
            {
                float2 d = uv - 0.5;
                // Корректируем на соотношение сторон
                d.x *= _ScreenParams.x / _ScreenParams.y;
                float dist = length(d);
                float v    = smoothstep(_VignetteRadius,
                                        _VignetteRadius - _VignetteSoftness,
                                        dist);
                return lerp(1.0 - _VignetteStrength, 1.0, v);
            }

            // ── Fragment ─────────────────────────────────────────────
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv  = input.texcoord;
                half4  col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // 1. Grain
                float g = Grain(uv) * _GrainStrength;
                col.rgb += g;

                // 2. Color grade
                col.rgb = ApplyGrade(col.rgb);

                // 3. Vignette
                col.rgb *= Vignette(uv);

                return col;
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Шаг 2: Создать ScriptableRenderPass**

```csharp
// Assets/Scripts/Rendering/BlockoutCameraGrainPass.cs
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace BreathCasino.Rendering
{
    /// <summary>
    /// Full-screen blit pass. Выполняется после рендера прозрачных объектов.
    /// Читает финальный цветовой буфер, пишет обработанный результат.
    /// </summary>
    public sealed class BlockoutCameraGrainPass : ScriptableRenderPass
    {
        private Material _material;

        private static readonly int GrainStrength    = Shader.PropertyToID("_GrainStrength");
        private static readonly int GrainSize        = Shader.PropertyToID("_GrainSize");
        private static readonly int GrainSpeed       = Shader.PropertyToID("_GrainSpeed");
        private static readonly int VignetteStr      = Shader.PropertyToID("_VignetteStrength");
        private static readonly int VignetteRadius   = Shader.PropertyToID("_VignetteRadius");
        private static readonly int VignetteSoft     = Shader.PropertyToID("_VignetteSoftness");
        private static readonly int ExposureId       = Shader.PropertyToID("_Exposure");
        private static readonly int ContrastId       = Shader.PropertyToID("_Contrast");
        private static readonly int SaturationId     = Shader.PropertyToID("_Saturation");
        private static readonly int ShadowTintId     = Shader.PropertyToID("_ShadowTint");

        public BlockoutCameraGrainPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public void Setup(Material mat) => _material = mat;

        // Render Graph API (Unity 6)
        public override void RecordRenderGraph(RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (_material == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle src = resourceData.activeColorTexture;
            TextureHandle dst = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, renderGraph.GetTextureDesc(src), "GrainDst", false);

            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                "BlockoutCameraGrain", out var passData);

            passData.Material = _material;
            passData.Source   = src;

            builder.UseTexture(src);
            builder.SetRenderAttachment(dst, 0);
            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, data.Source, new Vector4(1, 1, 0, 0),
                    data.Material, 0);
            });

            resourceData.cameraColor = dst;
        }

        private class PassData
        {
            public Material Material;
            public TextureHandle Source;
        }
    }
}
```

- [ ] **Шаг 3: Создать ScriptableRendererFeature**

```csharp
// Assets/Scripts/Rendering/BlockoutCameraGrainFeature.cs
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BreathCasino.Rendering
{
    /// <summary>
    /// Renderer Feature для URP. Добавить в Universal Renderer Data через Inspector:
    /// Edit → Project Settings → Graphics → URP Asset → Renderer → Add Renderer Feature.
    /// </summary>
    public sealed class BlockoutCameraGrainFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            [Header("Grain")]
            [Range(0f, 0.5f)] public float grainStrength = 0.18f;
            [Range(0.5f, 4f)] public float grainSize     = 1.5f;
            [Range(0.5f, 8f)] public float grainSpeed    = 2.0f;

            [Header("Vignette")]
            [Range(0f, 1f)]   public float vignetteStrength = 0.55f;
            [Range(0f, 1f)]   public float vignetteRadius   = 0.45f;
            [Range(0f, 1f)]   public float vignetteSoftness = 0.35f;

            [Header("Color Grade")]
            [Range(-2f, 0f)]  public float exposure    = -0.4f;
            [Range(0f, 2f)]   public float contrast    = 1.2f;
            [Range(0f, 1f)]   public float saturation  = 0.55f;
            public Color shadowTint = new Color(0.05f, 0.08f, 0.12f, 0f);
        }

        public Settings settings = new();

        private BlockoutCameraGrainPass _pass;
        private Material               _material;

        public override void Create()
        {
            Shader shader = Shader.Find("BreathCasino/CameraGrain");
            if (shader == null)
            {
                Debug.LogWarning("[GrainFeature] Shader 'BreathCasino/CameraGrain' not found. " +
                                 "Ensure BlockoutCameraGrain.shader is in the project.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass     = new BlockoutCameraGrainPass();
            _pass.Setup(_material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null || _pass == null) return;

            // Не применяем в превью сцены/камерах SceneView
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;

            UpdateMaterial();
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
        }

        private void UpdateMaterial()
        {
            if (_material == null) return;
            _material.SetFloat("_GrainStrength",    settings.grainStrength);
            _material.SetFloat("_GrainSize",        settings.grainSize);
            _material.SetFloat("_GrainSpeed",       settings.grainSpeed);
            _material.SetFloat("_VignetteStrength", settings.vignetteStrength);
            _material.SetFloat("_VignetteRadius",   settings.vignetteRadius);
            _material.SetFloat("_VignetteSoftness", settings.vignetteSoftness);
            _material.SetFloat("_Exposure",         settings.exposure);
            _material.SetFloat("_Contrast",         settings.contrast);
            _material.SetFloat("_Saturation",       settings.saturation);
            _material.SetColor("_ShadowTint",       settings.shadowTint);
        }
    }
}
```

- [ ] **Шаг 4: Подключить Feature в Unity Editor**

1. `Edit → Project Settings → Graphics`
2. Кликнуть на текущий `URP Asset` → найти `Renderer List` → открыть `Universal Renderer Data`
3. Внизу: `Add Renderer Feature → BlockoutCameraGrainFeature`
4. Настроить параметры в Inspector (можно менять в Play Mode — изменения видны сразу)

- [ ] **Шаг 5: Build-проверка**

```
dotnet build "Assembly-CSharp.csproj" -c Release 2>&1 | grep -E " error "
```
Ожидаемый результат: `0 Error(s)`

---

## Task 4: Фикс позиции карт противника — кодом, без Inspector

**Root cause:** `BlockoutHandAnimator.LowerBothHands()` устанавливает `enemyHandHolder.localPosition = enemyHandLowLocal` где `enemyHandLowLocal = (0, -0.45, 0.12)`. Но `EnsureEnemyFacesTable` устанавливает начальную позицию holder'а в `(0, -0.05, 0.5)`. При каждом `LowerBothHands` holder прыгает из `(0,-0.05,0.5)` в `(0,-0.45,0.12)` — карты летят.

**Прошлый фикс:** Изменили `enemyHandLowLocal` на `(0,-0.05,0.5)` через `SetEnemyHandPositionsForFacingTable`. Но если противник всё ещё показывает карты за спиной — значит сами карты спавнятся в неправильном месте.

**Дополнительная проблема:** В `RelayoutHand` карты позиционируются через `card.transform.localPosition` — это local-позиция относительно holder'а. Если holder — child enemyRoot повёрнутого на 180°, а Z-ось holder'а смотрит в сторону стола — это правильно. Но если holder физически находится в неверном месте (за врагом) карты там же.

**Решение:** Принудительно фиксировать `enemyHandHolder.position` (world-space) в `Awake`/`Start` через конкретную world-позицию, а не полагаться на иерархию.

**Файлы:**
- Изменить: `Assets/Scripts/Bootstrap/SceneBootstrap.cs`
- Изменить: `Assets/Scripts/Gameplay/BlockoutHandAnimator.cs`

- [ ] **Шаг 1: Добавить метод EnsureEnemyHandInFront в SceneBootstrap**

Заменить `EnsureEnemyFacesTable` целиком:

```csharp
private void EnsureEnemyFacesTable()
{
    if (enemyRoot == null) return;

    // Поворачиваем enemyRoot на 180° если ещё не повёрнут
    if (enemyRoot.localEulerAngles.y < 170f || enemyRoot.localEulerAngles.y > 190f)
    {
        enemyRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
        Debug.Log("[Bootstrap] Enemy orientation fixed — 180° toward table.");
    }

    // Выставляем localPosition holder'ов в local-пространстве enemyRoot.
    // Z > 0 в enemyRoot (повёрнутого 180°) = физически в сторону стола.
    // Эти значения — "опущенная рука" (low). High выставляется в HandAnimator.
    Vector3 handLow    = new Vector3(0f, -0.05f, 0.5f);
    Vector3 specialLow = new Vector3(0.22f, 0.03f, 0.54f);

    if (enemyHandHolder    != null) enemyHandHolder.localPosition    = handLow;
    if (enemySpecialHolder != null) enemySpecialHolder.localPosition = specialLow;

    // Синхронизируем HandAnimator: low == начальная позиция, high == поднятая рука
    if (handAnimator != null)
    {
        handAnimator.SetEnemyHandPositionsForFacingTable(
            low:  handLow,
            high: new Vector3(0f, -0.08f, 0.72f));
    }

    Debug.Log($"[Bootstrap] EnemyHandHolder world pos: {enemyHandHolder?.position}");
}
```

- [ ] **Шаг 2: Добавить SetEnemyHandPositionsForFacingTable с named params в BlockoutHandAnimator**

Проверить сигнатуру в `BlockoutHandAnimator.cs` — метод уже есть, убедиться что параметры называются `low` и `high`:

```csharp
public void SetEnemyHandPositionsForFacingTable(Vector3 low, Vector3 high)
{
    enemyHandLowLocal  = low;
    enemyHandHighLocal = high;
}
```

- [ ] **Шаг 3: Защита в LowerBothHands — не трогать карты в чужом parent**

В `BlockoutHandAnimator.SetHandPosition` изменить чтобы логировать текущую позицию при каждом вызове — так видно телепортацию:

```csharp
private void SetHandPosition(BlockoutSide side, Vector3 localPos)
{
    if (side == BlockoutSide.Player)
    {
        if (playerHandHolder    != null) playerHandHolder.localPosition    = localPos;
        if (playerSpecialHolder != null) playerSpecialHolder.localPosition = localPos;
    }
    else
    {
        if (enemyHandHolder    != null) enemyHandHolder.localPosition    = localPos;
        if (enemySpecialHolder != null) enemySpecialHolder.localPosition = localPos;
        Debug.Log($"[HandAnimator] Enemy hand moved to local {localPos} (world: {enemyHandHolder?.position})");
    }
}
```

*(Убрать Debug.Log после отладки.)*

- [ ] **Шаг 4: Проверить в Unity Editor**

Запустить сцену `BlockoutTest`. Открыть иерархию. Найти `enemyHandHolder`. В Inspector → Transform должна показывать localPosition `(0, -0.05, 0.5)` в момент старта. Карты должны появляться ПЕРЕД противником (с точки зрения стола), а не сзади.

---

## Task 5: Дораздача карт когда рука пустая + дебаг-логирование колоды

**Проблема:** По MECHANICS.md если колода закончилась — новые карты не раздаются, игрок доигрывает с тем что есть. НО: если рука совсем пустая (0 карт) — это делает дуэль невозможной. Нужно экстренно дать 2 карты по шансу.

**Текущий код:** `RefillHandsToRoundLimit` уже есть emergency refill на 3 карты каждому, но только когда deck И discard пустые. Нужно также обрабатывать случай когда у противника 0 карт в руке.

**Дебаг:** Добавить в `EnsureDeckForRound` подробный лог какие карты созданы, с каким весом и типом.

**Файлы:**
- Изменить: `Assets/Scripts/Managers/BlockoutCardManager.cs`

- [ ] **Шаг 1: Добавить дебаг-лог в EnsureDeckForRound**

Найти метод `EnsureDeckForRound` (~строка 168) и заменить:

```csharp
private void EnsureDeckForRound(int roundIndex)
{
    _lastKnownRoundIndex = roundIndex;
    if (_currentDeckRoundIndex == roundIndex && _mainDeck.Count > 0) return;
    _currentDeckRoundIndex = roundIndex;

    int[] deckSizes = { 12, 17, 24 };
    int size = deckSizes[Mathf.Clamp(roundIndex, 0, 2)];
    _mainDeck.Clear();
    _mainDiscard.Clear();

    for (int i = 0; i < size; i++)
        _mainDeck.Add(CreateWeightedMainCard());

    ShuffleList(_mainDeck);

    // ── Дебаг-лог колоды ────────────────────────────────────────────
    LogDeckContents(roundIndex, size);
}

private void LogDeckContents(int roundIndex, int totalSize)
{
    int threats   = 0;
    int resources = 0;
    var weightCounts = new System.Collections.Generic.Dictionary<int, int>();

    foreach (var card in _mainDeck)
    {
        if (card.IsThreat)  threats++;
        else                resources++;

        if (!weightCounts.ContainsKey(card.weight)) weightCounts[card.weight] = 0;
        weightCounts[card.weight]++;
    }

    System.Text.StringBuilder sb = new();
    sb.AppendLine($"[CardManager] Deck Round {roundIndex + 1} ({totalSize} cards):");
    sb.AppendLine($"  Threat: {threats} ({100f * threats / totalSize:0.#}%)");
    sb.AppendLine($"  Resource: {resources} ({100f * resources / totalSize:0.#}%)");
    sb.AppendLine("  Weight distribution:");

    var sortedWeights = new System.Collections.Generic.List<int>(weightCounts.Keys);
    sortedWeights.Sort();
    foreach (int w in sortedWeights)
    {
        int cnt = weightCounts[w];
        // Посчитать сколько Threat с этим весом
        int threatW = 0;
        foreach (var card in _mainDeck)
            if (card.weight == w && card.IsThreat) threatW++;
        sb.AppendLine($"    w={w}: {cnt} total ({threatW} Threat, {cnt - threatW} Resource) " +
                      $"= {100f * cnt / totalSize:0.#}%");
    }

    Debug.Log(sb.ToString());
}
```

- [ ] **Шаг 2: Добавить лог при раздаче карт в руку**

Найти метод `RefillMainHand` (~строка 357) и добавить лог:

```csharp
private void RefillMainHand(Transform holder, List<BlockoutCardDisplay> targetList,
    bool isEnemy, int targetCount)
{
    if (holder == null) return;

    int before = targetList.Count;
    while (targetList.Count < targetCount)
    {
        var data = DrawMainCard();
        targetList.Add(SpawnCard(mainCardPrefab, holder, targetList.Count, targetCount, isEnemy, true, data));
        Debug.Log($"[CardManager] Dealt to {(isEnemy ? "Enemy" : "Player")}: {data.cardName} w={data.weight} IsThreat={data.IsThreat}");
    }

    int dealt = targetList.Count - before;
    if (dealt > 0)
        Debug.Log($"[CardManager] {(isEnemy ? "Enemy" : "Player")} hand: {before} → {targetList.Count} (+{dealt} cards)");
}
```

- [ ] **Шаг 3: Добавить экстренную раздачу когда рука полностью пустая**

В `RefillHandsToRoundLimit` после первого прохода `RefillMainHand` добавить отдельную проверку для пустых рук:

```csharp
public void RefillHandsToRoundLimit(int currentRoundIndex,
    Transform playerHandHolder   = null,
    Transform playerSpecialHolder = null,
    Transform enemyHandHolder    = null,
    Transform enemySpecialHolder = null)
{
    if (playerHandHolder   != null) _playerHandHolder   = playerHandHolder;
    if (playerSpecialHolder != null) _playerSpecialHolder = playerSpecialHolder;
    if (enemyHandHolder    != null) _enemyHandHolder    = enemyHandHolder;
    if (enemySpecialHolder != null) _enemySpecialHolder = enemySpecialHolder;

    EnsureDeckForRound(currentRoundIndex);

    bool allowSpecials = currentRoundIndex >= 1;
    int  mainCount     = GetMainCardsPerHand(currentRoundIndex);

    RefillMainHand(_playerHandHolder, _playerMainCards, false, mainCount);
    RefillMainHand(_enemyHandHolder,  _enemyMainCards,  true,  mainCount);

    if (allowSpecials)
    {
        RefillSpecialHand(_playerSpecialHolder, _playerSpecialCards, false, specialCardsPerHand);
        RefillSpecialHand(_enemySpecialHolder,  _enemySpecialCards,  true,  specialCardsPerHand);
    }

    // Экстренная дораздача если рука полностью пустая после всего выше
    // (MECHANICS.md: колода не пересоздаётся, но 0 карт делает дуэль невозможной)
    EmergencyRefillIfEmpty(_playerHandHolder, _playerMainCards, false, currentRoundIndex);
    EmergencyRefillIfEmpty(_enemyHandHolder,  _enemyMainCards,  true,  currentRoundIndex);

    // Если deck и discard оба пусты — аварийное пополнение колоды
    if (_mainDeck.Count == 0 && _mainDiscard.Count == 0)
    {
        const int emergencyCount = 3;
        Debug.Log("[CardManager] Deck+discard empty — emergency refill 3 cards each side.");
        for (int i = 0; i < emergencyCount * 2; i++)
            _mainDeck.Add(CreateWeightedMainCard());
        ShuffleList(_mainDeck);

        RefillMainHand(_playerHandHolder, _playerMainCards, false, mainCount);
        RefillMainHand(_enemyHandHolder,  _enemyMainCards,  true,  mainCount);
        if (allowSpecials)
        {
            RefillSpecialHand(_playerSpecialHolder, _playerSpecialCards, false, specialCardsPerHand);
            RefillSpecialHand(_enemySpecialHolder,  _enemySpecialCards,  true,  specialCardsPerHand);
        }
    }

    RelayoutHands();
}

/// <summary>
/// Если рука полностью пустая — выдаём ровно 2 случайных карты
/// (экстренный режим, без учёта оставшейся колоды).
/// </summary>
private void EmergencyRefillIfEmpty(Transform holder,
    List<BlockoutCardDisplay> hand, bool isEnemy, int roundIndex)
{
    if (holder == null || hand.Count > 0) return;

    const int emergencyCards = 2;
    Debug.Log($"[CardManager] EMERGENCY: {(isEnemy ? "Enemy" : "Player")} hand empty — dealing {emergencyCards} cards.");

    for (int i = 0; i < emergencyCards; i++)
    {
        // Если колода пустая — создаём карту напрямую
        BlockoutCardData data = _mainDeck.Count > 0
            ? DrawMainCard()
            : CreateWeightedMainCard();

        hand.Add(SpawnCard(mainCardPrefab, holder, hand.Count, emergencyCards, isEnemy, true, data));
        Debug.Log($"[CardManager] Emergency card: {data.cardName} w={data.weight}");
    }
}
```

- [ ] **Шаг 4: Build-проверка**

```
dotnet build "Assembly-CSharp.csproj" -c Release 2>&1 | grep -E " error "
```
Ожидаемый результат: `0 Error(s)`

---

## Self-Review

**Spec coverage:**
- ✅ EnemyAI переписан как pure class — Task 1
- ✅ AudioManager создан — Task 2
- ✅ Шейдер нуар+зерно — Task 3
- ✅ Позиция карт врага — Task 4
- ✅ Дораздача при пустой руке — Task 5
- ✅ Дебаг-лог колоды — Task 5

**Placeholder scan:**
- Все методы содержат полный код. Нет TBD/TODO.

**Type consistency:**
- `EnemyAI` — `ChooseAttackCards(IReadOnlyList<BlockoutCardDisplay>)` без `playerWeight` — обновлено в GameManager.GetAIAttackCards.
- `ChooseDefenseCards(IReadOnlyList<BlockoutCardDisplay>, int)` — сигнатура не изменилась.
- `ChooseShotTarget(List<BlockoutBulletType>, float)` — GameManager должен передавать `_chamber` напрямую, не через `_enemyAI.CalculateBlankPercentage` (метод удалён).

**Важное:** GameManager использует `ChooseTarget` — проверить что вызов обновлён:
```csharp
// Строка ~806 GameManager — найти ChooseTarget и убедиться что вызывает новый API:
private BlockoutSide ChooseTarget(BlockoutSide shooter, BlockoutBulletType bullet)
{
    if (_enemyAI == null) return BlockoutSide.Player;
    return _enemyAI.ChooseShotTarget(_chamber);
}
```
