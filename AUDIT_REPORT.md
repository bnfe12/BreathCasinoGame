# BreathCasino — Аудит проекта

**Дата:** 2025  
**Unity:** 6000.2.13f1  
**Пакеты:** Input System 1.14.2, URP 17.2.0

---

## 1. Архитектура

| Компонент | Статус | Замечания |
|-----------|--------|-----------|
| GameManager | ✅ | Singleton, управляет раундами, HP, фазами |
| CardManager | ✅ | Колоды, раздача, MakeCard/MakeSpecial |
| TableManager | ✅ | Слоты, дуэли, PlayerSubmit/EnemySubmit |
| TicketManager | ✅ | Билеты, UseByIndex, DamageMultiplier |
| OxygenSystem | ✅ | Таймер кислорода, Last Breath |
| PlayerHand | ✅ | Карты игрока, Submit, InteractionRaycaster |
| EnemyAI | ✅ | AI ходы, ReceiveGun |
| GameHUD | ✅ | Polling в Update, не зависит от событий |
| InteractionRaycaster | ✅ | TryRaycastPlayerCard → PlayerCardDisplay (не враг), слои PlayerCard/Gun/Ticket |

**Риски:**
- Много синглтонов — возможны проблемы при перезагрузке сцен
- GameManager.Start создаёт HUD при `hud == null` — порядок Awake/Start может влиять

---

## 2. Конфигурация проекта

| Файл | Статус |
|------|--------|
| **TagManager** | ✅ Слои 6–9: PlayerCard, EnemyCard, Gun, Ticket |
| **EditorBuildSettings** | ⚠️ MainMenu, SampleScene, BreathCasino — BreathCasino не первый |
| **InputManager** | ⚠️ Старый Input Manager — проект использует новый Input System |
| **ProjectSettings** | ✅ Color Space: Linear, разрешение 1024×768 |

**Рекомендации:**
- Сделать BreathCasino первой сценой в Build Settings (если это основная игра)
- Убедиться, что Active Input Handling = Input System Package (или Both)

---

## 3. Код — найденные и исправленные проблемы

### TicketSelectUI
- ❌→✅ `AddComponent<RectTransform>()` — UI-объекты уже имеют RectTransform
- Исправлено: везде используется `GetComponent<RectTransform>()` + null-проверки

### SceneSetup
- ✅ `EnsureLayersInTagManager()` — слои создаются до использования
- ✅ `SafeSetLayer` / `SafeSetLayerRecursive` — проверка 0–31, try-catch

### GameHUD
- ✅ Polling в Update — не зависит от событий
- ✅ Защита от null для GameManager/OxygenSystem

### PlayerHand
- ✅ Раздельные `_selectedMain` / `_selectedSpec`
- ✅ InteractionRaycaster.TryRaycastPlayerCard

---

## 4. Потенциальные проблемы

### OxygenSystem
- `Refill()` вызывается, но в классе есть `AddOxygen` — проверить, что `Reset()` и `Refill()` корректно инициализируют `_cur`/`_max`

### GameManager.EnsureInteractableLayers
- Вызывается в Start — `TicketStackInteractable.Instance` может быть null, если порядок Awake другой

### Input
- `BreathCasinoInput` использует `Keyboard.current` / `Mouse.current` — в Editor при потере фокуса могут быть null
- `InputBridge` с `DefaultExecutionOrder(-1000)` вызывает `InputSystem.Update()`

### CardManager
- `allCards` / `allSpecialCards` — если пусты, `DealCards` не раздаст карты (есть предупреждение в лог)

---

## 5. Структура Assets

```
Assets/
├── Scripts/        ✅ Организовано по папкам (Camera, Core, Data, Editor, etc.)
├── Prefabs/        ✅ Cards, SpecialCards, Tickets
├── ScriptableObjects/ ✅ Cards, SpecialCards, Tickets
├── Scenes/         ✅ BreathCasino, MainMenu, SampleScene
├── Settings/       ✅ URP (PC_RPAsset, Mobile_RPAsset)
└── InputSystem_Actions.inputactions ⚠️ Есть, но BreathCasinoInput не использует — читает Keyboard/Mouse напрямую
```

---

## 6. Рекомендации

1. **Порядок сцен:** Установить BreathCasino первой в Build Settings, если это главная сцена.
2. **Input:** Проверить Project Settings → Player → Active Input Handling = Input System Package.
3. **Тесты:** Добавить EditMode/PlayMode тесты для GameManager, TableManager, TicketManager.
4. **Документация:** Добавить README с инструкцией Full Quick Setup и Ensure Interaction Layers.

---

## 7. Исправления, внесённые в сессиях

### Ранее
- TicketSelectUI: убраны все `AddComponent<RectTransform>()`, добавлены null-проверки
- SceneSetup: `EnsureLayersInTagManager()`, `SafeSetLayer` с try-catch
- GameHUD: переписан с polling
- PlayerHand: раздельные main/spec, InteractionRaycaster
- InteractionRaycaster: кэш слоёв, HitHasLayer по иерархии
- BreathCasinoInput: ленивая инициализация K/M
- CardCreator: EnsureInteractionLayers в начале FullQuickSetup

### Последние (дуэль, карты, камера)
- **TableManager:** переписан на AttackSlot/DefenseSlot. Правило «на карту атаки нельзя класть карту атаки». CanAttackerPlace/CanDefenderPlace, PutCard с SetParent(slot) для видимости.
- **PlayerCardDisplay / EnemyCardDisplay:** раздельные скрипты, наследуют CardDisplayBase. CardManager создаёт по isPlayerCard. InteractionRaycaster — только PlayerCardDisplay.
- **CameraRenderFix:** GetUniversalAdditionalCameraData, SetRenderer(0), CameraRenderFixBoot (отложенный фикс). Решает «No cameras rendering» при Quick Scene.
- **CameraRenderFixEditor:** исправление m_RendererIndex при sceneOpened и EnteredPlayMode.
- **Правила дуэли:** угроза не бьёт угрозу; ThreatPassed вызывает DiscardAll перед выдачей пистолета.
