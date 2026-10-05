# Core Game Rules

**Version:** 3.4 (§11 and §17 — the Card-cast restriction approved by `TASK-191`
Q-4 (OPTION B) is recorded: §11 gains item 4 stating that a Card cast is an
**auxiliary action within the current Turn** — it does not increment `Turn`, does
not resolve the board, does not independently trigger the Boss response, and is
limited to one successful cast per committed Turn, with the precise constraint
owned by `CARD_RULES.md` §3 item 6; §17 gains a paragraph fixing where a Card cast
sits relative to the swap resolution order — a cast is **not** a step of that
order, does not advance it, and does not reach step 18, so the Boss response is
reached only through a committed Swap. **The order itself is unchanged**: no step
is added, removed, or reordered, and the Match-3 Turn remains the authoritative
unit of combat progression. Rationale: ADR-021. No gameplay value is changed.
Prior 3.3: (§17 gained one clarifying paragraph — step 11's evaluation
point: "Trigger Relics" is evaluated, and its effects applied, after step 10
("Charge Passive") and **before** step 12 ("Generate Resources"), step 13
("Update Power"), and step 14 ("Resolve Player Effects"), so a Relic's Trigger
and Condition read the resolution state before this Swap's own steps 12–14
(TASK-142). **The order itself is unchanged**: no step is added, removed, or
reordered; no Trigger, Condition form, effect, magnitude, threshold, lifetime,
or gameplay value changes; `RELIC_RULES.md` §8 keeps ownership of the
Condition forms and effect lifetimes. Prior 3.2: (§12 — one paragraph added:
Power remains an integer-valued
resource and a Card's cost is the integer `EffectiveCardCost` composed by
`CARD_RULES.md` §3.6, including any Relic-applied reduction (TASK-134 D4/D5 plus
the Product Owner `EffectiveCardCost` truncation decision). This is a
cross-reference establishing that the new cost composition produces no
fractional Power; §12's 0–100 range, generation rules, and the §17 resolution
order (including step 11 "Trigger Relics" before step 14 "Resolve Player
Effects") are unchanged, and no gameplay value is added. Prior 3.1: §9.3 Pet XP reference finalized per TASK-062 — the Pet XP
balance/reward decisions are now decided, not open; `PET_RULES.md` §5.1–§5.5
owns them. Prior 3.0: §9.3 rewritten — Pet Level is no longer derived from
Player Level; the `Player.Level × Pet Level Multiplier` formula is
**RETIRED** and replaced by the independent two-track model: Player XP /
Level owned by `COMBAT_RULES.md` §7, Pet XP / Level owned by
`PET_RULES.md` §5; ADR-016. Prior 2.4: §9.3 Pet Level formula synchronized with the completed
derivation contract — floor before clamp — PET_RULES.md §5; prior 2.3:
§9.3 Pet Level formula resolved with clamp per
ADR-012 / PET_RULES.md §5.1; prior 2.2: Player = account/owner,
Pet = combat character — battle-end condition, combat stats, Power,
Relic loadout, and Boss triggers now reference the active Pet rather
than a separate Player combat identity)
**Status:** MVP Source of Truth

> This document answers: **"What are the fundamental rules of the game?"**
> It defines core invariants, cross-system definitions, and resolution
> order. It intentionally does NOT duplicate the exact mechanics owned by
> domain rule documents (`MATCH3_RULES.md`, `ELEMENT_RULES.md`,
> `COMBAT_RULES.md`, `PASSIVE_RULES.md`, `PET_RULES.md`, `CARD_RULES.md`,
> `RELIC_RULES.md`, `BOSS_RULES.md`). Where this document references a
> domain document, that domain document owns the exact values/behavior.

If a task, AI suggestion, or implementation conflicts with this document,
the conflict must be identified and reported before implementation
(see §20).

---

# 1. Core Battle Rules

1. A battle has one Player (the account/owner), one selected Pet (the
   combat character), and one Boss.
2. A player may own multiple Pets; only one Pet is active in a battle.
   The Player owns the collection and issues Swap/Card/Skill requests;
   the active Pet is the combat character that fights the Boss.
3. The player uses a Match-3 board to generate combat resources for the
   active Pet.
4. A battle ends when either the Boss or the active Pet reaches 0 HP.
5. The server is authoritative for battle state and results (§18).

---

# 2. Turn Rules

1. A Turn represents one player Swap/Action.
2. Turn ≠ Match ≠ Cascade ≠ Combo. One Turn may produce multiple Matches.
3. Boss mechanics may use Turn-based triggers; Passive systems should
   primarily use Match-based triggers (see `PASSIVE_RULES.md`).

```text
One Swap → Match 1 → Cascade → Match 2 → Cascade → Match 3
Result: 1 Turn, 3 Matches, Combo 3
```

---

# 3. Match Rules

1. A Match consists of 3 or more compatible Gems in a valid horizontal or
   vertical line.
2. Each individual Match is counted, including Matches created by Cascades.
3. Match count is independent from Turn count and can charge Passives and
   Relics.
4. Match 4 / Match 5 / L-T patterns have enhanced effects and may generate
   Special Gems.

Exact board size, detection algorithm, and Special Gem behavior: see
`MATCH3_RULES.md`.

---

# 4. Cascade Rules

1. Matched Gems are removed, remaining Gems fall (gravity), new Gems spawn,
   and the board is re-checked for new Matches.
2. Cascades continue until no new Match exists.
3. Every Cascade Match counts toward total Match count and contributes to
   Combo for the Swap that started it.

Exact cascade loop and edge cases: see `MATCH3_RULES.md` §4.

---

# 5. Combo Rules

1. Combo counts consecutive Matches caused by one Swap.
2. Combo starts at 1 on the first Match and increases with each subsequent
   Cascade Match.
3. Combo resets when a new Swap begins; it does not persist between
   separate Swaps unless a future mechanic explicitly changes this. A Swap
   "begins" when it is committed — a rejected Swap never begins and therefore
   never resets Combo (`MATCH3_RULES.md` §6.1, §2.1.5).
4. Combo may modify damage and can activate Combo-threshold Relics.

Default damage multipliers (**configurable**):

```text
Combo 1   = 1.00×
Combo 2   = 1.10×
Combo 3   = 1.20×
Combo 4   = 1.35×
Combo 5+  = 1.50×
```

This table is canonical here. Other documents must reference it, not copy it.

---

# 6. Gem Rules

MVP has four functional Gem types: `ATK`, `DEF`, `HP`, `POWER`. Gems are not
elemental — Element belongs to Pets, Bosses, Skills and Effects (§8).

Exact per-Gem resource formulas and match-tier multipliers: see
`COMBAT_RULES.md` and `MATCH3_RULES.md`.

---

# 7. Special Gem Rules

Match 4, Match 5, and L/T patterns each produce a distinct Special Gem with
an area-clear effect. Exact Special Gem types and behavior are defined in
`MATCH3_RULES.md` (this document intentionally defers full definition
there).

---

# 8. Element Rules

MVP uses five elements: `Mộc, Hỏa, Thổ, Kim, Thủy`, with only the Tương Khắc
(overcoming) relationship — Tương Sinh is explicitly out of MVP scope.

```text
Mộc → Thổ → Thủy → Hỏa → Kim → Mộc
```

Rules:

1. Element advantage/disadvantage affects damage.
2. Element does not determine Pet Passive, Card compatibility, or Relic
   compatibility — any Pet may use any compatible Card or Relic.

Exact damage modifier values and matchup resolution: see `ELEMENT_RULES.md`.

---

# 9. Pet Rules

1. Players can collect multiple Pets; only one is active per battle.
   The Player is the owner; the active Pet is the combat character whose
   HP and battle stats determine the battle outcome (§1.4, §14).
2. Every Pet has exactly one Element, one Passive, and one Signature Skill.
3. Pets have Level, Star, and Tier progression. Pet Level is the Pet's
   **own** progression value, derived from that Pet instance's own
   accumulating XP — **not** from Player Level. The Pet XP contract
   (initial values, rewards, formula, and cap) is owned by
   `PET_RULES.md` §5.1–§5.5. Player Level is a separate,
   independent account attribute (range 1–50, no combat stats) whose
   XP → Level contract is owned by `COMBAT_RULES.md` §7; the two tracks
   are independent (`PET_RULES.md` §5.1, ADR-016). There is no Evolution
   system.
4. Higher Tier must not simply be a raw stat multiplier of lower Tier — Pet
   identity comes primarily from Passive + Signature Skill + statistics
   together.

MVP Pets:

```text
Thanh Xà  → Mộc
Xích Lang → Hỏa
Sơn Hùng  → Thổ
Bạch Hổ   → Kim
Huyền Quy → Thủy
```

Exact Tier/Star/Level rules: see `PET_RULES.md`.

---

# 10. Passive Rules

1. Passive progression is primarily Match-based; every Match (including
   Cascade matches) may contribute to progress.
2. A Passive has a threshold; reaching it makes it Ready and triggers it.
3. Progress resets after triggering unless the Passive explicitly specifies
   otherwise.
4. Passive progress must be visible to the player where practical.
5. Alternate supported triggers exist (Combo, HP threshold, Damage dealt,
   Battle start, Card cast) but Match-based is the default.

Exact per-Pet thresholds/effects: see `PASSIVE_RULES.md`.

---

# 11. Card Rules

MVP has two Card categories: **Basic Card** (shared) and **Pet Skill Card**
(tied to the active Pet). MVP Basic Cards: `Heal`, `Shield`, `Power Charge`.

1. Cards are active player actions, distinct from a Swap.
2. Cards consume or generate Power according to their definition and cannot
   bypass server validation.
3. A Card cast is an explicit game event and may trigger Relics.
4. A Card cast is an **auxiliary action within the current Turn**, not a Turn
   of its own: it does not increment `Turn`, does not resolve the board, and
   does not independently trigger the Boss response (§17 step 18). Exactly one
   successful Card cast is allowed per committed Turn; after it, further casts
   are rejected until the next committed Turn. The Match-3 Swap remains the
   authoritative unit of combat progression, and it is the Swap's resolution
   that advances `Turn` and resolves the Boss response. The precise constraint
   is owned by `CARD_RULES.md` §3 item 6 and is not restated here.

Exact Power costs and effects: see `CARD_RULES.md`.

---

# 12. Power Rules

```text
Range: 0–100
```

Power is the active Pet's battle resource (range 0–100). It is generated
by POWER Gem matches, Special Matches, Relics, and other defined effects,
and is spent to cast Cards/Pet Skills. Power must never exceed the
configured maximum unless an explicit future mechanic allows it.

Power remains an integer-valued resource: a Card's cost is the integer
`EffectiveCardCost` its owning rule composes, so spending Power never
produces a fractional pool. The amount a given Card costs at cast time —
including any reduction a Relic applies — is owned by `CARD_RULES.md` §3.6
and is not restated here.

Exact generation rates: see `COMBAT_RULES.md` §2.

---

# 13. Relic Rules

1. Relics are passive build modifiers, not direct player actions.
2. The active Pet's battle loadout holds 3–5 Relics in MVP, selected by
   the Player before battle start; there is no global Player relic
   loadout. Relics are not restricted by Element.
3. Multiple Relics may trigger from the same event; trigger order must be
   deterministic.
4. Relic effects must not create uncontrolled infinite trigger chains.

Full supported trigger list, ordering, and MVP Relic list: see
`RELIC_RULES.md`.

---

# 14. Combat Rules

Combat stats for the active Pet: `HP, Max HP, ATK, DEF, Power, Crit,
Status Effects`. These belong to the combat character (the active Pet);
the Player (account) carries no separate combat stats during battle.

Damage conceptually flows in this fixed order:

```text
Base Damage → Combo Modifier → Element Modifier → Other Modifiers
  → Defense → Final Damage
```

No client-provided damage value is authoritative. Exact mathematical
formulas: see `COMBAT_RULES.md`.

---

# 15. Boss Rules

Every Boss has: `Element, HP, Max HP, ATK, DEF, Passive, Skill`. Bosses may
react to Turn, Match Count, Combo, active Pet Power, active Pet HP, Boss
HP, or Status.

1. Bosses should have unique mechanics; not all Bosses should use the same
   trigger pattern.
2. Boss difficulty should not depend exclusively on HP/ATK inflation.
3. Boss mechanics must be deterministic where possible; Boss state is
   server authoritative.

Exact MVP Boss list and mechanics: see `BOSS_RULES.md`.

---

# 16. Event Rules

Core events (canonical list — do not duplicate elsewhere; if a
`GAME_EVENTS.md` is created later, it becomes the owner of ordering/payload
detail and this list moves there):

```text
BattleStarted, TurnStarted, TurnEnded
SwapStarted, SwapResolved
MatchCreated, MatchResolved, CascadeCreated, ComboChanged, GemMatched
PowerChanged
PassiveCharged, PassiveTriggered
RelicTriggered
CardCast, PetSkillCast
DamageCalculated, DamageDealt, DamageTaken
BossSkillCast
BattleWon, BattleLost
```

Events exist to decouple gameplay systems from each other.

---

# 17. Event Resolution Rules

A Swap resolves in this fixed logical order (canonical — domain documents
may expand individual steps but must not reorder them):

```text
1. Validate Swap
2. Resolve Board
3. Detect Match
4. Remove Matched Gems
5. Apply Gravity
6. Spawn Gems
7. Detect Cascade
8. Update Combo
9. Count Matches
10. Charge Passive
11. Trigger Relics
12. Generate Resources
13. Update Power
14. Resolve Player Effects
15. Calculate Damage
16. Apply Element Modifier
17. Apply Final Damage
18. Resolve Boss Response
19. End Turn
```

**Step 11's evaluation point, stated exactly.** Step 11 ("Trigger Relics") is
evaluated, and its effects are applied, at the position the list above fixes:
after step 10 ("Charge Passive") and **before** step 12 ("Generate Resources"),
step 13 ("Update Power"), and step 14 ("Resolve Player Effects"). A Relic's
Trigger and Condition therefore read the resolution state as it stands at that
point — before this Swap's own steps 12–14 have been applied. Which value each
Condition form reads is owned by `RELIC_RULES.md` §8.1 and is not restated here.

```text
step 10  Charge Passive
   ↓
step 11  Trigger Relics       ← condition evaluated here, effect applied here
   ↓
step 12  Generate Resources    \
step 13  Update Power           >  not yet applied when step 11 evaluates
step 14  Resolve Player Effects /
   ↓
step 15  Calculate Damage
```

1. **This constrains evaluation, not only reporting.** "The implementation may
   split these into multiple internal steps" permits a step to be divided into
   several internal operations (e.g. per-pass resource accounting); it never
   permits executing step 11 after steps 12–14. No Relic is ever evaluated
   against a state that already includes this Swap's steps 12–14.
2. **The single write-back does not reorder the steps.** `GAME_STATE.md` §5.1
   writes the whole resolved `BattleState` once, after the resolution, and
   writes nothing mid-resolution. That defers when the resolution's combined
   result becomes *visible*; it moves no evaluation point, and it does not
   authorise evaluating step 11 against the post-write-back state.
3. **Effect application happens at that same point.** A Relic's effect is
   applied at step 11 — before this Swap's resource generation, Power update,
   player-effect resolution, and damage. How long the applied effect then
   persists is its declared `lifetime` (`RELIC_RULES.md` §8.3), which is that
   document's and is unchanged.
4. **Nothing else changes.** This paragraph states the observation point the
   order above already fixes. It adds, removes, and reorders no step, and
   authors no Trigger, Condition form, effect, magnitude, threshold, or
   lifetime (`RELIC_RULES.md` §3, §8).

Step 18 ("Resolve Boss Response") expands to:

```text
18a. Boss Passive — evaluate the Boss's Passive trigger condition against
     the post-damage battle state. If the trigger is met, apply the
     Passive effect and emit PassiveCharged/PassiveTriggered
     (BOSS_RULES.md §3, GAME_EVENTS.md §2).
18b. Boss Skill — evaluate Skill eligibility: SkillCharge ≥ Charge
     Requirement AND SkillCooldown = 0 (BOSS_RULES.md §4,
     GAME_STATE.md §2.4.3). If eligible, execute the Skill (damage
     through Damage Pipeline, apply non-damage effects) and emit
     BossSkillCast. Reset SkillCharge to 0, set SkillCooldown to the
     Boss's cooldown value. If not eligible, skip to 18c.
18c. Boss Attack — if the Boss Skill did not fire (step 18b skipped),
     the Boss performs a basic attack: Base Damage = Boss.ATK, Element
     = Boss.Element, through Damage Pipeline (COMBAT_RULES.md §3) to
     the active Pet. Emit DamageCalculated, DamageDealt (source=boss,
     target=player), DamageTaken (source=boss, target=player). The
     `target=player` wire label denotes the player's side (the active
     Pet) — it is a fixed protocol label, not a separate Player HP pool
     (SIGNALR_PROTOCOL.md §3.2).
```

Step 19 ("End Turn") expands to:

```text
19a. Tick Status Effects — tick each active damage-over-time Status Effect
     (e.g. Burn) exactly once, through the Damage Pipeline
     (COMBAT_RULES.md §3, §5.2 item 3). This is the last combat effect of
     the Turn — it runs after the Boss Response (step 18) — and it fires
     once per resolved Turn (COMBAT_RULES.md §5.1).

     This step is also the single duration-consumption point for Turn-based
     Buff/Debuff Status Effects: exactly one Turn of duration is consumed
     here for each active instance, regardless of how many apply/refresh
     operations occurred earlier in the same Turn (COMBAT_RULES.md §5.3).
     That rule is owned by COMBAT_RULES.md §5.3 and is not restated here.

     Step 19a remains one step in one fixed position. This does not add a
     resolution step, does not change its ordering, and does not change the
     damage-over-time tick itself.
```

The implementation may split these into multiple internal steps, but
observable game behavior must preserve this logical ordering.

**Where a Card cast sits relative to this order.** The list above is the
resolution order of a **committed Swap**, and it is unchanged. A Card cast is
not one of its steps and is not inserted into it: a cast is an auxiliary action
resolved between Swaps, on the player's request, against the current committed
state. It therefore does not advance this sequence, does not increment `Turn`,
does not resolve the board, and does not itself reach step 18 — the Boss
response is reached only through a committed Swap's resolution. What bounds the
cast is a cast-count rule, not a position in this order: at most one successful
Card cast per committed Turn (`CARD_RULES.md` §3 item 6, §11 item 4). Once that
one cast has resolved, further casts are rejected until the next committed Turn,
so a player cannot chain casts in place of resolving the Swap above.

---

# 18. Server Authority Rules

Client may send: `Swap, Card Cast, Pet Skill Cast`.

Client must NOT send authoritative: `Damage, HP, Boss HP, Power, Match
Result, Combo, Passive Progress, Reward`.

Server determines all authoritative state. The client renders the resulting
events.

---

# 19. MVP Scope Rules

The canonical IN / OUT / FUTURE classification lives in `MVP_SCOPE.md`
(`docs/00-overview/MVP_SCOPE.md`). This document does not repeat it — every
other document, including this one, must reference `MVP_SCOPE.md` rather
than list scope independently.

---

# 20. Rule Change Policy

Game rules must not be silently changed by AI Agents.

```text
Detect Conflict → Report Conflict → Propose Change → Human Approval
  → Update Rules → Update GDD/Domain Rules if necessary → Implement
```

AI must not resolve a design conflict by silently choosing a new mechanic.

---

# 21. Source of Truth Hierarchy

Document layering (top = most general, bottom = most concrete):

```text
GDD.md
    ↓
GAME_RULES.md
    ↓
Specific Domain Rules (MATCH3, ELEMENT, COMBAT, PASSIVE, PET, CARD, RELIC, BOSS)
    ↓
TDD.md
    ↓
ARCHITECTURE.md
    ↓
ADR
    ↓
Task
    ↓
Code
```

When resolving a **conflict** between documents, precedence is the reverse
of specificity (most specific wins):

```text
Specific Domain Rule > GAME_RULES.md > GDD.md > TDD/Architecture > ADR > Task > Code
```

If a design change is discovered during implementation:

1. Do not silently modify a lower-level document to match code.
2. Identify and report the conflict.
3. Propose the smallest documentation change required.
4. Apply the change only after explicit authorization.
5. If approved, update the owning document first (or together with the
   implementation) — never leave documentation and code contradicting each
   other.

---

# 22. Design Principles

The game should prioritize: clear player feedback, deterministic gameplay,
meaningful Match-3 decisions, understandable resource management, distinct
Pet identities, strategic Element matchups, build diversity through Relics,
Boss mechanics over stat inflation, server-authoritative gameplay, and
expandability without unnecessary MVP complexity.
