# BREATH CASINO - PRESENTATION BRIEF

## Purpose

This file is a presentation-ready summary of the current project state.

Use it when:

- preparing a milestone presentation
- explaining the project to a new collaborator
- summarizing the current vertical prototype

Gameplay truth still lives in `Assets/MECHANICS.md`.
This file is a communication layer, not a mechanics override.

---

## 1. Project Identity

**Breath Casino** is a dark first-person 1v1 survival card duel prototype.

The player is forced into a seated duel against an opponent in a gas mask. The match is played at a physical table with a central mechanical lift that issues cards and tickets. Card combat can escalate into revolver shooting, turning tactical advantage into a life-or-death action sequence.

### Core identity

- first-person psychological chamber duel
- card tactics fused with physical world interaction
- mechanical table as a real gameplay object
- survival pressure through HP, oxygen, and revolver risk
- narrative tension delivered through intro, sound, and enemy dialogue

---

## 2. Genre And Positioning

### Genre

- card duel
- survival chamber game
- atmospheric first-person prototype

### Player fantasy

- survive an enforced ritual game
- read risk through cards and chamber state
- physically interact with the table, lever, tickets, and revolver
- endure the presence of an experienced opponent who is already part of this world

### Unique angle

This is not a traditional card battler with a flat UI board.

The project combines:

- card combat
- physical scene interaction
- a central mechanical delivery device
- revolver-based threat resolution
- diegetic dialogue and atmospheric framing

---

## 3. High-Level Game Loop

1. Start a round
2. Issue cards through the central mechanism
3. Generate a chamber for the mini-round
4. One side attacks
5. The other side defends
6. Compare weights and active effects
7. If the attacker wins with a `Threat`, move into shooting
8. When the chamber empties, start a new mini-round
9. When HP defeat resolves the round, move to the next round or end the match

### Design intention

The loop creates rising pressure:

- cards create tactical pressure
- `Threat` creates lethal pressure
- the revolver turns that pressure into an embodied risk moment

---

## 4. Round Structure

There are **3 rounds**.

Each round has:

- different HP
- different oxygen pressure
- different hand size
- different chamber range
- different deck profile

### Current active round values

| Round | Player HP | Enemy HP | O2 Start | Start Hand | Empty-Hand Refill | Bullets |
|---|---:|---:|---:|---:|---:|---:|
| 1 | 2 | 2 | 60s | 4 | 3 | 2-3 |
| 2 | 3 | 3 | 50s | 5 | 3 | 3-5 |
| 3 | 4 | 4 | 40s | 6 | 3 | 4-6 |

### Design effect

- Round 1 teaches the structure under lower load
- Round 2 expands options and adds ticket runtime
- Round 3 becomes the most dangerous state with larger hands and possible explosive bullets

---

## 5. Card System

### Active main cards

- `Resource`
- `Threat`

### Confirmed active rules

- `Threat` is an attack card
- `Threat` attacks may not combine with another normal main card
- `Threat` may only be accompanied by a special card
- defense cannot use `Threat`
- the player may sum two normal defense cards
- defense validation happens on submit, not on first placement
- if the player's staged defense is weaker than the enemy's staged `Threat` attack, it is rejected and returned to hand
- `SPACE` with no cards is a valid skip

### Current active special-card scope

The current prototype keeps the special-card scope intentionally narrow:

- `Cancel`
- `Duplicate`

This protects the core loop while the base duel remains under active stabilization.

---

## 6. Deck And Draw Rules

### Deck behavior

- a round deck is created in memory at round start
- each round uses its own deck recipe
- draws are random from that current round deck
- if the deck empties, it is rebuilt for the same round without resetting the match state

### Current deck sizes

- Round 1: `17`
- Round 2: `24`
- Round 3: `27`

### Hand rules

- round start hands are `4 / 5 / 6`
- refill is always `3`
- refill only happens when a side has completely run out of main cards
- refill is side-specific, not shared

### Important presentation rule

Cards do not spawn directly into the hand “from nowhere”.

The intended and active presentation flow is:

1. cards exist in the round deck in memory
2. they are physically issued on the mechanism shelf
3. they are taken from the shelf
4. they then enter the side’s hand

---

## 7. Chamber And Bullet Rules

### Bullet types

- `Blue = Blank`
- `Red = Live`
- `Yellow = Explosive`

### Current chamber truth

- every mini-round guarantees at least `1 Blank`
- every mini-round guarantees at least `1 Live`
- round 3 may include `1 Explosive` with a `50%` chance
- round 1 uses `2-3` bullets
- round 2 uses `3-5` bullets
- round 3 uses `4-6` bullets

### Why this matters

The chamber is risky, but not purely random chaos.

The player has:

- partial structural knowledge
- incomplete ordering knowledge
- pressure to interpret risk instead of merely guessing

---

## 8. Shooting Phase

Shooting is a distinct phase, not a direct abstract effect.

### Active rules

- shooting starts only if the attacker wins with a `Threat`
- winning by plain weight without `Threat` does not start shooting
- the revolver must be physically picked up
- the gun is not freely usable outside the correct shooting phase
- player and enemy both visibly use the gun

### Current O2 rules from shots

- damaging shot into opponent: `+30s`
- non-damaging shot or self-directed outcome: `+10s`

### Intent

The revolver is meant to feel like the material consequence of a successful threat, not a separate minigame disconnected from the card system.

---

## 9. Ticket System

Tickets are a separate utility layer and must not destabilize the core duel.

### Current ticket truth

- tickets are inactive in round `1`
- tickets are active only in rounds `2` and `3`
- tickets do not attack
- tickets do not defend
- tickets affect the gun state and the players
- ticket use is voluntary and lever-driven

### Current active ticket subset

- `Inspection`
- `MedicalRation`

### Access flow

- each side has its own lever
- hold lever = skip defense
- click or pull lever = open ticket access
- the ticket still has to be clicked after opening the access state
- closing ticket access is also lever-driven

---

## 10. The Central Table Mechanism

The mechanism is one of the main identity pillars of the project.

### Active design truth

- the table has one shared central lift mechanism
- it raises a central body from inside the table
- that lift exposes two separate shelves
  - one facing the player
  - one facing the enemy
- those shelves are used to issue cards and tickets
- the gun rests on the mechanism while docked
- the docked gun must move with the mechanism
- the visible gears are part of the mechanism’s motion identity

### Intended motion language

- staged motion
- mechanical imperfection
- slight shake
- imperfect settling
- non-sterile movement

This is important for presentation because the mechanism is effectively the physical face of the game system.

---

## 11. Opponent Role

The opponent is more than an AI solver.

### Their current role

- opponent in a gas mask
- physically present across the table
- can attack, defend, shoot, and use tickets
- reacts through scriptable dialogue
- partially explains the world and the rules through conversation

### Dialogue direction

- dialogue should be diegetic or near-diegetic
- dialogue should appear near or around the enemy, not pasted to the center of the player camera
- the enemy should feel like someone who has survived this ritual before

---

## 12. Intro Narrative Draft

The current opening scenario draft is:

1. black screen
2. casino ambience in the background
3. a guard orders the protagonist not to open their eyes
4. the player is seated and restrained at the table
5. the player notices the opponent already strapped in, wearing a gas mask
6. loud unpleasant club music erupts from a speaker
7. the opponent reacts to the noise and the player is briefly disoriented
8. the music fades
9. the opponent begins to speak

### Current opening-monologue direction

The opponent’s early lines establish:

- this is not their first time
- the player is in a lethal game
- there is very little time
- rules will be explained during play
- the opponent speaks from age, experience, and exhaustion

This foundation is now represented in runtime-friendly form, even though the final script is not locked yet.

---

## 13. Post-Shot Dialogue Direction

The current draft also includes a post-shot reflection pool.

The themes are:

- age and survival experience
- prior victory and repeated survival
- the damaged state of the world after catastrophe
- failed conversation under pressure

These lines are intended to deepen the relationship between duel and narrative without pausing the core loop too heavily.

---

## 14. Technical Stack

- `Unity 6`
- `URP`
- `C#`
- `Input System`
- `TextMeshPro`

### Key runtime systems

- `GameManager`
- `BCCardManager`
- `TicketManager`
- `EnemyAI`
- `SceneBootstrap`
- `BCDealMechanism`
- `BCCameraController`
- `BCIntroSequenceController`
- `BCEnemyDialogueController`

---

## 15. What Already Works

### Core runtime

- 3-round structure
- round-based decks
- hidden enemy hand
- attack / defense / resolution flow
- `Threat`-gated shooting
- HP and O2 progression
- mini-round restart after chamber depletion

### Physical presentation

- mechanism-driven card issue
- ticket issue through the same physical delivery logic
- docked gun behavior
- camera effects for shot / damage / survival states

### Experience layer

- intro sequence foundation
- start menu
- settings panel
- language support surface
- world-space enemy dialogue foundation

---

## 16. Current Limitations

The prototype is playable, but not content-complete.

### Open areas

- the narrative script is still in draft
- the intro needs fuller direction and body animation
- the mechanism shelves still need final scene tuning
- some scene-linked motion depends on correct authored anchors
- the visual polish of UI and table motion is still evolving

This is appropriate for a vertical prototype: the loop exists, but content and final presentation are still expanding.

---

## 17. Key Strengths

- strong identity and atmosphere
- unusual combination of card tactics and first-person staging
- physical table mechanism as a standout gameplay object
- narrative embedded in gameplay instead of detached cutscene-only exposition
- scalable foundation for later content growth

---

## 18. Current Development Stage

**Current stage:** playable vertical prototype

That means:

- the core gameplay loop is already implemented
- the project already has a clear experiential identity
- the next priority is directed polish, stronger narrative timing, and scene reliability

---

## 19. Recommended Presentation Message

If a short presentation summary is needed, use:

> Breath Casino is a playable atmospheric first-person survival card duel prototype where tactical card combat, a physical central table mechanism, revolver-based risk, and diegetic narrative presentation are fused into one unified duel experience.

---

## 20. Suggested Slide Order

1. Project title
2. Concept and genre
3. Narrative premise
4. Core gameplay loop
5. Round structure
6. Card system
7. Chamber and revolver
8. Tickets
9. Central mechanism
10. Opponent and dialogue
11. Technical architecture
12. What already works
13. Current limitations
14. Next development stage
