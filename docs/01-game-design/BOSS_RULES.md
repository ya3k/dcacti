# Boss Rules

**Version:** 2.12 (§6.2.6's client-visibility constraint reworded, applying the
TASK-208 Product Owner decision **D-208-03**: the Boss's canonical technical
Identity (`BossState.BossId`, §6.4) is client-visible alongside the
already-delivered live `HP`/`MaxHP`, and the rest of `BossState` remains
server-side. **Wording only** — no stat, magnitude, duration, trigger, target,
threshold, operator, effect, skill, passive, or content value changes; no new
gameplay rule, mechanic, or Boss telegraph is introduced; no Element,
presentation, portrait, or asset metadata is added to the wire contract; and no
`BossState` member, SignalR member, `BattleState` member, event, Redis key, or
database column is added. The wire contract is owned by `SIGNALR_PROTOCOL.md`
§4.4 and the state boundary by `GAME_STATE.md` §2.4; both are referenced, not
restated. Prior 2.11: §6/§6.2/§6.2.3 **Mộc Yêu** passive trigger threshold updated
from every 5 player matches to every 8 player matches per Product Owner MVP balance
decision TASK-200 / Q-8 in TASK-191. Regeneration magnitude remains strictly 5%
MaxHP (250 HP); event ordering, turn semantics, deterministic behavior, and server
authority are preserved. Prior 2.10: §6.2.4 **Sơn Thạch Vệ** amended per TASK-173,
applying the recorded Product Owner decision (Option B): the ambiguous
retrigger-guard sentence — "The retrigger guard is the authored one-time behavior
above, not a new state field." — is retired and replaced by the explicit contract. The
Passive's reset behavior
is the existing non-default `Persistent` token, and `PASSIVE_RULES.md` §4's Boss
Passive clause makes that behavior govern **firing eligibility**: the Passive is
eligible to fire at most once per battle, eligibility is consumed on the first
activation, and the 3-Turn effect's expiry at step 19a does **not** restore it —
remaining at or below 50% HP, or leaving and re-entering the threshold, produces
no further activation. A new battle creates fresh eligibility. **No magnitude,
duration, trigger, operator, representation, or `TargetStat` changed**; §6.1's
Enrage contract stays separate, and no new `BossState` field, reset token, event,
SignalR member, `BattleState` member, Redis key, or database column is
introduced. Decision source: TASK-173. Prior 2.9: (§6 MVP Boss reference **completed** per TASK-172, applying the
TASK-171 Product Owner decisions: the two remaining MVP Bosses are now
content-defined, so §6/§6.1/§6.2/§6.3/§6.4 each gain one row per Boss and §6.3.1's
closing "not yet content-defined" deferral note is retired. §6.1 gains **Sơn
Thạch Vệ** (Thổ, HP/MaxHP 3000, ATK 120, DEF 0, EnrageThreshold 1500,
Initial State `Idle`) and **Kim Lôi Vương** (Kim, HP/MaxHP 2800, ATK 140, DEF 0,
EnrageThreshold 2100, Initial State `Idle`). §6.2 gains their two Passive rows and
the `PassiveThreshold = null` disposition for both (neither is match-charged), and
§6.2 gains §6.2.4/§6.2.5 — their per-Passive detail — with the former §6.2.4
renumbered to §6.2.6. §6.3 gains their two Skill rows and §6.3.1 items 4 and 5.
§6.4 gains their two identity rows. **Both new Passives reuse the unchanged
existing contracts**: the Turn-based `BuffDebuff` `TargetStat = "ATK"` modifier of
`COMBAT_RULES.md` §5.5 (the §6.2.1 Rage shape), the §5.3 Turn-based lifecycle,
`PASSIVE_RULES.md` §4's reset forms, and §3 item 2's closed trigger list (`Boss
HP`, `Combo`). Both Skills are direct damage through the unchanged Damage
Pipeline using §6.3's unchanged charge/cooldown mechanism. **No new Element,
trigger category, status type, `TargetStat`, damage type, Boss State, resource,
representation, event, SignalR member, `BattleState` member, Redis key, or
database column is introduced, and no schema or provisioning change is made** —
`MaxHP`/`ATK`/`DEF`/`EnrageThreshold` remain non-column Domain content
(`DATABASE.md` §1). The `BossDefinition` rows and Domain `BossDefinitions`
entries for both Bosses remain a separate, later provisioning task. Decision
source: TASK-171; applying task: TASK-172.
Prior 2.8: (§6.2.4 reconciled with the TASK-160 Product Owner decision
**D-2A**, which authorized the Boss's live `HP` and `MaxHP` for MVP client
synchronization. The deferral sentence — which recorded non-exposure as
intentional and left "client-visible Boss HP or Boss StatusEffects" to "a
separate future protocol decision" — is replaced by the recorded outcome: live
Boss HP **is** delivered (`SIGNALR_PROTOCOL.md` §4.4 owns the wire contract, and
`GAME_STATE.md` §2.4 owns the state boundary), while `BossState` itself and the
Boss's `StatusEffects[]` remain **not** client-visible. **No gameplay rule,
magnitude, duration, trigger, target, or effect changed**; §6.2.1–§6.2.3,
§6.3–§6.4, and §7 are unchanged, and no new Battle Event, SignalR method,
`BattleState` member, Redis key, or database column is introduced. Decision
source: TASK-160; applying task: the downstream documentation-resolution task.
Prior 2.7: (§6.2.2 completed per TASK-155, applying the TASK-154 Product
Owner decision (Option B): the Boss-carried Thủy Ma healing-reduction instance
is now reconciled with the Pet-scoped Heal Resolution step that applies it.
§6.2.2 states the **applicable-instance selector** — the Status Effect identity
`Id = "boss-thuy-ma-heal"`, the value already recorded as Thủy Ma's `PassiveId`
in §6.4, reused as the instance's `Id` under `GAME_STATE.md` §2.3.1 item 1 and
item 6 rather than a new identity or a `TargetStat` value — and states the
**explicitly authorized cross-entity read**: the Pet-side Heal Resolution step
(`COMBAT_RULES.md` §4 item 7) reads that Boss-held instance at its Applicable
Heal Modifiers stage, **one-directionally and non-mutating**, selecting by `Id`.
§6.2.2 also records that the modifier is persistent/Turn-based, is not consumed
by healing, by an action, or once, and is removed only by the existing step-19a
Turn-duration lifecycle, and confirms the −50% applies to the Raw Heal before
`COMBAT_RULES.md` §4 item 1's clamp, which stays last. The magnitude (`50%`),
duration (`3 turns`), Battle Start trigger, Pet-only target, reapplication rule,
and the "no new event or protocol" boundary are **UNCHANGED**. No new `TargetStat`
value, no `PendingStatusEffects[]`, no second in-flight representation, no new
Battle Event, SignalR member, `BattleState` member, Redis key, or database column
is introduced, and `GAME_STATE.md` §2.3.1's invariants are unchanged. Mechanics
remain referenced to their owners (`COMBAT_RULES.md` §4 items 1 and 7, §5.2
item 2, §5.3, §5.4.5, §5.5.3) rather than restated. Decision source: TASK-154;
applying task: TASK-155. Prior 2.6: (§6.2.1's Hỏa Long Rage damage-scope bullet reconciled per
TASK-126 with the Boss Skill Step-1 composition TASK-125 decided and
`COMBAT_RULES.md` §3.4 now owns. The bullet previously stated that Rage reaches
"only Boss damage whose Step-1 `Attack` input derives from `BossState.ATK` — the
Boss basic attack", which contradicted the resolved composition; it now states
that Rage reaches Boss damage through the Step-1 `EffectiveBossATK`
contribution — present in **every** Boss damage instance, basic attack and Boss
Skill alike — while never reaching a Skill's **authored Base Damage** value
(Flame Burst's 150 remains 150 and does not itself receive the +20%). Mechanic
and composition are referenced to their owners (`COMBAT_RULES.md` §3.4, §5.5)
rather than restated. §6.3/§6.3.1's authored magnitudes are unchanged, as is the
Rage `+20%` / 3-turn magnitude and every TASK-123/TASK-124-recorded behavior.
No magnitude changed, no new event, SignalR member, `BattleState` member, Redis
key, or database column introduced. Prior 2.5: §6.2 corrected and expanded per TASK-124 — the Boss Passive
effect contract TASK-123 decided is now recorded at its canonical owner. The
Thủy Ma trigger is corrected from the stale `Passive (always active)` wording
to **Battle Start** (`PASSIVE_RULES.md` §3, a one-time trigger), and the
retired pointer to an alternate trigger that §3 does not define is removed.
§6.2 gains §6.2.1–§6.2.4: Hỏa Long Rage (representation, damage scope, apply
point, duration, reapplication), Thủy Ma healing reduction (trigger,
representation, application site, duration window, reapplication), Mộc Yêu
regeneration (timing, base, truncation, clamp, direct `BossState.HP` update,
repetition), and the server-authority/intentional-client-invisibility
statement. Mechanics are referenced to their owners (`COMBAT_RULES.md` §4
item 7, §5.2 item 2, §5.3, §5.5; `GAME_STATE.md` §2.3.1/§2.4.1;
`GAME_RULES.md` §17 step 18a) rather than restated. No magnitude changed, no
new event, SignalR member, `BattleState` member, Redis key, or database column
introduced. Prior 2.4: §6.4 identity contract completed per TASK-048 — the Thủy Ma
and Mộc Yêu technical Identities, previously recorded `UNRESOLVED`, are now
recorded as `"boss-thuy-ma"` and `"boss-moc-yeu"`; the code-alignment note is
corrected now that source and tests carry the canonical Identities (TASK-047);
prior 2.3: §6.4 identity contract resolved per TASK-046 — BossId =
canonical technical Boss Identity (e.g. `"boss-hoa-long"`), display name =
presentation-only content, `BossDefinitionId` = persistence PK (DATABASE.md);
prior 2.2: Boss triggers and
terminal checks reference active Pet HP/Power instead of a separate Player
combat identity; wire `target`/`BattleLost` semantics clarified — §3.3
sub-step citations corrected to 18b/18c; §5.4 Enrage ordering stated; §6.2
Thủy Ma Always-Active clarified; §6.3 Turn citation corrected; §6.4 identity
contract added)
**Status:** MVP Domain Rule
**Parent:** GAME_RULES.md

Expands GAME_RULES.md §15 (Boss Rules). Conflicts resolve in favor of
GAME_RULES.md.

---

# 1. Boss Structure

```text
Boss
├── Element      (exactly one of the Five Elements)
├── HP / Max HP
├── ATK
├── DEF
├── Passive       (automatic, reactive — see §3)
├── Skill          (Boss-initiated action — see §4)
└── State          (internal enum, e.g. Idle / Charging / Enraged / Stunned)
```

A battle has exactly one Boss (GAME_RULES.md §1.1).

---

# 2. Design Principle: Mechanics Over Stats

Per GAME_RULES.md §15.1–15.3 and GDD §13:

1. Boss difficulty must come primarily from **mechanics** (Passive triggers,
   Skill timing, reactive punishes), not from HP/ATK inflation alone.
2. Every MVP Boss must have a Passive AND a Skill that are mechanically
   distinct from each other and from other Bosses' Passive/Skill.
3. Bosses must not share the same trigger pattern (GAME_RULES.md §15.2) —
   see §3.2 below for how MVP satisfies this.

---

# 3. Boss Passive

1. A Boss Passive is automatic and reactive, similar in structure to a Pet
   Passive (PASSIVE_RULES.md), but triggered by the *player's* actions or
   battle state rather than the Boss's own Matches (Bosses do not match
   Gems).
2. Supported Boss Passive triggers (mirrors GAME_RULES.md §15, "Bosses may
   react to"):
   ```text
   Turn
   Match Count (player's)
   Combo (player's)
   Active Pet Power
   Active Pet HP
   Boss HP
   Status (on Boss or active Pet)
   ```
3. Boss Passive effects are automatic — no player input required, and no Boss
   "casting" input required either; they resolve as part of normal battle
   flow (see Event Resolution, GAME_RULES.md §17).

## 3.1 Determinism

Boss Passive/Skill logic must be deterministic given the same battle state
history (GAME_RULES.md §15.4) — no hidden randomness beyond what is
explicitly declared as part of the Skill's design (e.g. a Skill that already
states "random target" may use server RNG, but an undeclared random branch is
not allowed).

## 3.2 Trigger Pattern Diversity

To satisfy "not all Bosses use the same trigger pattern," each MVP Boss's
Passive must use a different primary trigger category from §3.2's list than
the other MVP Bosses where possible. See §6 for the MVP assignment.

## 3.3 Timing Within the Resolution Order

Boss Passive fires at Step 18 of GAME_RULES.md §17, after Player Damage
(Steps 15–17) and before Boss Skill (step 18b) and Boss Attack (step 18c).

1. **The Passive fires once per player action, after all player damage is
   resolved.** This means the Passive sees the post-damage battle state
   (active Pet HP, Boss HP, Combo, match count). A Boss Passive that triggers
   on "Boss HP below threshold" evaluates against the HP after the player's
   damage, not before.
2. **The Passive fires before the Boss Skill.** The Boss Skill's timing
   rule (charge requirement, cooldown) is evaluated after the Passive
   resolves. If the Passive's effect changes the battle state (e.g. a
   self-buff), the Boss Skill sees that updated state.
3. **The Passive fires before the Boss Attack.** If the Boss Skill does
   not fire (charge not met or cooldown active), the Boss performs a basic
   attack (step 18c). The Passive's effect is already applied at that point.
4. **Boss Skill damage does not re-trigger the Boss Passive.** The Passive
   resolves once at Step 18 and does not re-evaluate after the Skill
   (step 18b) or Attack (step 18c). If a Boss Passive triggers on "Boss HP
   below threshold," it evaluates once against the post-player-damage state
   — not again after the Boss's own actions.

---

# 4. Boss Skill

1. A Boss Skill is a distinct, usually stronger action the Boss performs,
   analogous to a Pet's Signature Skill but Boss-initiated rather than
   player-cast.
2. Boss Skills are triggered by the Boss's own timing rule (e.g. every N
   Turns, or when a State condition is met) — this timing rule is itself
   part of the Boss's design and must be explicit, not implicit.
3. Boss Skill damage (if any) goes through the full Damage Pipeline
   (COMBAT_RULES.md §3), including the Boss's Element for Element Modifier
   purposes (ELEMENT_RULES.md §5).
4. Boss Skills may apply non-damage effects (debuffs, resource drain, etc.)
   as shown in the MVP examples below.

---

# 5. Boss State

1. Boss "State" is an internal enum used to gate which Passive/Skill logic is
   currently active (e.g. a Boss might be Idle until a Passive-triggered
   condition flips it to Enraged, changing its Skill behavior).
2. State is entirely server-authoritative (GAME_RULES.md §15.5) and must be
   exposed to the client only through emitted events (e.g. `BossSkillCast`)
   and rendered state, never computed client-side.
3. MVP Bosses may use State minimally (e.g. a simple Idle/Casting toggle); a
   full multi-phase State machine is a Boss Phases feature explicitly
   deferred to Future Expansion (GDD §20), not required for MVP.
4. **Enrage** is a permanent state transition triggered when `BossHP <
   EnrageThreshold` (per-Boss value in §6.1). Once Enraged, the Boss remains
   Enraged for the rest of the battle — no timer, no duration field. The
   Enrage threshold and any Enrage-specific behavior changes (e.g. Skill
   damage increase) are defined per Boss in §6. Enrage is evaluated after
   Player→Boss damage (GAME_RULES.md §17 steps 15–17) and **before** the
   Boss HP terminal check and Boss Response (step 18): the state transition
   is applied whenever the HP condition holds, including when Player damage
   has just reduced Boss HP to 0 (the terminal check then ends the battle
   with no Boss Response). Order: Player→Boss Damage → Enrage → terminal
   Boss HP check → Boss Response 18a–18c → terminal active Pet HP check.
5. **Stun** is a temporary state that prevents the Boss from acting. Duration
   is measured in Turns and tracked by `StatusEffects[]` (its state contract is
   `GAME_STATE.md` §2.3.1 / §5.1.1; decay timing follows the Turn-based rule in
   `COMBAT_RULES.md` §5.3). For MVP, no content-defined Boss applies Stun.

---

# 6. MVP Boss Reference

```text
Boss        Element   Passive (trigger)                          Skill (Effect Magnitudes)                                  Skill Timing
---------   -------   -----------------------------------------  ---------------------------------------------------------  ----------------------
Hỏa Long    Hỏa       Every 5 Player Matches → gain Rage          Flame Burst → 150 Dmg + Burn (50 dmg/tick, 2 Turns)       Charge: 5 matches, CD: 2T
Thủy Ma     Thủy      Battle Start → healing received reduced    Drain Power → 120 Dmg + −20 flat Pet Power                Charge: 4 matches, CD: 3T
Mộc Yêu     Mộc       Every 8 Player Matches → Regen HP           Root → 100 Dmg + −30% Pet ATK (2 Turns)                   Charge: 6 matches, CD: 2T
Sơn Thạch Vệ Thổ      Boss HP ≤ 50% → gain Rage                    Earthquake → 150 Dmg                                       Charge: 5 matches, CD: 0T
Kim Lôi Vương Kim     Player Combo ≥ 4 → gain Rage                Thunder Strike → 180 Dmg                                   Charge: 5 matches, CD: 0T
```

Skill effect targets (`−Pet Power`, `−Pet ATK`) are the active Pet's
stats in `PetState` — not a separate Player entity.

### 6.1 MVP Boss Base Stats (Project-Owner Approved)

```text
Boss        Element   HP / MaxHP   ATK   DEF   EnrageThreshold   Initial State
---------   -------   ----------   ---   ---   ---------------   -------------
Hỏa Long    Hỏa       5000         100   50    1500 (30%)        Idle
Thủy Ma     Thủy      5000         100   50    1500 (30%)        Idle
Mộc Yêu     Mộc       5000         100   50    1500 (30%)        Idle
Sơn Thạch Vệ Thổ      3000         120   0     1500 (50%)        Idle
Kim Lôi Vương Kim     2800         140   0     2100 (75%)        Idle
```

### 6.2 Boss Passive Details

This subsection is the **canonical owner** of the MVP Boss Passive **rows** —
which Boss carries which Passive effect and what its trigger is. The *mechanics*
that consume these values are owned elsewhere and are referenced, not restated
(`.ai/workflow/documentation/documentation-change.md` §2).

```text
Boss        Passive Effect                                    Passive Trigger
---------   -----------------------------------------------   ----------------------
Hỏa Long    Gain +20% ATK (Rage) for 3 turns                  Every 5 Player Matches
Thủy Ma     Active Pet healing reduced by 50% for 3 turns     Battle Start
Mộc Yêu     Regenerate 5% MaxHP                               Every 8 Player Matches
Sơn Thạch Vệ Gain +20% ATK (Rage) for 3 turns                 Boss HP ≤ 50%
Kim Lôi Vương Gain +20% ATK (Rage) for 1 turn                 Player Combo ≥ 4
```

**PassiveThreshold (match-charged passives only):** Hỏa Long uses
PassiveThreshold = 5 and Mộc Yêu uses PassiveThreshold = 8
(TASK-200 / PASSIVE_RULES.md §2 — progress increments per Player Match,
Threshold evaluated once per Cascade batch). Thủy Ma's trigger
is **Battle Start** (`PASSIVE_RULES.md` §3 — "Battle Start (one-time
trigger)"), **not** match-based: it has no PassiveThreshold for match
counting, is never charged via `PassiveTracker.Charge` on Player Matches, and
emits no `PassiveCharged`/`PassiveTriggered` from match progress.

Sơn Thạch Vệ's trigger (`Boss HP ≤ 50%`) and Kim Lôi Vương's trigger
(`Player Combo ≥ 4`) are likewise **not** match-based — they are evaluated
against the post-damage battle state at Boss Response step 18a (§3.3 item 1),
not accumulated per Player Match. Both therefore have **`PassiveThreshold =
null`**: neither is charged via `PassiveTracker.Charge` on Player Matches, and
neither emits `PassiveCharged` from match progress. Per `DATABASE.md` §1 note
item 3, `null` means **no match-charging threshold**; it is **not** a statement
that either Passive is always-active — each is threshold-triggered, exactly as
Thủy Ma's is event-triggered.

#### 6.2.1 Hỏa Long — Rage

- **Magnitude and duration:** `+20% ATK` for 3 turns. These are this
  document's values and are not restated elsewhere.
- **Representation and consumption:** a Turn-based `BuffDebuff` Status Effect
  instance in `BossState.StatusEffects[]` with `TargetStat = "ATK"`,
  `Magnitude = +20%`, `RemainingTurns = 3`. `BossState.ATK` remains the
  immutable/base value and is never overwritten; Rage is **not** a separate
  `BossState` field (the existing Status Effect model — `GAME_STATE.md` §2.3.1,
  §2.4.1).
- **Damage scope:** Rage reaches Boss damage through the Step-1 `EffectiveBossATK`
  input — the contribution derived from `BossState.ATK`. That contribution is
  present in every Boss damage instance, the basic attack included, so Rage
  reaches both the basic attack and a Boss Skill. What it never reaches is a
  Skill's **authored Base Damage** value: Flame Burst's 150 (§6.3.1 item 1)
  remains 150 and does **not** itself receive the +20%; the Skill's Step-1
  damage carries the modifier only through its `EffectiveBossATK`
  contribution. The `+20%` is applied to that Step-1 input, **not** to Step 4.
  Composition owner: `COMBAT_RULES.md` §3.4 ("Boss Skill Step-1 composition");
  consumption rule: `COMBAT_RULES.md` §5.5.
- **When it applies:** at Boss Response step 18a (`GAME_RULES.md` §17 step
  18a). The application does not retroactively modify damage already resolved
  earlier in that Turn.
- **Duration and reapplication:** owned by `COMBAT_RULES.md` §5.3 (the
  Turn-based duration lifecycle) and §5.2 item 2 (the refresh-not-stack
  default). A re-trigger while Rage is active refreshes the existing instance
  to `RemainingTurns = 3`; it does **not** create a second instance and does
  **not** stack magnitude. At most one +20% Rage instance is active at a time,
  with the same source identity.
- **No new state or protocol:** Rage adds no `BossState` member, no Battle
  Event, and no SignalR member. The consumption rule is
  `COMBAT_RULES.md` §5.5.

#### 6.2.2 Thủy Ma — healing reduction

- **Magnitude and duration:** active Pet healing reduced by `50%` for
  3 turns. These are this document's values and are not restated elsewhere.
- **Trigger:** **Battle Start** — a one-time trigger (`PASSIVE_RULES.md` §3),
  evaluated once when the battle session is created, before the first Turn.
  It is **not** match-charged and is **not** always-active.
- **Representation:** the existing Turn-based Buff/Debuff Status Effect model
  (`GAME_STATE.md` §2.3.1), held in `BossState.StatusEffects[]` — no new
  trigger mechanism, no `PassiveTracker.Charge`, and no
  `PassiveCharged`/`PassiveTriggered` from match progress. The instance's
  `Type` does not select a stat consumer; the applicable-instance selector is
  the **`Id`** (below).
- **Applicable-instance selector:** the instance is identified by the Status
  Effect identity `Id = "boss-thuy-ma-heal"` under `GAME_STATE.md` §2.3.1
  item 1 (an `Id` is an identity, not a definition) and item 6 (at most one
  instance per identity per entity, `Id` being the uniqueness key, so a
  re-application refreshes that instance). The value is Thủy Ma's recorded
  **`PassiveId`** (`BOSS_RULES.md` §6.4) — reused as this instance's `Id`, not
  a new identity, and deliberately **not** a `TargetStat` value. A Pet-side
  read selects the applicable Boss-held instance by that `Id` (the
  `BOSS_RULES.md` §6.3.1 per-Boss declaration pattern); it does **not** select
  by `Type` + `TargetStat`. This is a `BuffDebuff`-typed instance in the
  documented element model, and it is **not** a `TargetStat`-consumed
  `BuffDebuff`: it is not consumed by a stat rule, and
  `COMBAT_RULES.md` §5.4.5 / §5.5.3 consequently open no new non-`"ATK"`
  `TargetStat` case for it.
- **The authorized cross-entity read:** the Pet-side **Heal Resolution** step
  (`COMBAT_RULES.md` §4 item 7) is **explicitly authorized to read** this
  Boss-held instance — the read direction is Pet-side step → Boss-owned
  instance. It is **one-directional and non-mutating**: the step reads
  `BossState.StatusEffects[]` and does not write `BossState`, and it does not
  consume, decrement, remove, or otherwise alter the instance. The read is
  evaluated at that step's **Applicable Heal Modifiers** stage, and it is the
  only mechanism by which this effect reaches Pet healing; no other document
  and no second carrier is involved.
- **Where the −50% is applied:** at the shared **Heal Resolution** step
  (`COMBAT_RULES.md` §4 item 7), **before** the existing overheal clamp
  (`COMBAT_RULES.md` §4 item 1). It is **one applicable Heal modifier**, not a
  special-cased site. It reaches Pet healing from any existing source that uses
  that resolution — explicitly including Card Heal and HP-Gem healing. The
  modifier applies to the **Raw Heal**, through that stage, to produce the
  Final Heal Amount; `COMBAT_RULES.md` §4 item 1's clamp remains unchanged and
  still last. It does **not** modify MaxHP and does **not** affect Shield.
- **Duration:** applied at Battle Start with `RemainingTurns = 3`; the
  Battle Start application is not a Turn and consumes no duration unit; the
  effect is active throughout Turns 1, 2, and 3; the existing Turn-based
  lifecycle decrements at the End Turn / step 19a boundary
  (`COMBAT_RULES.md` §5.3); after step 19a of Turn 3, `RemainingTurns` reaches 0
  and the effect expires before Turn 4. No new duration mechanism or lifecycle
  phase is introduced. The modifier is **persistent / Turn-based**: it is
  **not** consumed per heal, not consumed per action, and not consumed once,
  and it survives Turn transitions, Swaps, non-healing actions, and multiple
  heals for as long as it is active. It is removed **only** by the existing
  Turn-duration lifecycle at the step 19a resolution that reaches
  `RemainingTurns = 0` (`COMBAT_RULES.md` §5.3; `GAME_STATE.md` §5.1.1 item 5),
  and a stored zero is never an active state (`GAME_STATE.md` §2.3.1 item 8).
- **Reapplication:** a further application while an instance is active
  refreshes that instance to the full 3-turn duration. It does **not** stack
  additively (−50% + −50% = −100% is explicitly not the behavior); at most one
  active instance exists at a time, with the same source identity. The refresh
  rule is `COMBAT_RULES.md` §5.2 item 2's MVP default and is referenced, not
  restated. *(Reachability note: because the Battle Start trigger is one-time,
  no second application source exists in the current MVP content; the rule is
  the safe behavior and applies to any future re-application source.)*
- **No new event or protocol:** the effect introduces no Battle Event and
  emits no `PassiveCharged`/`PassiveTriggered`. Application, refresh,
  decrement, and expiry are state changes only; there is no `HealingReduced`,
  `BossPassiveApplied`, or `BossPassiveExpired` event.

#### 6.2.3 Mộc Yêu — regeneration

- **Trigger cadence:** `PassiveThreshold = 8` (every 8 Player Matches, charged via
  `PassiveTracker.Charge` using the default reset behavior; updated from 5 matches
  under Product Owner MVP balance decision TASK-200 / Q-8 in TASK-191).
- **Magnitude:** heals exactly `5%` of Mộc Yêu's MaxHP — this document's
  value. At the §6.1 MVP MaxHP of `5000`, the amount is
  `truncate(5000 × 5 / 100) = 250`.
- **When it applies:** during Boss Response step 18a, at the point the Boss
  Passive effect is applied (`GAME_RULES.md` §17 step 18a; §3.3).
- **Rounding:** truncated **toward zero** to an integer HP amount — the same
  integer convention `COMBAT_RULES.md` §3 step 6 uses for Final Damage.
- **Clamping:** `Final HP = min(CurrentHP + RegenAmount, MaxHP)`. No overheal
  is retained — the same shaping as `COMBAT_RULES.md` §4 item 1's "restore HP
  up to Max HP; overheal is discarded", which is referenced, not restated.
- **Applied as:** a **direct authoritative `BossState.HP` update**. No
  persistent `StatusEffect` instance is created, and the regeneration is
  **not** routed through `COMBAT_RULES.md` §4 item 7's Heal Resolution step,
  whose scope is Pet-HP healing only.
- **Observability:** no new Battle Event. The resulting state is server-side
  and authoritative, synchronized through the existing authoritative state
  mechanism (see §7 and §8).
- **Repetition:** each valid Mộc Yêu Passive activation applies **one** 5%
  MaxHP regeneration. It does **not** stack as a persistent modifier.

#### 6.2.4 Sơn Thạch Vệ — Rage on an HP threshold

- **Magnitude and duration:** `+20% ATK` for 3 turns. These are this
  document's values and are not restated elsewhere.
- **Trigger:** `Boss HP ≤ 50%`, category **Boss HP** (§3 item 2's closed
  trigger list). It is evaluated at Boss Response step 18a against the
  **post-damage** battle state (§3.3 item 1) — the HP after the player's damage
  for that Turn, not before. It is **not** match-charged and is **not**
  always-active: it is a threshold trigger, and its `PassiveThreshold` is
  `null` (see §6.2's paragraph above).
- **This trigger is NOT the Enrage transition.** `EnrageThreshold` is the
  separate §6.1 base-stat field, and §5 item 4 owns the permanent
  `Idle → Enraged` transition at `BossHP < EnrageThreshold`. The two boundaries
  coincide numerically at this Boss (`EnrageThreshold = 1500` with
  `MaxHP = 3000` is 50%), but they remain **separate contract concepts** with
  different boundary operators: §5 item 4's Enrage uses strict `<`, while this
  Passive's trigger uses `≤`. Enrage is defined per Boss in §6; this Passive
  does not re-express it, and no second Enrage mechanism exists.
- **Representation and consumption:** a Turn-based `BuffDebuff` Status Effect
  instance in `BossState.StatusEffects[]` with `TargetStat = "ATK"`,
  `Magnitude = +20%`, `RemainingTurns = 3` — the same representation the §6.2.1
  Rage uses, under the existing consumption rule `COMBAT_RULES.md` §5.5
  (§5.5.1 the rule, §5.5.2 the damage it reaches, §5.5.4 the base stat is never
  overwritten). `BossState.ATK` remains the immutable/base value and is never
  overwritten; this effect is **not** a separate `BossState` field
  (`GAME_STATE.md` §2.3.1, §2.4.1).
- **Duration and reapplication:** the instance's duration is consumed by the
  unchanged Turn-based lifecycle (`COMBAT_RULES.md` §5.3) at the step-19a
  boundary. This Passive is authored as **one-time**, and its reset behavior is
  the non-default **No reset / persistent** form whose storage token is the
  existing `Persistent` (`DATABASE.md` §1 note item 3) — documented here per
  `PASSIVE_RULES.md` §4 item 3, whose Boss Passive clause makes that behavior
  govern **firing eligibility**:
  1. It triggers at `Boss HP ≤ 50%`, evaluated against the post-damage state at
     step 18a (§3.3 item 1).
  2. It applies `+20% ATK` for 3 Turns, in the representation above.
  3. Its firing eligibility is **consumed** upon the first activation.
  4. When the 3-Turn effect expires at step 19a, firing eligibility is **not**
     restored: the Passive does not re-trigger, even if Boss HP remains
     `≤ 50%` for any number of later Turns, and even if the Boss leaves the
     threshold and later re-enters it.
  5. A new battle creates fresh firing eligibility.

  Consequently no second application source exists within a battle, and no
  refresh or stacking behavior arises. `Persistent` here does **not** mean the
  effect lasts indefinitely, and no new reset token is introduced.
- **When it applies:** at Boss Response step 18a (`GAME_RULES.md` §17 step
  18a). The application does not retroactively modify damage already resolved
  earlier in that Turn. Because §3.3 item 2 fixes the Skill evaluation after
  the Passive resolves, the Skill sees this Rage's updated state.
- **No new state or protocol:** adds no `BossState` member, no Battle Event, no
  SignalR member, no status type, and no `TargetStat` value. Firing eligibility
  is governed by the definition's `Persistent` reset behavior under
  `PASSIVE_RULES.md` §4 and is consumed upon first activation; no new
  `BossState` field is added.

#### 6.2.5 Kim Lôi Vương — Rage on a Combo threshold

- **Magnitude and duration:** `+20% ATK` for 1 turn. These are this document's
  values and are not restated elsewhere.
- **Trigger:** `Player Combo ≥ 4`, category **Combo** (§3 item 2's closed
  trigger list; `PASSIVE_RULES.md` §3's alternate form "Combo (e.g. `on Combo
  ≥ N`)"). It is evaluated at Boss Response step 18a against the post-damage
  battle state (§3.3 item 1). It is **not** match-charged: its
  `PassiveThreshold` is `null` (see §6.2's paragraph above).
- **This trigger is NOT the Enrage transition.** `EnrageThreshold` is the
  separate §6.1 base-stat field, owned by §5 item 4's permanent
  `Idle → Enraged` transition at `BossHP < EnrageThreshold` — here `2100` with
  `MaxHP = 2800`. The Combo trigger does not drive Enrage, the Enrage
  transition does not trigger this Passive, and the two mechanics are not
  merged.
- **Representation and consumption:** a Turn-based `BuffDebuff` Status Effect
  instance in `BossState.StatusEffects[]` with `TargetStat = "ATK"`,
  `Magnitude = +20%`, `RemainingTurns = 1` — the same representation the §6.2.1
  Rage uses, under the existing consumption rule `COMBAT_RULES.md` §5.5
  (§5.5.1 the rule, §5.5.2 the damage it reaches, §5.5.4 the base stat is never
  overwritten). `BossState.ATK` remains the immutable/base value and is never
  overwritten; this effect is **not** a separate `BossState` field
  (`GAME_STATE.md` §2.3.1, §2.4.1). No `BossNextAttackATKModifier`, no
  `ComboPunishmentModifier`, and no new `StatusEffect` type is introduced: the
  stated intent — the Boss's next relevant attack is stronger — is reached
  through the existing Step-1 `EffectiveBossATK` contribution, which is present
  in every Boss damage instance (`COMBAT_RULES.md` §5.5.2).
- **Duration and reapplication:** the instance's duration is consumed by the
  unchanged Turn-based lifecycle (`COMBAT_RULES.md` §5.3) at the step-19a
  boundary. Reset behavior is the documented **Default** (`PASSIVE_RULES.md`
  §4 item 1), and a re-trigger while the instance is active follows the existing
  refresh-not-stack default (`COMBAT_RULES.md` §5.2 item 2, §5.5.5) — it
  refreshes the existing instance and does **not** stack magnitude or create a
  second instance. No new stacking behavior is authored.
- **When it applies:** at Boss Response step 18a (`GAME_RULES.md` §17 step
  18a). The application does not retroactively modify damage already resolved
  earlier in that Turn. Because §3.3 item 2 fixes the Skill evaluation after
  the Passive resolves, the Skill sees this Rage's updated state.
- **No new state or protocol:** adds no `BossState` member, no Battle Event, no
  SignalR member, no status type, and no `TargetStat` value.

#### 6.2.6 Applied effects and client visibility

All five effects are **server-authoritative** (§8, `GAME_RULES.md` §18,
`ADR-001`). The client does not calculate, predict, or authoritatively apply
any of them.

**The Boss's live `HP`/`MaxHP` and its canonical technical Identity are
client-visible; the rest of `BossState` is not.** The SignalR projection
delivers the Boss's current `HP` and `MaxHP` and `BossState.BossId` (§6.4) —
and only those three members — as the `bossState` member of the existing state
push. The live-health authorization is the Product Owner decision recorded by
TASK-160 (**D-2A**); the Identity authorization is the Product Owner decision
recorded by TASK-208 (**D-208-03**), which is the documentation amendment this
revision applies. The projection's member names, types, presence, and
client-boundary rules are owned by `SIGNALR_PROTOCOL.md` §4.4; this document
states the visibility
constraint and does not restate the wire contract. `BossState` itself is **not**
exposed: the Identity travels as an identity only, with no display name,
Element label, portrait, or asset key attached, and `Element`, `ATK`, `DEF`,
`State`, `PassiveId`, `PassiveProgress`, `SkillCharge`, `SkillCooldown`, and
the Boss's `StatusEffects[]` stay server-side (`GAME_STATE.md` §2.4), so the
effects above
remain **not** directly client-visible as state — the player sees their
consequences through the Boss's changing HP and through the emitted events
(§7), not by reading a status list.

**Boss `StatusEffects[]` is explicitly not authorized for delivery**, then or
now: D-2A named it among the excluded members, and delivering it would be a
separate future protocol decision. Note that a *Boss-side* effect can still be
observable through the *Pet's* delivered state where the effect is a Pet-held
instance (e.g. `Root`'s ATK debuff, §6.3.1 item 3, sits in
`PetState.StatusEffects[]`) — that is the Pet's collection being delivered, not
the Boss's, and it does not widen this boundary.

### 6.3 Boss Skill Timing & Effect Details

Each Boss Skill has two timing parameters:

- **Charge Requirement**: number of player matches required before the Skill
  is eligible to fire. Matches increment `BossState.SkillCharge`
  (`GAME_STATE.md` §2.4.3). When `SkillCharge ≥ Charge Requirement` AND
  `SkillCooldown = 0`, the Skill fires.
- **Cooldown (CD)**: turns remaining after each Skill use before the Skill
  can fire again. Decrements by 1 at each Turn increment
  (`MATCH3_RULES.md` §8.1 — one committed Swap begins exactly one Turn;
  the stored Turn advances once in that resolution's single write-back,
  `GAME_STATE.md` §5.1). The Skill is blocked while `CD > 0`.

After the Skill fires: `SkillCharge` resets to 0, `SkillCooldown` resets to
the Boss's cooldown value.

```text
Boss        Skill         Charge Req.   CD (T)   Base Dmg   Secondary Effect & Magnitude
---------   -----------   -----------   ------   --------   --------------------------------------------------------
Hỏa Long    Flame Burst   5 matches     2        150        Burn: 50 fixed damage/tick for 2 Turns (step 19a ticks)
Thủy Ma     Drain Power   4 matches     3        120        -20 flat Pet Power (instant, no duration)
Mộc Yêu     Root          6 matches     2        100        -30% Pet ATK debuff for 2 Turns
Sơn Thạch Vệ Earthquake   5 matches     0        150        None
Kim Lôi Vương Thunder Strike 5 matches   0        180        None
```

These are **MVP base configuration** — not universal balance invariants. The
project owner approved these values. They are configuration defaults used at
battle creation; they do not represent formulas or scaling rules.

#### 6.3.1 Boss Skill Effect Magnitudes & Duration Semantics

1. **Flame Burst (Hỏa Long):**
   - **Base Damage:** 150 (deals damage through Damage Pipeline to active Pet).
   - **Secondary Effect (Burn):** Applies Burn status effect to the active Pet.
   - **Burn Magnitude:** Fixed 50 damage per tick (does not scale with Boss ATK, Pet ATK, percentage MaxHP, or elemental multipliers).
   - **Burn Duration & Timing:** 2 Turns = exactly 2 End Turn ticks (`GAME_RULES.md` §17 step 19a; `COMBAT_RULES.md` §5.1, §5.2). When applied during Turn $N$ Boss Response (step 18b), Burn tick #1 (50 damage) occurs at Turn $N$ step 19a (End Turn). Burn tick #2 (50 damage) occurs at Turn $N+1$ step 19a (End Turn), after which the Burn expires before Turn $N+2$.
2. **Drain Power (Thủy Ma):**
   - **Base Damage:** 120 (deals damage through Damage Pipeline to active Pet).
   - **Secondary Effect (Drain Power):** Instantly subtracts 20 flat Power from the active Pet (`PetState.Power = max(0, PetState.Power - 20)` per `GAME_RULES.md` §12).
   - **Representation:** Flat Power reduction (not a percentage).
   - **Duration:** None (instant stat reduction, not a persistent status effect).
3. **Root (Mộc Yêu):**
   - **Base Damage:** 100 (deals damage through Damage Pipeline to active Pet).
   - **Secondary Effect (Root):** Applies a -30% Pet ATK debuff to the active Pet (`COMBAT_RULES.md` §5.1 Buff/Debuff).
   - **Representation:** Percentage-based ATK reduction (-30% active Pet ATK).
   - **Root Duration:** 2 Turns using the authoritative Turn model (`MATCH3_RULES.md` §8.1 / `GAME_RULES.md` §17). Root is a Turn-based Buff/Debuff, so its one-Turn-of-duration consumption point is governed by the canonical rule in `COMBAT_RULES.md` §5.3 and is not restated here.
4. **Earthquake (Sơn Thạch Vệ):**
   - **Base Damage:** 150 (deals damage through Damage Pipeline to active Pet).
   - **Secondary Effect:** **None.** The Skill applies no debuff, no status effect, and no resource change.
   - **Duration:** None applies — no secondary effect exists to persist.
   - **Board Effect:** **None.** The Skill performs no board transformation, gem destruction, gem conversion, freeze, lock, or any other board mechanic.
5. **Thunder Strike (Kim Lôi Vương):**
   - **Base Damage:** 180 (deals damage through Damage Pipeline to active Pet).
   - **Secondary Effect:** **None.** The Skill applies no debuff, no status effect, and no resource change.
   - **Duration:** None applies — no secondary effect exists to persist.
   - **Board Effect:** **None.** The Skill performs no board transformation, gem destruction, gem conversion, freeze, lock, or any other board mechanic.

**All five MVP Bosses are now content-defined.** The set is complete at five
(`MVP_SCOPE.md` §1; `ROADMAP.md` §1 Phase 2). Each has an Element, a Passive with
an explicit trigger category from §3.2, a Skill with an explicit timing rule and
effect, and its §6.1 base stats and §6.4 identities recorded above. Every Boss's
Passive trigger category is distinct from the others' (§3.2): Match Count (Hỏa
Long, Mộc Yêu), Battle Start (Thủy Ma), Boss HP (Sơn Thạch Vệ), and Combo
(Kim Lôi Vương). No Boss-specific value remains to be authored — the definitions
in §6, §6.1–§6.4 are the contract a downstream provisioning task transcribes
(`DATABASE.md` §1 note item 5).

### 6.4 Identity Contract (BossId, PassiveId, SkillId)

Canonical string identities for the five content-defined MVP Bosses. These
are the values the Domain `BossDefinition` / `BossState` identity fields carry
and the values emitted on events (`PassiveCharged`/`PassiveTriggered.sourceId`,
`BossSkillCast.sourceId`, `BattleStarted.BossId`). They are fixed here so no
task invents its own.

Three distinct Boss identity concepts exist and are never collapsed
(TASK-046):

- **BossId (canonical technical Identity)** — the stable machine-readable
  game-level Boss ID. Owned by this section. Used by `BossState.BossId`
  (`GAME_STATE.md` §2.4), `BossDefinition.Identity` (`DATABASE.md` §1),
  event `sourceId` when `source = "boss"` (`GAME_EVENTS.md` §2,
  `SIGNALR_PROTOCOL.md` §3.2.16–§3.2.18), and `POST /api/battle/start`'s
  `bossId` (`API_CONTRACTS.md` §3).
- **Display name** — the human-readable content name (this document's §6
  reference tables). Presentation only; never a technical identifier in
  state, events, persistence, or the API.
- **`BossDefinitionId`** — the persistence primary key of the
  `BossDefinition` row (`DATABASE.md` §1). Owned there, not here.

Naming convention for BossId (contract-level): `boss-<ascii-kebab-case-name>`
— ASCII only, lowercase, kebab-case, stable, no Vietnamese diacritics, no
display/localization text, no spaces, no runtime-generated or
runtime-slugified identifiers.

```text
Boss        BossId (BossState.BossId)    Display Name   PassiveId                     SkillId
---------   ---------------------------  -------------  ----------------------------  -------------
Hỏa Long    "boss-hoa-long"              "Hỏa Long"     "boss-hoa-long-rage"         "flame-burst"
Thủy Ma     "boss-thuy-ma"               "Thủy Ma"      "boss-thuy-ma-heal"          "drain-power"
Mộc Yêu     "boss-moc-yeu"               "Mộc Yêu"      "boss-moc-yeu-regen"         "root"
Sơn Thạch Vệ "boss-son-thach-ve"          "Sơn Thạch Vệ" "son-thach-ve-enrage"        "earthquake"
Kim Lôi Vương "boss-kim-loi-vuong"        "Kim Lôi Vương" "kim-loi-vuong-combo"       "thunder-strike"
```

- **BossId** is the canonical technical Identity — machine-readable, never
  the display name. All five content-defined MVP Bosses have a recorded
  technical Identity (`"boss-hoa-long"`, `"boss-thuy-ma"`, `"boss-moc-yeu"`,
  `"boss-son-thach-ve"`, `"boss-kim-loi-vuong"`), following the convention
  above; neither the display name nor any runtime-derived slug is used as a
  technical identifier.
- **Display name** is content/presentation only — the human-readable name in
  this document's §6 reference tables. It is never used as a technical
  identifier in state, events, persistence, or the API.
- **`BossDefinitionId`** is the stable persistence/database identity of the
  `BossDefinition` row (`DATABASE.md` §1) — a persistence key, distinct from
  both the canonical technical Identity above and the display name.
- **PassiveId** identifies the Boss Passive in `PassiveCharged`/
  `PassiveTriggered` payloads (`source = "boss"`). Values follow the
  kebab-case pattern of Pet PassiveIds (e.g. `PassiveId("xich-lang")`).
  This section states no more specific value form for a Boss's `PassiveId` or
  `SkillId` than it states for the five entries above; the two newest entries
  are the values the Product Owner fixed, recorded verbatim and not normalized
  to the earlier rows' observed spellings.
- **SkillId** identifies the Boss Skill in `BossSkillCast.skillId`
  (`SIGNALR_PROTOCOL.md` §3.2.18).
- Examples in `SIGNALR_PROTOCOL.md` §3.2.16–§3.2.18 use these exact values.
- Source and tests carry these exact values — the three Domain Boss
  definitions authored before this contract's completion use these canonical
  Identities and emit them as `sourceId`, so source code, tests, and this
  contract are aligned (TASK-047). The two newest rows are **content-defined
  here only**: their Domain `BossDefinitions` entries and `BossDefinition` rows
  are not yet authored or provisioned, so no source or test value exists for
  them yet (`DATABASE.md` §1 note item 5).

---

# 7. Events

```text
PassiveCharged     emitted when Boss Passive progress increments
                   (shared event with Pet Passive — GAME_EVENTS.md §2,
                    SIGNALR_PROTOCOL.md §3.2.16; source = "boss")

PassiveTriggered   emitted when Boss Passive threshold is crossed
                   (shared event with Pet Passive — GAME_EVENTS.md §2,
                    SIGNALR_PROTOCOL.md §3.2.17; source = "boss")

BossSkillCast      emitted when a Boss Skill resolves
                   (SIGNALR_PROTOCOL.md §3.2.18)

BattleWon          emitted when Boss HP reaches 0
BattleLost         emitted when the active Pet's HP reaches 0
                   (SIGNALR_PROTOCOL.md §3.2.19)
```

Boss Passive triggers use the general Passive events (`PASSIVE_RULES.md` §7)
with `source = "boss"` to distinguish from Pet Passives. No Boss-specific
passive event name is needed.

Boss state changes (Idle → Enraged, Idle → Stunned) are inferable from the
sequence of `DamageDealt`, `DamageTaken`, `BossSkillCast`, and `PassiveTriggered`
events — no dedicated `BossStateChanged` event exists.

---

# 8. Server Authority

All Boss HP, State, Passive progress and Skill resolution are server
authoritative (GAME_RULES.md §15.5, §18). The client never determines when a
Boss Skill fires or what it does — it only renders the resulting events.
