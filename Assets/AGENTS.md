# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Project Overview

**Breath Casino** — 1v1 карточная дуэль с револьвером. FPS камера, бильярдный стол, механизм в центре, карты и билеты. Текущий проект — играбельный Unity-прототип core loop, не финальная игра.

**Рабочая сцена прототипа:** `Assets/Scenes/BlockoutTest.unity`

## Source of Truth

- `Assets/MECHANICS.md` — **источник истины по механикам**. Текущее подтверждённое состояние прототипа фиксируется именно там.
- `Assets/PLANS.md` — статус и план разработки, но не источник истины по правилам.

Если `PLANS.md`, старые комментарии или старые идеи противоречат `MECHANICS.md`, доверять нужно `MECHANICS.md`.

## Common Commands

```bash
dotnet build "Assembly-CSharp.csproj" -c Release
dotnet build "Assembly-CSharp-Editor.csproj" -c Release
```

Edit-mode tests лежат в `Assets/Tests/EditMode/`.
Для быстрой технической проверки изменений используй `dotnet build`.
Для поведенческой проверки gameplay предпочитай прогон в Unity Editor.

## Unity Setup

- Unity 6 (6000.x), URP
- TextMeshPro используется в проекте
- В коде используется `UnityEngine.InputSystem`
- Нет надёжного локального CLI-пайплайна для полного gameplay-test; Unity Editor остаётся основным способом проверки логики сцены

## Current Prototype Architecture

### Main runtime pieces

- `Assets/Scripts/Managers/GameManager.cs` — текущий монолит core loop. Держит фазы, мини-раунды, turn order, resolution, shooting, HP/O2, переходы между раундами.
- `Assets/Scripts/Managers/BlockoutCardManager.cs` — текущий deck/hand manager: initial deal, refill to round limit, discard, spawn, relayout.
- `Assets/Scripts/Bootstrap/SceneBootstrap.cs` — связывает сцену, менеджеры и ключевые ссылки при старте.
- `Assets/Scripts/AI/EnemyAI.cs` — логика выбора атаки/защиты и решения по выстрелу.
- `Assets/Scripts/Managers/TicketManager.cs` — текущая логика билетов и связанных side effects.

### Important supporting areas

- `Assets/Scripts/Interaction/` — raycast-based интеракции карт, слотов, билетов и пистолета.
- `Assets/Scripts/Table/` — слоты и поведение объектов стола.
- `Assets/Scripts/Gameplay/` — card/ticket display, hand visuals, shared gameplay presentation.
- `Assets/Scripts/Camera/` — camera states and transitions.
- `Assets/Scripts/Testing/` — scene validation helpers.

## Current Gameplay Truth To Preserve

These are important current behaviors and should not be “simplified away” unless explicitly changed in `Assets/MECHANICS.md`:

- Игрок и противник чередуют карточные фазы внутри одного мини-раунда.
- Суммирование двух защитных карт — важная механика.
- Проверка защиты игрока происходит **на подтверждении хода**, а не в момент выкладывания одной карты.
- Если суммарная защита игрока слабее уже выложенной Threat-атаки противника, такие карты возвращаются обратно в веер и ход не принимается.
- `SPACE` без карт считается валидным пропуском хода.
- Shooting должен стартовать только после победы атакующего с Threat-картой.
- Победа защитой или победа по весу без Threat не должна запускать стрельбу.
- После опустошения барабана должен стартовать новый мини-раунд, а руки должны восполняться до лимита раунда.

## Working Conventions In This Repo

- Не возвращать `Test*` naming — рабочие имена уже production-style: `GameManager`, `EnemyAI`, `SceneBootstrap`, `TicketManager`.
- Если меняешь механику, сначала сверься с `Assets/MECHANICS.md`, потом уже меняй код.
- Если меняешь scene-linked классы или имена MonoBehaviour, помни что сцена `Assets/Scenes/BlockoutTest.unity` содержит прямые YAML-ссылки.
- Для gameplay-фиксов особенно важны ручные проверки в Unity: build green не гарантирует правильный loop.

## Known Technical Reality

- `GameManager.cs` — большой монолит. Не дробить без явной необходимости; сначала чини текущую логику точечно.
- `BlockoutCardManager.cs` — временный hybrid deck/hand/spawn manager. Не путать с будущей финальной архитектурой.
- Билеты и часть более широких систем всё ещё прототипные; если их поведение не подтверждено в `MECHANICS.md`, не считай его окончательным.

## High-Risk Areas

При изменениях будь особенно осторожен в этих местах:

- coroutine-переходы в `GameManager.cs`
- логика подтверждения хода игрока (`SPACE`, staged cards, возврат карт в руку)
- условия старта Shooting
- refill/deal logic between mini-rounds
- scene wiring in `BlockoutTest.unity`

## Anti-Patterns / Bugs

Если видишь это в коде — это почти наверняка ошибка:

- O2 сбрасывается между мини-раундами
- стрельба запускается не через Threat-победу атакующего
- слабая защита игрока принимается как валидная без возврата карт в веер
- пистолет доступен вне корректной shooting-phase логики
- новый мини-раунд не стартует после опустошения барабана
- полная пересдача рук между мини-раундами вместо восполнения до лимита
