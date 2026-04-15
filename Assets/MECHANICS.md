# BREATH CASINO - MECHANICS.md

## Active Truth

This file is the active gameplay truth for the repository.

Priority:

1. Current user instructions
2. This file
3. `Assets/PLANS.md`
4. `CLAUDE.md`
5. Archived notes

If archived rules contradict the active rules below, the archived rules are for reading history only and have no implementation weight.

## User Override Notes - 2026-04-08

These notes were requested directly by the user and currently override older wording below without deleting history:

- O2-driven Last Breath costs the player `1 HP` from the current value instead of forcing HP to `1` in all rounds
- The opponent must not mirror or inherit the player's Last Breath HP result
- Both sides may sum two normal combat cards across the two neutral slots
- The two slots are neutral and may hold normal or special cards; normal + normal defense sums by total weight
- Debug flow should allow direct jumps to round `2` and round `3` to validate specials
- Round cards should be issued as a temporary physical stack first, then moved into hand only after pickup
- The physical card stack should appear inside the real animated deal drawer or shelf, not as a floating abstract spawn point
- The table uses one shared central lift mechanism built into the table, not two unrelated drawers
- The shared lift raises one block upward from inside the table and exposes two separate shelves, one per side
- The shelf object itself is the intended animation target; the card socket inside it should inherit the shelf motion
- The same drawer-based issue flow is used both for round-start hands and for the 3-card refill after a side runs out of main cards
- The gun must visibly rest on and move with the table deal mechanism while docked
- The docked gun should stay synchronized with the drawer / cabinet movement while it is resting there
- The deal mechanism should feel mechanical rather than perfectly clean: small shake, side drift, imperfect settling, and end-point vibration are desired
- Visible gears on the cabinet are part of the motion logic and should animate together with the cabinet raise/lower cycle
- The shared cabinet uses two ticket acceptors, one per side, so both the player and the enemy insert tickets through their own side of the mechanism
- Ticket usage should visibly animate the ticket entering into the acceptor gap instead of disappearing instantly
- Tickets are always usable; when the player takes a ticket from the table, the cabinet should auto-present the acceptor so the ticket can be inserted
- Enemy ticket usage should stay automatic, but visually livelier than an instant silent resolve
- Tickets are also issued through the same raised shelf area used for cards, on the correct side for each player
- If cards and/or tickets are pending for either side in the same beat, the shared mechanism stays open until every pending issue for both sides has been taken
- The mechanism should close only after all currently issued cards and tickets have been accepted from both shelves
- The deal mechanism motion is two-stage: the main lift body rises first, then the side shelves extend
- The recommended Play-mode default is the mechanism hidden inside the table, fully lowered and retracted, until gameplay actively opens it
- Each side has its own lever on its own side of the table
- Holding a side's lever is the physical skip-turn action, and this skip interaction is only valid during the defending decision window
- Clicking or pulling a side's lever opens that side's ticket-access interaction instead of skipping
- After the ticket drawer is opened by lever pull, the side must still click the ticket itself to take or use it
- Closing or returning the ticket drawer back into the table should also require lever interaction rather than an invisible instant retract
- If one side opens ticket access and the opponent sees it, the opponent may decide to open and use a ticket too, but is not forced to do so
- Tickets affect the gun state and the players only; they are not attack cards, defense cards, or direct card-combat tools
- Tickets only enter active runtime from round `2` through round `3`; round `1` does not issue tickets
- One confirmed ticket earn case is a successful defensive overtake, such as beating a normal attack weight `5` with a normal defense weight `6`
- Shooting flow must show explicit gun pickup for both player and enemy instead of instant teleport-fire behavior
- The current opening narrative draft uses a black-screen intro with casino ambience, a guard warning the player not to open their eyes, a forced seat/strap setup, a loud music shock beat, and then an opening monologue from the gas-mask opponent
- The current opponent narrative draft also includes a post-shot reflection line pool that should trigger after damaging shots without hard-blocking the live card loop
- Enemy dialogue support must exist as scriptable event-driven lines even before the final narrative script is written
- Dialogue presentation should be diegetic or world-space, positioned near or below/above the enemy, not pasted in the center of the player's camera
- Interaction prompts should follow the same diegetic direction rather than relying only on flat HUD overlays
- Future UI work must remain resolution-aware and scale-aware
- The game needs a start menu with settings, credits, and licenses
- Settings should stay intentionally small for now; a confirmed graphics option is a toggle for dynamic lighting rather than a large graphics menu

## Current Verified Direction

- The project is moving from the old blockout scene toward `Assets/Scenes/mainScene.unity`
- The environment source is `Assets/Scenes/mainScene.fbx`
- The old `BlockoutTest` scene has been removed from the active project
- Cards, slots, tickets, and temporary anchors may still use placeholder prefabs

## 1. Round Structure

- The game uses 3 rounds
- Each round keeps its own deck rules
- The current round continues until HP defeat resolves the round
- Mini-rounds continue inside the round

### Round values

| Parameter | Round 1 | Round 2 | Round 3 |
|---|---:|---:|---:|
| HP | 2 | 3 | 4 |
| O2 start | 60s | 50s | 40s |
| Deck size | 17 | 24 | 27 |
| Start hand size | 4 | 5 | 6 |
| Empty-hand refill | 3 | 3 | 3 |
| Bullets per mini-round | 2-3 | 3-5 | 4-6 |
| Explosive bullet | No | No | 50% chance, max 1 |

## 2. Decks And Draws

### Main deck

- A main deck is created in code at the start of the round
- The deck exists in memory first, then cards are dealt out from it
- Deck generation is not fully random
- Deck composition depends on the current round and the configured deck size
- Card draws from the deck are random
- If the deck becomes empty during the round, a new deck for the same round is generated and play continues from the exact current state

### Current in-memory deck content

Current working deck recipes use only main combat cards:

- `Resource`
- `Threat`

Current working round recipes:

#### Round 1 deck, 17 cards
- Resource 2 x4
- Resource 3 x3
- Resource 4 x2
- Threat 4 x1
- Resource 5 x2
- Resource 6 x2
- Threat 6 x1
- Resource 7 x1
- Threat 8 x1

#### Round 2 deck, 24 cards
- Resource 2 x5
- Resource 3 x4
- Resource 4 x3
- Threat 4 x2
- Resource 5 x3
- Resource 6 x2
- Threat 6 x2
- Resource 7 x1
- Resource 8 x1
- Threat 8 x1

#### Round 3 deck, 27 cards
- Resource 2 x5
- Resource 3 x4
- Resource 4 x3
- Threat 4 x2
- Resource 5 x3
- Resource 6 x3
- Threat 6 x2
- Resource 7 x2
- Resource 8 x1
- Threat 8 x2

### Hand rules

- At round start, each side receives round-based main cards:
  - Round 1: `4`
  - Round 2: `5`
  - Round 3: `6`
- A side draws `3` new main cards only when that side has completely run out of main cards
- If only one side is empty, only that side draws
- Refill is not "fill to cap"

## 3. Enemy Card Visibility

- Enemy hand cards are hidden from the player
- Hidden enemy cards should display as `?`
- Cards can be revealed when they are actually committed to combat resolution

## 4. Special Cards

### Current active scope

The current priority is a stable playable card loop, so only two special effects are active right now:

- `Cancel`
- `Duplicate`

All other special-card ideas or previous implementations are archived only unless re-approved later.

### Hand layout

- Main cards and special cards share one holder per side
- If there are no specials, main cards stay centered
- If specials exist, main cards shift left and specials appear on the right

## 5. Duel Rules

- Threat cards are attack cards
- Threat attack cards may not be combined with another main card; only special cards may accompany a Threat attack
- Defense cannot use Threat cards
- Player defense validation happens on submit or confirm, not on first placement
- If player defense total is weaker than the already staged enemy Threat attack, the staged defense is rejected and returned to hand
- `SPACE` with no staged cards is a valid skip
- Shooting starts only when the attacker wins with a Threat attack
- Winning by plain weight without Threat does not start shooting
- Blocking or defending a Threat does not start shooting

## 6. Shooting

- Gun flow must use an explicit pickup or confirm step
- The gun must not be freely usable outside the proper shooting state
- Enemy or player may choose target according to shooting rules
- Bullet color mapping is active truth:
  - `Blue` = `Blank`
  - `Red` = `Live`
  - `Yellow` = `Explosive`
- Every mini-round chamber guarantees at least:
  - `1` blue blank in all rounds
  - `1` red live in all rounds
- Round `3` may also include `1` yellow explosive with a `50%` chance

### O2 from shots

- Damage shot into opponent: `+30s`
- Non-damaging shot: `+10s`
- Self-shot with damage must not be treated as opponent-hit reward

## 7. Tickets

- Tickets are lower priority than the core card loop
- Ticket logic must stay simple and safe while card gameplay is stabilized
- Tickets must not destabilize deck flow or refill rules
- Tickets are not part of attack or defense card resolution
- Tickets are voluntary utility actions that can be used when the side chooses, through the lever-driven ticket interaction flow
- Ticket runtime is disabled in round `1` and active only from rounds `2` and `3`
- Current active ticket subset:
  - `Inspection`
  - `MedicalRation`
- Archived ticket types may remain in enums or old code for history, but active runtime must reject them instead of re-enabling their old effects

### Ticket access flow

- Each side opens ticket access through its own lever
- Hold lever: skip turn during defense only
- Click or pull lever: open ticket interaction
- Clicking the ticket itself is still required after opening the ticket shelf
- The same lever interaction is used again to return or close the ticket shelf back into the table
- Enemy ticket use is allowed to mirror or answer the player's ticket interaction, but remains an AI choice
- Ticket earn rules will continue to expand later, but the currently confirmed earn example is winning a defense by overweighting a normal attack
- Ticket insertion should use the side-specific acceptor animation before applying the ticket effect

## 8. Camera And Input

- `W`, `S`, mouse wheel up, and mouse wheel down must work reliably
- Camera transitions must remain stable even while card layouts change
- The camera flow must support the single-holder hand layout cleanly
- Dialogue and interaction guidance should not be centered as a normal HUD-only overlay when a world-space presentation is possible

## 9. Presentation And Menus

- Enemy dialogue must support scripted event triggers even before the final full scenario is written
- Dialogue placement should live near the enemy actor, either above or below, in world space or a diegetic presentation style inspired by Dead Space rather than glued to the camera center
- Interaction prompts should aim for the same grounded presentation style
- Menu and interface work must scale correctly across different resolutions and screen scales
- The game needs a start menu flow with at least:
  - start game
  - settings
  - credits
  - licenses
- Settings scope can stay small for now; a confirmed early option is toggling dynamic lighting

## 10. Scene Direction

### Active target

- Target scene: `Assets/Scenes/mainScene.unity`
- Environment source: `Assets/Scenes/mainScene.fbx`
- Gun source: `Assets/Scenes/gun1212.fbx`

### Temporary scene setup

The temporary generated main scene should include:

- managers root
- debug HUD
- camera
- gameplay anchors or placeholders
- slots and holders with current working scripts
- a generated scene builder entry point: `Breath Casino/Main Scene/Rebuild mainScene`

The user will manually continue scene dressing and script placement where desired.

## Archived Context

The repository previously used blockout-specific rules, blockout naming, and an archived `BlockoutTest` scene that is no longer part of the active project.

That material is retained only as history and must not override the active rules above.
