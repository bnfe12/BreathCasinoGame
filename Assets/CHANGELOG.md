# BREATH CASINO — CHANGELOG

Все изменения по милестонам. Новые записи сверху.

---

## M3 — Full Cycle (2026-03-20)

### Новые файлы
- `Assets/Scripts/UI/RoundTransitionUI.cs` — fade in/out экран перехода между раундами. Singleton. CanvasGroup анимация 0.4s fade. Субтитры по раунду.

### Assets/Scripts/Core/GameManager.cs
- `RestartGame()` — полный сброс без перезагрузки сцены: StopAllCoroutines, сброс всех полей, Chamber, руки, билеты, слоты, O2, колоды, StartRound(0)
- `PlayerSkipTurn()` — пропуск хода по Tab. Attack skip: смена хода → BulletReveal. Defense skip: враг получает пистолет → Shooting
- `TransitionToRound(int)` — coroutine, показывает RoundTransitionUI перед StartRound
- `NextRound()` — теперь вызывает TransitionToRound вместо прямого StartRound
- `AddO2(float)` — удалён мёртвый параметр `bool isPlayer` (не использовался), обновлены все 12 call sites
- `UseTicket` MedicalRation — исправлен баг: `PlayerHP =` → `_playerHP =` (свойство было get-only)
- `ShowVictory()` / `ShowDefeat()` — обновлены call sites: теперь передают аргументы (roundsCleared / reason)

### Assets/Scripts/UI/HUDDisplay.cs
- `ShowVictory(int roundsCleared)` — активирует gameOverPanel, заголовок "VICTORY", подзаголовок
- `ShowDefeat(string reason)` — активирует gameOverPanel, заголовок "GAME OVER", подзаголовок = reason
- `HideGameOver()` — скрывает панель, RemoveAllListeners
- Оба Show* вызывают RemoveAllListeners перед AddListener (защита от дублирования)
- `HideBulletReveal()` — очищает bulletRevealText
- `ShowBulletReveal()` — теперь сортирует патроны перед отображением, префикс "Chamber contains:"
- `ShowSkipPenalty(string)` — показывает сообщение в messageText на 2s, с CancelInvoke защитой
- `ShowInspection()` — добавлен CancelInvoke перед Invoke (защита от стакинга)
- Новые SerializeField поля: `gameOverPanel`, `gameOverTitleText`, `gameOverSubtitleText`, `restartButton`

### Assets/Scripts/Camera/CameraController.cs
- Tab key → `GameManager.Instance?.PlayerSkipTurn()` — вынесен ДО cooldown guard (не блокируется cooldown камеры)

### Assets/Scripts/Cards/DeckManager.cs
- `ResetAllDecks()` — публичный метод, пересоздаёт все три колоды. Вызывается из RestartGame
- Инициализация вынесена в `InitializeDecks()` → `BuildAllDecks()`

### Assets/Scripts/O2/O2System.cs
- `ForceReset()` — жёсткий сброс O2 при рестарте игры (семантически отличается от ResetO2 который вызывается при смене раунда)

### Настройка в Unity Editor (требуется вручную)
- Создать Canvas → Panel "GameOverPanel" (неактивен по умолчанию): TMP заголовок + подзаголовок + Button Restart → подключить в HUDDisplay Inspector
- Создать Canvas → Panel "RoundTransitionPanel" (неактивен по умолчанию): CanvasGroup + TMP roundText + subtitleText → добавить RoundTransitionUI компонент → подключить ссылки

---

## M2 — Сис��емы O2 (до 2026-03-20)

### Новые файлы
- `Assets/Scripts/O2/O2System.cs` — синглтон таймера. `AddSeconds()`, `ResetO2()`, события `onO2Changed` / `onO2Depleted`
- `Assets/Scripts/O2/O2GaugeController.cs` — стрелка манометра, lerp + jitter при высоком давлении, два экземпляра (10 ATM / 6 ATM)
- `Assets/Scripts/O2/O2LEDDisplay.cs` — TMP дисплей MM:SS, мигает при <20s
- `Assets/Scripts/O2/O2HUDTimer.cs` — HUD таймер O2

---

## M1 — Core Loop (до 2026-03-20)

### Новые файлы
- `Assets/Scripts/Core/GameEnums.cs` — все enum: GamePhase, CardType, SpecialType, PatronType, TicketType, DuelResult, CameraState
- `Assets/Scripts/Core/RoundData.cs` — параметры раунда: HP, handLimit, патроны, O2 бонусы, aiO2Coefficient
- `Assets/Scripts/Core/GameManager.cs` — монолит FSM. Фазы, раунды, мини-раунды, HP, O2, дуэль, стрельба, билеты
- `Assets/Scripts/Cards/CardData.cs` — структура карты (тип, вес, specialType, IsSpecial)
- `Assets/Scripts/Cards/DeckManager.cs` — три колоды, взвешенная раздача, DrawCard/DrawSpecial
- `Assets/Scripts/Cards/HandController.cs` — рука игрока, веер карт
- `Assets/Scripts/Cards/CardView.cs` — визуал карты (TMP цифры по углам, цвет по типу)
- `Assets/Scripts/Cards/SlotController.cs` — размещение карт в слоты
- `Assets/Scripts/AI/EnemyAI.cs` — AI: атака (приоритет Threat), защита (мин. карта >= атаки), стрельба (blanks >60% → в себя)
- `Assets/Scripts/Camera/CameraController.cs` — 5 состояний, lerp переходы, mouse look, cooldown 1s
- `Assets/Scripts/UI/HUDDisplay.cs` — текстовый HUD (phase, HP, BulletReveal, tickets, lastBreath)
- `Assets/Scripts/UI/TicketController.cs` — управление билетами игрока
- `Assets/Scripts/UI/TicketView.cs` — визуал билета
- `Assets/Scripts/Shooting/Chamber.cs` — барабан: Generate, Fire, Peek, Shuffle, IsEmpty
- `Assets/Scripts/Shooting/GunController.cs` — пистолет: ПКМ смена цели, выстрел

---

## 2026-04-08 - user override pass

- Shortened the post-damage screen-hit pulse so blur and distortion clear much faster after impact
- Changed O2-driven Last Breath to consume 1 HP from the current player HP instead of hard-forcing player HP to 1 in every round
- Kept enemy HP independent from the player's Last Breath outcome
- Enabled cleaner two-slot normal-card summation flow by auto-using the first free neutral slot for both normal and special staging
- Let enemy attack logic use up to two-card summed attacks, matching the same two-slot summation rule available to the player
- Added debug round jumps with F2 and F3 for faster round-2 and round-3 special-card checks
- Added physical round-card supply stacks and pickup flow before cards enter each hand
- Added a separate deal-mechanism animation script and separate card-supply-stack script
- Synced the docked gun with the deal mechanism and added more visible enemy gun pickup timing before AI shots
- Recorded the clarified cabinet truth: real drawer-issued cards, visible gear motion target, and right-side ticket acceptor as active design direction
- Rebuilt the deal mechanism around one shared central lift with a player shelf and enemy shelf, plus mechanical sway, settling, and gear spin
- Routed ticket issuing through the same mechanism shelves as cards and kept the shared mechanism open until both sides finish all pending pickups
- Recorded the new lever-and-ticket truth: one lever per side, hold to skip on defense, click to open ticket access, tickets only from rounds 2-3, and dialogue/UI moving toward world-space presentation with a proper start menu

## 2026-04-08 - lever, ticket, dialogue, and menu implementation

- Added per-side lever gameplay with click-to-open ticket access, hold-to-skip defense, visible close/retract flow, and enemy lever playback
- Moved voluntary ticket use behind the shared table mechanism and lever flow, with ticket runtime gated to rounds 2 and 3
- Added the first confirmed ticket earn rule in runtime: defensive overweight wins against normal attacks now issue a ticket
- Added AI ticket response and proactive enemy ticket usage hooks so enemy shelf use is visible instead of resolving silently
- Added world-space enemy dialogue scaffolding with scriptable event keys and a runtime dialogue display near the enemy body
- Added a runtime start menu with settings, credits, licenses, resolution-aware scaling, and a dynamic-lighting toggle
- Removed the remaining `Esc` ticket-return shortcut from player-facing controls so ticket shelf closure stays lever-driven
- Fixed physical ticket interaction so tickets become clickable only while actually in hand and no longer steal clicks from the shelf stack
- Added safer UI/runtime fallbacks for built-in fonts and made world-space dialogue overlays non-blocking for gameplay raycasts

## 2026-04-09 - external build recovery

- Added `Directory.Build.targets` to mirror Unity-generated assemblies from `Library/ScriptAssemblies` into `Temp/bin/Debug` before `dotnet build`
- Restored successful `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` builds with `/p:BuildProjectReferences=false` outside the fragile current Unity Temp state
- Replaced the obsolete TMP word-wrapping API in the world-space dialogue display
- Silenced Unity Inspector-only CS0649 noise in the affected runtime files so the standard `dotnet build` check now completes cleanly with `0 warnings, 0 errors`

## 2026-04-10 - deal mechanism acceptors and staged motion

- Added side-specific ticket acceptor sockets so both the player and the enemy can insert tickets through their own side of the table mechanism
- Added a base ticket-insertion animation that pushes the ticket into the acceptor gap before the ticket effect resolves
- Changed the cabinet motion to a staged sequence where the main lift body rises first and the side shelves extend after it
- Added a configurable closed-on-Play default for the cabinet so scenes can start with the mechanism fully hidden inside the table unless intentionally overridden
- Made bootstrap choose the best-configured deal mechanism in scene instead of blindly grabbing the first child under `tableRoot`, which prevents a blank duplicate component from hijacking the cabinet logic
- Added runtime scaffolding for missing mechanism empties so `LiftRoot`, shelf roots, card sockets, ticket sockets, gun rest, and side-specific ticket acceptors can be auto-created safely for tuning
- Moved the start menu into a dark full-screen overlay with a left-side menu column and locked the gameplay camera while the menu is open
- Added absolute lift-height tuning so the mechanism can now start from its edit-mode world `Y` and raise to a fixed world `Y` target instead of relying only on relative offsets
- Tightened interaction raycasts so world geometry blocks pickup and use actions; objects can no longer be clicked through walls or table surfaces if a collider is in the way
- Rebuilt the start menu into a full-screen dark overlay with a left-side navigation column and a separate content area
- Replaced `Licenses` with `Overview`, added `Exit` plus a confirmation dialog, and added a corner language switcher with a broad placeholder language list
- Added graphics settings for resolution selection and display mode selection (`Fullscreen Window`, `Windowed`, `Exclusive Fullscreen`) alongside the existing dynamic-lighting toggle
- Split game start into a dedicated intro sequence pipeline so pressing `Start Game` no longer jumps directly into the round loop
- Added a no-video intro sequence controller with black-screen story captions, hidden seat-in camera motion, and a centered radial "eyes opening" reveal before gameplay begins
- Added intro camera anchors in bootstrap and automatic fallback generation for the side-seat start pose and the seated gameplay pose
- Added a dedicated UI shader for the eye-opening mask effect and kept the menu / intro overlays independent from the broken scene `UI` root scale
- Made the scene validator stop treating a missing `Directional Light` as a hard failure, because the current main scene can legitimately run on non-directional lighting
- Fixed the intro eye-reveal UI shader for Unity `Image` rendering by adding the required `_MainTex` property
- Doubled the shared gameplay card scale so issued stacks, hand cards, and slotted cards all stay visually consistent at the larger physical size
- Prevented invalid `Threat + normal main card` attack combinations for both player staging and enemy AI; Threat attacks now pair only with specials
- Added automatic hierarchy repair inside the deal mechanism so imported Blender parent chains can be normalized onto `LiftRoot` / `ShelfRoot` / socket relationships at runtime
- Strengthened impact camera feedback with heavier blur haze, bright white glare patches, and milky highlight bloom on shots and player damage

## 2026-04-10 - menu, localization, and safe mechanism anchors

- Rebuilt the start menu as a fully independent overlay canvas, kept separate from the gameplay HUD and scene UI roots
- Added persistent menu preferences through `PlayerPrefs` for language, resolution, fullscreen mode, and dynamic-lighting state
- Added menu hover and click sound hooks so button audio can be assigned directly through the existing `BCAudioManager` UI cue fields
- Added a top-right debug-actions overlay and new hotkeys for mechanism toggle, enemy dialogue test, hit-haze test, gun re-dock, and ticket-shelf debug flow
- Reworked world-space enemy dialogue into a screen-space-follow bubble so scripted lines stay readable near the enemy without blocking input
- Localized the visible menu, HUD labels, debug overlay, and the main runtime status messages for the currently active translated languages
- Changed mechanism socket repair to stop reparenting imported Blender groups directly; instead the system now creates and uses dedicated safe runtime anchors inside each shelf
- Added a dedicated internal gun dock anchor path so the pistol can follow the mechanism without requiring the imported mesh hierarchy itself to become the dock

## 2026-04-10 - mechanism pose stabilization and menu selectors

- Replaced drift-prone mechanism motion with fixed closed/open pose caching so the lift and both shelves now animate from a stable authored closed state and return to that exact state on retract
- Changed the staged cabinet motion to a clearer mechanical sequence: lift rises first, shelves extend after; on close, shelves retract first and then the lift lowers
- Added back-style easing and stronger staged motion so the cabinet movement reads more like a machine instead of a flat linear lerp
- Restored the shared card presentation scale to a smaller physical size after the previous test doubling made the cards oversized in-scene
- Changed gun docking to preserve the authored scene world-scale of the full gun assembly, which prevents the multi-part revolver from inflating when parented to holders or the mechanism dock
- Replaced the fragile runtime dropdown implementation for resolution and display mode with explicit left/right selector rows
- Replaced language cycling with a compact language picker panel and limited the visible choices to the currently supported translated UI languages

## 2026-04-12 - architecture cleanup pass 1

- Removed the unused `RoundManager`, `CardPhaseManager`, and `ShootingManager` classes because they were no longer referenced by runtime and had already drifted away from the active gameplay truth in `MECHANICS.md`
- Moved HUD snapshot formatting and the debug-hotkeys legend out of `GameManager` into dedicated runtime helper files so the core loop no longer carries UI formatter types inline
- Split `GameManager` into a partial debug/presentation slice so HUD refresh, debug input, round-jump debug flow, and aim-update logic are isolated from the main duel/round loop
- Added a shared `BCRuntimeFontProvider` and switched runtime menu, tooltip, intro, scene overlay, and card-number rendering to that single cached font source instead of repeating the same fallback code in multiple classes
- Restored a green `Assembly-CSharp` and `Assembly-CSharp-Editor` build after the cleanup, including explicit project-file sync for the removed manager scripts and newly extracted helper files

## 2026-04-12 - intro draft and chamber-rule sync

- Synced round bullet rules with the current scenario draft so round 1 now generates `2-3` bullets, round 2 keeps `3-5`, and round 3 keeps `4-6`
- Kept the guaranteed red/live and blue/blank chamber structure in all rounds and changed round-3 yellow/explosive generation to a `50%` max-one rule
- Replaced the generic intro placeholder captions with the current black-screen casino / guard / restraint / loud-music narrative draft
- Added an opening opponent monologue sequence to the enemy dialogue controller and trigger it automatically on round 1 start
- Added a first post-shot reflection dialogue bank so damaging shots can surface the current narrative draft without blocking the round loop

## 2026-04-12 - presentation and AI handoff docs

- Added `Assets/PRESENTATION_BRIEF.md` as a structured project presentation summary with concept, loop, systems, current status, and suggested presentation flow
- Added `Assets/AI_HANDOFF_2026-04-12.md` as a continuity handoff for the next AI or collaborator, covering current truth, architecture, fragile areas, safe next steps, and scene-mechanism expectations
