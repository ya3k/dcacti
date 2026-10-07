# TASK-212 — Post-TASK-211 Product & Gameplay Gap Audit

```text
Task ID:            TASK-212
Type:               AUDIT (evidence-based product/gameplay gap classification;
                    no gameplay, UI, backend, contract, or documentation change)
Status:             DONE
Risk:               NONE (read-only audit; zero production/test/docs file modified)
Priority:           HIGH (determines the next implementation task)
Primary Agent:      review / product-audit
Evidence base:      current working tree (src/, tests/, docs/, tasks/, scripts/)
                    + a live browser E2E run against the real stack
Model:              DeepSeek Harness agent
```

**Scope discipline.** This task audits only. No production code, test, migration,
contract, ADR, or `docs/` file was modified. No temporary instrumentation was
added. The single file created is this record. TASK-207 / TASK-208 / TASK-208A /
TASK-210 / TASK-211 are treated as shipped baseline; where this audit disagrees
with a recorded claim, the disagreement is **reported** (`AGENTS.md` §16) and the
completed record is **not** edited (`tasks/TASK_LIFECYCLE.md` §3).

**No implementation is performed here.** Every task below is a proposal with a
reserved or new ID, not authorization. TASK-212 … TASK-217 do not exist as files;
they are *proposals* inside `TASK-207-product-roadmap-and-gameplay-gap-audit.md`
§G (`TASK-207:378-487`, "Each entry is a proposal, not an authorization").

**Audit-local gap IDs.** Gaps below use an audit-local `A-nn` prefix. Prior IDs
are cross-referenced explicitly (`G1…G15` = TASK-207; `U1…U15` = TASK-207;
`NG-01…NG-23` = TASK-211 audit) so nothing is silently renumbered.

---

## 0. Verification Performed (audit evidence base)

```text
git status --porcelain                 26 modified tracked files + 6 untracked
                                       task records — byte-identical before and
                                       after this audit (verified twice)
git diff --check                       PASS (exit 0)
npx tsc --noEmit (client)              PASS (exit 0, no output)
npx vitest run  (client)               PASS — 20 files / 817 tests
Live stack preconditions               PostgreSQL listening on 5433 and Redis on
                                       6379 were already up; no Docker CLI on PATH
Started for this audit                 backend  `dotnet run --project
                                       src/backend/GameServer.Api/GameServer.Api.csproj`
                                       (ASPNETCORE_ENVIRONMENT=Development,
                                       /health -> "Healthy" at :5000)
                                       client   `npm run dev` (200 at :5173)
Browser E2E  npm run verify:e2e:smoke   PASS — RUN 1: 132 checks / 0 failures;
                                       RUN 2: 132 checks / 0 failures;
                                       "ALL SMOKE TEST RUNS PASSED CLEANLY"
                                       (Microsoft Edge headless, real backend +
                                       PostgreSQL + Redis + SignalR, two real
                                       battles played to their own terminal
                                       resolution: 10 swaps / 7 s and 11 swaps / 8 s)
Post-run cleanup                       both dev servers stopped; 0 listening
                                       sockets on 5000/5173; 0 dotnet processes
Artifacts written                      only the gitignored `**/*-shots/` PNGs
                                       (`.gitignore:19`); no tracked file touched
```

**Backend unit suites (`dotnet test`, 2 919 tests) were NOT re-run** by this
audit: no backend file was changed by TASK-211 or by this audit, and TASK-211's
record reports them green at this tree. Backend claims below are verified by
**direct source and test reading**, not by execution.

**Limitations.** (a) The E2E exercises the **Vite dev server**, so
`import.meta.env.DEV` is true and the DEV-gated overlays are hidden only by CSS
for the player-facing frame — the production-build absence of the overlays is
source-verified only. (b) The E2E's real battles use a mechanical swap policy, not
a skilled player, so its VICTORY/DEFEAT outcomes are not balance evidence. (c) No
EF/SQL execution was performed for the TASK-216 reassessment; that conclusion is a
reading of EF Core 10.0.12 semantics plus an existing test.

**Method.** Every headline claim was checked against source, tests, docs or the
browser. Findings were gathered by direct reads plus five parallel read-only
inspections performed *within* this audit (cards/relics/pet-skills; player-loop
scenes; documentation drift; progression/XP; content reachability). Items this
audit could not independently re-verify are marked `UNVERIFIED`.

---

## 1. Executive Summary

1. **The product is functionally complete, presentation-mature, and green on every
   automated surface.** Auth → Main Menu → Lobby → Battle → Result →
   Collection / Battle History → replay runs end-to-end against the real server;
   the live browser E2E passes **132 checks × 2 runs** with zero uncaught
   exceptions, zero fatal console errors and zero unexpected API responses; 817
   client tests and `tsc --noEmit` are clean. Maturity is now
   **vertical-slice-complete, presentation-mature, decision-poor**.
2. **TASK-211's ten implemented corrections are all verified present**, not merely
   claimed: the Lobby has a real `< BACK` and a scoped `RETRY`, the shared
   transport reads the `API_CONTRACTS.md` §6 envelope so the server's own
   `message` reaches the player, `ResultScene` has explicit loading / success /
   failure reward states with a run-guarded retry, `durationTurns` renders, and
   `Final Pet HP:` no longer mislabels the Pet's HP as the player's.
   `StatusOverlay` is DEV-gated. Confirmed by reading plus E2E checks
   (`phase3c.*`, `phase4b.*`, `phase7b.*`, `phase8.*`, `phase10.*`).
3. **The battle's *mechanics* are decision-complete; the battle's *decisions* are
   not.** Card casting is fully implemented server-side — eight card definitions,
   `EffectiveCardCost` composition, an affordability check, six effect types,
   one-cast-per-committed-Turn, four documented rejection codes. But the player is
   asked to spend a resource whose prices it cannot see, because
   `SIGNALR_PROTOCOL.md` §4 item 15 forbids the client to compute cost,
   affordability or legality *and* the modifier collection the price depends on is
   deliberately undelivered. Verdict: **functional, and mechanically
   decision-complete — but missing the *informed* decision layer** that
   `GDD.md:218-222` ("the player continuously decides between spending Power for
   survival now or saving it") and `GDD.md:303-306` require.
4. **Build diversity — `GDD.md:61-62` pillar 2 — is not exercisable at all.** The
   starter grant is exactly 1 Pet / 3 Basic Cards / 3 Relics
   (`PlayerStarterGrantFactory.cs:56,69-74,90-95`) against an exactly-3-card and
   3–5-relic requirement, and **no code path anywhere creates an ownership row
   outside player creation**: the `AddAsync` / `AddUnlockAsync` interfaces have
   zero production callers, there is no unlock/purchase/drop/quest endpoint among
   the nine documented routes, and no migration grants rows. Of 28 provisioned
   definitions, **7 are ownable and 15 are permanently unreachable** while
   `MVP_SCOPE.md:51,62,67` presents 5 Pets / 5 Pet Skill Cards / ~10 Relics as IN.
   The only genuinely free loadout decision is which of 5 Bosses to fight.
5. **A second documented MVP system is inert by Product Owner decision.** The Pet
   Passive is fully implemented *except its effect*: charge, threshold, reset
   behaviour and both events land, no Pet passive effect is ever applied (grep for
   `PassiveEffect|ApplyPassive|PetPassiveResolver` → no matches), the five
   `passive-*` ids exist only in provisioning migrations, and the effect
   magnitudes are authored nowhere. `TASK-191` Q-11 → `TASK-196` → `TASK-198` →
   `TASK-200` Q-11 decided "REMAIN UNIMPLEMENTED FOR MVP". So the player watches
   `Passive: passive-xich-lang  0 / 5 Matches` fill and nothing happens — while
   `PASSIVE_RULES.md:216-224` defines all five effects and `MVP_SCOPE.md:52`
   lists "Pet Element, Passive, Signature Skill" as IN. **TASK-207 rated this P1
   (G8) and TASK-211's audit dropped it without resolving it.**
6. **The most severe *unaddressed* defect is on the battle's primary interaction
   surface, and it is worse than any prior record states.** Because
   `GET /api/cards` never returns a `PetSkill` row (`API_CONTRACTS.md:783-786`;
   membership *is* the unlocked set), `BattleScene` cannot resolve the delivered
   `petState.equippedCards` fourth entry, so in every real battle the Signature
   Skill tile renders `Card: card-inferno` **and its click dispatches
   `CardCast(card-inferno)` instead of `PetSkillCast()`**. The live E2E observes
   exactly this: `phase6d.signatureSkillControlRemainsUsable —
   CardCast card-inferno: rejected (INSUFFICIENT_POWER)`. It only works because
   `CardCastExecutor.cs:56` accepts `PetSkill` cards, which contradicts
   `BattleHub.cs:1108`'s own "one requested **Basic** Card cast". Two defects mask
   each other; the client tests inject the missing definition
   (`SceneLifecycle.test.ts:312`) and therefore never see it.
7. **TASK-216's subject does not exist.** The reported Player-XP persistence
   ordering risk is **REFUTED by reading**: `PlayerRepository.SaveProgressionAsync`
   guards on `_dbContext.Entry(player)` (`:197`), and EF Core 10.0.12's
   `DbContext.Entry` runs single-entity change detection (`TryDetectChanges`), so
   the mutated Player is `Modified`, not `Unchanged`, and `SaveChangesAsync`
   (`:220`) runs. An existing test
   (`PlayerProgressionPersistenceTests.cs:54-90`) already asserts the 100 XP / Level
   2 round-trip on exactly that path and would fail if the guard fired. The real,
   *different* exposure on that path is that the result row, the Player grant and
   the Pet grant are **three separate `SaveChangesAsync` calls with no explicit
   transaction**, so a failure between them leaves the persisted `RewardSummary`
   claiming XP the rows do not hold — and no test asserts the composed path's
   Player XP.
8. **The remaining backlog is almost entirely decision-gated, and the
   documentation drift is larger and more damaging than "P3".**
   `ARCHITECTURE.md:641-665` names eight components that **do not exist**
   (`BattleResolutionService`, `Match3Engine`, `ElementResolver`,
   `RelicTriggerEngine`, `BossController`, `BattleEventBus`,
   `PersistenceRepository`, `DiscordService`) and its §1 directory trees do not
   match `src/`; `SIGNALR_PROTOCOL.md:826,848-852` still calls `otherModifiers` a
   "pass-through `1.00` in MVP" while the code rolls crit and folds it in
   (`DamagePipeline.cs:424-437`) — a genuine §4 conflict. TASK-217 as proposed
   (P3, "documentation and cross-reference reconciliation") is under-scoped and
   risks double-booking items already owned by TASK-213/TASK-215.
9. **New, previously unreported gaps** (all evidence-backed, listed in §3):
   `CardCast` accepting `PetSkill` cards (A-02); `CollectionViewerScene` having
   **no run guard** although TASK-211 added one to the Lobby, Result and History
   (A-12); Boss status effects undelivered, so a Card's Burn on the Boss is
   invisible (A-08); `BattleScene` having no exit, so a battle whose outcome never
   arrives strands the player (A-16); the `A-13` cluster of player-facing
   technical strings (a transport name, a protocol citation, raw machine codes as
   rejection reasons, raw battle/card/pet/relic ids, and a `String(error)`
   fallback that survives in three of four `describeError` copies); and the
   duplicate task ID TASK-211 (two files) against `tasks/README.md:95`.
10. **Recommendation: TASK-212 must be SPLIT, and its descriptive half built
    next.** The roadmap's next item conflates two different contract owners and
    two different value classes: exposing *what a Card or Relic does* is an
    additive `API_CONTRACTS.md` §5.3/§5.4 read-contract decision on two GET
    responses; exposing *what a cast costs right now* needs a
    `SIGNALR_PROTOCOL.md` §4 realtime projection decision whose motivating case
    (a `CardCost` modifier) is unreachable because Emergency Core is provisioned
    but never granted and has no acquisition path. The first restores meaning to
    the screen the player passes through before every battle and to the in-battle
    cast decision; the second cannot pay off until content is reachable. All
    higher-impact items than either half are decision-gated, so the split's first
    half is the correct next task — with the decision chain (TASK-213 first) run
    alongside it.

---

## 2. Verified Completed Areas

Only claims re-verified against the current repository or the live browser run.
Line numbers are the current working-tree lines.

### 2.1 The TASK-207 → TASK-208 → TASK-208A → TASK-209 → TASK-210 chain

| Claim | Verdict | Evidence |
|---|---|---|
| The four authorized projection members are delivered: `petState.hp` / `maxHp` / `power`, `bossState.bossId` | IMPLEMENTED | `SIGNALR_PROTOCOL.md` §4.3 item 15 / §4.4 item 10; live E2E `phase5b.hudPetHpIsVisible` (`"1000 / 1000"`), `phase5b.hudPetPowerIsVisible` (`"0 / 100"`), `phase5b.hudBossIdentityIsCorrect` (`boss-kim-loi-vuong` → `Kim Lôi Vương`) |
| Both HUDs are player-facing, no developer readout | IMPLEMENTED | live screenshot `smoke-shots/run2-06b-battle-hud.png`: `BOSS Kim Lôi Vương 2800 / 2800`, `PET Xích Lang 1000 / 1000`, `POWER 0 / 100`, `MATCHES 0`, the passive block, `Effects: none`, four cast tiles; and E2E `phase5b.noDeveloperDiagnosticsInTheHud` / `phase5b.rawEventLogIsNotRendered` (`presentedEventCount: 0`) |
| Damage floaters are anchored on the delivered party that took the hit | IMPLEMENTED | E2E `phase5c.damageFloatersAreAnchoredOnThePartyThatTookTheHit` (boss floater x 147 vs boss gauge centre 161; pet floater x 122 vs pet centre 136) |
| The presentation never mutates authoritative state | IMPLEMENTED | E2E `phase5c.feedbackMutatesNoAuthoritativeState` (identical before/after snapshot) |
| Reconnect/resync converges on authoritative state with no replay | IMPLEMENTED | E2E `phase6c.transportReconnected`, `phase6c.resyncCarriedHpAndPowerAsState_NoReplay` |
| No gameplay HUD claim in `ARCHITECTURE.md` is stale | **STILL STALE** | `ARCHITECTURE.md:593-594` "No gameplay HUD exists yet"; mirrored in code at `GameViewport.ts:25` |

### 2.2 Non-battle screens after TASK-211 (all ten items re-verified)

| TASK-211 claim | Verdict | Evidence |
|---|---|---|
| Lobby `< BACK` in every state → `MainMenuScene` only | IMPLEMENTED | `LobbyScene.ts:1091,1140-1142,632`; E2E `phase3c.lobbyOffersAnExitAndTheStart`, `phase3c.backReturnedToMainMenu`; `LobbyScene.test.ts:1723,1748,1759,1770` |
| Lobby `RETRY` scoped to the failed operation, inert while in flight | IMPLEMENTED | `LobbyScene.ts:1097-1099,594-607`; E2E `phase4b.retryRepeatsTheStartOperation` |
| The §6 error envelope is read and the server `message` reaches the player; no status/path/machine code | IMPLEMENTED | `ApiService.ts:75-93,106-120`; E2E `phase4b.rejectedStartIsReadable` = `Battle start failed: A Pet must be selected and it must be owned by the player.` and `phase4b.serverAnsweredWithTheDocumentedEnvelope` reading `{"error":"PET_NOT_OWNED","message":"…"}` off the wire; `LobbyScene.test.ts:1038-1087` |
| Result reward block has explicit loading / success / failure + run-guarded retry | IMPLEMENTED | `ResultScene.ts:43,46,52,58,563-589,598-620,644-655,680-733`; E2E `phase7b.*` (7 checks) incl. `phase7b.rewardFailureIsNotAZeroReward` |
| `durationTurns` renders from the delivered result | IMPLEMENTED | `ResultScene.ts:568-570,730`; E2E `phase10.durationIsRenderedFromTheDeliveredResult` (`"Duration: 10 turns"` == delivered 10) |
| `Final Player HP:` relabelled to the Pet | IMPLEMENTED | `ResultScene.ts:524,552`; E2E `phase10.terminalPetHpIsLabelledAndVerbatim` (`"Final Pet HP: 0"`); wire member untouched |
| Lobby has a real async run guard and a single transition claim | IMPLEMENTED | `LobbyScene.ts:352,428,487,630,851,868`; `SceneLifecycle.test.ts:4703` (settles-after-SHUTDOWN navigates nowhere); E2E `phase3c.stoppedLobbyRanItsTeardown` |
| Duplicate START protection; START blocked while loading | IMPLEMENTED | `LobbyScene.ts:790` (`startPending \|\| loading \|\| hasTransitioned`); `LobbyScene.test.ts:1908,1956,1987` |
| `StatusOverlay` no longer unconditional in production | IMPLEMENTED (source-verified) | `App.tsx:139-140` DEV-gates both overlays; note the gate is **not** test-pinned and the E2E runs the dev server |
| Post-result lifecycle (PLAY AGAIN with the preserved editable loadout; MAIN MENU; battle state cleared) | IMPLEMENTED | E2E `phase8.preservedLoadoutRestored`, `phase8.preservedLoadoutIsEditable`, `phase8.battle2RequestCarriesEditedLoadout`, `phase8.mainMenuClearedActiveBattleState` |

### 2.3 Corrections to prior records (reported, not edited)

| Prior claim | Correction | Evidence |
|---|---|---|
| TASK-210 §Remaining Issue 2: "`RelicTriggered` has no player-facing presentation … the battle scene has no Relic definition source" — framed as a contract gap | **Misclassified.** It is implementation-only: `GET /api/relics` already maps instance identity → definition `name` (`API_CONTRACTS.md:802-806`) and `GameRuntimePort.getRelics()` already exists. `BattleScene` simply never calls it. | `BattleEventWireProjection.cs:553-556,767-768`; `CollectionResponses.cs:131-135`; `GameRuntimeEvents.ts:534-538`; `BattleScene.ts` has **zero** `Relic` matches |
| TASK-207 §J-3 / TASK-208 §J-3 / TASK-211 NG-22: Player-XP persistence is ordering-dependent | **REFUTED.** EF Core 10.0.12 `DbContext.Entry` performs single-entity change detection, so the guard's `Unchanged` condition is not met after `GrantBattleXp`. An existing test already asserts the round-trip on that exact path. | `PlayerRepository.cs:197,215-220`; `GameServer.Infrastructure.csproj:12` (EF `10.0.12`); `PlayerProgressionPersistenceTests.cs:54-90` |
| TASK-207 §I: `API_CONTRACTS.md` "§2.8" session pointer at "seven sites" | **Imprecise.** 6 self-citations in `API_CONTRACTS.md` (4 live at `:515,532,625,679`; 2 in the version preamble at `:55,72`) plus 5 external pointers (`SIGNALR_PROTOCOL.md:122`; `DATABASE.md:1128`; `ADR-015:31,73,107,269`) = **11 repo-wide**. The session section is §2.3. | `API_CONTRACTS.md:241` is `## 2.3 Application Session Mechanism`; no §2.4–§2.8 headings exist |
| TASK-207 §I: 5 Pets / 5 Bosses / 3 Basic Cards / 5 Pet Skill Cards / ~10 Relics "IMPLEMENTED (content provisioned)" | **Correct, and now precisely counted:** 5 Pets, 8 Cards (3 Basic + 5 PetSkill), 10 Relics, 5 Bosses = 28 provisioned definitions; **7 ownable**. | `DATABASE.md:9-10`; migrations `20260929152651`, `20261004055006`, `20261004153916`, `20260926151112`, `20261004153916`; `PetCardRelicDefinitionPostgresProvisioningTests.cs:184,200,215` |
| TASK-211 audit §3 implicitly treated the gap list as complete | **It dropped TASK-207 G8** (Pet passive effect, rated P1, player 4 / game 4 / MVP 4) without resolving it. Reinstated here as A-06. | `TASK-207:209,357`; no NG-xx covers it; `TASK-200:57` Q-11 |

---

## 3. Remaining Gaps

Severity: **P0** blocks the core playable loop · **P1** major player-facing defect
or documented-intent violation on the happy path · **P2** important polish /
progression gap · **P3** nice-to-have.
Size: **S** ≤ 1 session · **M** 2–4 · **L** 5+.
Contract impact: `NONE` · `IMPLEMENTATION-ONLY` · `DOCUMENTATION-ONLY` ·
`DECISION-REQUIRED`.

### A-01 — The Signature Skill control is mislabelled *and* dispatches the wrong action

```text
Gap                     The fourth cast control is labelled `Card: card-inferno`
                        instead of `Skill: Inferno`, and clicking it sends
                        `CardCast(card-inferno)` instead of `PetSkillCast()`.
Current behavior        BattleScene resolves `petState.equippedCards[i]` through a
                        map built from `runtime.getCards()`. `GET /api/cards`
                        serves `PlayerUnlockedCard` rows and a `Category =
                        PetSkill` card is never one (`CARD_RULES.md` §1 item 4,
                        ADR-012 item 9), so the fourth definition is always
                        absent: `isPetSkill = false` -> label `Card: <raw id>` and
                        the click handler routes to `submitCardCast(cardId)`.
Evidence                BattleScene.ts:1230-1233,1253-1259 (label + dispatch);
                        API_CONTRACTS.md:783-786 (powerCost etc. not exposed;
                        membership IS the unlocked set); CardCastExecutor.cs:56
                        (accepts Basic *and* PetSkill); BattleHub.cs:1108
                        ("one requested Basic Card cast"); live E2E
                        phase6d.signatureSkillControlRemainsUsable =
                        "CardCast card-inferno: rejected (INSUFFICIENT_POWER)";
                        masked in tests because the harness injects the
                        definition (SceneLifecycle.test.ts:312, 3132, 3141).
Player impact           In every battle, on the primary interaction surface, the
                        Pet's Signature Skill is presented as a Card with a raw
                        developer id, and the action the client requests is not
                        the documented action for that button. It works today only
                        because the server's CardCast validation is broader than
                        its own documentation — so the two defects hide each
                        other. Any tightening of the server boundary, or any
                        future per-action bookkeeping, breaks the control.
Priority                P1
Effort                  S for the display fix once the read source exists; M with
                        the decision (decision record + doc amendment + client
                        change + the A-02 coordination)
Contract impact         DECISION-REQUIRED — the client has no name/category source
                        for the derived Signature Skill at all. Owner:
                        API_CONTRACTS.md §5.1 (derive it into the Pet read) or §3
                        (carry it in the battle-start response); NOT §5.3's
                        membership semantics, which would make a PetSkill card an
                        unlock row and contradict CARD_RULES.md §1 item 4 /
                        ADR-012 item 9. Inventing a client-side catalog would be a
                        second source of truth (AGENTS.md §7/§9).
Recommended action      Keep this as its own decision-then-implementation task
                        (TASK-219, TASK-211 audit §6 #4). It must NOT be absorbed
                        into TASK-212. Its scope must now include the dispatch,
                        not only the label.
```

### A-02 — `CardCast` accepts `PetSkill` cards, contradicting its own documented contract

```text
Gap                     The hub documents `CardCast` as "one requested Basic Card
                        cast", but the domain executor accepts any category.
Current behavior        `CardCastExecutor.cs:56-59` rejects only when the category
                        is neither Basic nor PetSkill; a PetSkill card passed to
                        `CardCast` is executed identically to `PetSkillCast` and
                        emits both `CardCast` and `PetSkillCast`.
Evidence                BattleHub.cs:1108 (doc comment) and :1118-1146;
                        CardCastExecutor.cs:56-59,120-123;
                        SIGNALR_PROTOCOL.md §2 defines CardCast and PetSkillCast as
                        distinct client->server methods with distinct argument
                        lists (§3.2.20/§3.2.21); live E2E phase6d proves the
                        client route is reachable in normal play.
Player impact           None directly today (one cast per committed Turn is
                        enforced globally by CardCastsUsedThisTurn, so there is no
                        duplication or economy exploit). It is a contract-
                        conformance and robustness defect: the documented boundary
                        is not the enforced boundary, and it is precisely what
                        keeps A-01 invisible.
Priority                P2
Effort                  S
Contract impact         IMPLEMENTATION-ONLY, but the choice must be coordinated
                        with A-01: either reject a non-Basic `CardCast` (and rely
                        on A-01's fix to keep the control working), or record that
                        `CardCast` is category-agnostic and correct the hub text.
                        Do NOT change the wire methods themselves.
Recommended action      Fold the coordination into TASK-219; report the choice as
                        part of that task's decision record.
```

### A-03 — Card and Relic effects are invisible at the decision point (the TASK-212 subject)

```text
Gap                     The Lobby asks the player to equip three Cards and three
                        Relics it cannot describe, and the Collection viewer can
                        describe neither.
Current behavior        `GET /api/cards` returns `cardId, name, category` only;
                        `powerCost`, `loadoutCopyLimit`, `effectDefinition` and
                        `playerId` are deliberately excluded. `GET /api/relics`
                        returns `relicId, name` only; `definitionId` and the
                        definition's `Trigger`/`Condition`/`EffectDefinition` are
                        excluded. Lobby rows therefore read
                        `● Heal  [Basic]  slot 1` and `● Berserker Core  slot 1`;
                        the Collection detail panel prints the wire members
                        verbatim, i.e. `Card ID:` / `Relic ID:` and raw ids.
Evidence                API_CONTRACTS.md:761-786, :788-811; CollectionResponses.cs
                        (three- and two-member responses); CollectionModels.ts
                        (no powerCost anywhere in the client);
                        LobbyScene.ts:1249-1261,1275-1288;
                        CollectionViewerScene.ts:663-686,765-779;
                        CARD_RULES.md:126-138, 369-400 (the effects exist and are
                        authored); RELIC_RULES.md:694-707, 1012-1020 (Trigger /
                        Condition / Effect exist and are authored)
Player impact           GDD.md:303-306 ("the player should always understand …
                        what each Card/Relic changes") and GDD.md:61-62 pillar 2
                        are unachievable. The player cannot learn that Heal is
                        20 % Max HP for 20 Power while Shield is a 20 % pool for
                        20 Power, or that Power Charge is free, or what the three
                        equipped Relics actually do. This is a happy-path,
                        every-session gap.
Priority                P1
Effort                  M (decision record + additive read-contract amendment +
                        Lobby and Collection rendering + tests)
Contract impact         DECISION-REQUIRED — but only the *additive read* half.
                        Owner: API_CONTRACTS.md §5.3/§5.4. Unlike A-04 this needs
                        no realtime projection, no state member, no event and no
                        runtime change.
Recommended action      This is the descriptive half of the split TASK-212
                        (TASK-212A) and the recommended next task (§5).
```

### A-04 — In-battle cost and affordability cannot be shown, and the client is forbidden to derive them

```text
Gap                     The cast tiles show no Power cost and no affordable /
                        unaffordable state, so the Power spend-versus-save
                        decision (GDD.md:218-222) is blind.
Current behavior        `BattleScene.renderCastControls` draws a rectangle and a
                        label per equipped card; every tile is unconditionally
                        `.setInteractive({ useHandCursor: true })`, and the only
                        gate is `isInputLocked()` (presentation lock / swap in
                        flight / action in flight). No cost is rendered. The
                        client *does* receive `petState.power` and renders a
                        `0–100` gauge from it.
Evidence                BattleScene.ts:1076-1077 (Power is known),
                        1211-1266,1573,1605,2044-2045;
                        SIGNALR_PROTOCOL.md §4 item 15 — "`power` authorizes no
                        client-side cost, affordability, or legality computation",
                        "it must never compute, predict, or reconstruct a
                        Card-cost modifier from events, from a `PowerChanged`
                        delta, or from a Card's definition", "Delivering it would
                        be a protocol change owned by its own task";
                        PetState.cs:637 `CardCostModifiers[]` is authoritative
                        Battle state and is NOT delivered (SIGNALR_PROTOCOL.md
                        §4.3 item 2 / §4 item 15); CARD_RULES.md:212-351
                        (EffectiveCardCost = truncate(PowerCost × (100 −
                        TotalReduction)/100), so the base cost alone is not the
                        price); RuntimeBoundaries.test.ts:452-477 forbids
                        `EffectiveCardCost`, `CardCostModifier`, `IsAffordable`
                        in client source
Player impact           The player gambles Power. It cannot tell whether Heal is
                        affordable, and it cannot learn the 20 / 20 / 0 / 80–100
                        cost ladder. Rejections do arrive as
                        `rejected (INSUFFICIENT_POWER)`, so the information is
                        reachable only by trial.
Priority                P2 — the *motivation* is currently unreachable: the only
                        provisioned `CardCost` source is Emergency Core, which is
                        "provisioned but not selected" in the starter set
                        (PlayerStarterGrantFactory.cs:81-84; TASK-084 Q2), so
                        `CardCostModifiers[]` is empty in every battle a real
                        account can play and base cost would currently be
                        accurate by accident. The contract must still be correct.
Effort                  S–M (decision) + S–M (implementation) once unblocked
Contract impact         DECISION-REQUIRED — a SIGNALR_PROTOCOL.md §4 realtime
                        projection decision (deliver `CardCostModifiers[]`, or a
                        per-card server-computed effective cost, or nothing).
                        API_CONTRACTS §5.3 alone is NOT sufficient, which is the
                        central correction to the roadmap's TASK-212 wording.
Recommended action      TASK-212B. Defer behind A-05/TASK-213: authorizing a
                        protocol member whose only consumer is unreachable content
                        would put the wire contract ahead of the product loop.
```

### A-05 — Build diversity is not exercisable, and 15 of 28 provisioned definitions are unreachable

```text
Gap                     Three of the Lobby's four decisions are forced; ownership
                        can never grow; MVP_SCOPE §1 presents unreachable content
                        as IN.
Current behavior        Starter grant = 1 Pet (pet-xich-lang), all 3 Basic Cards,
                        3 Relics (Berserker Core, Mana Crystal, Assassin Eye).
                        Requirements: exactly 1 Pet, exactly 3 Basic Cards,
                        3–5 Relics, exactly 1 Boss. There is exactly one
                        production ownership-write path, and it runs only on the
                        new-Player branch of account creation.
Evidence                PlayerStarterGrantFactory.cs:56,69-74,90-95,141-242;
                        PlayerRepository.cs:125-169 (sole writer, called only from
                        AuthController.cs:99-104,143-148);
                        IPetRepository.AddAsync / IRelicRepository.AddAsync /
                        ICardRepository.AddUnlockAsync have ZERO production
                        callers (only 4 test doubles); no unlock/purchase/drop/
                        claim/equip route among the nine documented endpoints;
                        LobbyScene.ts:97-99,705-709,734-738,936-950;
                        CardLoadoutService.cs:222 (Card order carries no gameplay
                        significance); RELIC_RULES.md §2.3 (Relic order IS slot
                        order, so it is the one remaining knob); DATABASE.md:
                        1320-1338 ("MVP bootstrap / test content", "must NOT
                        evaluate conditional top-ups"); MVP_SCOPE.md:51,62,67 vs
                        the 7 ownable rows; PET_RULES.md:79-80 ("may own an
                        arbitrary number of Pets") is unachievable
Player impact           GDD §1.3 pillar 2 is dead: the player picks a Boss and a
                        Relic permutation. The Collection is a static 1/3/3
                        display that can never change. 4 Pets, 4 Pet Skill Cards
                        and 7 Relics are permanently unreachable.
Priority                P1 (product loop)
Effort                  S (decision) — an acquisition implementation would be a
                        separate, larger task
Contract impact         DECISION-REQUIRED (MVP_SCOPE.md §1 vs DATABASE.md §2; any
                        acquisition mechanism would be a Rule Change and may be
                        FUTURE by MVP_SCOPE §4)
Recommended action      Keep as TASK-213, and answer it before A-04 and before any
                        further content-facing work. The code already answers the
                        question factually: it is starter-only ownership, and the
                        5-Pet / 10-Relic content exists as inert definition rows.
```

### A-06 — The Pet Passive effect is inert, and TASK-211's audit lost the finding

```text
Gap                     The passive charges, triggers, resets and emits — and has
                        no effect. The player watches the gauge fill for nothing.
Current behavior        PassiveTracker implements charge / threshold / reset and
                        both events; `BattleStateService` converts the tracker's
                        triggers into events only. No Pet passive effect is applied
                        anywhere, and no magnitude is authored.
Evidence                PassiveTracker.cs:161-280 (charge/threshold/reset);
                        BattleStateService.cs:1204-1208,1217-1220,1239-1240 (events
                        only — contrast the Boss branch at :1585-1780, which does
                        apply effects); grep for PassiveEffect|ApplyPassive|
                        PetPassiveResolver over src/backend -> no matches; the five
                        passive-* ids appear only in provisioning migrations;
                        PassiveTrackerTests.cs:815-831 pins
                        PassiveTriggered_ShouldCarryNoEffectSummaryMember
                        ("the effect summary is DEFERRED to the Combat stage");
                        PASSIVE_RULES.md:214-229 defines all five effects and states
                        the exact magnitudes are balance values held in config;
                        MVP_SCOPE.md:52 lists "Pet Element, Passive, Signature
                        Skill" as IN; live E2E phase5b.passivePresentationIsReadable
                        renders "Passive: passive-xich-lang  0 / 5 Matches
                        (reset: Default)" — note the passive is also shown as a
                        raw id, since no passive name is readable
Player impact           A documented MVP system's payoff never happens. GDD.md:
                        192-194 ("progress accumulates each Match, and reaching a
                        threshold triggers the Passive's effect, then resets") and
                        GDD.md:303-306 ("what their Pet Passive does and when it
                        triggers") are unmet. TASK-207 rated this P1 with player
                        impact 4.
Priority                P1 (as TASK-207 G8) — restored to the backlog because
                        TASK-211's audit dropped it
Effort                  S–M for the decision; M for the implementation
Contract impact         DECISION-REQUIRED, and larger than TASK-215 as currently
                        scoped. TASK-191 Q-11 -> TASK-196 -> TASK-198 -> TASK-200
                        Q-11 decided "REMAIN UNIMPLEMENTED FOR MVP", but
                        PASSIVE_RULES.md §8 (a specific domain rule) defines all
                        five effects and MVP_SCOPE.md §1 lists Passive as IN — the
                        same §4 shape as the Tier/Star conflict, and the magnitudes
                        are authored nowhere (PASSIVE_RULES.md:227-229), so
                        implementation cannot precede the decision.
Recommended action      Fold into TASK-215 (expand it from "Tier/Star" to "the
                        documented-but-inert Pet progression and Passive axes"),
                        and report it per AGENTS.md §4 rather than resolving it.
```

### A-07 — Relic triggers are invisible in battle (implementation-only, verified solvable)

```text
Gap                     `RelicTriggered` reaches the client and is discarded; the
                        player never learns which Relic fired.
Current behavior        The event carries `{ type, relicId }` where `relicId` is
                        the owned Relic *instance* identity. BattleScene parses and
                        formats it into a development-only log that is never drawn,
                        and `describeEventCallout` returns null for it by an
                        explicit design note.
Evidence                BattleEventWireProjection.cs:553-556,767-768;
                        SIGNALR_PROTOCOL.md §3.2.23 ({type, relicId}; no effect
                        summary); BattleEventPresenter.ts:454-463,536-537,624-628;
                        BattleScene.ts:380,1716,1740,2040 (dev log only);
                        BattleScene.ts has zero `Relic` matches and never calls
                        `getRelics()`; GET /api/relics ALREADY returns
                        instance identity -> definition name
                        (API_CONTRACTS.md:802-806; CollectionResponses.cs:131-135);
                        GameRuntimePort.getRelics() exists
                        (GameRuntimeEvents.ts:534-538). All three starter Relics
                        fire regularly in a real battle (OnMatchCount >= 3,
                        OnMatchCount >= 4, OnCombo >= 3) — in the E2E battle the
                        delivered state reached matchCount 4 and combo 4.
Player impact           A build mechanic that fires 2–3 times per battle produces
                        no feedback at all, and the ATK / Crit / Power modifiers
                        those Relics apply are invisible too, so the player's
                        damage and Power change with no attribution. This is the
                        only substantial battle-legibility gap that needs no
                        contract decision.
Priority                P2
Effort                  S
Contract impact         IMPLEMENTATION-ONLY. TASK-210's "contract gap"
                        classification is wrong and is corrected here.
Recommended action      TASK-218: build an instance-id -> name map from the
                        existing `getRelics()` exactly as `loadCardDefinitions()`
                        already does, show the name only (never the effect, never a
                        guessed name — TASK-208 §D), and fall back to the raw id
                        when unresolved.
```

### A-08 — Boss status effects never reach the client, so a Card's Burn on the Boss is invisible

```text
Gap                     The Boss projection is three members; the Boss's
                        StatusEffects[] — Burn, Shield, rage — are not delivered.
Current behavior        `RuntimeBossState` = bossId, hp, maxHp. Burn is applied to
                        the Boss server-side by a card cast and ticked at step
                        19a; the client renders only the resulting HP movement and
                        a damage floater. Boss passives that apply a rage status or
                        regeneration are equally invisible.
Evidence                GameRuntimeEvents.ts:271-302 (three-member boss state and
                        its "deliberately not modelled here" note);
                        SIGNALR_PROTOCOL.md §4.4 item 3 (not delivered);
                        BattleHub.cs:1274-1275; CardCastExecutor.cs:226-245 (Burn
                        applied to the Boss); StatusEffectLifecycle.cs:496-503
                        (step-19a tick). Only the Pet's collection is delivered
                        (BattleHub.cs:1301-1344) and rendered (BattleScene.ts:
                        1090-1092,2196-2224) — as a raw `id` with the magnitude and
                        duration, since no status-effect catalog is readable.
Player impact           The player's own damage-over-time is invisible. A relic
                        that scales burn damage (Burning Curse) is unreachable
                        today, so the immediate impact is Burn's 2 turns on the
                        Boss. Also affects readability of boss mechanic states.
Priority                P2
Effort                  S–M (decision) + S (implementation)
Contract impact         DECISION-REQUIRED — a SIGNALR_PROTOCOL.md §4.4 projection
                        decision, exactly the shape of TASK-208 D-208-03.
Recommended action      New task, after A-05/A-07 and alongside A-04's protocol
                        decision (one protocol decision can cover both).
```

### A-09 — Player Level / XP has no read surface; a fresh account shows `—`

```text
Gap                     Progression is only visible inside a finished battle's
                        reward block or the newest Battle History entry.
Current behavior        Player XP/Level are modelled, awarded (+100 win / +0
                        loss), persisted and returned — but only inside `rewards`
                        of `GET /api/battle/{id}/result` and
                        `GET /api/battle/history`. No progression read endpoint
                        exists among the nine documented routes. Battle History
                        renders `Level N · XP M` from the newest delivered element
                        and `—` on a fresh account, because fabricating a value is
                        forbidden.
Evidence                Player.cs:46,52,83,95,104,141,170,220-232,249-260;
                        BattleResultService.cs:367-375,511-523;
                        PlayerRepository.cs:191-222; API_CONTRACTS.md:126-144 (§1
                        endpoint summary — nine routes, no Player read);
                        BattleHistoryScene.ts:417-418,511-546,532-538;
                        MainMenuScene.ts / App.tsx / StatusOverlay.tsx show no
                        Player XP/Level; Pet XP is returned by NO endpoint
                        (API_CONTRACTS.md:734 excludes `xp`)
Player impact           The retention signal is invisible wherever the player is
                        not reading a past battle; a new account never sees its own
                        Level.
Priority                P2
Effort                  M
Contract impact         DECISION-REQUIRED (a new read endpoint, or a recorded
                        decision that Battle History remains its home)
Recommended action      Keep as TASK-214.
```

### A-10 — TASK-216's subject is refuted; the real exposure on that path is non-atomic rewards

```text
Gap                     The named ordering risk does not exist; a different,
                        smaller exposure does, and the composed path's persistence
                        is unasserted.
Current behavior        `SaveProgressionAsync` guards on
                        `_dbContext.Entry(player)`; in the pinned EF Core version
                        that call performs single-entity change detection, so a
                        mutated Player is `Modified` and `SaveChangesAsync` runs.
                        The three writes on the battle-end path (result row,
                        Player grant, Pet grant) are three separate
                        `SaveChangesAsync` calls with no explicit transaction
                        anywhere in the backend.
Evidence                PlayerRepository.cs:197,199-214,215-218,220;
                        PetRepository.cs:102-134; BattleResultService.cs:
                        352-354,491-493,511-530 ("no second pipeline, service, or
                        transaction architecture is introduced");
                        GameServer.Infrastructure.csproj:12 pins EF 10.0.12;
                        PlayerProgressionPersistenceTests.cs:54-90 already asserts
                        the same-context tracked round-trip (XP 100 / Level 2) and
                        would fail if the guard fired;
                        BattleResultEndpointTests.cs:604-679 composes the real
                        service + repositories and plays a real battle to terminal
                        but asserts only the reward member NAMES, never the Player
                        row's XP; zero matches for BeginTransaction /
                        IDbContextTransaction / TransactionScope under src/backend
Player impact           If a write fails between the result insert and the grants,
                        the durable RewardSummary claims XP the rows do not hold,
                        and the exactly-once pre-check prevents any retry from
                        re-awarding it. This is a narrow partial-durability window,
                        not a systematic loss.
Priority                P2 (the refutation is P3-informational; the atomicity
                        window and the missing assertion are P2)
Effort                  S
Contract impact         NONE expected
Recommended action      Replace TASK-216 as written. "Verify then fix only if
                        confirmed" has nothing to confirm. Correct it to:
                        (a) assert the composed battle-end path persists Player XP
                        through the real repositories; (b) report/handle the
                        three-write non-atomicity. Do not create an XP
                        transaction table without a decision (the service's own
                        comment forbids it).
```

### A-11 — Documentation and contract drift (larger than "P3")

```text
Gap                     Stale architectural claims a future task would act on,
                        plus one live contract conflict and several
                        cross-reference/envelope defects.
Current behavior        (a) ARCHITECTURE.md names eight components that do not
                        exist and its §1 directory trees do not match src/.
                        (b) `otherModifiers` is documented as a "pass-through
                        1.00 in MVP" in the technical contract while the domain
                        rule and the code both roll crit and fold it in.
                        (c) MVP_SCOPE §1 names no client surface, so by its own
                        §4 the five shipped screens are FUTURE. (d) The §6 error
                        envelope declares a required `message` the auth boundary
                        never sends; `INVALID_INPUT` (returned by AuthController)
                        is documented nowhere; `BOSS_NOT_FOUND` is not enumerated
                        where it is returned. (e) MVP_SCOPE claims an `OnMatch`
                        relic trigger the runtime never evaluates.
Evidence                (a) ARCHITECTURE.md:64-89,91-139,593-594,641-665,672-683,
                        694,708,717,720,734 — no such types under src/backend
                        (real: BattleStateService, BoardResolver, MatchDetector,
                        ElementMatchups, RelicResolver, BattleResultRepository,
                        RuntimeService, PassiveTracker, DamagePipeline); client
                        `public/` does not exist; drift propagated into
                        DATABASE.md:241,887 and REDIS_STATE.md:102,198.
                        (b) SIGNALR_PROTOCOL.md:826,848-852 vs COMBAT_RULES.md:
                        338-339,396-398 vs DamagePipeline.cs:424-437,540 and
                        Task177RelicFiringPointRuntimeTests.cs:152,479,485
                        (asserts 1.5 / 1.0 / 1.30); stale code comments at
                        DamageEvents.cs:104-110, DamagePipeline.cs:16,36-40,95-99,
                        PetState.cs:209.
                        (c) MVP_SCOPE.md:22-89 (no Client block) + :148 ("absent
                        from both §1 and §2 -> FUTURE") vs the five shipped scenes
                        and AuthScreen.
                        (d) API_CONTRACTS.md:836 vs
                        UnauthenticatedResponse.cs:49-50 (`{ error }` only, and
                        the doc itself emits that shape at :297,466,565);
                        AuthController.cs:57,63-67,72-76,126 (`INVALID_INPUT`, 0
                        docs hits); BattleController.cs:157,332 vs
                        API_CONTRACTS.md:417.
                        (e) MVP_SCOPE.md:68 vs RelicFiringPoint.cs:46-81 (five
                        members, no OnMatch) and RelicResolver.cs:394-401.
Player impact           Indirect but real: AGENTS.md §6 routes implementation
                        work to ARCHITECTURE.md §4/§6, and (a) sends an implementer
                        looking for types that do not exist. (b) misleads any
                        future crit/damage work and is a genuine AGENTS.md §4
                        conflict. (d) is the contract the freshly fixed §6 error
                        path depends on.
Priority                P1 for (a) and (b) as documentation blockers; P2 for the
                        rest
Effort                  M
Contract impact         (b) CONTRACT-DECISION-REQUIRED (the wire-contract owner
                        must ratify the correction; the domain rule governs per
                        AGENTS.md §2, so the technical prose is the stale side);
                        the rest DOCUMENTATION-ONLY
Recommended action      Keep as TASK-217 but SPLIT it: TASK-217A = the
                        architecture/component/directory-tree reconciliation
                        (P1-blocker class); TASK-217B = the cross-reference,
                        envelope and stale-claim sweep. Do NOT let it absorb A-05
                        (TASK-213) or A-06 (TASK-215) — both are Product Owner
                        decisions already owned.
```

### A-12 — `CollectionViewerScene` has no async run guard (new, TASK-211-shaped)

```text
Gap                     The Collection viewer renders into a torn-down scene when
                        its read settles after `< BACK`, and its RETRY has no
                        in-flight guard.
Current behavior        `loadCollections` has no run identity and an unconditional
                        `finally { this.loading = false; this.render(); }`. Its
                        `render()` reaches `drawBackButton` and `drawTabs`, which
                        call `this.add.rectangle/text(...)` — creating display
                        objects and registering pointer handlers after Phaser's
                        DisplayList shutdown, with nothing later to release them.
                        `retryLoad` is `{ void this.loadCollections(); }` with no
                        guard, unlike the Lobby's equivalent.
Evidence                CollectionViewerScene.ts:320-346,381-384,486-509,519-552,
                        507-508,549-550; contrast the deliberate handling in
                        BattleHistoryScene.ts:331-333,338-343 and the guards in
                        LobbyScene.ts:352,428,487,851 and ResultScene.ts:216,266,
                        315,689; the SceneLifecycle sweep dirties only
                        `selectedItemId` for this scene
                        (SceneLifecycle.test.ts:4361-4371), while the Lobby and
                        Result have dedicated run-guard suites (:4703, :4564).
Player impact           Reachable on a slow or failing collection read: the player
                        presses `< BACK` and the settling read paints into a dead
                        scene, leaking objects/handlers and risking an
                        engine-level error. Low frequency, real.
Priority                P2
Effort                  S
Contract impact         NONE — the TASK-205 pattern already governs this
                        (ARCHITECTURE.md §2.2.3)
Recommended action      Fold into the next robustness task (see §6 #5) with the
                        A-13 wording cluster; add the missing
                        settles-after-shutdown test.
```

### A-13 — Player-facing technical strings survive in several places

```text
Gap                     The player is shown transport names, a protocol citation,
                        raw machine codes and raw internal identifiers.
Current behavior        (i) A failed start caused by a not-yet-connected transport
                        reads `Battle start failed: SignalR connection is not
                        established.` (ii) A swap attempted with no delivered state
                        renders the runtime's own sentence including
                        `(SIGNALR_PROTOCOL.md §4)`. (iii) Rejection reasons are shown
                        as raw codes (`rejected (INSUFFICIENT_POWER)`). (iv) Raw
                        ids appear as `Card: card-inferno`, `Battle #1 <guid>`,
                        `Instance ID:` / `Card ID:` / `Relic ID:`, and
                        `Passive: passive-xich-lang`. (v) Four separate
                        `describeError` copies exist and three of them fall back to
                        `String(error)`, which can render an arbitrary object's
                        string form.
Evidence                (i) SignalRService.ts:705,724,732,772,800,826,869 +
                        GameRuntime.ts:598-613 -> LobbyScene.ts:861 (E2E-adjacent:
                        TASK-211's own Remaining Issue 1); also BattleScene.ts:
                        1504,1594,1625. (ii) GameRuntime.ts:485-488 ->
                        BattleScene.ts:1504. (iii) BattleScene.ts:1547-1549,
                        1658-1660. (iv) BattleScene.ts:1232-1233,1583,1594,
                        2006-2017; BattleEventPresenter.ts:644-655;
                        BattleHistoryScene.ts:702; CollectionViewerScene.ts:761,
                        772,778; live E2E phase5b.noDeveloperDiagnosticsInTheHud
                        shows `Passive: passive-xich-lang`. (v)
                        LobbyScene.ts:1378-1380 (safe) vs
                        CollectionViewerScene.ts:813-815,
                        BattleHistoryScene.ts:711-713,
                        GameRuntime.ts:1526-1531 (all `String(error)`)
Player impact           A player who meets a transient connection failure is told
                        about SignalR; a rejected swap is explained by a machine
                        code; the Signature Skill is a raw id. Individually small,
                        collectively the remaining legibility debt on the surfaces
                        TASK-210/211 just polished.
Priority                P2
Effort                  S–M
Contract impact         IMPLEMENTATION-ONLY for (i)–(iv); (v) is an internal
                        consolidation with a behaviour choice (what a non-Error
                        rejection renders) that should be recorded
Recommended action      One dedicated client-presentation task. Do not bundle it
                        with A-07 (different root cause) and do not let it absorb
                        A-01 (contract-gated).
```

### A-14 — The duplicate-username error never matches (NG-11)

```text
Gap                     Registration with a taken name shows a raw machine token.
Current behavior        AuthScreen matches `USERNAME_ALREADY_TAKEN`; the backend
                        returns `USERNAME_ALREADY_EXISTS`. The match fails and the
                        documented Vietnamese message never appears.
Evidence                AuthScreen.tsx:41-51 vs AuthController.cs:84 and
                        API_CONTRACTS.md:199, ADR-020:46,
                        AuthPlayerOwnershipTests.cs:126; ApiService.ts:185-189
                        throws the code alone; no AuthScreen.test.tsx exists
Player impact           One of the most likely first-run errors is untranslated.
Priority                P2
Effort                  S
Contract impact         NONE (the code is correct on the wire; the client's
                        match string is wrong)
Recommended action      One-line BUG. It has now been reported by two audits
                        without an owner; it should ride along with A-13 or be its
                        own S task.
```

### A-15 — No logout; a mid-session 401 never resets the session; a post-login connect failure shows a blank form

```text
Gap                     The session cannot be ended, and an expired token leaves
                        the player on screens offering a retry that cannot succeed.
Current behavior        No logout/signOut identifier exists anywhere in src/.
                        `ApplicationSession.clear()` has exactly one application
                        caller (a failed bootstrap connect). A 401 surfaces as a
                        generic message and changes nothing. Separately, a
                        post-login runtime connect failure sets session status to
                        error and re-renders AuthScreen with a freshly null local
                        message, so the player is bounced to a blank login form
                        while the token is already stored.
Evidence                repo-wide grep for logout/signOut -> 0 matches;
                        App.tsx:37-53,101-125; ApplicationSession.ts:89,108;
                        ApiService.ts:133-135,106-120; ADR-020 documents that a
                        sign-out clears the token; TASK-211 audit NG-12/NG-13
Player impact           An expired session is unrecoverable without a page reload,
                        and a failed connect after a successful login is
                        unexplained.
Priority                P2
Effort                  M
Contract impact         NONE on the API (the 401 contract exists; ADR-020 already
                        documents the intended sign-out)
Recommended action      Separate session-lifecycle task; both NG-12 and NG-13 in
                        one change.
```

### A-16 — `BattleScene` has no exit, so an unresolved battle strands the player (new)

```text
Gap                     The battle screen's only exit is a terminal
                        BattleWon/BattleLost.
Current behavior        No BACK / FORFEIT / QUIT control and no keyboard path
                        exists in BattleScene; the only `scene.start('ResultScene')`
                        is guarded by `outcomeHandled`. Connection state is at least
                        rendered safely ('Connecting…' / 'Reconnecting…' /
                        'Battle unavailable'), and reconnect/resync is implemented,
                        but if no outcome ever arrives there is no way out but a
                        page reload.
Evidence                BattleScene.ts:1686-1696 is the only transition; grep for
                        BACK|FORFEIT|QUIT|surrender|MainMenuScene in BattleScene.ts
                        -> 0 matches; E2E phase6c shows reconnect/resync works when
                        the transport recovers
Player impact           A genuine (if uncommon) dead end, and the last remaining
                        one in the loop now that the Lobby's is fixed.
Priority                P2 (P1 if the unrecoverable case is judged reachable in
                        production network conditions)
Effort                  S–M
Contract impact         DECISION-REQUIRED if the exit is a forfeit (conceding is
                        a game rule: GAME_RULES.md/GAME_EVENTS.md own the outcome
                        set, and a new terminal outcome or an abandonment path must
                        be authored). A pure "return to Main Menu, abandon the
                        battle" path is also a rule question, not a presentation
                        one.
Recommended action      Report as a stop condition (AGENTS.md §7/§20): the
                        missing rule is "may a player abandon an in-progress
                        battle, and what does that do to its record?" Do not
                        implement a guessed forfeit.
```

### A-17 — Heal has no amount feedback; no crit indicator (both documented refusals)

```text
Gap                     A heal's amount and a critical hit are not reported.
Current behavior        `ApplyHeal` emits no event and the canonical event list has
                        no heal event; the resulting HP is visible on the gauge, so
                        the amount is inferred, not shown — and deriving it from two
                        states is forbidden. Crit is folded into step 4's combined
                        `otherModifiers` with no separate member, and the client is
                        forbidden to reconstruct one.
Evidence                ResourceGenerator.cs:400-404; GAME_EVENTS.md has no heal
                        event, but :664-670 names the resolution route ("a value
                        the client needs but no event reports is a gap to be
                        resolved by adding an event, not by delivering state on a
                        side channel"); PlayerEffectHealingTests.cs:467-482;
                        COMBAT_RULES.md:396-398 + SIGNALR_PROTOCOL.md §4 item 14
Player impact           Cosmetic. The HP gauge moves; only the attribution is
                        missing. A ~5 % damage spike arrives unexplained.
Priority                P3
Effort                  S–M
Contract impact         DECISION-REQUIRED for both. Do NOT reopen the crit
                        decision on this evidence — the defect there is stale prose
                        (A-11b), not the contract.
Recommended action      Lowest-priority contract decisions; a heal event only if a
                        later presentation task needs it.
```

### A-18 — Minor Lobby/Collection consistency and stale-state defects

```text
Gap                     Silent truncation, mixed element vocabularies, a stale
                        start-error line, and lists not cleared on failure.
Current behavior        (i) Lobby lists slice at 8 rows with no "showing N of M"
                        notice (Collection computes 14 and prints the true count in
                        the tab label; History does it correctly). (ii) The Lobby
                        renders Pets with the English wire element and Bosses with
                        the Vietnamese display element on the same screen.
                        (iii) A rejected start's message survives loadout edits
                        (only `selectionMessage` is cleared). (iv) The Lobby and
                        Collection do not clear their loaded arrays on a failed
                        read, so a stale list/detail panel beside an error is
                        representable (latent today).
Evidence                LobbyScene.ts:106,1225,1249,1275,1231 vs :1308,681-766,
                        1059-1062,544-584; CollectionViewerScene.ts:61,341,728-742;
                        BattleHistoryScene.ts:578-587 (the correct pattern);
                        CollectionModels.ts:36-37,43
Player impact           Small individually; (i) becomes real the moment ownership
                        can grow (A-05), and (iv) is a robustness edge.
Priority                P3
Effort                  S
Contract impact         NONE
Recommended action      Fold into A-13's presentation task; make (i) a stated
                        requirement of any A-05 acquisition work rather than
                        fixing it now.
```

### A-19 — Traceability and task-record integrity

```text
Gap                     Records that shipped code and tests cite do not exist, and
                        one ID is used twice.
Current behavior        (i) No TASK-209 record exists although TASK-210 declares it
                        DONE and BattleScene.ts/App.tsx/tests cite its section
                        numbers. (ii) Two distinct files share the ID TASK-211.
                        (iii) TASK-175 and TASK-184 are cited by DATABASE.md and
                        RELIC_RULES.md as completed but have no records. (iv)
                        AGENTS.md:105 (and docs/AGENTS.md:105) say "ADR/ (12
                        ADRs)" while 22 exist and the index lists all 22 correctly.
                        (v) TASK-212…TASK-217 exist only as roadmap proposals;
                        TASK-217 is consequently an unbounded bucket.
Evidence                glob tasks/**/*209* -> none; BattleScene.ts:562,921,2036,
                        App.tsx:136; tasks/completed/TASK-211-non-battle-screen-
                        robustness-and-recovery.md and TASK-211-post-task-210-
                        product-audit.md; tasks/README.md:95 ("Numbers are never
                        reused"); DATABASE.md:5,16,23,806,1575,1580;
                        RELIC_RULES.md:4,764,1294; AGENTS.md:105;
                        docs/03-decisions/README.md:200-223
Player impact           None directly. It is process/traceability debt that makes
                        every later audit re-derive the same facts.
Priority                P3
Effort                  S
Contract impact         NONE
Recommended action      Bookkeeping task owned by an orchestrator/review role.
                        Do NOT reconstruct another task's record inside a gameplay
                        task, and do NOT renumber the duplicate TASK-211 file
                        without a decision (renaming a completed record is a
                        lifecycle question).
```

### A-20 — Retired-Discord residue is still wired into DI

```text
Gap                     ADR-020 declares Discord retired, but an inert
                        `IDiscordIdentityResolver` seam remains registered.
Current behavior        `GameServer.Infrastructure/Discord/` survives with four
                        files and
                        `services.AddSingleton<IDiscordIdentityResolver,
                        UnconfiguredDiscordIdentityResolver>()`. Nothing consumes
                        it and no Discord endpoint exists; it is dead but live
                        wiring against a document that says it was removed.
Evidence                ADR-020:24-28; ARCHITECTURE.md:622;
                        DependencyInjection.cs:101; the four files under
                        GameServer.Infrastructure/Discord/;
                        Application/Identity/DiscordIdentityResolution.cs:113;
                        no controller consumes it; TDD.md:75,93,100,241,252 still
                        describe a Discord Activity client
Player impact           None. It is a consistency and dead-code question, and it
                        interacts with A-11a/A-11b's Discord documentation drift.
Priority                P3
Effort                  S
Contract impact         DECISION-REQUIRED (delete the residue, or record it as
                        deliberately retained)
Recommended action      Decide it inside TASK-217A, since the accompanying
                        documentation correction is the same change.
```

---

## 4. TASK-212 Decision

```text
SPLIT
```

**Why not KEEP.** TASK-212 as proposed — "Expose Card cost/effect and Relic
effect at the decision point … it widens `API_CONTRACTS` §5.3/§5.4"
(`TASK-207:428-437`) — names one owner for two incompatible problems, and this
audit found that the second problem's owner is a different document entirely:

1. **Descriptive exposure (what a Card or Relic does) is a read-contract
   decision.** It is additive on two GET responses: `GET /api/cards` gains
   presentation-safe effect/description data; `GET /api/relics` gains the
   trigger/condition/effect text it currently withholds
   (`API_CONTRACTS.md:783-786,802-811`; `CollectionResponses.cs`). No state
   member, no event, no runtime change, no Redis field, no DB column.
2. **Cost/affordability (what a cast costs *right now*) is a realtime-protocol
   decision.** `SIGNALR_PROTOCOL.md` §4 item 15 states that "`power` authorizes no
   client-side cost, affordability, or legality computation", that the client
   "must never compute, predict, or reconstruct a Card-cost modifier from events,
   from a `PowerChanged` delta, or from a Card's definition", and that "Delivering
   it would be a protocol change owned by its own task". The composed price is
   `truncate(PowerCost × (100 − TotalReduction)/100)` over
   `PetState.CardCostModifiers[]` (`CARD_RULES.md:212-351`, `CardCostModifier.cs`,
   `EffectiveCardCost.cs:87-109`) — and that collection is explicitly **not
   delivered** (`SIGNALR_PROTOCOL.md` §4.3 item 2 / §4 item 15). So exposing
   `powerCost` in §5.3 alone would be *misleading* whenever a modifier is active,
   and the client would still be forbidden to compute affordability. KEEP would
   therefore bake a wrong-scoped, partly-forbidden implementation into the next
   task.
3. **Its motivating case is unreachable.** The only provisioned `CardCost` source
   is Emergency Core, which is "provisioned but not selected"
   (`PlayerStarterGrantFactory.cs:81-84`) and has no acquisition path
   (A-05), so `CardCostModifiers[]` is empty in every battle a real account can
   play. Spending a realtime-protocol decision on it before TASK-213 would put the
   wire contract ahead of the product loop.
4. **The Signature Skill is a third concern and must not be absorbed.**
   `TASK-207`'s U4/G12 folded it into the generic "card cost/effect" concern; this
   audit confirms it is a distinct read-source decision with a distinct owner
   section, and that it is **worse** than recorded (A-01: the control dispatches
   `CardCast` instead of `PetSkillCast` in every real battle). It stays a separate
   task (TASK-219), which TASK-211's audit already reserved.
5. **Relic presentation is not TASK-212's at all.** TASK-210 classified
   `RelicTriggered`'s missing name as a contract gap; this audit re-verified that
   it is implementation-only (A-07) — the read the client needs already exists and
   the client already holds the port. Folding it into a contract-decision task
   would delay an S-sized, decision-free win.

**Why SPLIT over MODIFY.** MODIFY would keep one task that must first wait for two
independent decisions in two different documents with two different characteristics
(one additive read, one wire projection), and it would still have to exclude the
Signature Skill by hand. The two halves differ in owner, in risk, in dependency
order and in payoff timing; this audit's §5/§6 treat them as separate tasks
(TASK-212A now, TASK-212B after TASK-213).

**Why not DROP.** The comprehension gap is real, documented
(`GDD.md:303-306`), on the happy path before every battle, and cheap to close for
the descriptive half. It is the highest-value work available whose implementation
does not require a *product* decision.

**Why not REPLACE.** Nothing in this audit supersedes the comprehension goal; the
roadmap's intent is correct and only its scope boundary is wrong.

### The split

```text
TASK-212A  Expose Card and Relic content at the loadout decision point
           Contract owner:  API_CONTRACTS.md §5.3 / §5.4 (additive read)
           Decision cost:   LOW — additive members on two existing GET responses
           Dependency:      none (can start immediately; decision is part of the
                            task, per the TASK-208 -> TASK-208A -> TASK-209
                            pattern)
           Serves:          GDD.md:303-306 comprehension; the in-battle cast
                            decision (A-03)

TASK-212B  In-battle Card cost and affordability presentation
           Contract owner:  SIGNALR_PROTOCOL.md §4 (new projection member or
                            server-computed effective cost)
           Decision cost:   HIGH — a wire-contract decision; §4 item 15 currently
                            forbids the computation outright
           Dependency:      TASK-213 (the only CardCost source is unreachable) and
                            the A-08 protocol decision, which should be taken in
                            the same protocol round
           Serves:          GDD.md:218-222 Power spend/save (A-04)
```

**Explicit boundaries recorded so the halves cannot drift back together.**
TASK-212A must not deliver `CardCostModifiers[]`, must not compute or imply
affordability, and must not invent a client-side modifier. TASK-212B must not
widen §5.3's membership semantics (that would make a `PetSkill` card an unlock row
and contradict `CARD_RULES.md` §1 item 4 / ADR-012 item 9). Neither half owns the
Signature Skill decision (TASK-219) or the Relic trigger callout (TASK-218).

---

## 5. Recommended Next Implementation Task

```text
Task ID                 TASK-212A   (new; the first half of the split TASK-212)
Title                   Expose Card and Relic content at the loadout decision point
Priority                P1
Type                    CONTRACT DECISION + client implementation (one task, the
                        TASK-208 -> TASK-208A -> TASK-209 shape; if the reviewer
                        enforces one type per task, split the decision record and
                        the client rendering into TASK-212A-1 / TASK-212A-2)
```

**Objective.** Let the player understand what each Card and each Relic does — what
it costs, what it changes, when it reacts — on the screens where those choices are
made, using only values the authoritative documents authorize, without widening
the realtime projection, without inventing a second content catalog, and without
computing affordability.

**Scope.**

1. **Decision record + authoritative amendment (owner: `API_CONTRACTS.md` §5.3 /
   §5.4).** Decide and record the smallest presentation-safe widening of the two
   read responses. Open questions the record must answer:
   * May a Card response carry its authored `powerCost` and a
     presentation-safe form of its effect (effect type + magnitude + duration),
     and in what spelling — raw `effectDefinition` or a flattened, described
     form? (`DATABASE.md` §1/§3 own the storage shape; `CARD_RULES.md` §2/§4.1
     own the values.)
   * May a Relic response carry its `Trigger`, `Condition` and a
     presentation-safe effect summary, and in what spelling?
     (`RELIC_RULES.md` §8.2–§8.6 own the vocabulary.)
   * What does the *label* for each become, given that the client may present but
     never evaluate? `CARD_RULES.md` §5's Cards-are-active / Relics-are-passive
     boundary must survive the presentation.
   * Confirm explicitly that the amendment does NOT authorize cost computation,
     affordability, legality, or any `CardCostModifiers[]` reconstruction
     (`SIGNALR_PROTOCOL.md` §4 item 15 remains in force; §5.6's "no equip state"
     rule is untouched), and that `PetSkill` cards remain non-unlock rows.
2. **Client rendering.** Lobby rows and the Collection detail panel present the
   newly authorized members for Cards and Relics, with the existing wire-member
   presentation limit respected (presentation only; no rule text invented, no
   magnitude derived, no client catalog).
3. **Fail closed.** An absent or unexpected new member degrades to the current
   presentation (name and category), never to a fabricated value — the TASK-208
   "never a guessed name" rule extended to the new text.

**Non-goals.**

* No in-battle cost tile, affordability state, disabled state, or cost
  computation (that is TASK-212B; `SIGNALR_PROTOCOL.md` §4 item 15).
* No new wire member, event, SignalR message, Redis field, DB column or migration.
* No change to §5.3's or §5.4's *membership* semantics (unlock = presence;
  PetSkill cards are never unlock rows).
* No Signature Skill read source or label/dispatch change (TASK-219).
* No Relic trigger callout in battle (TASK-218).
* No acquisition, ownership, unlock, loadout-rule or gameplay change (TASK-213).
* No tier/star/passive scope change (TASK-215), no `durationTurns`/reward work,
  no documentation sweep (TASK-217), no auth/session work (TASK-214/A-15).

**Expected implementation areas.**

```text
docs/02-technical/API_CONTRACTS.md        §5.3 / §5.4 (the decided widening) and
                                          §5.6 / §6 consistency re-read
src/backend/GameServer.Api/Controllers/
    CollectionResponses.cs                the two response shapes
    CollectionController.cs               only if a binding/derivation changes
src/backend/GameServer.Application/
    Collection/CollectionQueryService.cs  carry the newly exposed definitions
src/frontend/client/src/services/api/CollectionModels.ts   the two client models
src/frontend/client/src/game/scenes/LobbyScene.ts          card + relic rows
src/frontend/client/src/game/scenes/CollectionViewerScene.ts  detail panel
src/frontend/client/tests/CollectionService.test.ts        the new members and
                                                           the exclusions
src/frontend/client/tests/LobbyScene.test.ts               rendering + fallback
src/frontend/client/tests/CollectionViewerScene.test.ts    rendering + fallback
tests/backend/**/CollectionEndpointTests.cs                exact member sets, and
                                                           the still-excluded list
docs/ (any other file that restates §5.3/§5.4)             reference only
NOT touched: SIGNALR_PROTOCOL.md, GAME_STATE.md, GAME_EVENTS.md,
             REDIS_STATE.md, DATABASE.md, migrations, BattleHub.cs,
             BattleScene.ts, GameRuntime*.ts, any game rule document
```

**Contract impact.** `API_CONTRACTS.md` §5.3 and §5.4 only — an **additive**
widening of two read responses, decided and recorded first (`AGENTS.md` §4/§17;
the TASK-208 → TASK-208A precedent). No realtime/SignalR change, no state model
change, no ADR required (no architecture, storage or authoritative-model change).
`SIGNALR_PROTOCOL.md` must be explicitly re-read to confirm nothing it owns is
touched, and §5.6's no-equip-state rule must be preserved.

**Estimated effort.** **M** (decision record + two response widenings + two
rendering surfaces + tests). If split by type: decision **S**, implementation
**S–M**.

**Verification.**

```text
Required commands
  git diff --check                                     PASS
  npx tsc --noEmit                                     PASS
  npx vitest run                                       PASS (817 existing tests
                                                       unchanged or extended)
  dotnet test (Api + Application + Domain +
              Infrastructure)                          PASS
  npm run verify:e2e:smoke                             PASS (132 checks x 2 runs)

Required new coverage
  Cards   the response carries exactly the newly authorized members and still
          excludes playerId, loadoutCopyLimit and every other excluded column
  Cards   a PetSkill definition is still NOT returned by GET /api/cards for an
          owner who does not hold an unlock row (membership semantics unchanged)
  Relics  the response carries the newly authorized members and still excludes
          playerId, acquiredAt and definitionId
  Lobby   the new Card and Relic text renders; the loadout rules, submission and
          preserved-loadout behaviour are unchanged
  Lobby   / Collection  an absent or malformed new member degrades to the current
          presentation with no fabricated value and no unhandled rejection
  Client  no cost, affordability, legality or modifier computation is introduced
          (extend RuntimeBoundaries.test.ts's forbidden-term set rather than
          weakening it)
  Client  no equip/loadout state is read from the collection responses (§5.6)

Required E2E
  a Lobby check that the new Card and Relic text is present on the player's screen

Required documentation
  API_CONTRACTS.md §5.3/§5.4 amended; the version preamble records the change;
  every other site that restates the two member lists re-read for consistency
```

---

## 6. Next 3–5 Task Sequence

Ranked by actual product impact. Effort: **S** ≤ 1 session · **M** 2–4 · **L** 5+.

| # | Task | Title | Priority | Effort | Contract impact | Why here |
|---|---|---|---|---|---|---|
| 1 | **TASK-212A** (SPLIT) | Expose Card and Relic content at the loadout decision point | **P1** | M | DECISION-REQUIRED (additive read, §5.3/§5.4) | Highest documented-intent gap that is closable without a product decision: `GDD.md:303-306` and pillar 2 are unmet on the happy path before every battle, and the fix cannot cause a wrong price to be shown because it does not touch cost. Its decision cost is far lower than TASK-211's audit assumed — the *descriptive* half needs no realtime projection while the *affordability* half does, which is exactly why the halves must separate. |
| 2 | **TASK-218** (unblocked) | Name the Pet's Relic triggers in battle from the read the client already has | P2 | S | NONE (implementation-only) | The only substantial player-facing battle gap with zero contract risk, verified solvable: `GET /api/relics` already maps instance identity → name and the runtime port already exposes it. All three starter Relics fire 2–3 times per real battle and produce no feedback. It can run in parallel with #1. |
| 3 | **TASK-213** (decision) | Content reachability: 5 Pets / ~10 Relics / 5 Pet Skill Cards vs starter-only ownership | **P1** (product loop) | S (decision) | DECISION-REQUIRED (MVP_SCOPE §1 vs DATABASE.md §2) | The largest structural hole and the gate on the *meaning* of #1: with 3 owned Cards and 3 owned Relics against exact-3 requirements, the player equips a fixed set, so description improves comprehension but cannot change a build. Answering it also fixes the ordering of TASK-212B, whose only motivating modifier is unreachable content. |
| 4 | **TASK-219** (decision, then fix) | Pet Signature Skill read source — and the wrong action its absence causes | **P1** (visibility + correctness) | M | DECISION-REQUIRED (API_CONTRACTS §5.1 or §3) | Reordered upward from TASK-211's #4 because this audit found the defect is functional, not cosmetic: in every real battle the Signature Skill tile dispatches `CardCast` instead of `PetSkillCast` and works only because the server's `CardCast` is broader than its own documentation (A-01/A-02, proven by the E2E). It must resolve the read source and coordinate the server boundary in one decision record. |
| 5 | **TASK-215** (decision, expanded) | Documented-but-inert Pet progression and Passive axes | P2 | S (decision) | DECISION-REQUIRED (MVP_SCOPE §1 / ROADMAP §1 vs PET_RULES §5.7/§6/§8) | Two genuine `AGENTS.md` §4 conflicts, one of which TASK-211's audit dropped: "Tier, Star, Level progression" has no implementation path, and the Pet Passive effect is inert by TASK-200 Q-11 while `PASSIVE_RULES.md:216-224` defines all five effects and `MVP_SCOPE.md:52` lists Passive as IN. Neither may be implemented before the decision, and the magnitudes are authored nowhere. |

**Immediately behind, and why they are not in the top 5.**

```text
TASK-212B  In-battle cost/affordability                     P2  S–M
           Blocked by a SIGNALR_PROTOCOL §4 decision AND by TASK-213; take its
           protocol decision in the same round as A-08's, but implement after
           content is reachable.

A-08 task  Boss status-effect projection (Burn/rage/shield)  P2  S–M
           Same protocol decision round as TASK-212B; a Card's Burn on the Boss
           is currently invisible.

A-10 task  Corrected TASK-216: assert the composed reward path, and the
           three-write non-atomicity                          P2  S
           TASK-216 as written ("verify then fix only if confirmed") has nothing
           to confirm — the ordering risk is refuted. Cheap, independent, and it
           protects the only reward the game grants.

TASK-214   Account progression read surface                  P2  M

TASK-217A  Architecture/component/directory-tree reconciliation  P1 (blocker
           class as documentation)                           M
           `ARCHITECTURE.md:641-665` names eight components that do not exist and
           §1's trees do not match src/, so AGENTS.md §6 sends an implementer to
           a type that is not there. Documentation-only in *production* terms,
           but the highest-risk stale claim in the repository.

A-12/A-13  Collection run guard + player-facing technical strings  P2  S–M
A-14       NG-11 duplicate-username mapping                     P2  S
A-15       Logout / 401 session reset / post-login bounce        P2  M
TASK-217B  Cross-reference, envelope and stale-claim sweep        P3  M
A-16       Battle abandonment rule (STOP CONDITION, not a task)   P2  —
A-19       Traceability/task-record integrity                     P3  S
A-17/A-20  Heal event + crit-indicator decisions; Discord residue P3  S
```

**Changes to TASK-211's ordering, and why.**

* **TASK-212 is split, and its descriptive half takes the next slot.** The new
  evidence is that the two halves have different owners and that only the
  affordability half needs a wire decision — so the previous ranking's reason for
  deferring TASK-212 ("its first half is a decision") no longer holds for the
  descriptive half.
* **TASK-218 keeps its #2 position** from TASK-211's audit (it was #2 behind the
  now-complete TASK-211) and remains the best value-per-risk item on the board:
  implementation-only, S, verified solvable, every battle.
* **TASK-219 rises from #4 to #4-by-severity but #4 in order** — it is re-ranked
  by the new proof that its defect is functional (wrong RPC) rather than only
  cosmetic. It does not take the next slot because it still needs a decision that
  a single task cannot self-approve in the same breath as #1.
* **TASK-213 keeps its substance and moves behind #1**, because the descriptive
  half does not depend on it and is cheaper; TASK-212B additionally depends on it.
* **TASK-215 is expanded** to carry the Pet Passive conflict (A-06) that TASK-211's
  audit dropped, and **TASK-216 is dropped as written** and replaced by the
  corrected A-10 item.
* **TASK-217 is split** into an architecture-reconciliation half (raised to the
  documentation-blocker class) and a cross-reference/envelope sweep, with two
  items removed from it because TASK-213/TASK-215 already own them.
* **Nothing in the old roadmap is dropped**: TASK-212 survives as two tasks,
  TASK-213/214/215 keep their substance, TASK-216 is corrected rather than
  deleted, TASK-217 is split and re-scoped.

---

## 7. Contract / Documentation Classification

### 7.1 Implementation-only (the authoritative documents already prescribe it, or the data is already delivered)

| ID | Gap | Why it is implementation-only | Size |
|---|---|---|---|
| A-07 | `RelicTriggered` has no player-facing name | `GET /api/relics` already maps instance identity → definition `name` (`API_CONTRACTS.md:802-806`) and the runtime port already exposes `getRelics()`. `BattleScene` never calls it. **TASK-210's contract-gap classification is wrong.** | S |
| A-02 | `CardCast` accepts `PetSkill` cards | `BattleHub.cs:1108` and `SIGNALR_PROTOCOL.md` §2 fix the documented boundary; the executor does not enforce it. Coordinated with A-01. | S |
| A-12 | `CollectionViewerScene` has no run guard | `ARCHITECTURE.md` §2.2.3 and the TASK-205 pattern already govern this; three sibling scenes implement it. | S |
| A-13 (i–iv) | Transport names, a protocol citation, raw codes and raw ids on player surfaces | The player-safe mapping already exists (`BattleScene.ts:2138-2153`) and the §6 envelope is already read. | S–M |
| A-14 | Duplicate-username code mismatch | The wire code is correct and documented; the client's match string is wrong. | S |
| A-15 | No logout; 401 never resets the session; post-login blank bounce | ADR-020 already documents a sign-out that clears the token; the 401 contract exists. | M |
| A-18 | Lobby/Collection truncation, mixed element vocabulary, stale error line, uncleared lists | No contract involved; `BattleHistoryScene.ts:578-587` is the correct pattern to copy. | S |
| A-10 (part) | No assertion that the composed reward path persists Player XP | The behaviour already works; only coverage and the recorded reasoning are missing. | S |

### 7.2 Documentation-only

| ID | Gap | Owner | Note |
|---|---|---|---|
| A-11a | `ARCHITECTURE.md:641-665` names eight non-existent components; §1's directory trees do not match `src/`; the HUD claim at `:593-594` is stale; the drift is echoed in `DATABASE.md:241,887` and `REDIS_STATE.md:102,198` | `ARCHITECTURE.md` | Highest-risk stale documentation in the repository: `AGENTS.md` §6 routes backend work here. TASK-217A. |
| A-11c | `MVP_SCOPE.md` §1 names no client surface, so §4 makes the five shipped screens FUTURE | `MVP_SCOPE.md` | TASK-217B. |
| A-11d | `API_CONTRACTS.md:836` requires a `message` the auth boundary never sends; `INVALID_INPUT` documented nowhere; `BOSS_NOT_FOUND` unenumerated | `API_CONTRACTS.md` | TASK-217B; the freshly fixed §6 client path depends on this contract. |
| A-11e | `MVP_SCOPE.md:68` claims an `OnMatch` relic trigger the runtime never evaluates | `MVP_SCOPE.md` / `RELIC_RULES.md` | TASK-217B (or TASK-213 if the trigger is meant to be IN). |
| A-11 (rest) | `ROADMAP.md:14-17` Phase 0 "IN PROGRESS"; `:42-44` Thanh Xà / Sơn Hùng "remain to be provisioned"; `GDD.md:103-104` "two entry points" (there are three); `GDD.md:112-114` cites `MVP_SCOPE.md` §1 for the replay loop; `API_CONTRACTS.md` §2.8 pointers (6 self / 11 repo-wide) and sibling §2.4–§2.7 pointers; `AGENTS.md:105` "12 ADRs" (22 exist); ADR-003's status vs ADR-020's amendment; `REDIS_STATE.md:309-311`; `GAME_STATE.md:1150-1157` | various | TASK-217B. One correction to carry: TASK-207's "seven sites" figure is 6 self-citations / 11 repo-wide. |
| A-11 (code comments) | Stale "not implemented" comments: `DamageEvents.cs:104-110`, `DamagePipeline.cs:16,36-40,95-99`, `PetState.cs:209`, `CardDefinition.cs:53-58`, `CardDefinitionConfiguration.cs:100-101`, `RelicDefinition.cs:52-55,146-150`, `Application/DependencyInjection.cs:98-99`, `GameViewport.ts:25` | implementation-side | Report with TASK-217; each is a comment truthfulness defect, not behaviour. |
| A-11a (adjacent) | `TDD.md:75,93,100,241,252` still describe a Discord Activity client (self-contradicted by `TDD.md:67` "Standalone Web SPA; ADR-020") | `TDD.md` | TASK-217A, with A-20's residue decision. |

### 7.3 Contract-decision-required

These cannot be implemented by any task until a Product Owner decision is recorded
and the owning document amended (`AGENTS.md` §7, §17, §18).

| ID | Gap | Decision owner / document | What must be decided | Size |
|---|---|---|---|---|
| A-03 | Card/Relic effect content is not exposed at the decision point | `API_CONTRACTS.md` §5.3 / §5.4 | Whether to widen the two read responses, and in what presentation-safe form (raw `effectDefinition` vs described text; `Trigger`/`Condition`/effect summary for Relics) — **without** authorizing cost, affordability or any modifier computation | M |
| A-04 | In-battle Card cost/affordability cannot be shown | `SIGNALR_PROTOCOL.md` §4 (not §5.3) | Whether to deliver `CardCostModifiers[]`, or a server-computed per-card effective cost, or nothing — §4 item 15 currently forbids the client-side computation outright | S–M |
| A-08 | Boss status effects are undelivered | `SIGNALR_PROTOCOL.md` §4.4 | Whether the Boss projection gains presentation members for its Status Effects (the D-208-03 shape) | S–M |
| A-01 | No read source for the derived Signature Skill | `API_CONTRACTS.md` §5.1 or §3 — **not** §5.3's membership semantics | How the derived Signature Skill's identity/category/display name reaches the client without making a `PetSkill` card an unlock row, and how the client's action dispatch is made correct in the same change | M |
| A-05 | Ownership cannot grow; MVP_SCOPE §1 content is unreachable | `MVP_SCOPE.md` §1 vs `DATABASE.md` §2 | Whether the content listed as IN becomes reachable, and by what mechanism (any mechanism is a Rule Change and may be FUTURE) | S (decision) |
| A-06 | The Pet Passive effect is inert; the magnitudes are authored nowhere | `MVP_SCOPE.md` §1 / `ROADMAP.md` / `PASSIVE_RULES.md` §8 vs TASK-200 Q-11 | Whether the five Passive effects ship, and if so their magnitudes (`PASSIVE_RULES.md:227-229` says they are config, not rule) — or whether `MVP_SCOPE.md` §1's "Passive" claim is narrowed | S–M |
| A-09 | No progression read surface | `API_CONTRACTS.md` (new read) or a recorded decision that Battle History remains the home | Whether to add a Player read | M |
| A-11b | `otherModifiers`: `SIGNALR_PROTOCOL.md:826,848-852` ("pass-through `1.00` in MVP") vs `COMBAT_RULES.md:338-339,396-398` + `DamagePipeline.cs:424-437` + tests asserting 1.5/1.0/1.30 | `SIGNALR_PROTOCOL.md` §3.2.13 (owner of the wire description) | Ratify the correction of the technical prose. Per `AGENTS.md` §2 the specific domain rule governs and the code agrees with it, so the technical text is the stale side — but a wire-description correction is the protocol owner's call, and this audit only detects, explains, names the owner and stops | S |
| A-10 (part) | Whether the battle-end reward writes should be atomic | `BattleResultService`'s own recorded decision ("no second pipeline, service, or transaction architecture") vs an explicit transaction | Whether to formalize atomicity, or to record the partial-durability window as accepted | S |
| A-16 | A battle cannot be abandoned | `GAME_RULES.md` / `GAME_EVENTS.md` (the closed outcome set) | Whether a player may leave an in-progress battle and what that does to its record. **Reported as a stop condition, not designed here** (`AGENTS.md` §7/§20) | S–M |
| A-17 | Heal amount; crit indicator | `GAME_RULES.md` §16 + `GAME_EVENTS.md` §2 + `SIGNALR_PROTOCOL.md` §3.2 (closed event set); crit: `COMBAT_RULES.md` §3.3 item 6 + `SIGNALR_PROTOCOL.md` §4 item 14 | Whether to add a heal event. **Do not reopen the crit decision** — the current decision is coherent and the defect is stale prose (A-11b) | S–M |
| A-20 | Retired-Discord residue is still registered in DI | `ADR-020` / `ARCHITECTURE.md` | Delete the residue, or record it as deliberately retained | S |

### 7.4 Historical / task-record

| ID | Item | Note |
|---|---|---|
| A-19 (i) | No TASK-209 record exists although TASK-210 declares it DONE and shipped code/tests cite its sections (`BattleScene.ts:562,921,2036`; `App.tsx:136`) | Traceability gap (`tasks/README.md` §5). An audit does not create another task's record |
| A-19 (ii) | Two files share the ID TASK-211 (`TASK-211-non-battle-screen-robustness-and-recovery.md`, `TASK-211-post-task-210-product-audit.md`) against `tasks/README.md:95` "Numbers are never reused" | New finding; renaming a completed record is a lifecycle decision, not an edit |
| A-19 (iii) | TASK-175 and TASK-184 are cited as completed by `DATABASE.md:5,16,23,806,1575,1580` and `RELIC_RULES.md:4,764,1294` but have no records | Reported by TASK-207; still true, now with the full citation list |
| A-19 (iv) | `AGENTS.md:105` and `docs/AGENTS.md:105` say "ADR/ (12 ADRs)"; 22 exist and the index lists all 22 correctly | New finding; the index itself is clean |
| A-19 (v) | TASK-212 … TASK-217 exist only as roadmap proposals; TASK-217 is consequently an unbounded bucket | This is why TASK-217 must be split and scoped before it runs |
| A-19 (vi) | `Task-207`'s "seven sites" §2.8 figure is imprecise (6 self / 11 repo-wide) | Corrected here; the completed record is not edited |
| A-19 (vii) | TASK-210 §Remaining Issue 2 misclassifies the Relic gap as a contract gap; TASK-211's audit dropped TASK-207 G8 and restated TASK-216's refuted risk | Reported, records unedited |

---

## 8. Evidence

### 8.1 Commands executed by this audit

```text
git status --porcelain, git status --short, git log --oneline, git diff --stat,
git diff --check, git check-ignore -v, git ls-files
npx tsc --noEmit                        (src/frontend/client)      PASS
npx vitest run --reporter=basic         (src/frontend/client)      PASS 20/817
dotnet run --project src/backend/GameServer.Api/GameServer.Api.csproj
                                        (Development; /health "Healthy")
npm run dev                             (client dev server, 200)
npm run verify:e2e:smoke                (Microsoft Edge headless)  132 checks x 2
                                        runs, 0 failures
Get-NetTCPConnection / Get-Process      (stack state before and after)
glob tasks/**/*   ·   Select-String "TASK-209" / "Q-11" / "CardCostModifiers" /
"Emergency Core" / "describeError" / "SignalR connection is not established"
across the repository
```

### 8.2 Source areas inspected

```text
Frontend  BattleScene.ts (HUD, cast controls, action paths, terminal handoff),
          BattleEventPresenter.ts, GameRuntime.ts, GameRuntimeEvents.ts,
          SignalRService.ts, ApiService.ts, LobbyScene.ts, ResultScene.ts,
          CollectionViewerScene.ts, BattleHistoryScene.ts, MainMenuScene.ts,
          PreloaderScene.ts, App.tsx, StatusOverlay.tsx, AuthScreen.tsx,
          CollectionModels.ts, RewardSummaryFormat.ts, GameViewport.ts,
          scripts/standalone-web-smoke.mjs
Backend   CardCastExecutor.cs, EffectiveCardCost.cs, CardCostModifiers.cs,
          CardCostModifier.cs, CardDefinition.cs, CardCategory.cs, RelicResolver.cs,
          RelicDefinition.cs, RelicFiringPoint.cs, PassiveTracker.cs,
          StatusEffect.cs, StatusEffectLifecycle.cs, DamagePipeline.cs,
          DamageEvents.cs, PetState.cs, Player.cs, Pet.cs, PetTier.cs,
          PlayerStarterGrantFactory.cs, PlayerRepository.cs, PetRepository.cs,
          CardRepository.cs, IRelicRepository.cs, ICardRepository.cs,
          BattleResultService.cs, BattleStateService.cs, BattleStartService.cs,
          CardLoadoutService.cs, RelicLoadoutService.cs, CollectionQueryService.cs,
          CollectionController.cs, CollectionResponses.cs, BattleController.cs,
          AuthController.cs, BattleHub.cs, BattleEventWireProjection.cs,
          UnauthenticatedResponse.cs, DependencyInjection.cs (Application +
          Infrastructure), GameDbContext.cs, GameServer.Infrastructure.csproj
Migrations 20260929152651, 20261004055006, 20261004153916, 20260926151112,
          20261004094100, 20261003074309, 20261002090000, 20261001112446,
          20261005120735, 20260927130330, 20260927144250
```

### 8.3 Tests inspected (not modified)

```text
Client (20 files / 817 tests, all run)
  SceneLifecycle.test.ts (160) — the run-guard suites at :4564 (Result) and :4703
    (Lobby), the six-scene teardown sweep, the cast-input block at :3084-3200
    (including the injected card-inferno definition at :312)
  GameRuntime.test.ts (129), LobbyScene.test.ts (83), CollectionViewerScene.test.ts
    (55), BattleEventPresentation.test.ts (54), ResultScene.test.ts (51),
    BattleService.test.ts (49), SignalRService.test.ts (42),
    BattleHistoryScene.test.ts (34), CollectionService.test.ts (27),
    RuntimeBoundaries.test.ts (54 incl. 8 scene-file and 3 runtime-file it.each
    rules and the forbidden-term set at :452-477), GameViewport.test.ts (18),
    ApplicationSession.test.ts (12), PhaserGame.test.tsx (9),
    StatusOverlay.test.tsx (9), ViewportCss.test.ts (9), AppLifecycle.test.tsx (7),
    GameShell.test.tsx (6), PreservedLoadout.test.ts (6),
    ViewportDebugOverlay.test.tsx (3)

Backend (read)
  Cards   CardCastExecutorTests.cs, CardCastTurnAllowanceTests.cs,
          BattleStateServiceCardCastTests.cs, BattleHubCardCastTests.cs,
          BattleHubPetSkillCastTests.cs, CardEffectDefinition*Tests.cs
  Relics  RelicResolverTests.cs, Task177RelicFiringPoint*Tests.cs,
          RelicStageResolutionTests.cs, RelicWireProjectionTests.cs,
          CardCostRelicIntegrationTests.cs,
          RemainingMvpRelicDefinitionProvisioningTests.cs
  Passive PassiveTrackerTests.cs (:815 no-effect-summary pin),
          PassiveEventSourceTests.cs, BossPassiveEffectsTests.cs,
          StatusEffectTests.cs, StatusEffectLifecycleTests.cs
  Progression PlayerXpProgressionTests.cs, PetXpProgressionTests.cs,
          PlayerXpBattleRewardTests.cs, PetXpBattleRewardTests.cs,
          PlayerProgressionPersistenceTests.cs (:54-90 the discriminating test),
          PetProgressionPersistenceTests.cs, PlayerXpSchemaTests.cs,
          PetXpSchemaTests.cs
  API     CollectionEndpointTests.cs, BattleStartEndpointTests.cs,
          BattleResultEndpointTests.cs (:604-679 composes the real path),
          BattleHistoryEndpointTests.cs, AuthPlayerOwnershipTests.cs,
          PlayerStarterGrantFactoryTests.cs, CardLoadoutServiceTests.cs,
          RelicLoadoutServiceTests.cs, CollectionQueryServiceTests.cs,
          PetCardRelicDefinitionPostgresProvisioningTests.cs,
          BossDefinitionPostgresProvisioningTests.cs
```

### 8.4 Live browser behaviour inspected

```text
E2E run (this audit, current tree)
  RUN 1: 132 checks / 0 failures   RUN 2: 132 checks / 0 failures
  Key observations used as evidence:
    phase4.starterGrantLoaded                     {"pets":1,"cards":3,"relics":3}
    phase4.fiveBossesRendered                     the five canonical Bosses
    phase4b.serverAnsweredWithTheDocumentedEnvelope
                                                  {"error":"PET_NOT_OWNED",
                                                   "message":"A Pet must be
                                                   selected and it must be owned
                                                   by the player."}
    phase5b.noDeveloperDiagnosticsInTheHud         HUD text incl.
                                                   "Passive: passive-xich-lang"
                                                   (raw passive id on screen)
    phase5b.hudPetHpIsVisible / hudPetPowerIsVisible
                                                   "1000 / 1000", "0 / 100"
    phase6c.resyncCarriedHpAndPowerAsState_NoReplay
    phase6d.signatureSkillControlRemainsUsable     "CardCast card-inferno:
                                                   rejected (INSUFFICIENT_POWER)"
                                                   -> A-01 proven in a real battle
    phase7.terminalPetHpIsLabelledAsThePet         "Final Pet HP: 850"
    phase7b.rewardFailureIsNotAZeroReward
    phase10.durationIsRenderedFromTheDeliveredResult  "Duration: 10 turns" == 10
    phase10.serverOutcomeRendered / phase8.battle2OutcomeRendered  DEFEAT
                                                   (mechanical swap policy; not
                                                   balance evidence)
  Static artifacts re-inspected (gitignored, regenerated by the run)
    smoke-shots/run2-06b-battle-hud.png   the player-facing battle frame
    collection-viewer-shots/*, battle-history-shots/*, smoke-shots/*
```

### 8.5 Authoritative documents inspected

```text
AGENTS.md (and its docs/ duplicate)     §2, §4, §6, §7, §8, §10, §12, §13, §14,
                                        §15, §16, §17, §18, §20, §22
docs/00-overview/  GDD.md, MVP_SCOPE.md, ROADMAP.md
docs/01-game-design/ GAME_RULES.md (referenced), CARD_RULES.md, RELIC_RULES.md,
                     PASSIVE_RULES.md, COMBAT_RULES.md (relevant sections),
                     PET_RULES.md §3–§8, BOSS_RULES.md (referenced),
                     ELEMENT_RULES.md (referenced)
docs/02-technical/ API_CONTRACTS.md §1, §3, §4, §5.1–§5.6, §6;
                   SIGNALR_PROTOCOL.md §2, §3.2.13/§3.2.20/§3.2.23, §4 (incl.
                   items 4, 13, 14, 15, 16, 17), §4.3, §4.4, §7;
                   GAME_STATE.md §2.3–§2.3.7, §5.1; GAME_EVENTS.md §2 (referenced);
                   ARCHITECTURE.md §1, §2.2, §2.3, §5; TDD.md; DATABASE.md §1, §2,
                   §3, §5; REDIS_STATE.md §7
docs/03-decisions/ README.md (ADR index), ADR-001, ADR-002, ADR-003, ADR-008,
                   ADR-011, ADR-012, ADR-015, ADR-016, ADR-017, ADR-018, ADR-020,
                   ADR-021, ADR-022
tasks/            README.md, TASK_LIFECYCLE.md, TASK_TYPES.md
```

### 8.6 Completed task records inspected

```text
TASK-084 (starter ownership contract), TASK-191 (balance pass, Q-11 origin),
TASK-194 (baseline evidence), TASK-196 (decision evidence), TASK-198 (decision
integration), TASK-200 (Q-6/Q-7/Q-11), TASK-207, TASK-208, TASK-208A, TASK-210,
TASK-211-non-battle-screen-robustness-and-recovery,
TASK-211-post-task-210-product-audit
```

### 8.7 Prior claims this audit could not re-verify

```text
UNVERIFIED  the exact 817/783 historical test counts of TASK-210/211
            (this audit ran 817 on the current tree; the 783 figure is TASK-211's
            recorded baseline and was not reproduced)
UNVERIFIED  the production (DEV=false) build's freedom from the dev overlays —
            source-verified only; the E2E exercises the dev server and hides
            them with CSS
UNVERIFIED  backend test-suite pass state (2 919 tests) — not re-run; no backend
            file changed
UNVERIFIED  the EF Core 10.0.12 change-detection conclusion by execution — it is
            a reading of the pinned version's documented `DbContext.Entry`
            behaviour plus the existing passing test at
            PlayerProgressionPersistenceTests.cs:54-90; no EF/SQL run was performed
            during this audit
```

---

## 9. Explicitly Out of Scope for This Audit

* No gameplay, balance, UI, backend, contract, ADR, migration or documentation
  change.
* No resolution of the Pet progression / Passive conflict, the content-reachability
  question, the Signature Skill read source, the Tier/Star policy, the heal event,
  the crit representation, the battle-abandonment rule, or the XP atomicity
  question — each is reported with its owner and left for a decision
  (`AGENTS.md` §4/§7/§20).
* No reconstruction of the missing TASK-209 record, no renaming of the duplicate
  TASK-211 file, and no edit of any completed record.
* No re-litigation of anything TASK-203–TASK-211 shipped; their outcomes are
  treated as baseline and their corrections are reported, not edited.
* No new product requirement was invented: every "missing" item is traced either
  to a `docs/` statement or to a delivered-but-unused value.
* Unrelated pre-existing working-tree changes were left untouched: the 26 modified
  tracked files (TASK-208A/209/210/211 deliverables) and the 6 untracked task
  records are byte-identical before and after this audit.

---

## 10. Final Report

```text
TASK-212 AUDIT COMPLETE

Decision: KEEP | MODIFY | SPLIT | DROP | REPLACE  ->  SPLIT

  212A  descriptive read-contract exposure (API_CONTRACTS §5.3/§5.4) — recommended next
  212B  in-battle cost/affordability (SIGNALR_PROTOCOL §4) — deferred behind TASK-213

Recommended next task: TASK-212A
Implementation: NOT DONE
Production changes: NONE
Test changes: NONE
Audit record: tasks/completed/TASK-212-post-task-211-product-audit.md
```
