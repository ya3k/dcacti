# Core Game Rules

**Version:** 3.11 (§11, §12, §15, §16 and §17 step 18 updated per **ADR-029 / TASK-303**, applying the explicit Product Owner decision that the Boss behaves as a genuine opponent using the same resource and cast principles as the Player: **§12's Power is actor-relative** (the active Pet and the Boss each hold their own integer 0–100 Power), **§15** routes Boss POWER-Gem matches into `BossState.Power` instead of `SkillCharge` and fixes the one-action-per-response-phase policy, **§16** gains the dedicated `BossCardCast` Boss action event (the Player `CardCast` contract is unchanged), and **§17 step 18** becomes action selection plus an either/or board action or Boss Card cast. This explicitly reverses the TASK-258 §2 **D2** rule that forbade `BossState.Power`; `ADR-026`'s POWER bullet is reconciled by `ADR-029`. **No step is added, removed, or reordered; no damage, healing, combo, Element, Relic or Passive value changes; no numeric balance value is authored by this revision** — every balance value the migration depends on remains UNRESOLVED and recorded in `ADR-029`. Runtime implementation is staged behind `ADR-029`'s Slices 1–4; the transitional `SkillCharge` path remains in force until the migration's parity gate is met. Prior 3.10: (§16's canonical event list gains `MatchedGemSummary` — one
additive, presentation-only Battle Event that reports, once per committed board
action, how many Gems of each Gem type that action's board cycle matched. It is
emitted after the cycle's whole cascade resolution and adds no step, no ordering
change, and no gameplay value; its meaning and payload are owned by
`GAME_EVENTS.md` §2 and its wire schema by `SIGNALR_PROTOCOL.md` §3.2.29. It is
reporting of the matched-Gem accounting `COMBAT_RULES.md` §2 item 6 already
defines, and it feeds no pool, no damage, no heal, no DEF buff, no Power gain, and
no Boss SkillCharge. Prior 3.9: (§17 step 18a's actor-relative resource list reconciled with **TASK-299**: a DEF-Gem match generates a temporary **DEF stat buff** for the acting actor (`COMBAT_RULES.md` §2, §5.4.6, §5.5.6), not a Shield; the order of §17 is unchanged — no step is added, removed, or reordered, and HP/POWER/ATK behavior is untouched. Prior 3.8: §3 item 4, §6, §7, §12, §14, §17 step 15, step 18d reconciled per TASK-287 under operator decisions OD-1–OD-7: Special Gems are removed entirely from gameplay — Match-4, Match-5, and L/T patterns are valid matches that create no board objects and gain no special resource multipliers; §7 Special Gem Rules is retired; match-tier multipliers are retired with resource formulas governed by `COMBAT_RULES.md` §2 and `MATCH3_RULES.md` §5.7; attack eligibility across Player and Boss is gated strictly on a confirmed valid ATK-gem match on the acting entity's own resolution, retiring `ClearedGemCounts.Atk` as an eligibility predicate. Prior 3.7: (§16 gains `CascadeStepResolved` per the Operator Decision approving TASK-278 A-1(i) and A-4: the detection pass's gravity+spawn transition is reported by one additive Battle Event inside the round's existing single `ReceiveEvents` batch, emitted once per recorded pass after that pass's Gravity and Spawn have completed, with `passDepth`, `dropMoves` and `spawnedGems` and no board; the canonical event list gains one name and no other list entry, step, or gameplay value moves. Prior 3.6 (§17 gains the playable-board invariant statement per ADR-026 / TASK-267: the committed board of a non-terminal Round must contain at least one legal match-producing adjacent swap, evaluated on the finished board after step 18 and step 19a and before the `GAME_STATE.md` §5.1 write-back, with terminal Rounds exempt; the paragraph adds and reorders no step. Prior 3.5 (§1.3, §2, §14, §15, §17 updated per ADR-026 / TASK-259: Player and Boss share the one 8×8 Match-3 board; both sides act by making valid swaps; cell type determines effect; acting side determines ownership and target; attack damage is strictly gated on confirmed ATK-gem matches (D1); default fallback basic attacks are retired (step 18c); Boss skills deal zero direct damage (D3); Boss SkillCharge charges exclusively from Boss POWER matches (D2); Turn advances once per committed atomic round (D4). Prior 3.4: §11 and §17 — the Card-cast restriction approved by `TASK-191`
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
3. Player and Boss share one Match-3 board. Both sides act by making
   valid swaps on that board. The type of cells matched determines the effect;
   the acting side determines ownership and target. Neither side performs a
   default fixed-damage attack independently of the board (ADR-026).
4. A battle ends when either the Boss or the active Pet reaches 0 HP.
5. The server is authoritative for battle state and results (§18).

---

# 2. Turn Rules

1. A Turn represents one atomic combat Round consisting of a Player action phase
   and the Boss response phase (ADR-026, D4). Turn advances by exactly 1 upon the
   commit of the atomic round.
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
4. Match 4, Match 5, and L/T patterns are valid matches, detected and counted as
   single Matches carrying no additional board object and no special resource
   multiplier.

Exact board size, detection algorithm, and match-resolution semantics: see
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

Exact per-Gem resource formulas: see `COMBAT_RULES.md` and `MATCH3_RULES.md`.

---

# 7. Special Gem Rules (Retired)

Special Gems do not exist in gameplay (OD-1, OD-2). Match 4, Match 5, and L/T
patterns are valid matches that clear only their own matched cells and create no
board objects, area-clear effects, or detonations. See `MATCH3_RULES.md` §5 for
historical retirement details.

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

MVP has three Card categories: **Basic Card** and **Pet Skill Card** (the
Player's two categories, tied to the active Pet where applicable) and **Boss
Card** (the Boss's own Card category). MVP Basic Cards: `Heal`, `Shield`, `Power
Charge`. Card categories, the shared cast contract, and each category's exact
semantics are owned by `CARD_RULES.md` §1 and §3 and are not restated here.

1. Cards are active actions, distinct from a Swap.
2. Cards consume or generate Power according to their definition and cannot
   bypass server validation.
3. A Card cast is an explicit game event and may trigger Relics. This is a
   **Player-cast** consequence only: Relics are Player-only and never trigger on
   a Boss Card cast (`ADR-029`).
4. A Card cast is an **auxiliary action within the current Turn**, not a Turn
   of its own: it does not increment `Turn`, does not resolve the board, and
   does not independently trigger the Boss response (§17 step 18). Exactly one
   successful Card cast is allowed per committed Turn; after it, further casts
   are rejected until the next committed Turn. The Match-3 Swap remains the
   authoritative unit of combat progression, and it is the Swap's resolution
   that advances `Turn` and resolves the Boss response. The precise constraint
   is owned by `CARD_RULES.md` §3 item 6 and is not restated here.
5. **Item 4's allowance is the Player's, and is Player-only.** A Boss Card cast
   is not an auxiliary action: it is one of the two actions the Boss may select
   inside step 18 (§17), it is bounded by the Boss's own Power and cooldown and
   by its action policy, and it neither reads nor spends the Player's
   one-per-Turn allowance (`CARD_RULES.md` §3 item 9, `BOSS_RULES.md` §4.3,
   `ADR-029`).

Exact Power costs and effects: see `CARD_RULES.md`.

---

# 12. Power Rules

```text
Range: 0–100 (integer, per actor)
```

Power is a **per-actor battle resource**. The active Pet holds `PetState.Power`
and the Boss holds `BossState.Power`; each actor has exactly one Power value,
each is an integer in the range 0–100, and neither is derived from, transferred
to, or shared with the other (`GAME_STATE.md` §2.3, §2.4; `ADR-029`).

1. Power is generated by POWER Gem matches, Relics, and other defined effects,
   attributed to the actor whose own board action produced the match
   (`COMBAT_RULES.md` §2). Player POWER matches never generate Boss Power.
2. Power is spent only by the actor that holds it: the Player's Power is spent
   on that Player's Cards and Pet Skills, and the Boss's Power is spent on its
   own Boss Cards.
3. Power must never exceed the configured maximum unless an explicit future
   mechanic allows it. The 0–100 bound is applied at the single point the
   persistent value is written.
4. **Relics are Player-only.** No Relic generates, modifies, or reads
   `BossState.Power` (`ADR-029`).

Power remains an integer-valued resource: a Card's cost is the integer
`EffectiveCardCost` its owning rule composes, so spending Power never
produces a fractional pool. The amount a given Card costs at cast time —
including any reduction a Relic applies — is owned by `CARD_RULES.md` §3.6
and is not restated here.

Exact generation rates: see `COMBAT_RULES.md` §2. The Boss's own Power rules
(its acquisition, its spend and its transitional relationship to the legacy
`SkillCharge`) are owned by `BOSS_RULES.md` §4.1.

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

Damage conceptually flows in this fixed order, gated strictly on a confirmed
ATK match (ADR-026, D1):

```text
Base Damage → Combo Modifier → Element Modifier → Other Modifiers
  → Defense → Final Damage
```

Damage may be calculated and applied if and only if the acting side's own
resolution contains at least one confirmed valid ATK-gem match (OD-6, ADR-026 D1).
Cleared-gem counts are reporting-only and do not serve as an eligibility predicate.
Only after confirming an ATK match may the combat formula use the acting side's
ATK stat. If no valid ATK-gem match is confirmed, no attack occurs, the Damage
Pipeline does not execute, zero attack damage is dealt, and applicable attack
modifiers (e.g. NextAttack modifiers) are preserved unconsumed. No client-provided
damage value is authoritative. Exact mathematical formulas: see `COMBAT_RULES.md`.

---

# 15. Boss Rules

Every Boss has: `Element, HP, Max HP, ATK, DEF, Power, Passive, Skill, State`.
Bosses may react to Turn, Match Count, Combo, active Pet Power, active Pet HP,
Boss HP, or Status.

1. Bosses share the Match-3 board and act by executing valid swaps on that board (ADR-026).
2. Boss skills modify the board, resources, or combat modifiers/multipliers, and
   deal zero direct damage by default (ADR-026, D3).
3. Bosses generate **Power** exclusively from POWER Gem matches produced by the
   Boss's own board match action, by the same generation rule and on the same
   integer 0–100 scale as the active Pet; Player POWER matches never generate
   Boss Power. The Boss spends that Power on its own Boss Cards. This **reverses
   TASK-258 §2 D2** (which had ruled out `BossState.Power`) and is recorded by
   `ADR-029`; the exact rules are owned by `BOSS_RULES.md` §4.1 and the
   generation rate by `COMBAT_RULES.md` §2.
4. **The Boss takes exactly one action per response phase** — either a board
   match action or a Boss Card cast — chosen by the explicitly documented,
   deterministic action policy. It never performs both in one response phase and
   never automatically casts every eligible ability. Eligibility, ordering,
   cooldown handling, determinism and tie-breaking are owned by `BOSS_RULES.md`
   §4.3.
5. **Encounter mechanics are not Boss Card actions.** Enrage, HP-threshold
   regeneration, and HP-threshold or Combo-threshold passives remain passive or
   trigger-based mechanics; they never become player-like Card actions, and are
   not reclassified merely to achieve architectural symmetry (`BOSS_RULES.md`
   §5, §6.2; `ADR-029`).
6. Bosses should have unique mechanics; not all Bosses should use the same
   trigger pattern.
7. Boss difficulty should not depend exclusively on HP/ATK inflation.
8. Boss mechanics must be deterministic where possible; Boss state is
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
MatchCreated, MatchResolved, CascadeCreated, ComboChanged, GemMatched,
CascadeStepResolved, MatchedGemSummary
PowerChanged
PassiveCharged, PassiveTriggered
RelicTriggered
CardCast, PetSkillCast
DamageCalculated, DamageDealt, DamageTaken
BossSkillCast, BossCardCast
BattleWon, BattleLost
```

`MatchedGemSummary` is the canonical name of the one **per-board-cycle matched-Gem
report**: how many Gems of each Gem type the acting side matched across every
cascade pass of that cycle, counted once per cleared cell. It is emitted once per
committed board action — the Player's committed Swap and the Boss Response
Phase's board action — after that cycle's whole cascade resolution and inside the
round's existing single `ReceiveEvents` batch. It reports the accounting that
already exists (`COMBAT_RULES.md` §2 item 6; `MATCH3_RULES.md` §5.7, §5.8.2) and
creates no value of its own: it is not damage, healing, DEF buff magnitude, Power
gain, or Boss SkillCharge. Its meaning and payload are owned by `GAME_EVENTS.md`
§2; its wire schema by `SIGNALR_PROTOCOL.md` §3.2.29; the cascade cycle it closes
by `MATCH3_RULES.md` §4.

`CascadeStepResolved` was added to this list by the Operator Decision approving
TASK-278 A-1(i) and A-4 ("Approve the addition of `CascadeStepResolved` to the
existing `ReceiveEvents` batch protocol"). It is a **report of a detection
pass's gravity+spawn transition**: it is emitted inside the round's existing
single `ReceiveEvents` batch, once per recorded pass and only after that pass's
Gravity and Spawn have completed, and it carries no board. Its meaning and
payload are owned by `GAME_EVENTS.md` §2; its wire schema by
`SIGNALR_PROTOCOL.md` §3.2.28; the pass cycle it reports by `MATCH3_RULES.md`
§4.

`BossCardCast` was added to this list by **ADR-029 / TASK-303**. It is the
canonical name of the **dedicated Boss action event**: it reports that the Boss
chose and resolved a Boss Card cast in its response phase
(`BOSS_RULES.md` §4.2–§4.3). It is distinct from `CardCast`, which remains the
Player's Card-cast event with its existing payload unchanged — the Boss's cast
is never reported as a `CardCast`, so a consumer always distinguishes the two by
the discriminator, and no actor/source member is added to `CardCast`. Its
meaning and payload are owned by `GAME_EVENTS.md` §2; its wire schema by
`SIGNALR_PROTOCOL.md` §3.2.30. `BossSkillCast` remains the event of the
transitional, pre-migration Boss ability path (`BOSS_RULES.md` §4.4).

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
15. Calculate Damage          (gated on confirmed valid ATK match; if no valid ATK match is confirmed, steps 15–17 do not run)
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

Step 18 ("Resolve Boss Response") expands to the Boss response phase on the shared board (ADR-026), under the Boss's one-action-per-response-phase policy (ADR-029):

```text
18a. Boss Action Selection & Board Action — if the Boss is alive and not Stunned,
     the Boss first selects exactly ONE action for this response phase under its
     documented deterministic action policy (BOSS_RULES.md §4.3): either a board
     match action or a Boss Card cast. It never performs both and never
     automatically casts every eligible ability. If the selected action is the
     board match action, the Boss evaluates candidate swaps on the shared board
     via deterministic AI, executes a valid swap, resolves board cascades, and
     generates actor-relative resources (POWER -> Boss Power per BOSS_RULES.md
     §4.1, DEF -> temporary DEF buff, HP -> Heal). If the selected action is a
     Boss Card cast, no Boss swap is executed here.
18b. Boss Passive — evaluate the Boss's Passive trigger condition against
     the battle state. If the trigger is met, apply the Passive effect and emit
     PassiveCharged/PassiveTriggered (BOSS_RULES.md §3, GAME_EVENTS.md §2).
     The Passive is a passive/trigger-based mechanic, never a Card action, and
     it is evaluated whether 18a selected a board action or a cast.
18c. Boss Card Cast — if and only if 18a selected a Boss Card cast, evaluate the
     cast's eligibility: the Boss Card is in the Boss's authored capability set,
     BossState.Power >= its EffectiveCardCost, and its cooldown counter is 0
     (BOSS_RULES.md §4.2–§4.4, CARD_RULES.md §3 item 9, GAME_STATE.md §2.4.3).
     If eligible, deduct the cost from BossState.Power, execute the
     non-damaging Card effect (modifying board, resources, or combat modifiers;
     ADR-026, D3), emit BossCardCast, and set the cooldown counter to the Boss
     Card's authored value.
18d. Boss Attack — if and only if the Boss's own board resolution (18a) produced
     at least one confirmed valid ATK-gem match (OD-6, ADR-026 D1), the Boss's
     ATK stat applies and damage is calculated through the Damage Pipeline
     (COMBAT_RULES.md §3) to the active Pet. Cleared-gem accounting is
     reporting-only and is never the eligibility predicate. Emit DamageCalculated,
     DamageDealt (source=boss, target=player), DamageTaken (source=boss,
     target=player). If no valid ATK-gem match was confirmed — including a
     response phase in which the selected action was a cast rather than a board
     action — zero attack damage is dealt. The legacy fallback basic attack
     (Base Damage = Boss.ATK independently of the board) is RETIRED (ADR-026, D1).
     The `target=player` wire label denotes the player's side (the active Pet) —
     it is a fixed protocol label, not a separate Player HP pool
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

**The committed board of a non-terminal Round is playable.** Before the Round's
single write-back (`GAME_STATE.md` §5.1), the board the resolution produced is
held to `MATCH3_RULES.md` §1.6's playable-board invariant: every committed,
non-terminal board on which the Player may act contains at least one legal
match-producing adjacent swap, and a board that contains none is replaced by
§1.6's deterministic regeneration on the battle's existing PRNG stream. This
paragraph adds no step to the list above and reorders none — it is evaluated on
the finished board, after the Boss response phase (step 18) and the step 19a
Status Effect pass, and before the write-back that `GAME_STATE.md` §5.1 owns.
A terminal Round (`§1.4` above) performs no recovery, and a deadlocked terminal
board is committed as it is (`MATCH3_RULES.md` §1.6.4). The operation is
server-internal and emits no event: it is reported by the state it commits
(`MATCH3_RULES.md` §1.6.6, `GAME_EVENTS.md` §3 item 8).

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

**A Boss Card cast is not a Player Card cast and is not outside this order.** The
paragraph above scopes the *Player's* auxiliary cast. The Boss's cast is one of
the two actions the Boss selects **inside** step 18 (18a selects it, 18c resolves
it), so it is part of a committed Swap's resolution, it is never emitted as a
`CardCast`, and the Player's one-per-Turn allowance neither bounds nor is spent
by it (`CARD_RULES.md` §3 item 9, `BOSS_RULES.md` §4.3, `ADR-029`). The list
above is unchanged by that statement: no step is added, removed, or reordered.

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
