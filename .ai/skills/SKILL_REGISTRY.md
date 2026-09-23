# .ai/skills/SKILL_REGISTRY.md — Skill Registry

**Version:** 1.0  
**Status:** Binding  
**Scope:** Master inventory and classification for all upstream and DCacti-custom AI skills.

> This registry classifies every skill available in the repository. Skills provide reusable capability procedures for agents during workflow execution. They reference authoritative documents in `docs/` and never override game rules, technical contracts, or ADRs.

---

# 1. Skill Classification Definitions

| Classification | Definition |
| --- | --- |
| **ACTIVE** | Frequently used during normal implementation tasks in DCacti. |
| **CONDITIONAL** | Invoked only when a specific feature or subsystem explicitly requires the capability. |
| **DORMANT** | Valid and supported skill, but not actively required by the current MVP feature baseline. |
| **NOT_REQUIRED** | Not applicable or out of scope for the current DCacti architecture / MVP scope. |

---

# 2. Master Skill Registry Table

| Skill Identifier | Layer / Source | Status | Auto-Route | Purpose / Capability |
|---|---|---|---|---|
| **Upstream Phaser Skills (`.ai/skills/phaser/`)** | | | | |
| `phaser/scenes` | upstream | **ACTIVE** | yes | Phaser 4 Scene lifecycle, scene management, transitions, boot |
| `phaser/sprites-and-images` | upstream | **ACTIVE** | yes | Creating/manipulating Sprite and Image game objects, textures, origins |
| `phaser/input-keyboard-mouse-touch` | upstream | **ACTIVE** | yes | Pointer interactions, hit areas, drag-and-drop, input events |
| `phaser/tweens` | upstream | **ACTIVE** | yes | Property tweening, easing curves, tween chains, visual motions |
| `phaser/animations` | upstream | **ACTIVE** | conditional | Sprite sheet and texture atlas frame animations |
| `phaser/loading-assets` | upstream | **ACTIVE** | yes | Asset loading pipeline, asset cache, preload lifecycle |
| `phaser/events-system` | upstream | **ACTIVE** | yes | Phaser EventEmitter pattern, scene/game events, custom dispatch |
| `phaser/game-setup-and-config` | upstream | **ACTIVE** | yes | Phaser Game configuration, scale manager, canvas setup |
| `phaser/cameras` | upstream | **CONDITIONAL** | conditional | Camera effects (shake, flash, zoom), viewports, screen transitions |
| `phaser/particles` | upstream | **CONDITIONAL** | conditional | ParticleEmitter VFX (gem breaks, attack impacts, spell sparkles) |
| `phaser/graphics-and-shapes` | upstream | **CONDITIONAL** | conditional | Primitive shapes, debug overlays, HP bar fills, board boundary lines |
| `phaser/time-and-timers` | upstream | **CONDITIONAL** | conditional | Clock plugin, TimerEvent, delayed callbacks, visual sequence pacing |
| `phaser/actions-and-utilities` | upstream | **CONDITIONAL** | conditional | Batch operations, grid alignment helpers, array utilities on sprites |
| `phaser/groups-and-containers` | upstream | **DORMANT** | no | Container hierarchies and object pools (DCacti uses custom View classes) |
| `phaser/game-object-components` | upstream | **DORMANT** | no | Game object mixin internals and low-level component references |
| `phaser/render-textures` | upstream | **DORMANT** | no | Dynamic / offscreen render textures and snapshots (not needed in MVP) |
| `phaser/geometry-and-math` | upstream | **DORMANT** | no | Geom shapes and math utilities (standard JS Math / vector helpers used) |
| **DCacti Custom Client Skills (`.ai/skills/client/`)** | | | | |
| `client/react-phaser-boundary` | DCacti | **ACTIVE** | yes | React UI Shell ↔ Phaser Game Runtime boundary & coordination |
| `client/phaser-architecture` | DCacti | **ACTIVE** | yes | Phaser 4 scene patterns, runtime port, 1280x720 scaling & lifecycle |
| `client/phaser-match3` | DCacti | **ACTIVE** | yes | 8x8 Match-3 grid presentation, cell coordinates, gem visual lifecycle |
| `client/phaser-battle-presentation` | DCacti | **ACTIVE** | yes | BattleScene visual composition, view hierarchies, animation sequencing |
| `client/client-event-projection` | DCacti | **ACTIVE** | yes | Server event stream → client state projection → Phaser presentation |
| `client/client-state-authority` | DCacti | **ACTIVE** | yes | Server-authoritative enforcement; strictly prohibits client game logic |
| **Repository Core & Domain Skills (`.ai/skills/`)** | | | | |
| `discovery/documentation-discovery` | DCacti | **ACTIVE** | yes | Identify, read, and bound authoritative docs for a task |
| `discovery/impact-analysis` | DCacti | **ACTIVE** | yes | Trace code and documentation dependency chains |
| `gameplay/gameplay-behavior-derivation` | DCacti | **ACTIVE** | yes | Derive expected gameplay behavior from domain rules |
| `gameplay/authority-determinism-audit` | DCacti | **ACTIVE** | yes | Audit state ownership, server authority, RNG, ordering |
| `backend/api-contract-validation` | DCacti | **ACTIVE** | conditional | Validate REST behavior against `API_CONTRACTS.md` |
| `backend/persistence-analysis` | DCacti | **ACTIVE** | conditional | Analyze Redis/PostgreSQL against storage docs |
| `realtime/realtime-protocol-validation` | DCacti | **ACTIVE** | conditional | Validate SignalR/event delivery, ordering, resync |
| `testing/test-scenario-generation` | DCacti | **ACTIVE** | yes | Derive doc-sourced test scenarios (Given/When/Then) |
| `quality/scope-validation` | DCacti | **ACTIVE** | yes | Validate task scope against `MVP_SCOPE.md` |
| `quality/documentation-consistency` | DCacti | **ACTIVE** | yes | Detect and classify documentation discrepancies |
| `quality/architecture-conformance` | DCacti | **ACTIVE** | yes | Check layering, module ownership, ADR compliance |
| `quality/implementation-review` | DCacti | **ACTIVE** | yes | Structured review against quality checklist |

---

# 3. Upstream Phaser Skills Provenance & Metadata

Upstream Phaser skills are maintained in `.ai/skills/phaser/` as the project's baseline Phaser knowledge layer. They provide framework-level API mechanics and must not be edited with DCacti-specific business logic.

- **Source Type:** Downloaded upstream Phaser 4 skill package
- **Installation Path:** `.ai/skills/phaser/`
- **Import Date:** 2026-09 (Repository baseline)
- **Upstream Scope:** Phaser 4 Core Systems (Scenes, Input, Tweens, Sprites, Animations, Cameras, Events, Loader, Particles, RenderTextures, Time, Actions, Math, Components)
- **Policy:** Read-only reference baseline. Do not download additional skills from SkillsMP, do not modify upstream files, and do not duplicate game rules inside them.

---

# 4. DCacti Custom Skills Architecture

Custom skills in `.ai/skills/client/` encapsulate DCacti-specific implementation knowledge and conventions. They bridge Phaser 4 generic capabilities with DCacti's technical design (`docs/02-technical/`):

```text
               docs/ (Authoritative Source of Truth)
                                │
       ┌────────────────────────┴────────────────────────┐
       ▼                                                 ▼
.ai/skills/phaser/                               .ai/skills/client/
(Generic Phaser 4 APIs)                         (DCacti Frontend Architecture)
- scenes, tweens, input                          - react-phaser-boundary
- sprites, animations                            - phaser-architecture
- loading, events                                - phaser-match3
                                                 - phaser-battle-presentation
                                                 - client-event-projection
                                                 - client-state-authority
```
