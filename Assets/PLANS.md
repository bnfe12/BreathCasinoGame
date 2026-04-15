# BREATH CASINO - PLANS.md

## Active Planning Notes

This file tracks the current implementation plan.

Source-of-truth reminder:

- Gameplay truth lives in `Assets/MECHANICS.md`
- This file is a working execution plan
- Archived plan items may stay here for history, but they must not override active mechanics

## Current Stage

**Stage:** migration from archived blockout flow to the new main-scene runtime

**Primary scene target:** `Assets/Scenes/mainScene.unity`

**Environment source:** `Assets/Scenes/mainScene.fbx`

## Current State Snapshot - 2026-04-13

- `Assets/Scenes/mainScene.unity` is the only active gameplay scene target
- `Assets/Scenes/BlockoutTest.unity` has been removed from the active project
- The deal mechanism now uses a simplified shared-lift model:
  - the authored start pose is the closed/start truth
  - `liftRoot` is the single primary animation driver
  - both shelves and the gun move together with the lift
  - side shelf extension is intentionally disabled for now
- The current lift raise amount is driven by `liftRaisedLocalOffset` in `BCDealMechanism`
- Atmosphere is now driven toward a warm yellow-sand look:
  - yellow/orange fog
  - warmer ambient colors
  - runtime dust particles
- Camera fringe / contour lines were reduced and now fade with depth/fog
- Material spawning pressure was reduced by caching shared materials for cards, tickets, and slot visuals

## Immediate Follow-Up Backlog - 2026-04-13

### 1. Mechanism final stabilization

- [ ] Verify in Play Mode that the mechanism itself rises and lowers correctly in `mainScene`
- [ ] Verify that both shelves stay synchronized with the lift for the entire motion
- [ ] Verify that card spawn, ticket spawn, and gun docking do not move shelf roots anymore
- [ ] Tune final raise height by adjusting `liftRaisedLocalOffset`
- [ ] Decide later whether shelf side-extension should return as a second phase or stay removed
- [ ] If mechanism behavior is still unstable after scene validation, replace the current motion pass with a cleaner dedicated animation/state driver

### 2. Scene validation in Unity

- [ ] Validate authored hierarchy links in `mainScene`:
  - `liftRoot`
  - `PlayerShelfMotionRoot`
  - `EnemyShelfMotionRoot`
  - issue sockets
  - ticket accept sockets
  - gun rest anchor
- [ ] Confirm that shelf visuals are children of the motion roots, not independent movers
- [ ] Confirm that enemy shelf visual rotation lives on the visual child only, not on the motion root

### 3. Atmosphere tuning

- [ ] Tune fog density for readability vs suffocating sandstorm mood
- [ ] Tune dust particle count, size, and velocity after in-editor visual review
- [ ] Tune contour fade so distant objects do not stay too visible through dense fog
- [ ] If needed, move more of the line/fog interaction into shader-side fog weighting instead of post-process approximation

### 4. Intro and shooting presentation

- [ ] Finish the mini-round intro sequence:
  - bullet reveal with physical presentation
  - emphasis on the gun
  - room dimming through fog/light reduction rather than hard black
  - revolver drum spin sound
  - gun placement sound
  - mechanism rise only after that beat
- [ ] Add inspector-assignable audio slots where clips are still placeholders

### 5. Settings and UI

- [ ] Validate all settings controls in Play Mode on the rebuilt scrollable settings panel
- [ ] Keep overlay UI crisp and isolated from scene upscaling
- [ ] Continue moving nonessential graphics settings into `Advanced Options`

### 6. Architecture / optimization

- [ ] Continue removing duplicated runtime responsibilities from `GameManager`
- [ ] Review hot loops and repeated allocations in gameplay flow
- [ ] Keep shared material caching as the default path for temporary visuals
- [ ] When adding future presentation objects, ensure temporary runtime objects are cleaned up with their owners

### 7. Deferred work to revisit later

- [ ] FSR2 / newer FSR integration investigation
- [ ] O2-system integration decision against current architecture
- [ ] Full intro direction pass with reactive enemy/body animation
- [ ] More aggressive split of presentation logic away from core round logic

## Immediate Priorities

### Active user override work - 2026-04-08

- [ ] Shorten post-damage camera hit effect
- [ ] Keep O2 Last Breath as `-1 HP` instead of hard-forcing `1 HP` in later rounds
- [ ] Preserve enemy HP independently from player Last Breath
- [ ] Let both sides use two-slot normal-card summation cleanly
- [ ] Add direct debug jumps to rounds `2` and `3` for special-card validation
- [x] Replace direct hand spawning with physical round card stacks plus pickup
- [x] Anchor the physical card stacks inside the real animated drawer / shelf object instead of floating scene points
- [x] Raise/lower the deal mechanism and keep the docked gun synchronized with it
- [x] Make the deal script attachable directly to the chosen cabinet / drawer object in scene
- [x] Show explicit enemy gun pickup before AI shooting
- [x] Add mechanical secondary motion for the cabinet: shake, slight drift, imperfect stop, and settling
- [x] Animate the visible gears together with cabinet movement
- [x] Replace the old twin-drawer assumption with one shared lift block plus one shelf per side
- [x] Add per-side ticket acceptor flow so both player and enemy insert tickets through the mechanism with a visible in-slot animation
- [x] Make cabinet motion two-stage so the lift body rises before the shelves extend
- [x] Keep the cabinet closed and lowered by default on Play unless a scene explicitly opts into a raised start
- [x] Make enemy ticket use feel physically present instead of purely invisible logic
- [x] Keep the shared mechanism open until both sides finish taking all currently issued cards and tickets
- [x] Add one lever per side and split lever input into hold-to-skip vs click-to-open-ticket flow
- [x] Restrict tickets to rounds `2` and `3`
- [x] Implement confirmed ticket earn cases, starting with defensive overweight wins
- [x] Let AI optionally answer the player's ticket interaction with its own ticket interaction
- [x] Make ticket shelf closing a visible lever-driven action back into the table
- [x] Add scriptable enemy dialogue triggers and data plumbing before final narrative writing
- [x] Present dialogue in a world-space / diegetic style near the enemy instead of camera-centered HUD
- [ ] Present interaction prompts in a world-space / diegetic style instead of camera-centered HUD
- [x] Build a start menu with settings, credits, and licenses
- [x] Keep menu and UI layout resolution-aware and screen-scale-aware
- [x] Add a small settings surface including a dynamic-lighting toggle
- [x] Add the current black-screen opening narrative draft to the intro system as the active placeholder story beat
- [x] Add the current opening monologue and post-shot opponent dialogue draft to the enemy dialogue controller as scriptable content
- [ ] Expand the intro from black-screen captions into a fuller directed sequence with chair restraint, speaker/music timing, and reactive enemy body animation when the final scene animation pass begins
- [ ] Decide how much of the opponent monologue should block gameplay versus continue in parallel with the first live round

### 1. Repair runtime stability

- [x] Fix current compile errors
- [x] Restore green `Assembly-CSharp` build
- [x] Remove active blockout wording from working runtime paths

### 2. Stabilize the card loop

- [x] Round decks use sizes `17 / 24 / 27`
- [x] Round decks are generated once per round from fixed round rules
- [x] Card draws are random from the current round deck
- [x] If a round deck empties, regenerate the same round deck and continue from the same game state
- [x] Each side starts a round with `4 / 5 / 6` main cards
- [x] A side draws `3` new main cards only when that side reaches `0` main cards
- [x] Refill is side-specific, not shared

### 3. Stabilize special cards

- [x] Restrict active special-card scope to `Cancel` and `Duplicate`
- [x] Keep other special-card ideas as archived or inactive context only
- [x] Move main and special cards into one holder layout per side
- [x] Keep main cards centered when no special exists
- [x] Shift main cards left and specials right when specials are present

### 4. Enemy information rules

- [x] Enemy hand cards are hidden
- [x] Hidden enemy cards display `?`
- [x] Played cards can reveal when committed to combat

### 5. Camera and interaction reliability

- [x] Keep camera state flow aligned with the current single-holder hand layout
- [ ] Validate `W`, `S`, wheel up, and wheel down manually in-editor on the generated scene
- [ ] Run a manual pass on card selection and slot placement during camera transitions
- [x] Preserve a clean debug HUD while the final UI is still temporary

### 6. Main scene bootstrap

- [x] Create `Assets/Scenes/mainScene.unity`
- [x] Instantiate `mainScene.fbx` as the environment root
- [x] Create required gameplay placeholders and anchors
- [x] Add managers, holders, slots, debug HUD, and bootstrap wiring
- [x] Leave final scene dressing to the user

### 7. Audio flexibility

- [x] Expand the sound manager so it is easier to grow later without rewriting the system
- [x] Keep the API usable even while real audio content is still incomplete

### 8. Ticket scope

- [x] Restrict active ticket generation to a minimal safe subset
- [x] Keep archived ticket effects readable in code without treating them as active design
- [x] Reject archived ticket types in active runtime instead of executing legacy effects
- [x] Move all voluntary ticket use behind lever-driven access instead of direct hotkey-only use
- [x] Separate ticket utility from card combat so tickets never enter attack or defense slots

### 10. Lever and dialogue pass

- [x] Add player lever interaction component with hold-detection for skip and click/pull for ticket access
- [x] Add enemy lever action playback so AI decisions are visible on the table
- [x] Add lever state machine support for open, hold, close, and blocked states
- [x] Add ticket-open and ticket-close waits to the core loop without breaking current card phases
- [x] Add enemy dialogue controller with event-driven line hooks
- [x] Add a first world-space dialogue presenter near the enemy body

### 11. Frontend and menu pass

- [x] Replace temporary start flow with a proper main menu scene or menu state
- [x] Add settings UI with a dynamic-lighting toggle
- [x] Add credits and licenses screens
- [ ] Validate menu readability and scale at multiple resolutions

### 9. GameManager split

- [x] Start separating non-critical responsibilities out of `GameManager`
- [x] Extract HUD formatting into a dedicated helper
- [x] Remove unused legacy manager stubs that duplicated outdated round/card/shooting logic
- [x] Split debug input and HUD refresh flow into a separate partial slice of `GameManager`
- [ ] Plan the next safe split: ticket access flow, duel resolution, or scene presentation responsibilities
- [ ] Consider splitting intro narrative, enemy dialogue sequencing, and other presentation-only flows further out of `GameManager` once the new story beats stabilize

## Current Working Assumptions

These assumptions are active until the user replaces them with stricter rules:

- Special cards are lower scope than the base main-card loop
- Tickets are lower priority than card gameplay stabilization
- The old blockout scene has been removed; `mainScene` is the only active gameplay target

## Archived Context

Older plan items referenced:

- the removed `BlockoutTest` scene
- blockout setup tooling
- old `O2System` naming
- old deck or hand manager naming

These remain useful only to understand previous work and should not drive new implementation decisions.
