---
name: phaser-battle-presentation
description: "Use this skill when developing or animating BattleScene presentation in DCacti. Covers composing BoardView, PlayerView, PetView, BossView, floating combat text, HUD, animation sequencing, and presentation phase management."
---

# skills/client/phaser-battle-presentation.md — Skill: DCacti Phaser Battle Presentation

**Version:** 1.0  
**Status:** Binding  
**Scope:** Orchestration of visual elements, animations, and combat feedback inside `BattleScene.ts`.

> Governs the composition and animation sequencing of DCacti's `BattleScene`, coordinating the board, player status, pet avatar, boss entity, and visual combat feedback from server battle events.

---

## Purpose

Define the visual layout and animation sequencing for the DCacti battle screen:
1. Compose distinct View components (`BoardView`, `PlayerView`, `PetView`, `BossView`, `CombatEffectsView`, `BattleHUDView`).
2. Sequence visual animations matching the server's authoritative resolution pipeline (`GAME_RULES.md` §17).
3. Display floating damage numbers, elemental status indicators, and health bar tweening.
4. Manage input locking during attack and cascade phases.

---

## When to Use

- Implementing or updating `BattleScene.ts` and its visual child views.
- Sequencing multi-phase battle animations (e.g. Gem Match → Power Charge → Pet Skill Cast → Boss Damage → Boss Attack).
- Displaying floating combat numbers (damage, heal, critical hits).
- Presenting boss turn intents, phase changes, and defeat/victory transitions.

---

## When Not to Use

- Writing raw match-3 grid coordinate math (use `client/phaser-match3`).
- Calculating combat damage numbers, defense mitigation, or turn counters (server Domain logic).
- Upstream tween or sprite syntax lookup (use `phaser/tweens`, `phaser/animations`, `phaser/sprites-and-images`).

---

## Authoritative Sources

- `docs/01-game-design/GAME_RULES.md` §17, §18 — Event resolution pipeline and server authority
- `docs/01-game-design/COMBAT_RULES.md` §1–§6 — Combat steps and damage presentation requirements
- `docs/01-game-design/BOSS_RULES.md` §1–§5 — Boss phases and attack intents
- `docs/02-technical/GAME_EVENTS.md` §1–§5 — Battle event definitions
- `docs/02-technical/ARCHITECTURE.md` §2.2 — Frontend presentation layering

---

## Battle Presentation Composition

```text
┌─────────────────────────────────────────────────────────────────┐
│                           BattleHUD                             │
│     Turn: 5 | Boss Intent: [Flamestrike] | Status: In Battle    │
├───────────────────┬─────────────────────────┬───────────────────┤
│    PlayerView     │        BoardView        │     BossView      │
│   - HP Bar        │   - 8x8 Match-3 Grid    │   - Boss Sprite   │
│   - Power Meter   │   - Gem Sprites         │   - HP / Shield   │
│   - Status Icons  │   - Selection / Drag    │   - Cast Indicator│
├───────────────────┴─────────────────────────┴───────────────────┤
│                     PetView & Effects Layer                     │
│   - Pet Sprite / Idle / Skill Animation                         │
│   - Floating Damage Numbers (DamageText)                        │
│   - Particle FX (Gem breaks, attack slashes)                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## Animation Sequencing Pipeline

Battle events delivered via `ReceiveEvents` must be animated in the deterministic sequence specified by `GAME_RULES.md` §17:

```text
1. Swap Animation
   └── Tween gem swap positions
2. Match & Cascade Phase
   ├── Flash / dissolve matched gems
   ├── Spawn special gems
   └── Animate gem drops and refill
3. Power & Passive Accumulation Phase
   ├── Tween player Power bar fill
   └── Pulse Pet avatar if passive threshold reached
4. Player Attack / Pet Skill Phase
   ├── Play Pet / Player attack animation (lunge / cast FX)
   ├── Boss flash white tint (`setTintFill(0xffffff)`)
   ├── Spawn floating damage text over Boss (e.g. "-120 Physical")
   └── Smoothly tween Boss HP bar fill downwards
5. Boss Action Phase (if Boss acted)
   ├── Play Boss attack animation and screen shake (`phaser/cameras`)
   ├── Spawn floating damage text over Player
   └── Tween Player HP bar fill downwards
6. Turn Wrap-up & Phase Check
   ├── Check victory / defeat conditions (trigger ResultScene if battle concluded)
   └── Unlock player input for next turn
```

---

## Combat Effects Guidelines

- **Damage Text:** Spawn bitmap text or Text object, tween upwards `y - 40`, fade `alpha: 0` over 600ms, destroy on complete.
- **Critical Hits:** Scale 1.5x with yellow/red tint and brief camera shake (`this.cameras.main.shake(100, 0.005)`).
- **Health Bars:** Smooth tween duration: 200–300ms using `Phaser.Math.Easing.Sine.Out`.
- **Boss Phase Transition:** Brief flash overlay, dramatic roar animation, update visual sprite frame.

---

## Do / Don't

| Do | Don't |
|---|---|
| Sequence animations in the exact chronological order of received server events. | Play all attack, match, and damage animations simultaneously in a jumbled mess. |
| Smoothly animate HP bar reductions with tweens. | Snap HP bars instantly without visual feedback. |
| Lock player input during the animation sequence. | Allow player swaps while attack sequences are resolving. |
| Read damage numbers and combat results directly from server event payloads. | Calculate damage formulas or critical hit chances in Phaser. |

---

## Stop Conditions

- Received event payload lacks necessary fields to animate the sequence (e.g. target ID, damage value, remaining HP).
- Animation sequencing contradicts `GAME_RULES.md` §17 event ordering.

---

## Traceability

- **Reads:** `docs/01-game-design/GAME_RULES.md`, `docs/01-game-design/COMBAT_RULES.md`, `docs/01-game-design/BOSS_RULES.md`, `docs/02-technical/GAME_EVENTS.md`
- **Used by:** `client` agent, `orchestrator` agent
- **Related Skills:** `client/phaser-match3`, `client/client-event-projection`, `phaser/tweens`, `phaser/animations`, `phaser/cameras`, `phaser/particles`
