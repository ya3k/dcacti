# TASK-219A-A — Post-Implementation Signature Skill Contract Audit

**Type:** AUDIT ONLY — no production code, test, E2E script, contract document,
migration, or historical task record is modified by this task.
**Subject:** TASK-219A — Signature Skill identification / protocol correction.
**Decision authority for the contract under audit:**
`tasks/completed/TASK-213-content-reachability-decision.md` §5 (Product Owner
decision, "Signature Skill is IN; the defect is the identification source").
**Status:** Audit complete — see §11.

---

## 0. Method, evidence base, and limitations

### 0.1 What was audited

The chain, end to end:

```text
docs/00-overview/MVP_SCOPE.md §1            (IN / OUT)
docs/01-game-design/CARD_RULES.md §1/§4.1   (the Card model)
docs/01-game-design/PET_RULES.md §8         (5 Pets, one Signature Skill each)
docs/02-technical/API_CONTRACTS.md §5.1/§5.2/§5.3/§5.6
docs/02-technical/SIGNALR_PROTOCOL.md §2 / §3.2.20-§3.2.22 / §4.3 item 13 / §7
docs/02-technical/DATABASE.md §1/§2
docs/02-technical/GAME_STATE.md §2.3
docs/03-decisions/ADR/ADR-012, ADR-021
        ↓
PetDefinition.SignatureSkillCardId → CardDefinition
        ↓
PetSignatureSkillItem → PetCollectionItem → PetSignatureSkillResponse
        ↓
CollectionQueryService.ListPetsAsync / GetOwnedPetAsync
        ↓
ICardRepository.ListDefinitionsAsync → CardRepository.ListDefinitionsAsync
        ↓
GET /api/pets, GET /api/pets/{petId}
        ↓
BattleScene.loadPetCatalog → signatureSkills map → renderCastControls
        ↓
PetSkillCast (canonical) / CardCast (Basic, and the fail-closed path)
        ↓
tests/backend/** · src/frontend/client/tests/** ·
src/frontend/client/scripts/standalone-web-smoke.mjs
```

### 0.2 Evidence base (read-only)

```text
HEAD                        afe5b14  (master)
Worktree at audit time      54 modified tracked + 18 untracked, 0 deleted
                            (72 entries; `git diff --check` exit 0)
Git operations used         log, branch --show-current, status, show, diff --stat
                            (read-only; nothing staged, committed, stashed,
                            reset, cleaned, or checked out)
Task records read           TASK-213 §5/§9/§11, TASK-221A §9/§10/§11,
                            TASK-221, TASK-223, TASK-224 (backlog),
                            TASK_LIFECYCLE.md, tasks/README.md
Delegated read-only review  two scoped reviewers (ownership/reconnect;
                            tests/E2E); every load-bearing claim below was
                            re-read at the cited site by this audit
```

### 0.3 Limitations (stated, not hidden)

1. **TASK-219A is not separable by diff.** All `TASK-207 → TASK-223` work is
   uncommitted on one worktree, so no git command can isolate TASK-219A's edits
   from TASK-221's (five-Pet / ten-Relic grant), TASK-208A's (state projection),
   or TASK-210/211's (battle presentation). Every verdict below is therefore
   derived from **current code + authoritative documents + call sites**, not
   from a change set. Where a claim could only hold "at the time TASK-219A was
   written", it is not made.
2. **No live PostgreSQL, Redis, or browser run.** The 147-check E2E number is
   verified statically from the script (§7.6). The one test that asserts 5/5
   against a live database skips silently without one
   (`PetCardRelicDefinitionPostgresProvisioningTests.cs:396-397`) — recorded as
   a coverage fact (C-1), not as a failure.
3. **No test suite was executed.** This audit changes no executable artifact, so
   run status is not part of its evidence; test *content* was audited instead.

---

## 1. Contract consistency (audit scope 1)

### 1.1 The two TASK-219A document changes are the right two

| Document | Version | Why it is the owner |
|---|---|---|
| `docs/02-technical/API_CONTRACTS.md` | 1.19 | Owns the §5 collection read member set. §5.1 gains `signatureSkill`; §5.3 states the unchanged membership; §5.6 states it is not an equip member. |
| `docs/02-technical/SIGNALR_PROTOCOL.md` | 2.18 | Owns the client identification rule as a protocol matter (§4.3 item 13) because the identification feeds the cast dispatch. |

No third document is required, and none of the other modified documents in the
worktree carries a 219A edit: `MVP_SCOPE.md` is 1.6 (TASK-213),
`GAME_STATE.md` is 2.25 (TASK-208A), `DATABASE.md` records TASK-169/172/173/221,
`BOSS_RULES.md` records TASK-172/173. That matches the decision's own assignment
(`TASK-213:390`) exactly.

### 1.2 `signatureSkill` is consistently defined on every Pet read surface

| Surface | Statement | Verdict |
|---|---|---|
| `API_CONTRACTS.md` §5.1 member table (`:747-760`) | `signatureSkill` object = `PetDefinition.SignatureSkillCardId` resolved through the `CardDefinition` it references | ✓ |
| §5.1 nested table (`:765-776`) | `cardId` = the FK; `name` = that definition's `Name`; `category` = that definition's `Category` as §5.3 spells it | ✓ |
| §5.1 rule list items 1–7 (`:778-821`) | derived-not-owned; §5.3 must not be widened; always present, never `null`; no cost/affordability/legality/`effectDefinition`; it is the client's identification source *and the only one*; §5.2 is the same object; no new definition-identity member | ✓ |
| §5.2 (`:849-868`) | same bare object, "it therefore carries §5.1's `signatureSkill` member too, with the same members and the same rules; this section defines no shape of its own" | ✓ |
| §5.3 (`:961-967`) | "a `PetSkill` `CardDefinition` is still never an unlock row … **The Pet's Signature Skill reference is delivered by §5.1's `signatureSkill`, never here**" | ✓ |
| §5.6 (`:1091-1093`) | "`signatureSkill` (§5.1) is not an equip member" | ✓ |
| `SIGNALR_PROTOCOL.md` §4.3 item 13 (`:1867-1914`) | identification from the delivered Pet reference; not from a `Category` read out of the owned Card collection; derived, not owned; §5.3 unchanged; `PetSkillCast` canonical; `CardCast(<signature cardId>)` remains protocol-conformant *and implemented*; Basic Cards keep `CardCast` | ✓ |

`cardId`/`name`/`category` semantics match the authoritative contract in both
directions: the wire example (`API_CONTRACTS.md:736-740`) is the Xích Lang /
`card-inferno` / Inferno / `PetSkill` triple that
`docs/01-game-design/CARD_RULES.md` §4.1 authors, and the deeper owners are
referenced rather than restated (§5.1 cites `PET_RULES.md` §8 and
`CARD_RULES.md` §4 item 1 for existence and count; §5.3 owns the category
spelling; `DATABASE.md` §1/§3 owns the stored member names).

### 1.3 No contradictory sentence remains in the authoritative documents

Checked and clean:

```text
"The client maintains no local registry and performs no card validation"
  → replaced by what the client actually does: holds no AUTHORITATIVE Card
    registry, renders only delivered values, validates no Card
    (SIGNALR_PROTOCOL.md:16-17, :1876-1884). Accurate, not merely softened:
    BattleScene does hold a delivered `cardDefinitions` map
    (BattleScene.ts:395) for Basic Card names, which is exactly why the old
    sentence was false (TASK-213 §11.2 N-04).

"category === 'PetSkill'" as an identification rule
  → survives only as the DESCRIPTION of the removed rule in the 2.18 version
    header (SIGNALR_PROTOCOL.md:8) and as the explicit prohibition in item 13
    (`:1894-1898`). No normative text identifies by category anywhere.

Card membership
  → API_CONTRACTS §5.3, DATABASE.md §2, MVP_SCOPE.md §1 (:112-113),
    CARD_RULES.md §1 item 4 agree: the Signature Skill is never an unlock row
    and the array is the unlocked state.
```

Cross-document spot checks that could have contradicted the correction but do
not: `GAME_EVENTS.md` §2 (`:547-584`, `PetSkillCast`'s confirmation carried by
`cardId`, no dedicated member), `SIGNALR_PROTOCOL.md` §3.2.21 item 2 (same),
`CARD_RULES.md` §6 (`:487-491`, `CardCast` for every successful cast plus
`PetSkillCast` for the Skill), `ARCHITECTURE.md` §2.2.1 (`:294-306`, both cast
methods implemented), `ADR-012` item 9/10 (unlock flags; battle-scoped equip).

**No new vocabulary, endpoint, request member, query parameter, header,
pagination member, cost/affordability/legality member, `effectDefinition` copy,
ownership row, schema, index, migration, Domain type, SignalR member, event, or
Redis contract** was introduced — asserted by the two version headers and
verified independently in §2.5 and §3.4 below.

**Verdict §1:** consistent. The correction is stated on the owning surfaces, the
membership rule is explicitly protected from widening, and the canonical /
conformant dispatch split is stated once in each owning document.

---

## 2. Backend projection (audit scope 2)

### 2.1 The chain, traced

```text
PetDefinition.SignatureSkillCardId        Domain/Pets/PetDefinition.cs:115
        ↓ (required FK; EF mapping)
                                        Configurations/PetDefinitionConfiguration.cs:92-99
                                        (HasOne<CardDefinition>().WithMany()
                                         .OnDelete(Restrict); no navigation property)
CardDefinition                            resolved by the collection projection, below
        ↓
PetSignatureSkillItem                     Application/Collection/PetSignatureSkillItem.cs:44
        ↓
PetCollectionItem.SignatureSkill          Application/Collection/PetCollectionItem.cs:61-68
        ↓
CollectionQueryService.ListPetsAsync      Application/Collection/CollectionQueryService.cs:98-133
CollectionQueryService.GetOwnedPetAsync   :164-194
CollectionQueryService.Project            :444-501
        ↓
PetSignatureSkillResponse                 Api/Controllers/CollectionResponses.cs:141-153
PetResponse.From                          :90-102
        ↓
GET /api/pets                             Api/Controllers/CollectionController.cs:95-112
GET /api/pets/{petId}                     :141-175
```

### 2.2 Direct mapping from existing authored data, one source of truth

`Project` reads the reference one-to-one from the FK and copies the referenced
definition's own stored values (`CollectionQueryService.cs:497-500`):

```text
new PetSignatureSkillItem(
    signatureSkill.CardDefinitionId,     ← re-read from the definition row, not
                                           re-typed from the FK value
    signatureSkill.Name,                 ← CardDefinition.Name
    signatureSkill.Category.ToString())  ← CardDefinition.Category, wire name
```

Nothing is composed, defaulted, recomputed, or duplicated. `PowerCost`,
`LoadoutCopyLimit`, and `EffectDefinition` are neither read nor copied, which is
what keeps §5.1 item 4's "no cost, no affordability, no cast-legality, no
`effectDefinition`" true structurally rather than by convention.
`PetDefinition.SignatureSkillCardId` remains the single source of the *identity*
and the referenced row remains the single source of the *name* and *category* —
no second representation of either exists on the client or in the DTO.

### 2.3 No N+1

`ListPetsAsync` issues exactly three queries regardless of collection size:

```text
1. owned instances        IPetRepository.ListByPlayerIdAsync      (Player-filtered)
2. PetDefinition rows     IPetRepository.ListDefinitionsAsync     (one IN query)
3. CardDefinition rows    ICardRepository.ListDefinitionsAsync    (one IN query)
```

The Signature Skill read is the third, and it is a **bulk** read over the
distinct `SignatureSkillCardId` set (`CollectionQueryService.cs:406-409`,
`:418-420`). With the MVP's five Pets sharing five distinct Skill Cards, a
per-Pet `GetDefinitionAsync` would have issued up to five queries; the bulk read
issues one. Per-Pet `Project` calls are dictionary lookups (`:464-471`).

### 2.4 Allocation and scope of the new repository member

```text
ICardRepository.ListDefinitionsAsync      Application/Cards/ICardRepository.cs:127-129
CardRepository.ListDefinitionsAsync       Infrastructure/Postgres/Repositories/CardRepository.cs:98-120
```

Behaviour: `CardDefinitions.Where(d => cardDefinitionIds.Contains(d.CardDefinitionId))`
— **no `playerId`, no `PlayerUnlockedCard` join**, no ordering promise, a
non-existent id simply absent, no placeholder fabricated.

**Classification: NECESSARY AND CORRECTLY SCOPED.** Justification, verified
rather than assumed:

1. **It is genuinely needed.** `PetDefinitionConfiguration.cs:96-99` maps the FK
   as `HasOne<CardDefinition>().WithMany()` **without a navigation property**, so
   the Pet-definition read cannot `Include` the Skill Card; `name` and
   `category` require the referenced row. The alternative to a bulk read is a
   per-Pet `GetDefinitionAsync` loop (N+1), and the alternative to *either* is a
   domain/EF mapping change (adding a navigation + `Include`) — a larger change
   to the Pet boundary than one content read.
2. **It is the existing pattern, not a new abstraction.** `IRelicRepository`
   already exposes the same shape for the same purpose
   (`Application/Relics/IRelicRepository.cs:116`, consumed by
   `CollectionQueryService.ListRelicsAsync` at `:304-306`). The Card boundary
   gains its analogue; no generic repository, cache, read model, or second
   abstraction layer is introduced.
3. **Its scope is the boundary's own.** It takes no `playerId` because a
   Signature Skill has no unlock row and an ownership filter would report it
   absent for the very Player reading their own collection
   (`ICardRepository.cs:109-116`) — and that is precisely the read the existing
   ownership-checked member (`ListUnlockedDefinitionsAsync`) must *not* be used
   for. Ownership scoping of `GET /api/pets` stays on the owned-instance read.
4. **No schema cost.** No index, migration, cache, or stored procedure; it runs
   against the existing `CardDefinition` table. `DATABASE.md` §4 lists no new
   index, and none exists in the tree.

It is therefore classified **necessary and correctly scoped**, not an
unnecessary abstraction and not an architectural concern requiring a follow-up
task.

### 2.5 No endpoint / schema / migration / index / ownership change

| Check | Evidence | Verdict |
|---|---|---|
| Endpoint routes | `CollectionController` still exposes exactly `pets`, `pets/{petId}`, `cards`, `relics` | unchanged ✓ |
| Wire members | `PetResponse` = 7 members (`CollectionResponses.cs:75-83`); `PetSignatureSkillResponse` = 3 (`:141-144`) | exactly §5.1 ✓ |
| Migrations | `Migrations/` contains no 219A file; the newest are TASK-168/172/173/221-era | none added ✓ |
| Indexes | no new index; `DATABASE.md` §4 unchanged in this respect | none ✓ |
| Domain types | `PetDefinition` / `CardDefinition` unchanged; only a new **read model** (`PetSignatureSkillItem`, Application layer) and a new **response** alias | no Domain change ✓ |
| Ownership rows | no writer added (§3) | none ✓ |
| Cost behaviour | no cost/affordability member or computation; `CardCostModifiers` untouched | none ✓ |

`signatureSkill` is always emitted: `PetCollectionItem.SignatureSkill` is a
required non-nullable record member, `PetSignatureSkillResponse.From` throws on
`null`, and the API's serializer options do not suppress nulls
(`Program.cs:153` sets only `WriteIndented`) while every member carries an
explicit `[JsonPropertyName]`. The contract's "always present, never `null`,
never omitted" (`API_CONTRACTS.md:795-802`) therefore holds by construction.

### 2.6 Unresolved-FK posture is consistent with the existing contract

`Project` throws `InvalidOperationException` for (a) a Pet whose definition row
is absent and (b) a definition whose `SignatureSkillCardId` does not resolve
(`CollectionQueryService.cs:449-471`). This is the posture §5.1 item 3 states
("a broken `DATABASE.md` §1 state rather than a contract case … refused rather
than answered with a fabricated or placeholder reference") and it is byte-for-byte
the posture §5.4 already took for an unresolvable `RelicDefinition`
(`:326-332`) before this task. No new error code, envelope, or status is
invented, and the failure is not silently converted into the optional-member
shape the contract forbids.

**Verdict §2:** the projection is a direct, single-source, bulk projection of
existing authored data; the new repository member is necessary, correctly
scoped, and precedent-following; no endpoint, schema, index, migration,
ownership, or cost behaviour was introduced.

---

## 3. Ownership semantics (audit scope 3)

### 3.1 Signature Skill creates no `PlayerUnlockedCard` row — by call site

Exhaustive search over `src/` for every construction and insertion site:

```text
Production writers of PlayerUnlockedCard rows       2, and only 2
  PlayerRepository.StageStarterOwnership            Repositories/PlayerRepository.cs:152-162
    (creation path only; counts come from the grant, no literal)
  CardRepository.AddUnlockAsync                     Repositories/CardRepository.cs:38-44

Production callers of AddUnlockAsync                0
  (interface: ICardRepository.cs:48; implemented; referenced only by test fakes)

Other acquisition verbs                            0 matches
  (AddOwnedCard | UnlockCard | GrantCard | AcquireCard over src/)

Migrations writing ownership rows                   0
  the only PlayerUnlockedCard data operation in any migration is a DELETE
  (20261005120735_AddAccountsTableAndDropDiscordUserId.cs:24)

New ownership model                                 0
  GameDbContext DbSets are unchanged in kind: Account, Player, Pet,
  PetDefinition, Relic, RelicDefinition, CardDefinition, PlayerUnlockedCard,
  BossDefinition, BattleResult — no Pet-Skill table, no second unlock table
```

The one place where a Signature Skill *could* have created ownership is the
projection itself, and it does not: `ListPetsAsync`/`GetOwnedPetAsync` are reads
with no `SaveChanges`, and the Skill read is the **unrestricted content** read,
deliberately not `ListUnlockedDefinitionsAsync` (`CollectionQueryService.cs:390-399`).

### 3.2 The starter grant still owns only the intended Basic Cards

```text
PlayerStarterGrantFactory.StarterCardDefinitionIds       :99-104
  ["card-heal", "card-shield", "card-power-charge"]
```

Exactly the three content-defined Basic Cards of `CARD_RULES.md` §2, which is
what `DATABASE.md` §2 item 1 states ("3 `PlayerUnlockedCard` rows … *Excluded:*
Pet Skill Cards … are never granted as unlocked Basic Cards") and what
`MVP_SCOPE.md` §1 restates ("3 Basic Cards all three, as `PlayerUnlockedCard`
unlock rows"). The grant resolves each definition through the unrestricted
`GetDefinitionAsync` and fails the whole grant if any row is missing
(`:190-212`, `:293-312`), so no partial or substituted set can be staged.

Creation-only is structural, not conventional: `AuthController` is the sole
caller of the composition (`AuthController.cs:99-102`, `:143-146`), and
`PlayerRepository.GetOrCreateForAccountAsync` returns an existing Player before
composing (`:36-39`).

### 3.3 `GET /api/cards` remains ownership-filtered

```text
CollectionController.Cards            Controllers/CollectionController.cs:197-210
        ↓
CollectionQueryService.ListCardsAsync CollectionQueryService.cs:213-253
        ↓
ICardRepository.ListUnlockedAsync     CardRepository.cs:47-63
  PlayerUnlockedCards.Where(uc => uc.PlayerId == playerId)
      .Join(CardDefinitions, ...)
```

No unrestricted definition read is on this path, so a `PetSkill` definition
cannot reach the array through it. The array is *definition-generic* by design —
membership is the Player's rows, not a category filter — and the guarantee that
no `PetSkill` row exists to be projected rests on §3.1's absence of writers.
That is the contract's own shape (§5.3: "presence in this array is the unlocked
state"), not a weakening.

The production path is proven, not just the unit path:
`AuthStarterOwnershipTests.RegisteringANewAccount_ShouldGrantTheDocumentedThreeBasicCards`
(`:103-122`, exactly three rows, all Basic) and
`RegisteredAccount_CanQueryStarterCollectionsImmediately`
(`:235-255`, `/api/pets` = 5, `/api/cards` = 3, `/api/relics` = 10 through the
real auth and controller path).

### 3.4 `/api/pets` resolves the Signature Skill independently of ownership

Proven by the test the contract exists for
(`CollectionQueryServiceTests.cs:229-262`): the Card collection is asserted
**empty** (`:250`), the Skill Card is asserted to have **no unlock row**
(`:253-255`), and the Pet still reports `card-inferno` / `Inferno` (`:260-261`).
The endpoint-level twin asserts the same across the real controller and store,
including that the definition exists while the unlock row does not
(`CollectionEndpointTests.cs:211-260`: `Assert.Equal(new[] { "card-heal" }, cardIds)`,
`Assert.DoesNotContain("card-inferno", …)`, `CardDefinitionExistsAsync("card-inferno") == true`,
`UnlockExistsAsync(owner, "card-inferno") == false`).

### 3.5 No hidden post-creation acquisition path

```text
Routes (API_CONTRACTS.md §1)         no granting endpoint; the nine documented
                                     routes are auth, battle start/state/result/
                                     history, and the four §5 reads
Hub methods (SIGNALR_PROTOCOL.md §2) Swap, CardCast, PetSkillCast, JoinBattle,
                                     GetBattleState, Ping — none grants content
Writers of any ownership row         1 (creation), see §3.1
DATABASE.md §2 item 2                "Existing Player: … performs no starter
                                     grant … Not a repair / top-up mechanism"
MVP_SCOPE.md §1                      "MVP has NO acquisition system"
```

`BattleStartService` snapshots the derived fourth entry into
`PetState.EquippedCards[]` and writes no ownership (`GAME_STATE.md` §2.3;
`BattleStartService.cs:354` records the same boundary).

**Verdict §3:** the Signature Skill is non-owned by construction and by test;
`/api/cards` is ownership-only; `/api/pets` resolves the Skill without
ownership; no post-creation acquisition path exists. No defect found.

---

## 4. Frontend identification (audit scope 4)

### 4.1 The runtime path, traced

```text
create()                                   BattleScene.ts:419-441
  void loadCardDefinitions()               :424  → GameRuntimePort.getCards()
  void loadPetCatalog()                    :425  → GameRuntimePort.getPets()
        ↓                                            ↓
GameRuntime.getPets()                      GameRuntime.ts:776-778
        ↓
ApiService.getPets()                       services/api/ApiService.ts:265-266
        ↓
GET /api/pets                              (the contract's §5.1 read)
        ↓
petCatalog.set(petId, pet)                 BattleScene.ts:1248
signatureSkills.set(pet.signatureSkill.cardId, pet.signatureSkill)
                                           :1254
        ↓
renderCastControls(state)                  :1294-1349
  signatureSkill = signatureSkills.get(cardId)          :1313
  isPetSkill     = signatureSkill !== undefined         :1314
  label          = isPetSkill ? `Skill: name` : `Card: name`  :1315-1316
  dispatch       = isPetSkill ? submitPetSkillCast()
                              : submitCardCast(cardId)  :1336-1341
        ↓
submitPetSkillCast() → runtime.requestAction({kind:'PetSkillCast'})  :1687-1713
submitCardCast(id)   → runtime.requestAction({kind:'CardCast', id})  :1655-1682
        ↓
GameRuntime.requestAction → SignalRService.petSkillCast / cardCast
        ↓
BattleHub.PetSkillCast / BattleHub.CardCast
```

The identification is a **value comparison against delivered data**, keyed by
`cardId`, with no positional assumption, no local registry of content, and no
per-Pet or per-card literal.

### 4.2 The §4 checklist, item by item

| Check | Finding |
|---|---|
| Remaining `Category == PetSkill` identification logic | **NONE.** The scene never reads `category` at all; a comment-stripped source scan asserts `.category` and `category ===` are absent and `signatureSkills.get(` is present (`SceneLifecycle.test.ts:3440-3447`). A same-tree grep confirms `category` appears in `BattleScene.ts` only in documentation prose. |
| Hard-coded Pet/card IDs | **NONE.** Ten canonical ids are asserted absent from the scene's non-comment source (`SceneLifecycle.test.ts:3425-3438`); grep over `src/frontend/client/src` finds the ids only inside documentation comments (`CollectionModels.ts:66`, `:126`). |
| Assumption that the Skill exists in `GET /api/cards` | **NONE.** The Card map is explicitly *not* an identification source (`BattleScene.ts:1185-1191`), and the map's only consumer for names is the fallback `cardDefinitions.get(cardId)?.name` (`:1315`) which is irrelevant for the Skill. |
| Dependence on a local registry | **NONE** in the sense the contract forbids. The scene holds delivered values only (the §5.1/§5.3 responses); it holds no authored content, no definition→effect catalogue, and computes no cost or legality (`AGENTS.md` §10). |
| Dependence on a particular Pet | **NONE.** `loadPetCatalog` maps **all** owned Pets' references (`:1247-1255`), so any of the five resolves; no single-Pet fixture is privileged. |
| Stale `isPetSkill` logic | **NONE.** `isPetSkill` is a local boolean derived from the delivered reference (`:1314`) — a naming holdover, not stale logic. It no longer consults a category. |
| Incorrect fallback behavior | **FAILS CLOSED, AS DECIDED.** An entry matching no delivered reference renders on the ordinary Card path and dispatches `CardCast(cardId)` (`:1336-1341`), and an unresolved display name falls back to the delivered identity, never a guessed name (`:1315`, `:2100-2106`). TASK-213 §5 prescribes exactly this: "It must fail closed to the raw `cardId` when the datum is absent … and must never guess a name." `CardCast(<signature cardId>)` remains server-conformant, so the fallback is functional, not broken. |

### 4.3 Dispatch split, verified in code and in tests

```text
Basic Card   equippedCards entry with no delivered match → CardCast(cardId)
Signature    equippedCards entry equal to a delivered signatureSkill.cardId
             → PetSkillCast (no identifier), the canonical client request
```

Client-side: `SceneLifecycle.test.ts:3450-3461` (`CardCast card-heal`),
`:3463-3474` (`PetSkillCast`, no cardId), `:3334`, `:3364`, `:3409`.
Runtime/transport: `GameRuntime.test.ts:2060-2075` (`[battleId,'card-heal',seq]`)
and `:2085-2098` (`PetSkillCast` with exactly two arguments),
`SignalRService.test.ts:521-547` (the documented hub method and argument
count). Server-side conformance of the fallback path is independently covered by
`BattleHubPetSkillCastTests` and by `CardCastExecutor` accepting an equipped
`PetSkill` id (`CardCastExecutor.cs:36-59`).

**Verdict §4:** identification comes from the delivered Pet read and from
nothing else; dispatch is `PetSkillCast` for the Skill and `CardCast` for Basic
Cards; the fallback is the decided fail-closed path. No defect found.

---

## 5. Reconnect / resync (audit scope 5)

### 5.1 What survives, and from what source

| Moment | Does identification survive? | How |
|---|---|---|
| Initial battle state (join push) | yes | The scene's map is filled from the durable `GET /api/pets` read issued at scene create; the join push supplies `equippedCards`, which the map is matched against. |
| Transport reconnect | yes | The map is **scene memory** and is never invalidated by a reconnect; `GetBattleState` re-renders the cast controls from the recovered `equippedCards` and never touches the Pet read (`GameRuntime.recoverBattleState`, `GameRuntime.ts:665-707`; the scene's `onBattleState` handler, `BattleScene.ts:435-437`). |
| `GetBattleState` resync | yes | §7's snapshot is the same projection as the §4 push member-for-member (`SIGNALR_PROTOCOL.md:2271-2300`), so `equippedCards` re-arrives and re-renders; the identification itself is not replayed from events. |
| PLAY AGAIN | yes | `ResultScene` → `LobbyScene` → `BattleScene` shutdown clears the map (`BattleScene.ts:568`) and a fresh `create()` re-reads `/api/pets` (`:425`, `:1238-1254`). |
| Document reload | n/a | No battle can be resumed after a reload — nothing persists a battle id (`GameRuntime.ts:668`); the requirement is not reachable, so nothing is claimed about it. |

### 5.2 No transient-only state is required

This is the audit's central §5 question, and the answer is clean: the
identification source is a **durable REST read of persistent content**
(`PetDefinition.SignatureSkillCardId` → `CardDefinition`), re-obtainable at any
time through `GET /api/pets`. It is not derived from a Battle Event, not
reconstructed from `BattleState`, not carried by an event batch (which §7 item 2
discards on resync), and not delivered only on the join push. A client that
holds only the recovered state plus the Pet read can always identify the Skill.

Coverage limitation (recorded as C-4, not a defect): the scene-level "reconnect"
test drives the harness state setter rather than the real runtime recovery path
(`SceneLifecycle.test.ts:3367-3410`), and no test asserts that a re-created
BattleScene re-reads `/api/pets` after PLAY AGAIN. The real transport leg is
covered by the E2E (§7.4) and the runtime leg by `GameRuntime.test.ts:1560-1700`.

### 5.3 Two findings, deliberately classified apart from the audited contract

**O-1 — transient presentation window on scene create (non-blocking, not a
contract defect).** `create()` fires `loadPetCatalog()` un-awaited
(`BattleScene.ts:424-425`) and renders the cast controls synchronously, so until
the `/api/pets` response lands the Skill tile shows `Card: <raw cardId>` and
would dispatch `CardCast`. If the read rejects, the map stays empty for the
whole battle and that state persists (`:1260-1263` swallows the failure). This
is **not** a contract violation: the contract fixes *which source* the client
identifies from and TASK-213 §5 explicitly prescribes failing closed to the raw
`cardId` when the datum is absent; `CardCast(<signature cardId>)` stays
server-conformant so the game remains correct and fully playable. It is a
presentation-latency and robustness observation whose *coverage* gap (no test
for a failing Pet read, C-3) belongs with the other verification residuals.
Attribution: TASK-219A (its own new read path); severity: non-blocking.

**P-1 — reconnect group re-join is unstated and unimplemented (pre-existing, out
of scope, NOT attributable to TASK-219A).** `SIGNALR_PROTOCOL.md` §3 item 3
says the group mechanism "is what allows a reconnect to simply rejoin"
(`:358-361`), but §7's reconnect flow defines only `GetBattleState(battleId)`
(`:2260-2266`). In the code, neither side re-joins: `BattleHub.OnConnectedAsync`
re-adds no group (`BattleHub.cs:807-823`) and `JoinBattle` is the only
`AddToGroupAsync` site (`:866`); the client's `onReconnected` handler calls only
`recoverBattleState()` (`GameRuntime.ts:194-217`), and `SignalRService` merely
forwards the transport callbacks (`SignalRService.ts:620-630`). Because SignalR
group membership is per-connection, a post-reconnect group broadcast
(`BattleStateUpdated`, `ReceiveEvents`) may not reach the reconnected
connection; the E2E's reconnect phase reconnects for real but never performs an
action afterwards, so it cannot observe this either way
(`standalone-web-smoke.mjs:2789-2812`).

```text
Contract boundary
  Owner of the reconnect flow            SIGNALR_PROTOCOL.md §7
  Owner of the group/delivery mechanism  SIGNALR_PROTOCOL.md §3, §4.1
  The gap                                §3's parenthetical implies a rejoin that
                                         §7 never requires of the client and no
                                         implementation performs. It is a missing
                                         rule + a missing implementation, not a
                                         conflict between two rules.
Smallest follow-up
  A new decision-then-implementation task on "who rejoins the battle group after
  a reconnect" (client re-invokes JoinBattle, or the server re-adds on connect),
  with one protocol sentence added to §7. Next unreserved id per
  tasks/README.md §3 (TASK-224 is in backlog; TASK-225 is reserved by
  TASK-223 §10 F-3).
Attribution
  Pre-existing; independent of the Signature Skill contract. It does not change
  this audit's verdict because identification does not depend on group
  membership (§5.2). Reported, not fixed, and not counted against TASK-219A.
```

**Verdict §5:** Signature Skill identification survives initial state,
reconnect, `GetBattleState`, and PLAY AGAIN, and requires no transient-only
state. O-1 is a non-blocking robustness/coverage observation on TASK-219A's own
path; P-1 is a pre-existing, separately classified reconnect defect outside the
audited contract.

---

## 6. Test quality (audit scope 6)

Verdicts are about **what the tests prove**, read at the assertion sites.

| # | Property | Verdict | What actually proves it |
|---|---|---|---|
| a | All 5 provisioned Pets | **Covered (5/5), spread across layers** | Projection: `CollectionQueryServiceTests.cs:174-226` — a five-entry `pet-* → card-*` table (`:182-186`) asserted per Pet (`:216-218`) *and* one-bulk-read assertion (`:221-225`). Provisioned content: `PetCardRelicDefinitionProvisioningTests.cs:333-343` (3) + `ThanhXaAndSonHungSignatureSkillProvisioningTests.cs:289-295` (2) assert the five `SignatureSkillCardId` values as migration text; `PetCardRelicDefinitionPostgresProvisioningTests.cs:402-422` asserts them against a live database when one exists. Client: `SceneLifecycle.test.ts:3281-3336` loops all five and asserts caption + dispatch each time. Gap: the API layer asserts **1/5** (`CollectionEndpointTests.cs:133-260`), and the E2E exercises **1/5** (§7.2) — recorded as C-1. |
| b | Pet-derived identification | **Proven** | `SceneLifecycle.test.ts:3242-3279`: the delivered Card read is asserted to be exactly the three Basics (`:3268-3272`), asserted to contain **no** `PetSkill` category (`:3273`) and **not** `card-inferno` (`:3278`), while the Skill is still identified and captioned `Skill: Inferno` (`:3262`). Backend twins: `CollectionQueryServiceTests.cs:229-262`; `CollectionEndpointTests.cs:211-260`. |
| c | No synthetic `PetSkill` row in Card ownership | **Client harness: proven clean. Backend/API harnesses: synthetic rows remain (production-unreachable) — deferred scope, classified as C-6** | Clean: `SceneLifecycle.test.ts:309-338` returns only the three Basic Cards and says why a synthetic row "would certify behaviour production cannot reach"; `CollectionEndpointTests.cs:1451-1464` + `:233-234`; `CollectionQueryServiceTests.cs:1109-1135`; `BattleResultSmokeTest.cs:487-524`; `PlayerStarterGrantFactoryTests.cs:224-243` names all five Skill ids and asserts none is granted; `TestStarterGrants.cs:69-76`. Residual synthetic unlock rows: `CollectionEndpointTests.cs:803/812` and `:840/851-855`; `CollectionQueryServiceTests.cs:530/536-538`, `:596-597/600`; `ApplicationSessionRESTTests.cs:667`; `BattleStartSmokeTest.cs:299`; `BattleStartEndpointTests.cs:858`; `RedisBattleStateSmokeTest.cs:554`; client `LobbyScene.test.ts:154-161` + `:612-619`, `CollectionViewerScene.test.ts:568/749/795`. Each serves a *different* purpose (wire-category spelling; the loadout validator's "unlocked but `PetSkill`, still rejected" defence; "the Lobby never submits a `PetSkill` card") and **none of them is on the Signature Skill identification path** — but they are exactly the fixture-realism residual TASK-213:609 assigned to TASK-219B. |
| d | Basic Card → `CardCast` | **Proven** | `SceneLifecycle.test.ts:3450-3461` and `:3534`; `GameRuntime.test.ts:2060-2075`; `SignalRService.test.ts:521-522`. E2E `:2540-2544` is weaker than it reads (see V-2). |
| e | Signature Skill → `PetSkillCast` | **Proven** | `SceneLifecycle.test.ts:3463-3474` (payload is exactly `{kind:'PetSkillCast'}`, no cardId), `:3334`, `:3364`, `:3409`; `GameRuntime.test.ts:2085-2098` (exactly two arguments); `SignalRService.test.ts:527-547`; `RuntimeBoundaries.test.ts:628-641`; server `BattleHubPetSkillCastTests.cs:134-174` asserts the accepted response, the `CardCast` → `PetSkillCast` order, and the wire `type`. |
| f | No hard-coded mapping | **Proven for the audited file; single-file scope** | `SceneLifecycle.test.ts:3412-3448` strips comments from `BattleScene.ts` and asserts ten canonical ids are absent, `.category` / `category ===` are absent, and `signatureSkills.get(` is present. Gap: only `BattleScene.ts` is scanned (C-5); the property does hold tree-wide today (ids appear only in documentation comments). |
| g | Card-read failure does not break identification | **Proven (Card read). Not covered (Pet read)** | `SceneLifecycle.test.ts:3338-3365` replaces `runtime.getCards` with a rejecting mock and still asserts captions `[Card: card-heal, Card: card-shield, Card: card-power-charge, Skill: Inferno]` and `[{kind:'PetSkillCast'}]`. The Pet read *is* the identification source and its failure path is untested (C-3). |
| h | Reconnect/resync | **Proven across three layers** | Server: `BattleHubReconnectRecoveryTests.cs:177-236`, `:292-396` (the §4 projection including `equippedCards` = `[… 'card-inferno']` at `:370-372`); `RedisBattleRecoverySmokeTest` asserts the same after Redis recovery. Runtime: `GameRuntime.test.ts:1560-1700` (`onReconnected` → `GetBattleState`). Scene: `SceneLifecycle.test.ts:3367-3410` re-renders and re-dispatches after a state push. E2E: a real stop/start plus the runtime's registered handler, then the caption assertion (`standalone-web-smoke.mjs:2791-2812`, `:2876-2884`). Caveat C-4: the scene-level leg uses the harness setter, not the runtime's recovery method. |

### 6.1 Fixtures that could pass through an impossible production state

Audited specifically, because a green suite is not evidence if it certifies a
state production cannot reach:

1. **`CollectionEndpointTests.cs:851-855` and `CollectionQueryServiceTests.cs:536-538`
   assert that a `PetSkill`-category row *is* a member of the `/api/cards`
   result.** The assertion is contract-correct (membership is the unlock rows),
   but it hard-codes a production-unreachable membership into the expected
   result. It does **not** make any Signature Skill test pass — the Skill tests
   use no unlock row — but it does mean a future accidental widening of
   `/api/cards` would not be caught by these cases. Classified as a coverage
   gap (C-6), not a defect.
2. **The loadout/start harnesses unlock a `PetSkill` card** (`BattleStartSmokeTest.cs:299`,
   `BattleStartEndpointTests.cs:858`, `RedisBattleStateSmokeTest.cs:554`) to prove
   the validator still rejects a submitted `PetSkill` id even when unlocked.
   Legitimately stronger than the reachable case, and not on the identification
   path. C-6.
3. **The client harness is now production-faithful** — the exact opposite of the
   fixture TASK-219B was written to remove. This is the strongest single piece of
   evidence that the correction is real rather than test-shaped: the Skill is
   identified while the Card read provably cannot contain it.
4. **`SceneLifecycle.test.ts:915-927` transcribes the five Pet → Skill pairs.**
   A test-side constant is expected (content is data), but the same mapping is
   transcribed independently in at least five places
   (`TestProvisionedContent.cs:88-95`, `CollectionQueryServiceTests.cs:180-187`,
   `PlayerStarterGrantFactoryTests.cs:81-88`,
   `PetCardRelicDefinitionPostgresProvisioningTests.cs:402-409`,
   `SceneLifecycle.test.ts:915-927`) with no single owner or cross-check. C-7.

### 6.2 Verification-artifact defect introduced by TASK-219A

**V-1 (latent one-run flake in the new phase 6d).**
`standalone-web-smoke.mjs:2701-2717` polls for the first non-empty `castText`
that differs from the previous phase's text, and the loop exits on the first
non-null sample. The scene sets `'PetSkillCast in flight…'` synchronously on
click (`BattleScene.ts:1698`), so a sample taken before the acknowledgement
lands latches that string: `phase6d.signatureSkillDispatchesPetSkillCast`
(`:2728-2732`, `startsWith('PetSkillCast')`) still passes, but
`phase6d.signatureSkillControlRemainsUsable` (`:2719-2724`, requires `accepted`
or `rejected`) fails outright, and the retry loop does not retry. It has not been
observed firing (the local round trip is usually faster than the poll interval),
so it is a latent flake, not a known failure.

Classification: a defect of the **E2E script** added by this work, not of
production behaviour and not of the contract. The smallest fix is one predicate
change (ignore the in-flight caption, or require `accepted|rejected` inside the
wait). Owner: TASK-219B, whose declared subject is precisely "tighten the
`phase6d` E2E check" (`TASK-213:609`). Non-blocking for the contract
post-condition.

A separate, weaker observation (**V-2**): the E2E's Basic-Card check
(`:2540-2544`, `castFeedback.startsWith('CardCast ')`) is satisfied by
`'CardCast card-heal in flight…'` as well as by an acknowledgement, so it proves
the Card branch ran rather than that the server accepted. Folding this into
TASK-219B is a one-line tightening, not a correctness gap: the same branch is
proven end-to-end by `BattleHubCardCast*` tests and the runtime tests.

### 6.3 Stale test comment (documentation-only)

`PetCardRelicDefinitionProvisioningTests.cs:351-352` still reads "the three Pet
Skills are 1" for a set that is now five. Comment text only; it affects no
assertion (the category encoding assertion at `:355` is unaffected).
Assignable to the documentation sweep, not to this audit.

**Verdict §6:** every property the correction must prove is proven at the layer
that can prove it, and the client harness was made production-faithful rather
than convenient. The gaps are (i) 5/5 not asserted at the API/E2E level, (ii) no
failing-Pet-read case, (iii) single-file source guard, (iv) residual synthetic
`PetSkill` unlock rows and a duplicated content mapping in fixtures. One E2E
check added by this work is racy (V-1). No implementation defect was found.

---

## 7. E2E evidence (audit scope 7)

### 7.1 What the run really exercises

| Check | Finding |
|---|---|
| Real `/api/pets` | **Yes.** `standalone-web-smoke.mjs:2177-2187` and `:2667-2678` call `s.runtime.getPets()`, i.e. `GameRuntime → ApiService → GET /api/pets`, against the running server. |
| Real `/api/cards` | **Not asserted.** The script contains no `getCards`, no `/api/cards` and no `/api/pets` route literal; the Card collection is only observed indirectly through the Lobby snapshot (`:567-572`). Contract item 7 therefore has **no E2E evidence** — C-2. It *is* covered by the API tests (`CollectionEndpointTests.cs:211-260`) and by the real-registration path (`AuthStarterOwnershipTests.cs:235-255`). |
| Real account and grant | **Yes.** Registration and login through the real UI (`:1095-1213`), the real starter grant read (`:1473-1486`). No ownership row is fabricated anywhere in the script. |
| Multiple Pets | **One of five per run.** The loadout Pet is chosen from that run's Lobby rows (`:1751`); the Skill control is addressed positionally as `equippedCards.length - 1` (`:2689-2693`). The only Pet-count assertion is `ownedPetsCount > 0` (`:1476`). C-1. |
| Signature Skill caption | **Yes, and self-referential to the response.** `:2667-2678` reads the reference out of the real `/api/pets` response for the equipped card id; `:2680-2687` requires `category === 'PetSkill'` and string `cardId`/`name`; `:2740` rejects any `/^(Card|Skill): card-/` caption; `:2742-2749` requires exactly one `Skill: ` caption equal to `Skill: ${deliveredSignatureSkill.name}`. Nothing is a script constant, and the `=== null` escape at `:2746` cannot false-pass because `:2680` fails the run first. |
| Activation / dispatch | **Yes, to the caption.** `:2719-2732` requires an `accepted|rejected` acknowledgement and a `PetSkillCast`-prefixed caption — the N-03 tightening TASK-213 asked for ("no longer any acknowledgement"). Weakness: the observation is the scene's own caption, not a captured hub frame (V-1 for the race; §7.7 for the proxy). |
| Basic Card activation | **Yes, branch-level.** `:2540-2544` `startsWith('CardCast ')` (V-2). |
| Reconnect / resync | **Yes, and it is a real transport stop/start.** `:2789-2812` (`connection.stop()` then `start()`, then the runtime's registered `onReconnected`), `:2814-2869` (HUD and gauges converge to the authoritative snapshot, no replay), `:2876-2884` (exactly one `Skill: ` caption carrying the delivered name, no raw id). |

### 7.2 How many checks, verified from the script

```text
152   static `record(` call sites in standalone-web-smoke.mjs
 −4   the never-taken `else` branch at :2759, :2764, :2769, :2774
      (the `if (skillControlIndex >= 0)` path at :2695 records the same four
       names at :2719, :2728, :2742, :2753)
 −1   the `catch` branch at :3024, which records and rethrows, so the success
      record at :3035 is the one that runs
────
147   checks in a fully successful run
```

The printed total comes from `checks.length` (`:3990`); there is no count
constant. This confirms the task statement's 147 independently of any prior
report, and it is consistent with TASK-221's 140 before phase 6d (+4) and phase
6c's new identification check (+1).

### 7.3 Does it prove the contract or only the current fixture?

It proves the contract for the run's own Pet, and it does so from the server's
own delivered values rather than from script constants — which is exactly the
property TASK-219A exists to establish, and it is strictly stronger than the
pre-219A phase 6d, which accepted any acknowledgement. It does **not** prove:

```text
multi-Pet coverage                    1/5 (C-1)
that /api/cards excludes the Skill    not asserted (C-2)
Basic Card acknowledgement            branch-level only (V-2)
the hub frame for either dispatch     caption proxy (§7.7)
```

### 7.4 The known floater duplication — classified separately

`phase5c.oneFloaterPerDamageInstancePlusPower` (`:2427-2434`) is the check that
can report a duplicated damage/Power floater. It is a **pre-existing,
unrelated** flake: the root cause is probe timing (a floater lives 600 ms of
*game* time while the old fixed `delay(900)` assumed wall-clock), already
documented and addressed by `waitForFeedbackToFade` (`:300-334`, called at
`:2394` and `:2407`) in TASK-221's record (`:417-430`). Phase 5c and
`pollFeedback` are untouched by the Signature Skill work, and the Skill path
emits no damage floaters.

```text
Classification:  PRE-EXISTING / UNRELATED (presentation-probe timing)
Attribution:     NOT TASK-219A — not silently attributed, and not counted
                 against its post-condition
```

**Verdict §7:** the E2E proves the corrected contract for the Pet it fights, from
the real `/api/pets` response, including caption, activation and a genuine
reconnect/resync. Its limits are single-Pet coverage, no `/api/cards`
assertion, and caption-level (not transport-level) dispatch observation; one
check it adds is racy (V-1).

---

## 8. Documentation / lifecycle (audit scope 8)

### 8.1 Are the two contract-document changes sufficient?

```text
Owner of the member set             API_CONTRACTS.md §5.1/§5.2/§5.3/§5.6   ✓ changed
Owner of the client identification  SIGNALR_PROTOCOL.md §4.3 item 13       ✓ changed
Owner of the Card/Skill model       CARD_RULES.md §1/§4.1                  unchanged, and correct
Owner of the Pet model              PET_RULES.md §8                        unchanged, and correct
Owner of the content set / scope    MVP_SCOPE.md §1 §"Content ownership"   already says "never an owned/unlock row"
Owner of stored shape / ownership   DATABASE.md §1/§2                      already says *Excluded:* Pet Skill Cards
Owner of state shape                GAME_STATE.md §2.3                     already says 3 Basic + 1 Pet Skill
Owner of the events                 GAME_EVENTS.md §2                      already consistent
```

Sufficient. No document outside the two needed a normative edit, because the
correction changes *which delivered datum the client reads* and *which hub
method the client uses* — both owned by the two amended documents — and changes
no model, membership, state, or event rule. The one drift sentence TASK-213 §11.2
recorded (N-04) was rewritten inside §4.3 item 13, where it lives.

**DOC-1 (precision observation, non-blocking).** The ownership-exclusion
proposition ("a `PetSkill` `CardDefinition` is still never a `PlayerUnlockedCard`
row") is cited to `CARD_RULES.md` §1 item 4 + `ADR-012` item 9
(`API_CONTRACTS.md:790-791`, `SIGNALR_PROTOCOL.md:1895-1896`,
`CollectionQueryService.cs:394`, `PetSignatureSkillItem.cs:22`). Its *owning*
statement is `DATABASE.md` §2 item 1's *Excluded* clause (`:1318-1322`) and
`MVP_SCOPE.md` §1 (`:112-113`); `CARD_RULES.md` §1 item 4 owns the composition
rule (the Skill is not submitted and cannot satisfy the Basic slot) and
`ADR-012` item 9 owns the unlock-flag ownership model. The proposition is true
and documented — **there is no conflict and no contradiction** — but the
citation points one step away from the owning text. No action required; if a
documentation sweep ever touches these lines, this belongs to `TASK-217B`'s
subject area (documentation precision), not to a new task.

### 8.2 Does the lifecycle require a completion record for TASK-219A?

```text
Finding:  ABSENT — no TASK-219A record exists anywhere under tasks/
          (`tasks/**` has no *219* file; `tasks/active/` holds only .gitkeep).
Rule:     tasks/TASK_LIFECYCLE.md §3 DONE — "All core/completion.md §1 criteria
          are satisfied by direct execution … File location: tasks/completed/",
          reached via `tasks/README.md` §7 steps 1–8, which begin with an
          existing task file in `backlog/`.
          tasks/TASK_LIFECYCLE.md §3 SUPERSEDED — an alternative closure for a
          task whose deliverables landed downstream; it too requires a record.
Reading:  TASK-219A was executed as an implementation task (an implementation
          task by TASK-213 §9 row 4: "I — the decision is now made"), so the
          lifecycle requires a record for it, and none exists.
          Consequence: TASK-224's Stop Condition 6 ("a second missing task
          record") is satisfied, and TASK-223 §6.2's P-4 ("no commit under a
          task with no record") would refuse to attribute TASK-219A's files to
          TASK-219A.
Charter:  this audit may create exactly ONE artifact — this record — so it does
          not author the missing record and does not edit history.
Precedent: TASK-221A recorded the audited task's post-condition inside the
          audit record ("CLOSED — audited PASS … Recorded here rather than by
          editing the immutable record"). This record follows that precedent for
          the post-condition (§11) and reports the separate missing record as a
          process follow-up (P-2 below).
```

**P-2 (process follow-up, not an implementation defect).** Author a
`tasks/completed/TASK-219A-…` record (declared file set + acceptance criteria +
validation evidence) before TASK-219A's files are committed under its id, or
explicitly disclose those files as unowned. This is TASK-224's subject matter;
TASK-224 is not in this audit's reassessment list and its scope is not expanded
here — the finding is reported so that the two cannot interact silently.

### 8.3 Historical records: untouched, and no superseding record required

```text
Left untouched (and must remain so)
  TASK-213 (decision), TASK-221, TASK-221A (prior audit), TASK-223,
  TASK-224 (backlog), TASK-211/212/212A audits — all cited, none edited.

Stale statements that are historical by construction
  TASK-221A §9.3 describes the pre-correction source state
    ("BattleScene.ts:1156-1173 … category === 'PetSkill'") and §10/§11
    recommend TASK-219A as the next task. Both are dated, point-in-time audit
    observations. TASK_LIFECYCLE.md §3 forbids editing a completed record, and
    TASK-221A's own convention treats dated evidence as needing no correction —
    so no superseding record is needed to keep the contract coherent.
  TASK-213 §5's statements about what *must be* amended remain true: the
    amendment it commissioned has been applied. Nothing in it is falsified by
    this implementation.
  TASK-217A §11.1's two stale *code comments* are unrelated to this contract
    and remain open under TASK-217A (§9 below).
```

No superseding record is required for any **contract** statement. The only stale
sentences are dated observations in immutable audit records.

**Verdict §8:** the two document changes are sufficient and correctly owned;
TASK-219A's lifecycle record is required and missing (P-2); no historical record
should be, or was, edited.

---

## 9. Downstream reassessment (audit scope 9)

Only the six named items; no scope expansion.

| Task | Classification | Basis (verified) |
|---|---|---|
| **TASK-219B** — "make the client harness model production; tighten the `phase6d` E2E check" | **READY — RE-SCOPED (reduced); do not execute as written** | Both named deliverables landed inside the TASK-219A work: the client harness is production-faithful (`SceneLifecycle.test.ts:309-338`, `:3242-3279`) and phase 6d now asserts `PetSkillCast` and the delivered name (`standalone-web-smoke.mjs:2719-2749`) — no longer "any acknowledgement". What remains is narrower and unblocked: V-1 (the phase-6d in-flight race), V-2 (Basic-Card acknowledgement tightening), C-3 (failing-Pet-read case), C-5 (source-guard scope), C-6 (residual synthetic `PetSkill` unlock rows). It must be re-scoped before pickup; alternatively a reconciliation record may close it as satisfied with those residuals folded into the files that own them. |
| **TASK-218** — name the Pet's Relic triggers in battle | **UNCHANGED** | Independent, implementation-only, no contract change (`TASK-213:607`, `TASK-221A §9.2`). It shares `BattleScene.ts` with this work, which is a sequencing courtesy rather than a dependency: the TASK-219A edits are already in the tree and TASK-218 adds Relic `name` lookups from the already-delivered `getRelics()` read. Not blocked, priority unchanged (P2). |
| **TASK-215A** — pin the Passive / Tier / Star deferral | **UNCHANGED** | Test-only, P3. TASK-219A touches no Passive, Tier, or Star behaviour; no assumption it relies on changed. `TASK-221A §9.4`'s scope note (five instances; the active-Pet reward property) still stands and is unaffected. |
| **TASK-216** — composed battle-end reward-path assertion + three-write non-atomicity | **UNCHANGED** | P2 (corrected subject). TASK-219A touches no reward, XP, or persistence path; the corrected subject and the refuted old premise are unaffected. |
| **TASK-217A** — architecture/component reconciliation + stale code comments | **UNCHANGED (still P1 documentation)** | Both named sites are still present and still stale: `BattleStartService.cs:249-251` ("BOSS_RULES.md §6 defines exactly three content-defined MVP Bosses", contradicted by the five provisioned Bosses) and `BattleStateService.cs:1581-1584` ("Passive EFFECT application is out of this task's scope for every Boss", contradicted by the implemented Boss Passive effects from `:1607`). TASK-219A authored no stale comment and removed none. |
| **TASK-217C** — apply the Card-cost authority ruling to `SIGNALR_PROTOCOL.md` §3.2.20 item 2 | **UNCHANGED (still P3 documentation)** | The superseded sentence is still present: `SIGNALR_PROTOCOL.md:1119` "a client that must show the spent Cost reads the Card's definition". TASK-219A's SIGNALR edit was scoped to §4.3 item 13 and the 2.18 header, so this remains TASK-217C's one-sentence job. |
| **TASK-212B** — in-battle Card cost and affordability | **DEFERRED (unchanged; do not re-enter the order)** | Its promotion triggers are unmet and TASK-219A did not move them: no cost source was added (the only modifier source is still `relic-emergency-core`), and the contract now states the exclusion *in this very member's rules* — `API_CONTRACTS.md` §5.1 item 4: "It carries no cost, no affordability state, no cast-legality judgment, and no `effectDefinition`". No `CardCostModifiers` behaviour, member, or computation was introduced by TASK-219A. |

---

## 10. Findings register

```text
IMPLEMENTATION DEFECTS FOUND                     NONE
  The contract correction is implemented correctly on every surface audited.

VERIFICATION-ARTIFACT DEFECTS (introduced by this work, non-blocking)
  V-1  phase-6d in-flight-caption race in standalone-web-smoke.mjs
       (:2701-2732 vs BattleScene.ts:1698) — latent one-run flake.
       → TASK-219B
  V-2  phase-6b Basic-Card check satisfied by the in-flight caption
       (:2540-2544) → TASK-219B

COVERAGE GAPS (separate from defects; none blocks the post-condition)
  C-1  5/5 Signature Skills are not asserted at the API or E2E level (1/5 each);
       the full set is proven at the projection, provisioned-content and client
       layers.
  C-2  The E2E never asserts `/api/cards` membership or categories, so contract
       item 7 has no E2E evidence (API/service tests cover it).
  C-3  No test for a failing Pet read — the identification source's own
       fail-closed path (O-1).
  C-4  The scene-level reconnect test uses the harness setter, and no test
       asserts re-identification after PLAY AGAIN re-creates BattleScene.
  C-5  The "no hard-coded mapping / no category test" guard scans only
       BattleScene.ts (source-text, not behavioural).
  C-6  Synthetic `PetSkill` unlock rows remain in several backend/API/client
       harnesses (production-unreachable input, none on the identification
       path) — TASK-213:609's deferred TASK-219B scope.
  C-7  The five Pet → Skill-card pairs are transcribed independently in ≥5
       fixtures with no single owner or cross-check.
  C-8  Stale test comment: PetCardRelicDefinitionProvisioningTests.cs:351-352
       ("three Pet Skills", now five).

OBSERVATIONS (no defect, no action required)
  O-1  Transient raw-id caption / fail-closed dispatch window at scene create
       (un-awaited, non-retried Pet read). The fallback is the behaviour
       TASK-213 §5 prescribed; only its coverage is missing (C-3).
  O-2  Identification keys off any owned Pet's delivered reference rather than
       explicitly the active Pet's. Harmless: `equippedCards` is derived from
       the active Pet, so only its entry can match.
  O-3  `isPetSkill` is a local name derived from the delivered reference — the
       "stale `isPetSkill` logic" checklist item is satisfied as NOT stale.
  DOC-1 The ownership-exclusion proposition is cited to CARD_RULES.md §1 item 4
       + ADR-012 item 9 while DATABASE.md §2 item 1 / MVP_SCOPE.md §1 own it.
       No contradiction; citation precision only.
  P-3  The starter grant resolves 18 definitions with 18 sequential
       GetDefinitionAsync calls (creation-time, per account). Pre-existing,
       unrelated to this contract, and not a 219A concern.

PRE-EXISTING / OUT OF SCOPE (classified apart — NOT attributed to TASK-219A)
  P-1  Reconnect group re-join is unstated (SIGNALR_PROTOCOL.md §3 item 3 vs
       §7) and implemented on neither side → post-reconnect group broadcasts
       may not resume. Smallest follow-up: a new decision-then-implementation
       task (next unreserved id). §5.3.
  P-2  No TASK-219A lifecycle record exists (TASK_LIFECYCLE.md §3 requires one).
       Smallest follow-up: author it, or disclose the files as unowned, inside
       TASK-224's subject area. §8.2.
  P-4  The phase-5c floater duplication — pre-existing presentation-probe
       timing, unrelated to this work. §7.4.
  P-5  TASK-217A's two stale code comments and TASK-217C's superseded §3.2.20
       sentence remain open by design. §9.
```

---

## 11. Verdict

```text
PASS
```

Rationale against the required PASS conditions:

```text
Contract consistent                              ✓ §1  (both owning documents, no contradiction left)
Pet-derived source authoritative                 ✓ §2  (single FK → definition row projection)
Signature Skill remains non-owned                ✓ §3  (no writer; exactly 3 Basic unlock rows)
/api/cards remains ownership-only                ✓ §3.3 + §3.2 (production path proven)
Canonical UI dispatch is PetSkillCast            ✓ §4.3 (code + unit + transport tests)
Basic Cards remain CardCast                      ✓ §4.3
5/5 provisioned Pets covered                     ✓ §6 (projection + provisioned content + client);
                                                        API/E2E 1/5 recorded as C-1, not a defect
Reconnect/resync remains correct                 ✓ §5  (durable source; survives snapshot and
                                                        PLAY AGAIN; P-1 classified apart)
No unintended architecture/schema/API change     ✓ §2.5 (no migration, endpoint, index, Domain,
                                                        or ownership change)
New repository boundary justified                ✓ §2.4 (necessary and correctly scoped)
No unresolved implementation defect              ✓ §10 (none; V-1/V-2 are E2E artifacts, C-* are
                                                        coverage gaps → TASK-219B)
```

No fix is implemented, no test is changed, no contract document is touched, no
historical record is edited, and no other file is created by this audit.

---

TASK-219A-A AUDIT COMPLETE

Decision: PASS
TASK-219A post-condition: HOLDS — the Signature Skill contract correction is closed: all nine authoritative decisions are implemented, consistently documented, and proven by tests that model production rather than a fixture.
Signature Skill source: Pet-derived and authoritative — `PetDefinition.SignatureSkillCardId` projected through the referenced `CardDefinition` as `GET /api/pets` / `GET /api/pets/{petId}` `signatureSkill` (`API_CONTRACTS.md` §5.1/§5.2); the client identifies from that delivered reference alone (`SIGNALR_PROTOCOL.md` §4.3 item 13).
Ownership semantics: UNCHANGED — the Signature Skill creates no `PlayerUnlockedCard` row (one creation-time writer, zero post-creation acquisition writers, no new ownership model); the starter grant still owns exactly the three Basic Cards; `/api/cards` remains ownership-filtered.
5-Pet coverage: 5/5 PROVEN at the projection layer, in the provisioned migration content, and in the client presentation loop; 1/5 at the API and E2E layers (recorded as coverage gap C-1, not a defect).
Canonical dispatch: `PetSkillCast` — `BattleScene.renderCastControls` → `submitPetSkillCast` → `GameRuntime` → `SignalRService` → `BattleHub.PetSkillCast`, with no card identifier.
Basic Card dispatch: `CardCast(cardId)` — unchanged; `CardCast(<signature cardId>)` remains server-conformant and is the decided fail-closed path.
Reconnect/resync: CORRECT for this contract — identification is a durable `GET /api/pets` read held in the scene, never event- or state-replayed, so it survives the §7 snapshot, and PLAY AGAIN re-creates the scene and re-reads it; no transient-only state is required. A separate pre-existing reconnect group-membership gap (P-1) is classified apart and not attributed to TASK-219A.
Repository boundary: `ICardRepository` / `CardRepository.ListDefinitionsAsync` — NECESSARY AND CORRECTLY SCOPED (one bulk content read replacing a per-Pet N+1; no `playerId`, no ownership filter, no index, cache, or read model; the exact analogue of the existing `IRelicRepository.ListDefinitionsAsync`).
E2E: 147 checks per successful run (verified from the script: 152 `record(` sites − 4 never-taken else-branch + 1 catch-branch); real registration, real starter grant, real `/api/pets`, real caption/presentation/dispatch assertions, and a genuine transport stop/start resync. Limits: 1/5 Pets, no `/api/cards` assertion (C-2), caption-level dispatch observation; one new latent race (V-1). The known phase-5c floater duplication is pre-existing, unrelated, and not attributed to TASK-219A.
Lifecycle record: REQUIRED AND ABSENT — no TASK-219A record exists under `tasks/` and `TASK_LIFECYCLE.md` §3 requires one for an executed implementation task; this audit may create only its own artifact, so authoring it is reported as P-2 (TASK-224's subject area). No historical record was edited and no superseding record is required for any contract statement.
Downstream recommendation: TASK-219B READY (re-scoped: V-1, V-2, C-3, C-5, C-6) · TASK-218 UNCHANGED · TASK-215A UNCHANGED · TASK-216 UNCHANGED · TASK-217A UNCHANGED · TASK-217C UNCHANGED · TASK-212B DEFERRED.
Implementation changes: NONE
Test changes: NONE

Implementation: NOT DONE
Production changes: NONE
Test changes: NONE
