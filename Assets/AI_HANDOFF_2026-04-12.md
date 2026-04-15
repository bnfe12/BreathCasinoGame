# BREATH CASINO - AI HANDOFF (2026-04-12)

## Purpose

This file is a direct handoff for the next AI assistant or collaborator.

It explains:

- what is true right now
- what changed recently
- what is stable
- what is still fragile
- where to continue next without re-discovering the whole project

---

## 1. Source Of Truth Order

Use this order when resolving contradictions:

1. latest direct user instructions
2. `Assets/MECHANICS.md`
3. `Assets/PLANS.md`
4. historical notes or archived ideas

Do **not** let old plans override `Assets/MECHANICS.md`.

---

## 2. Current Project Identity

This is a **playable vertical prototype**, not a finished game.

### Project summary

- first-person 1v1 survival card duel
- physical central table mechanism
- cards, tickets, and revolver
- atmospheric intro and scriptable enemy dialogue
- target scene has shifted toward `Assets/Scenes/mainScene.unity`

### Important tone

Do not flatten the project into a generic card battler.
Its identity depends on:

- physicality
- scene interaction
- the mechanism
- threat-to-shooting escalation
- enemy presence

---

## 3. Current Gameplay Truth To Preserve

These behaviors are important and should not be casually simplified away:

- 3-round structure
- mini-rounds inside rounds
- player and enemy alternate attack / defense card phases
- player defense can sum two normal cards
- player defense is validated on submit, not on first placement
- weak player defense returns to hand instead of being accepted
- `SPACE` with no cards is a valid skip
- shooting starts only after a winning `Threat` attack
- no `Threat` win = no shooting
- chamber depletion starts a new mini-round
- hands refill only when a side has fully run out of main cards

---

## 4. Current Round Truth

| Round | HP | O2 | Start Hand | Refill | Bullets | Explosive |
|---|---:|---:|---:|---:|---:|---:|
| 1 | 2 | 60s | 4 | 3 | 2-3 | No |
| 2 | 3 | 50s | 5 | 3 | 3-5 | No |
| 3 | 4 | 40s | 6 | 3 | 4-6 | 50% chance, max 1 |

### Chamber truth

- every mini-round guarantees at least `1 Blank`
- every mini-round guarantees at least `1 Live`
- round 3 may also include `1 Explosive`

### Bullet color truth

- `Blue = Blank`
- `Red = Live`
- `Yellow = Explosive`

---

## 5. Current Card Truth

### Active main card types

- `Resource`
- `Threat`

### Important rules

- `Threat` is attack-only
- `Threat` cannot combine with another normal main card
- `Threat` may combine only with a special card
- defense cannot use `Threat`
- two normal defense cards may sum

### Active special-card scope

Keep the active scope intentionally narrow:

- `Cancel`
- `Duplicate`

Do not casually re-enable archived special effects unless the user explicitly restores them into truth.

---

## 6. Current Ticket Truth

- tickets are disabled in round `1`
- tickets are active in rounds `2` and `3`
- tickets are utility actions, not attack / defense cards
- tickets affect the gun state and the players
- tickets use side-specific lever access
- hold lever = skip defense
- click/pull lever = open ticket access

### Current active ticket subset

- `Inspection`
- `MedicalRation`

### Important presentation truth

Tickets should be issued through the same central mechanism flow as cards and inserted through side-specific acceptors.

---

## 7. Intro And Dialogue Truth

### Intro direction

The current intro draft is now part of the active presentation foundation:

- black screen
- casino ambience
- guard warning the protagonist not to open their eyes
- restrained seat setup
- opponent already seated in a gas mask
- loud club-music shock
- opponent opening monologue

### Enemy dialogue direction

The enemy must feel like a present, scripted character.

Current runtime dialogue support includes:

- scripted keyed dialogue sets
- world-space / near-diegetic display
- opening monologue sequence
- post-shot reflection line pool

Do not move dialogue back to a generic center-screen HUD unless absolutely necessary.

---

## 8. Current Scene Direction

### Active target scene

- `Assets/Scenes/mainScene.unity`

### Legacy reference

- `Assets/Scenes/BlockoutTest.unity`

Use `mainScene` as the active presentation target unless the user explicitly says otherwise.

---

## 9. Mechanism Truth

The table uses **one shared central mechanism**, not two separate independent drawers.

### Current intended structure

- one `liftRoot`
- one `playerShelfRoot`
- one `enemyShelfRoot`
- side-specific card sockets
- side-specific ticket sockets
- side-specific ticket accept sockets
- one gun rest anchor on the mechanism
- optional gear roots for visible gear motion

### Motion truth

- open: lift rises first, shelves extend second
- close: shelves retract first, lift lowers second
- movement should feel mechanical, not sterile
- small drift, shake, and settling are desired

---

## 10. Mechanism Inspector Contract

For `BCDealMechanism`, the intended references are:

- `Lift Root` -> the shared lift block
- `Player Shelf Root` -> the player-side shelf
- `Enemy Shelf Root` -> the enemy-side shelf
- `Player Card Socket` -> inside `playerShelfRoot`
- `Enemy Card Socket` -> inside `enemyShelfRoot`
- `Player Ticket Socket` -> inside `playerShelfRoot`
- `Enemy Ticket Socket` -> inside `enemyShelfRoot`
- `Player Ticket Accept Socket` -> inside or aligned with the player-side ticket accept path
- `Enemy Ticket Accept Socket` -> inside or aligned with the enemy-side ticket accept path
- `Gun Rest Anchor` -> on the mechanism, ideally parented to `liftRoot`
- `Gear Roots` -> all visible gear transforms that should rotate

### Important warning

Do not use broad scene placeholders like `PlayerSlots` or `EnemySlots` as long-term issue sockets if they are not true shelf children.

If cards appear on top of the mechanism instead of the shelves, this is almost always a scene-anchor or parenting problem.

---

## 11. Current Known Fragile Area

The most fragile area right now is the **mechanism scene wiring**.

### Common symptom cluster

- cards spawning on top of the lift body
- drawers not extending correctly
- gun not following the mechanism correctly
- motion returning to the wrong pose

### Most likely causes

- card sockets still pointing to old transforms not inside the shelf
- player and enemy shelf roots assigned incorrectly
- gun dock anchor not parented to the mechanism
- imported Blender hierarchy carrying old parent assumptions

### Safe rule

If a scene-linked mechanic behaves incorrectly, inspect authored anchors first before rewriting gameplay logic.

---

## 12. Major Recent Code Changes

### Architecture cleanup pass 1

Recently completed:

- removed unused legacy manager stubs:
  - `CardPhaseManager`
  - `RoundManager`
  - `ShootingManager`
- extracted HUD formatting helpers out of `GameManager`
- split debug/presentation responsibilities into `GameManager.Debug.cs`
- added shared runtime font provider

### Why this matters

This means the project has already started moving away from dead code and duplicated responsibility.
Future refactors should continue in this direction carefully and incrementally.

---

## 13. Current Important Runtime Files

### Core

- `Assets/Scripts/Managers/GameManager.cs`
- `Assets/Scripts/Managers/GameManager.Debug.cs`
- `Assets/Scripts/Managers/BCCardManager.cs`
- `Assets/Scripts/Managers/TicketManager.cs`
- `Assets/Scripts/AI/EnemyAI.cs`

### Scene / mechanism

- `Assets/Scripts/Bootstrap/SceneBootstrap.cs`
- `Assets/Scripts/Table/BCDealMechanism.cs`

### Presentation

- `Assets/Scripts/UI/BCStartMenuController.cs`
- `Assets/Scripts/UI/BCIntroSequenceController.cs`
- `Assets/Scripts/AI/BCEnemyDialogueController.cs`
- `Assets/Scripts/UI/BCWorldSpaceDialogueDisplay.cs`
- `Assets/Scripts/Camera/BCCameraController.cs`
- `Assets/Scripts/Rendering/BCCameraGrainFeature.cs`

### Utility / UI

- `Assets/Scripts/UI/BCRuntimeFontProvider.cs`
- `Assets/Scripts/UI/HudSnapshot.cs`
- `Assets/Scripts/UI/HudFormatter.cs`
- `Assets/Scripts/UI/DebugActionsLegendBuilder.cs`

---

## 14. Current Safe Next Steps

If continuing development safely, the best next steps are:

1. stabilize mechanism scene anchors and shelf motion
2. finish mechanism visual tuning
3. expand intro direction with sound and body timing
4. continue splitting non-core presentation logic out of `GameManager`
5. keep tickets and dialogue additive, not loop-breaking

### Do not do this casually

- do not rewrite the full game loop from scratch
- do not merge tickets into the core attack/defense card system
- do not turn the mechanism back into abstract floating spawn points
- do not move dialogue back to generic center-screen messaging

---

## 15. Presentation Summary For Humans

If a collaborator asks “what is this project right now?”, the correct short answer is:

> Breath Casino is a playable atmospheric first-person survival card duel prototype where card tactics, a physical central delivery mechanism, revolver-based risk, and diegetic narrative presentation are already integrated into one core loop.

---

## 16. Build Checks

Current recommended checks:

```powershell
dotnet build "Assembly-CSharp.csproj" -c Release /p:BuildProjectReferences=false
dotnet build "Assembly-CSharp-Editor.csproj" -c Release /p:BuildProjectReferences=false
```

### Current status at handoff time

- `Assembly-CSharp` build: green
- `Assembly-CSharp-Editor` build: green

---

## 17. Current Documentation To Read First

For any new AI or collaborator, read in this order:

1. `AGENTS.md`
2. `Assets/MECHANICS.md`
3. this file
4. `Assets/PLANS.md`
5. `Assets/PRESENTATION_BRIEF.md`
6. then inspect the active runtime files

---

## 18. Final Reminder

This project already has a strong identity.

When continuing work, optimize for:

- preserving tension
- preserving physicality
- preserving the mechanism as the core interaction surface
- preserving the separation between cards, tickets, and shooting
- growing the project without flattening it into a generic systems prototype

---

## 19. Additional Handoff - 2026-04-13

### Latest implemented state

- `BlockoutTest` scene was removed from `Assets/Scenes`
- `mainScene` is the only active gameplay scene target
- `BCDealMechanism` was simplified on purpose:
  - authored closed pose is the truth
  - `liftRoot` handles the shared raise/lower
  - shelves do not extend independently right now
  - gun remains synchronized with the shared lift
- Raise amount is now controlled by serialized `liftRaisedLocalOffset`
- Warm sandy fog and runtime dust were added in `SceneBootstrap`
- Post-process object fringe was reduced and now fades with depth/fog

### What must be checked next in Unity Editor

1. Play Mode validation of mechanism movement in `mainScene`
2. Confirmation that shelf roots no longer drift during card or ticket issue
3. Final tuning of lift height
4. Visual tuning of fog and dust after live camera review
5. Validation that contour fade is acceptable in heavy fog

### Current practical rule for future mechanism work

- Do not reintroduce shelf side-extension until the shared raise/lower is fully stable
- Do not animate shelves through spawn sockets or spawn logic
- If the mechanism breaks again, prefer replacing the motion implementation cleanly instead of layering more corrective offsets
