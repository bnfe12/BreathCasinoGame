# BREATH CASINO — HOW_TO_WORK.md
# Как работать с ИИ и с кодом. До каждой мелочи.

---

## ЧАСТЬ 1 — КАК РАБОТАТЬ С ИИ

### Кто ты (ИИ)

Ты — разработчик проекта Breath Casino. Ты знаешь механики, архитектуру и историю проекта.
Твоя задача: привести код в соответствие с MECHANICS.md и PLANS.md.
Код подчиняется правилам, не наоборот.

### Приоритеты (строго по порядку)

1. **Работоспособность** — проект запускается, нет ошибок в Console
2. **Взаимодействие объектов** — игрок берёт карты, кладёт в слоты, берёт пистолет, стреляет
3. **Следование правилам** — логика кода = MECHANICS.md
4. **Качество кода** — только после 1-2-3

### Таблица решений — что делать самому, что спрашивать

| Ситуация | Действие ИИ |
|---|---|
| Маленький баг, 1 метод | Исправить самому, сообщить что сделал |
| Новая фича из PLANS.md | Реализовать, сообщить |
| Код плохой но работает | СПРОСИТЬ перед тем как трогать |
| Нужно переписать скрипт | СПРОСИТЬ, объяснить зачем |
| Архитектурное изменение | ТОЛЬКО после явного "да" от пользователя |
| Конфликт: код vs MECHANICS.md | Следовать MECHANICS.md, сообщить что исправил |
| Что-то непонятно в механике | СПРОСИТЬ, не придумывать |
| Нашёл баг не по задаче | Сообщить, не трогать без команды |

### Формат каждого ответа

```
✓ Сделал: [что и где]
✗ Не сделал: [что и почему]
⚠ Внимание: [на что обратить внимание / что проверить]
→ Следующий шаг: [рекомендация]
```

### Когда СТОП

Стоп = не делать ничего, написать пользователю и ждать:
- Нужно изменить больше 2 скриптов одновременно
- Нужно менять GamePhase машину состояний
- Нужно менять сигнатуры публичных методов
- Нужно удалять файлы или компоненты
- Нужно менять иерархию сцены (добавлять / удалять объекты)

### Предложения по механикам

Если ИИ видит проблему или хочет предложить изменение механики:
1. Записать предложение в `SUGGESTIONS.md` (создать если нет)
2. Сообщить пользователю: "Записал предложение в SUGGESTIONS.md"
3. НЕ менять MECHANICS.md самому — только после явного согласия пользователя
4. Пользователь читает, говорит "принять" или "нет" → тогда ИИ обновляет MECHANICS.md

Формат записи в SUGGESTIONS.md:
```
## [ДАТА] — [Название предложения]
Проблема: [что не работает или что неясно]
Предложение: [как изменить правило]
Затронет: [какие скрипты / механики]
```

### После каждой сессии

Обязательно обновить CHANGELOG_GLOBAL.md:
```
## [ДАТА]: [Краткое описание]
### Сделано
- [что]
### Файлы
- [скрипты]
### Проблемы
- [если есть]
```

---

## ЧАСТЬ 2 — АРХИТЕКТУРА ПРОЕКТА

### Стек

- Unity 6000.2.13f1 + URP 17.2.0
- Namespace: `BreathCasino.Blockout`
- Активная сцена: `BlockoutTest.unity`
- C# скрипты в `Assets/Scripts/`

### Главный принцип архитектуры

**Состояние игры — в данных. Объекты — только рендерят это состояние.**

Плохо:
- Хранить правду о дуэли в transform объекта
- Использовать позицию карты как источник истины
- Смешивать камеру и логику игры

Хорошо:
- Всё состояние в TestGameManager (фазы, HP, барабан, чья очередь)
- Объекты читают состояние и рендерят его
- Камера не знает про логику, логика не знает про камеру

### Скрипты — кто за что отвечает

| Скрипт | Ответственность | Не трогать без причины |
|---|---|---|
| `TestGameManager` | ВСЁ состояние игры. Фазы, раунды, HP, барабан, выстрел, дуэль. Единственный источник истины. | ⚠️ Высокий риск |
| `OxygenSystem` | Таймер O2. AddTime(), Reset(), TriggerLastBreath(). | ⚠️ Средний риск |
| `TestTicketManager` | Билеты игрока и врага. Apply(), UseByIndex(). | Средний риск |
| `BlockoutCardManager` | Колоды, раздача, создание визуалов карт. DealHands(). | Средний риск |
| `TestEnemyAI` | Только AI: выбор карт, выбор цели. Не содержит игровой логики. | Низкий риск |
| `BlockoutCameraController` | 5 состояний камеры. Пустышки. Переходы. | Низкий риск |
| `BlockoutHandAnimator` | handLow/handHigh анимация для обеих сторон. | Низкий риск |
| `BlockoutCardDisplay` | Визуал одной карты. Canvas, цвет, текст. | Низкий риск |
| `BlockoutCardInteractable` | Клик → выбор карты. ApplyVisualState(). OnDestroy cleanup. | Низкий риск |
| `BlockoutSlotInteractable` | Клик → разместить карту. Кэширует manager в Awake. | Низкий риск |
| `PlayerSelectionTracker` | Хранит выбранную карту игрока. | Низкий риск |
| `BlockoutCardSlot` | Якорь слота: SlotLabel, IsMainSlot, CardAnchor. HasCard, CurrentCard. | Низкий риск |
| `BlockoutSetupHelper` | Editor tool. Validate and Fix Scene. Camera Placeholders. Все новые фичи сюда. | — |
| `BlockoutTicketDisplay` | Физический 3D объект билета. | Низкий риск |
| `BlockoutTicketStack` | Стопка билетов на столе. | Низкий риск |
| `BlockoutTooltipDisplay` | Tooltip при наведении. | Низкий риск |
| `GunController` | Физика пистолета. PlaceOnTable(), PickUp(), ReturnToTable(). | Средний риск |
| `BulletVisual` | Визуал патрона. Create(BulletType), DropOnTable(), Freeze(), Hide(). | Низкий риск |
| `CameraRenderFix` | Фикс URP renderer index -1. Не трогать. | НЕ ТРОГАТЬ |

### GamePhase машина состояний

```
Waiting → BulletReveal → Dealing → Attack → Defense → Resolution → Shooting → GameOver
```

Переходы ТОЛЬКО через `TestGameManager.SetPhase(GamePhase)`.
Никакой другой скрипт не меняет фазу напрямую.

### Interface: IBlockoutInteractable

```csharp
public interface IBlockoutInteractable {
    void OnHoverEnter();
    void OnHoverExit();
    void OnClick();
    bool CanInteract { get; }
}
```

Реализуют: BlockoutCardInteractable, BlockoutSlotInteractable, BlockoutGunInteractable, BlockoutTicketInteractable, BlockoutTicketStackInteractable.

### ScriptableObjects

| SO | Поля | Создание |
|---|---|---|
| CardData | cardName, cardType, weight (2-8), customModelPrefab, cardSprite | Breach Casino → Create Default Deck |
| SpecialCardData | cardName, effectType, copiesInDeck (1-3) | Breach Casino → Create Default Special Cards |
| TicketData | ticketName, ticketType, requiresOwnTurn, requiresGun, effectValue | Breach Casino → Create Default Tickets |

### Новые фичи — правило

Новые фичи добавляются ТОЛЬКО через `BlockoutSetupHelper`.
НЕ в QuickSetupMenu. Причина: монолитный Quick Setup усложняет отладку (см. POSTMORTEM).

---

## ЧАСТЬ 3 — КАК РАБОТАТЬ С КОДОМ

### Перед тем как что-то менять

1. Прочитай MECHANICS.md — убедись что понимаешь правило
2. Найди скрипт где должна быть логика (таблица выше)
3. Оцени риск: сколько скриптов затронет изменение?
4. Если > 1 скрипта или сигнатура метода меняется → сообщи план сначала

### Диагностика проблем

| Симптом | Причина | Где искать |
|---|---|---|
| Объект не виден в Scene View | Проблема Setup / иерархии | Inspector, активность объекта |
| Виден в Scene, не виден в Game | Проблема камеры / рендера | Camera culling mask, near clip, renderer |
| Исчезает только в Play Mode | Проблема runtime логики | TestGameManager, Start() / Awake() порядок |
| NullReferenceException | Ссылка не назначена | Inspector поля, Awake() кэш |
| Карта не кладётся в слот | CanInteract = false | BlockoutSlotInteractable, GamePhase |
| Пистолет нельзя взять | CanInteract = false | BlockoutGunInteractable, GamePhase == Shooting |
| O2 не пополняется | Вызов AddTime() отсутствует | TestGameManager нужный метод |
| AI не ходит | thinkTime / coroutine | TestEnemyAI, SetPhase(Attack) |

### Правила написания кода

- Null-check на все внешние зависимости (GetComponent, FindObjectOfType)
- Кэшировать компоненты в Awake(), не в Update()
- Не использовать FindObjectOfType в Update() или часто вызываемых методах
- События (OnPlayerHPChanged, OnTicketsChanged) — для UI и побочных эффектов
- Не дублировать логику из TestGameManager в других скриптах

### Проверка после изменений

После каждого изменения проверить:
1. Проект компилируется без ошибок
2. Сцена открывается без ошибок в Console
3. Play Mode запускается
4. Конкретная механика которую менял — работает
5. Смежные механики не сломались (камера, взаимодействие, O2)

### Часто встречающиеся ловушки

- `OxygenSystem.Reset()` вызывать ТОЛЬКО при смене Раунда, не мини-раунда
- `DealHands()` — проверять `currentRoundIndex >= 1` для спецкарт
- `TestGameManager.SetPhase()` — единственный способ менять фазу
- `BlockoutSlotInteractable` — кэширует manager в `Awake()`, не в `Start()`
- `BlockoutCardInteractable` — `OnDestroy()` cleanup обязателен, иначе утечки событий
- Пустышки камеры — только через Setup Helper, не вручную в сцене

---

## ЧАСТЬ 4 — УПРАВЛЕНИЕ И ВВОД

### Полное управление (обязательно)

Игра управляема ТОЛЬКО мышью ИЛИ ТОЛЬКО клавиатурой. Оба пути должны работать.

| Действие | Мышь | Клавиатура |
|---|---|---|
| Выбрать карту | Клик по карте | 1–5 |
| Разместить карту | Клик по слоту | Space / Enter |
| Отменить выбор | Scroll вниз / клик мимо | S / Escape |
| Взять пистолет | Клик по пистолету | TBD |
| Переключить цель | ПКМ | ПКМ |
| Использовать билет | Клик по билету | 1–5 |
| Камера в карты | Scroll вниз | S |
| Камера в слоты | Scroll вверх | W |

### Слои (Layers) для Raycast

- `PlayerCard` — карты игрока
- `Slot` — слоты на столе
- `Gun` — пистолет
- `Ticket` — билеты

---

## ЧАСТЬ 5 — СТРУКТУРА ФАЙЛОВ

```
Assets/Scripts/
  Core/         GameEnums.cs, CardData.cs, SpecialCardData.cs, BreathCasinoInput.cs
  Managers/     BlockoutCardManager.cs, TestTicketManager.cs
  GameLogic/    TestGameManager.cs
  Systems/      OxygenSystem.cs
  AI/           TestEnemyAI.cs
  Camera/       BlockoutCameraController.cs
  Gameplay/     BlockoutHandAnimator.cs, PlayerSelectionTracker.cs
  Table/        BlockoutCardSlot.cs
  Items/        BulletVisual.cs, GunController.cs
  Visual/       BlockoutCardDisplay.cs, BlockoutCardDecals.cs
  Interaction/  BlockoutCardInteractable.cs, BlockoutSlotInteractable.cs,
                BlockoutGunInteractable.cs, BlockoutTicketInteractable.cs,
                BlockoutTicketStackInteractable.cs, IBlockoutInteractable.cs
  Tickets/      BlockoutTicketDisplay.cs, BlockoutTicketStack.cs
  UI/           BlockoutTooltipDisplay.cs, GameHUD.cs
  Bootstrap/    TestSceneBootstrap.cs
  Setup/        CameraRenderFix.cs
  Utils/        InputBridge.cs
  Editor/       BlockoutSetupHelper.cs, CardCreator.cs, PrefabCreator.cs,
                CameraRenderFixEditor.cs
```

Документация проекта:
```
MECHANICS.md        ← правила игры (источник истины)
PLANS.md            ← что сделано / не сделано
HOW_TO_WORK.md      ← этот файл
CHANGELOG_GLOBAL.md ← история изменений
SUGGESTIONS.md      ← предложения ИИ по механикам (создаётся по необходимости)
Docs/               ← архив старых планов и логов (только для справки)
```
