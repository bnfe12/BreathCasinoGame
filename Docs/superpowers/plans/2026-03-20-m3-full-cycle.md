# M3 — Full Cycle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Завершить игровой цикл — экран победы/поражения, визуальный переход между раундами, BulletReveal UI, штраф за пропуск хода, перезапуск без перезагрузки сцены.

**Architecture:** Все изменения — расширение существующих скриптов (GameManager, HUDDisplay) плюс один новый скрипт RoundTransitionUI. Никаких новых менеджеров — монолит GameManager остаётся единственным владельцем состояния. UI-слой только отображает то, что GameManager сообщает через методы HUDDisplay.

**Tech Stack:** Unity 6 URP, C#, TextMeshPro, Coroutines, Unity Input System

---

## Файловая карта

| Файл | Действие | Что меняется |
|------|----------|--------------|
| `Assets/Scripts/UI/HUDDisplay.cs` | Modify | GameOver panel, round transition panel, BulletReveal reveal/hide, skip penalty UI |
| `Assets/Scripts/UI/RoundTransitionUI.cs` | Create | Анимация перехода между раундами (fade + текст раунда) |
| `Assets/Scripts/Core/GameManager.cs` | Modify | SkipTurn(), RestartGame(), штраф за пропуск, вызов RoundTransitionUI, BulletReveal hide |
| `Assets/Scripts/Core/GameEnums.cs` | Modify | Добавить SkipPenalty enum (опц., если нужен) |

---

## Task 1: GameOver Screen — Victory & Defeat panels

**Цель:** Заменить текстовые заглушки `ShowVictory()` / `ShowDefeat()` на полноценные панели с кнопкой Restart.

**Files:**
- Modify: `Assets/Scripts/UI/HUDDisplay.cs`
- Modify: `Assets/Scripts/Core/GameManager.cs`

- [ ] **Step 1: Добавить поля панелей в HUDDisplay**

```csharp
[Header("Game Over")]
[SerializeField] private GameObject gameOverPanel;
[SerializeField] private TMP_Text gameOverTitleText;   // "VICTORY" / "GAME OVER"
[SerializeField] private TMP_Text gameOverSubtitleText; // "Round X cleared" / "You ran out of air"
[SerializeField] private UnityEngine.UI.Button restartButton;
```

- [ ] **Step 2: Реализовать ShowVictory / ShowDefeat / HideGameOver в HUDDisplay**

```csharp
public void ShowVictory(int roundsCleared)
{
    if (gameOverPanel) gameOverPanel.SetActive(true);
    if (gameOverTitleText) gameOverTitleText.text = "VICTORY";
    if (gameOverSubtitleText) gameOverSubtitleText.text = $"All {roundsCleared} rounds cleared";
    if (restartButton) restartButton.onClick.AddListener(() => GameManager.Instance.RestartGame());
}

public void ShowDefeat(string reason)
{
    if (gameOverPanel) gameOverPanel.SetActive(true);
    if (gameOverTitleText) gameOverTitleText.text = "GAME OVER";
    if (gameOverSubtitleText) gameOverSubtitleText.text = reason;
    if (restartButton) restartButton.onClick.AddListener(() => GameManager.Instance.RestartGame());
}

public void HideGameOver()
{
    if (gameOverPanel) gameOverPanel.SetActive(false);
    if (restartButton) restartButton.onClick.RemoveAllListeners();
}
```

- [ ] **Step 3: Обновить вызовы в GameManager**

Найти все `hud?.ShowVictory()` и `hud?.ShowDefeat()` и заменить:

```csharp
// В NextRound() когда next >= rounds.Length:
hud?.ShowVictory(rounds.Length);

// В ApplyDamage() и LastBreathTimer() при GameOver:
hud?.ShowDefeat("You ran out of air");   // из LastBreathTimer
hud?.ShowDefeat("You were shot");        // из ApplyDamage HP=0 без LastBreath
```

- [ ] **Step 4: Добавить RestartGame() в GameManager**

```csharp
public void RestartGame()
{
    _hasUsedLastBreath = false;
    PlayerHasGun = false;
    ShootingAtEnemy = true;
    _doubleDamageActive = false;
    PlayerHand.Clear();
    EnemyHand.Clear();
    PlayerTickets.Clear();
    EnemyTickets.Clear();
    ClearSlots();
    hud?.HideGameOver();
    deckManager?.ResetAllDecks();
    StartRound(0);
}
```

- [ ] **Step 5: Добавить ResetAllDecks() в DeckManager**

Открыть `Assets/Scripts/Cards/DeckManager.cs`, добавить публичный метод:

```csharp
public void ResetAllDecks()
{
    // Пересоздать все три колоды с нуля
    InitializeDecks(); // вызвать существующую инициализацию
}
```

> Примечание: проверить как DeckManager инициализирует колоды в Awake/Start и вынести в отдельный метод если нужно.

- [ ] **Step 6: Настроить в Unity Editor**
  - Создать Canvas → Panel "GameOverPanel" (изначально неактивный)
  - Добавить TMP_Text для заголовка и подзаголовка
  - Добавить Button "Restart"
  - Подключить ссылки в Inspector HUDDisplay

---

## Task 2: Round Transition Screen

**Цель:** Показывать экран перехода между раундами ("Round 2 begins", fade in/out) перед StartRound().

**Files:**
- Create: `Assets/Scripts/UI/RoundTransitionUI.cs`
- Modify: `Assets/Scripts/Core/GameManager.cs`

- [ ] **Step 1: Создать RoundTransitionUI.cs**

```csharp
using System.Collections;
using UnityEngine;
using TMPro;

public class RoundTransitionUI : MonoBehaviour
{
    public static RoundTransitionUI Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private float displayDuration = 2.5f;
    [SerializeField] private CanvasGroup canvasGroup;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (panel) panel.SetActive(false);
    }

    public IEnumerator ShowRoundTransition(int roundIndex)
    {
        if (panel) panel.SetActive(true);
        if (roundText) roundText.text = $"Round {roundIndex + 1}";
        if (subtitleText) subtitleText.text = GetSubtitle(roundIndex);

        // Fade in
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            float t = 0f;
            while (t < 0.4f) { t += Time.deltaTime; canvasGroup.alpha = t / 0.4f; yield return null; }
            canvasGroup.alpha = 1f;
        }

        yield return new WaitForSeconds(displayDuration);

        // Fade out
        if (canvasGroup != null)
        {
            float t = 0.4f;
            while (t > 0f) { t -= Time.deltaTime; canvasGroup.alpha = t / 0.4f; yield return null; }
            canvasGroup.alpha = 0f;
        }

        if (panel) panel.SetActive(false);
    }

    string GetSubtitle(int index)
    {
        return index switch
        {
            0 => "The duel begins",
            1 => "The stakes rise",
            2 => "Final round",
            _ => ""
        };
    }
}
```

- [ ] **Step 2: Обернуть StartRound() в GameManager через coroutine**

Заменить прямой вызов `StartRound(next)` в `NextRound()`:

```csharp
void NextRound()
{
    int next = CurrentRoundIndex + 1;
    if (next >= rounds.Length)
    {
        SetPhase(GamePhase.GameOver);
        hud?.ShowVictory(rounds.Length);
        return;
    }
    StartCoroutine(TransitionToRound(next));
}

IEnumerator TransitionToRound(int index)
{
    if (RoundTransitionUI.Instance != null)
        yield return RoundTransitionUI.Instance.ShowRoundTransition(index);
    StartRound(index);
}
```

- [ ] **Step 3: Настроить в Unity Editor**
  - Создать Canvas → Panel "RoundTransitionPanel" (изначально неактивный)
  - Добавить CanvasGroup компонент на Panel
  - Добавить TMP_Text для roundText и subtitleText
  - Добавить RoundTransitionUI компонент на GameObject
  - Подключить ссылки в Inspector

---

## Task 3: BulletReveal — показ и скрытие

**Цель:** BulletReveal сейчас показывает состав барабана через HUD текст, но никогда не скрывает его. Нужно скрывать после перехода в Dealing.

**Files:**
- Modify: `Assets/Scripts/UI/HUDDisplay.cs`
- Modify: `Assets/Scripts/Core/GameManager.cs`

- [ ] **Step 1: Добавить HideBulletReveal() в HUDDisplay**

```csharp
public void HideBulletReveal()
{
    if (bulletRevealText) bulletRevealText.text = "";
}
```

- [ ] **Step 2: Вызвать HideBulletReveal в начале DoDealing()**

```csharp
IEnumerator DoDealing()
{
    hud?.HideBulletReveal(); // скрыть после reveal
    // ... остальной код без изменений
```

- [ ] **Step 3: Улучшить ShowBulletReveal — показывать типы без порядка**

Текущий код показывает все патроны подряд — это правильно (состав виден, порядок скрыт). Добавить сортировку для читаемости:

```csharp
public void ShowBulletReveal(List<PatronType> contents)
{
    if (bulletRevealText == null) return;
    // Показываем состав (отсортированный), не порядок
    var sorted = new List<PatronType>(contents);
    sorted.Sort();
    var sb = new System.Text.StringBuilder("Chamber contains: ");
    foreach (var p in sorted) sb.Append($"[{p}] ");
    bulletRevealText.text = sb.ToString();
}
```

---

## Task 4: Skip Turn — штраф за пропуск хода

**Цель:** Кнопка Tab пропускает ход. Штраф: -1 O2 бонус (игрок не получает O2 за слот в этом ходу) + ход переходит противнику если атакующий пропустил.

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Modify: `Assets/Scripts/UI/HUDDisplay.cs`

- [ ] **Step 1: Добавить PlayerSkipTurn() в GameManager**

```csharp
/// <summary>Called by input when player presses Tab to skip their turn.</summary>
public void PlayerSkipTurn()
{
    if (CurrentPhase == GamePhase.Attack && IsPlayerTurn)
    {
        // Attacker skips — defender wins by default, no gun awarded
        hud?.ShowSkipPenalty("You skipped — no attack bonus");
        ClearSlots();
        IsPlayerTurn = !IsPlayerTurn;
        SetPhase(GamePhase.BulletReveal);
    }
    else if (CurrentPhase == GamePhase.Defense && !IsPlayerTurn)
    {
        // Defender skips — attacker wins automatically
        hud?.ShowSkipPenalty("You skipped defense — attacker wins");
        // Force AttackerWins result: give gun to attacker (enemy)
        PlayerHasGun = false;
        GiveTicket(toPlayer: false); // attacker (enemy) gets ticket
        ClearSlots();
        SetPhase(GamePhase.Shooting);
    }
}
```

- [ ] **Step 2: Добавить ShowSkipPenalty() в HUDDisplay**

```csharp
public void ShowSkipPenalty(string message)
{
    if (messageText) messageText.text = message;
    Invoke(nameof(ClearMessage), 2f);
}

void ClearMessage() { if (messageText) messageText.text = ""; }
```

- [ ] **Step 3: Подключить Tab к PlayerSkipTurn через Input System**

Открыть `Assets/Scripts/Camera/CameraController.cs` или найти где обрабатывается ввод. Добавить обработку Tab:

```csharp
// В Update() или в методе обработки ввода:
if (Keyboard.current.tabKey.wasPressedThisFrame)
    GameManager.Instance?.PlayerSkipTurn();
```

> Если ввод обрабатывается через InputSystem_Actions — добавить action "SkipTurn" в .inputactions файл и подписаться на него.

---

## Task 5: Restart без перезагрузки сцены — финальная проверка

**Цель:** Убедиться что RestartGame() (из Task 1) корректно сбрасывает все системы.

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Modify: `Assets/Scripts/Cards/DeckManager.cs`

- [ ] **Step 1: Проверить DeckManager.ResetAllDecks()**

Открыть `Assets/Scripts/Cards/DeckManager.cs`, убедиться что метод пересоздаёт все три колоды идентично Awake(). Если инициализация в Awake — вынести в `InitializeDecks()` и вызвать из обоих мест.

- [ ] **Step 2: Сбросить Chamber в RestartGame()**

```csharp
public void RestartGame()
{
    _hasUsedLastBreath = false;
    PlayerHasGun = false;
    ShootingAtEnemy = true;
    _doubleDamageActive = false;
    _chamber = new Chamber(); // сброс барабана
    PlayerHand.Clear();
    EnemyHand.Clear();
    PlayerTickets.Clear();
    EnemyTickets.Clear();
    ClearSlots();
    hud?.HideGameOver();
    hud?.UpdateHP(0, 0);
    deckManager?.ResetAllDecks();
    StartRound(0);
}
```

- [ ] **Step 3: Сбросить O2 при рестарте**

O2 уже сбрасывается в `StartRound()` через `O2System.Instance.ResetO2()` — проверить что это работает корректно после рестарта (O2System не в состоянии LastBreath).

Добавить в O2System если нужно:
```csharp
public void ForceReset()
{
    // Сбросить любые флаги LastBreath, восстановить нормальный таймер
    _depleted = false;
    ResetO2();
}
```

И вызвать в RestartGame():
```csharp
O2System.Instance?.ForceReset();
```

- [ ] **Step 4: Сбросить RoundTransitionUI**

```csharp
// В RestartGame() перед StartRound(0):
if (RoundTransitionUI.Instance != null)
    StopAllCoroutines(); // остановить любые активные переходы
```

---

## Task 6: Финальная интеграция и проверка в Editor

**Цель:** Убедиться что весь M3 цикл работает end-to-end в Unity Editor.

- [ ] **Step 1: Настроить сцену**
  - GameOverPanel — неактивен по умолчанию
  - RoundTransitionPanel — неактивен по умолчанию
  - Все ссылки в Inspector подключены

- [ ] **Step 2: Проверить полный цикл вручную**
  - Запустить игру → дойти до GameOver (дать врагу убить игрока)
  - Нажать Restart → игра начинается с Round 1 без п��резагрузки сцены
  - Дойти до победы в Round 1 → появляется RoundTransitionUI → начинается Round 2
  - Пройти все 3 раунда → Victory screen

- [ ] **Step 3: Проверить Skip**
  - В фазе Attack нажать Tab → ход переходит, штраф показан
  - В фазе Defense нажать Tab → враг получает пистолет и стреляет

- [ ] **Step 4: Проверить BulletReveal**
  - Начало мини-раунда → текст состава барабана виден 2 секунды
  - Переход в Dealing → текст исчезает

---

## Известные ограничения M3

- `VoidTransaction` билет (из MECHANICS.md §8) — не реализован в M1 и не входит в M3. Добавить в M4 или отдельным тикетом.
- Штраф за пропуск (Tab) задан как "ход переходит" — точный штраф в MECHANICS.md помечен TBD. Реализация выше соответствует логике игры, но может потребовать балансировки.
- Анимация механизма (подъём в Dealing) — M4, не M3.
