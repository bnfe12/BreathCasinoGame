# CLAUDE.md

## Current Source Of Truth

Priority order for this repository:

1. Direct user instructions in the current conversation.
2. `Assets/MECHANICS.md`
3. `Assets/PLANS.md`
4. This file (`CLAUDE.md`)
5. Archived notes and old blockout references

If older notes, old plans, old comments, or any `Blockout*` naming contradict the current user instructions or the active mechanics file, they are archival only and must not drive implementation.

## Current Project State

**Breath Casino** is currently being moved away from the old blockout setup toward a new main scene based on the Blender-exported environment in `Assets/Scenes/mainScene.fbx`.

### Active scene target

- Working target scene: `Assets/Scenes/mainScene.unity`
- Environment source asset: `Assets/Scenes/mainScene.fbx`
- Gun asset source: `Assets/Scenes/gun1212.fbx`
- The old `BlockoutTest` scene has been removed from the active project

### Active runtime direction

- Runtime should no longer depend on blockout naming or blockout-specific assumptions
- Cards, slots, tickets, and temporary interaction anchors may still use simple prefabs or placeholders
- Table, environment, and gun should be prepared around the custom scene assets
- The active editor entry point for scene generation is `Breath Casino/Main Scene/Rebuild mainScene`

## Current Gameplay Truth

### Rounds and decks

- Each round has its own deck size:
  - Round 1: `17`
  - Round 2: `24`
  - Round 3: `27`
- Each round has its own start hand size:
  - Round 1: `4`
  - Round 2: `5`
  - Round 3: `6`
- A round deck is created in code for the current round and remembered while the round is active
- The deck exists in memory first, and only then cards are dealt from that in-memory deck
- The deck composition is driven by the round configuration, not by fully random generation
- Draws from the deck are random
- If the round deck becomes empty, a new deck for the same round is created and play continues from the current game state

### Hand flow

- At round start, each side receives `4 / 5 / 6` main cards based on round
- Refill is not "fill to cap"
- A side receives `3` new main cards only when that side has completely run out of main cards
- Refill is side-specific: if only one side is empty, only that side draws

### Enemy information

- Enemy hand cards must be hidden from the player while in hand
- Played cards can be revealed in active combat resolution

### Special cards

- Current priority is a stable card game loop with only `2` active special effects
- Until expanded again, active special effects are:
  - `Cancel`
  - `Duplicate`
- Other special effects may remain in archived code or docs, but they have no active gameplay weight unless re-approved

### Tickets

- Tickets are lower priority than the core card loop
- For now, only a minimal safe subset should be considered active
- Active ticket subset for the current implementation pass:
  - `Inspection`
  - `MedicalRation`
- Ticket logic must not be allowed to destabilize the core card loop
- Archived ticket types may still exist in enums or old code, but active runtime should reject them instead of restoring legacy behavior

### Hand presentation

- Main cards and special cards now share one visual holder per side
- If there are no special cards, main cards stay centered
- If special cards are present, main cards shift left and specials appear on the right

### Camera

- `W`, `S`, mouse wheel up, and mouse wheel down must switch camera states reliably and consistently
- Camera transitions must not jam or depend on old blockout-only setup

## Active Technical Guidance

- Prefer targeted fixes over large rewrites unless a rewrite is clearly safer
- If a full rewrite becomes the safest option, stop and warn the user before doing it
- Remove active `Blockout` naming from working runtime or editor paths where it can cause confusion
- If `GameManager` keeps growing, split responsibilities in safe steps instead of one risky rewrite

## Archived Context

The repository previously used:

- the removed legacy `BlockoutTest` scene
- `Blockout*` runtime or editor helpers
- blockout scene validation or setup flows

These references are preserved only to understand previous implementation history. They are not the active target architecture anymore.
