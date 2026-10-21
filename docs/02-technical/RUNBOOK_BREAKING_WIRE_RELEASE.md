# Coordinated Release Runbook: Breaking Wire-Contract Deployment (BossSkillCast Retirement)

**Version:** 1.1 (TASK-317 — §3.2, §4 and §5 Phase 2 corrected against `REDIS_STATE.md` §1/§3: the drain window is derived from the documented inactivity expiry and the documented explicit battle-end delete, the elapsed-wait argument is replaced by an explicit verified zero-active-battle gate, and the production tooling the drain requires is named. `OD-316-1 = PREPARATION ONLY` is unchanged: this remains preparation documentation and no step here has been executed)
**Status:** DRAFT / PREPARATION ONLY (TASK-316, OD-316-1)
**Governing ADR:** ADR-029 Slice 5 / TASK-313
**Applicable Documents:** `SIGNALR_PROTOCOL.md` §3.2.18 / §3.2.30 / §8 item 10, `REDIS_STATE.md` §1 / §2 / §2.1 item 7 / §3 / §4 / §7, `DATABASE.md` §1 / §5

---

## 1. Overview & Architectural Context

Under **TASK-313 (ADR-029 Slice 5)**, the transitional legacy `BossSkillCast` event was retired and physically removed from the server wire projection (`BattleEventWireProjection.cs`) and frontend client presenter (`BattleScene.ts`, `BattleEventPresenter.ts`). The canonical `BossCardCast` event (`SIGNALR_PROTOCOL.md` §3.2.30) is now the sole Boss ability cast event emitted by the authoritative server.

Because `BossSkillCast` was removed from the closed wire contract closed set without negotiation or fallback shims, **pre-TASK-313 and post-TASK-313 builds are mutually incompatible across ability casts**. A pre-TASK-313 client encountering a post-TASK-313 server (or vice versa) during a Boss ability cast will experience client presentation rejection or protocol error.

Therefore, this release requires a **coordinated, breaking release procedure** centered on the **Decision B-2 Active Battle Drain** (`REDIS_STATE.md` §2.1 item 7).

---

## 2. Hard Prohibitions & Operational Constraints

In accordance with operator decisions OD-311-2 and OD-316-1:
- **NO IN-PLACE REDIS MUTATIONS:** Do NOT execute offline data migrations, backfills, `SCAN`-and-patch scripts, or bulk `DEL` on active battles in Redis.
- **NO TOLERANT-READER WIRE SHIM:** Do NOT reintroduce a tolerant reader, fallback discriminator, or dummy payload for `BossSkillCast`.
- **NO DATABASE CLEANUP MIGRATION:** Do NOT alter the database schema or rewrite historical migrations. Persisted JSONB `chargeRequirement` and `baseDamage` remain storage compatibility placeholders only (OD-311-1 = B).
- **NO DEPLOYMENT WITHOUT EXPLICIT PRODUCTION AUTHORIZATION:** This runbook documents the operational procedure only; no battle drain, deployment, or remote push is authorized within automated sessions.

---

## 3. Confirmed Repository Facts vs. Operational Assumptions

### 3.1 Confirmed Repository Facts
1. **Wire Event Removal:** `BattleEventType.BossSkillCast` (value 9) is retired and removed from `BattleEventWireProjection.cs`. No wire DTO carries `skillId`.
2. **Sole Ability Event:** `BattleEventType.BossCardCast` (value 13) is the canonical event; wire schema carries exactly `cardId` and `sourceId`, and no cost, Power, cooldown, or effect summary.
3. **Redis Deserialization Tolerance:** `BattleStateSerializer.cs` uses default `System.Text.Json` deserialization (no unmapped member disallow; no `[JsonRequired]`). Existing Redis snapshots containing legacy `"skillCharge"` deserialize into `BossState` without error, discarding the value (`REDIS_STATE.md` §2.1).
4. **PostgreSQL Compatibility:** `BossDefinitionConfiguration.cs` reads only `skillId` and `cooldownTurns`, writing `0` for legacy `baseDamage` and `chargeRequirement`. Live schema requires no migration.
5. **Frontend Runtime:** Phaser/React client recognizes only `BossCardCast`. Any incoming `BossSkillCast` discriminator would be unhandled.

### 3.2 Active Battle-State Key and Expiry — Contract Facts (`REDIS_STATE.md` §1, §3, §4)

These are the facts Phase 2's drain procedure is built on. They are stated here
because they decide the scan pattern and the maintenance window; the owning
document is `REDIS_STATE.md`, and where that document and this summary ever
disagree, that document governs.

1. **One state key per battle, and only that key.** `REDIS_STATE.md` §1 fixes the
   key structure as `battle:{battleId}:state`; `BattleStateRepository`
   (`GameServer.Infrastructure/Redis`) assembles it from the fixed prefix
   `"battle:"` and the fixed suffix `":state"`. It is the **only** state key the
   server writes: there is no per-battle index, no marker/tombstone key, no hash
   field, and no second record. (`REDIS_STATE.md` §1 also lists an *optional*
   `battle:{battleId}:lock` mutex that §4 item 4 permits but the implementation
   deliberately does not create — see `BattleStateRepository`'s type remarks.)
2. **The inactivity expiry is 30 minutes.** `REDIS_STATE.md`
   §3 states "Refreshed: TTL reset on every successful resolution (sliding
   expiry), default 30 minutes of inactivity", and `BattleStateRepository`
   encodes exactly that (`StateTtl = TimeSpan.FromMinutes(30)`).
3. **When the expiry is refreshed.** On battle creation, and on **every
   successful resolution** — the compare-and-set script writes the record and
   the new expiry together (`REDIS_STATE.md` §3, §4 items 2 and 5). It is **not**
   refreshed by a **rejected** action: a refused action writes nothing and does
   not touch the TTL (`REDIS_STATE.md` §4 item 7). An abandoned battle therefore
   expires roughly 30 minutes after its last *successful* resolution, which is
   why a merely "connected but idle" client cannot hold a live battle open
   beyond that window.
4. **A battle end deletes the key — but only after the durable commit succeeds.**
   `REDIS_STATE.md` §3 ties deletion to `BattleWon`/`BattleLost` **after** the
   battle-end database unit of work commits `BattleResult` and both progression
   tracks (`DATABASE.md` §1). So a battle that finishes normally usually leaves
   **no** key behind.
5. **A key can therefore still exist for a battle that is over.** Two documented
   paths keep it (`REDIS_STATE.md` §3, `DATABASE.md` §1 sourcing item 3):
   - the terminal result was committed but the **delete failed** — "no automatic
     retry is performed and no worker or queue exists for it: the sliding TTL
     above remains the cleanup path";
   - the terminal resolution reached **no durable result** (the battle-end unit of
     work did not commit, or no `BossDefinition` row resolved), in which case the
     record deliberately remains the battle's recoverable authoritative state.
6. **Consequence: the key's existence is not the drain signal.** A discovered
   `battle:*:state` key does not tell the operator whether a battle is genuinely
   in progress, finished-but-undeleted, or an abandoned record awaiting expiry.
   Only the record's own contents and its remaining TTL distinguish those cases
   (§5 Phase 2 step 2).
7. **Deleting keys by hand is prohibited.** `REDIS_STATE.md` §2.1 item 7 and this
   runbook's §2 forbid an offline data migration, backfill, `SCAN`-and-patch, or
   bulk `DEL`. The drain is achieved by **preventing new battles, letting live
   ones finish or expire, and verifying the result** — never by clearing keys.

### 3.3 Operational Assumptions Requiring Operator Verification

None of the following can be established from the repository. Each one changes
what the drain operator is able to do, so each must be confirmed before the
window opens.

1. **Ingress / Gateway Route Control:** The operational mechanism for freezing
   `/api/battle/start` (e.g. reverse proxy Nginx configuration, Traefik
   middleware, Cloudflare WAF rule, or API Gateway route disabling) must be
   verified on the target hosting environment.
2. **Redis Production Access and Topology:** Administrative/monitoring access
   (`redis-cli` or an equivalent portal) to observe battle keys, **which
   deployment uses** (the key pattern must be confirmed against a real key before
   the count is trusted), and whether the connection is **read-only** for
   observers. Whether operators have `KEYS`-class access or only `SCAN`, and
   whether the instance is shared with any other workload that could also match
   the `battle:*:state` pattern, must be confirmed.
3. **Maintenance Window Scheduling:** A window long enough to (a) freeze ingress,
   (b) wait out genuinely live battles, (c) absorb the expiry of battles that
   were already abandoned before the freeze, and (d) verify zero remaining
   active battles. Because the documented expiry is **30 minutes of inactivity**
   (§3.2 item 2), a battle that took its last successful resolution just before
   the freeze can remain as a key for up to ~30 minutes. **Size the window from
   `REDIS_STATE.md` §3's value, and treat the resulting duration as a scheduling
   estimate — not as evidence that the drain is complete.** Completion is
   established only by the verified scan in §5 Phase 2 step 3.
4. **Asset CDN Cache Invalidation:** The exact procedure to purge or invalidate cached client frontend bundles (`index-*.js`, `index-*.css`) on the delivery CDN.
5. **Rollback Artifact Availability:** That the pre-TASK-313 backend and client
   artifacts are still deployable in the target environment (registry/artifact
   retention, CDN version pinning) at the moment the window runs.

### 3.4 What This Runbook Cannot Establish

- It cannot prove how many battles are live in production, or that any drain has
  occurred. Those are facts about a running system, observed at execution time.
- It cannot prove the production Redis uses the documented key spelling; the
  contract fixes the server's spelling, and §5 Phase 2 step 1 verifies the
  deployed instance agrees before the drain depends on it.
- It no longer states that waiting a fixed number of minutes drains the store. No
  duration — 15, 30, 45, or any other — is presented as a substitute for the
  verified zero-active-battle confirmation in §5 Phase 2 step 3.

---

## 4. Release Preconditions & Authorization Gate

Prior to initiating any deployment activity, the executing operator must verify:

```text
[ ] Written Production Authorization obtained from Product Owner / Lead Operator.
[ ] Scheduled maintenance window announced to users / Discord Activity platform, sized
    from REDIS_STATE.md §3's documented inactivity expiry (§3.3 item 3) — not from an
    elapsed-wait argument.
[ ] Release candidate commit SHA verified on master (HEAD matches audited state).
[ ] Backend test suite 100% passing (3366 tests passed, 0 failed, 0 skipped).
[ ] Frontend test suite 100% passing (1267 tests passed, 0 failed).
[ ] Frontend production bundle built cleanly (tsc && vite build).
[ ] Target server and client release artifacts tagged with identical release tag.
[ ] Rollback release artifacts (pre-TASK-313 stable build) available for immediate redeployment
    and still retrievable from the target environment (§3.3 item 5).
[ ] Redis observation access confirmed for the whole window, and the deployed key
    pattern confirmed against one observed key (§3.3 item 2).
```

---

## 5. Step-by-Step Deployment Runbook

### Phase 1: Ingress Freeze (Initiate Drain)
1. **Freeze Battle Creation:**
   - Configure edge ingress / reverse proxy to return `HTTP 503 Service Unavailable` with `Retry-After: 300` for `POST /api/battle/start`.
   - Leave `POST /api/auth/*` and `GET /api/collection/*` active if non-disruptive, or route all Discord Activity traffic to maintenance notice.
2. **Verify Freeze:**
   - Execute curl test against `POST /api/battle/start`: confirm 503 response.
   - Confirm **no new battle keys appear after the freeze timestamp**. Take the first
     count only *after* the freeze is confirmed, so the baseline cannot include
     battles started before it.

### Phase 2: Active Battle Drain and Verified Zero-Active-Battle Confirmation

**The drain gate is the verified confirmation in step 3. Nothing else — and in
particular no elapsed duration — completes this phase.**

1. **Enumerate the deployed battle-state keys (§3.3 item 2).**
   - Confirm the deployed key pattern against at least one observed key before
     trusting the pattern.
   - Use a **non-blocking cursor scan**, never `KEYS`, on a production instance:
     ```bash
     redis-cli --scan --pattern 'battle:*:state'
     ```
     (`redis-cli --scan` runs `SCAN` under the hood. `SCAN` is a read; it does not
     mutate anything, and this runbook authorizes no `DEL`, no patch, and no
     rewrite — §2.)
2. **Classify every key found.** The key's presence alone is not evidence of a
   live battle (§3.2 item 6). For each key, read the record and its remaining
   TTL and record it in the operator log:
   ```bash
   redis-cli TTL "battle:{battleId}:state"
   redis-cli GET "battle:{battleId}:state"
   ```
   The record's `bossState.hp` / `petState.hp` and its `turn` / `sequence` values
   tell an operator whether the battle is still playable:
   - **HP pair both above 0** — the battle is genuinely still in progress. Let it
     finish (its terminal resolution deletes the key once the durable result
     commits), or let it expire as abandoned. Do not delete it.
   - **Either HP at 0** — the battle is **terminal** but its key survived (a
     failed battle-end delete, or a terminal resolution with no durable result —
     §3.2 item 5). It is not playable: a terminal battle admits no Swap and no
     cast (`MATCH3_RULES.md` §2.1.4 item 7, `CARD_RULES.md` §3 item 8), so it
     cannot produce the cross-build event mismatch this release exists to avoid.
     Record it, decide per the operator's data-retention policy whether the
     recorded result needs manual follow-up, and let the expiry clear it.
   - **Unreadable record** — treat as needing operator judgement, not as
     automatically safe. Do not overwrite or delete it.
3. **Confirm zero in-flight battles — the gate.**
   - Re-run the enumeration and require that **no** discovered key classifies as
     a still-playable battle (step 2, first bullet). Keys that classify as
     terminal or abandoned are recorded, and their disappearance is confirmed by
     the same scan; they do not by themselves block the deployment, but each one
     must appear in the operator log with its classification.
   - Record the exact command output, timestamp, and classification counts in the
     sign-off record (§7). **"We waited <N> minutes" is not an acceptable
     substitute for this output.**
4. **If an in-flight battle will not finish inside the window:**
   - Do **not** delete its key, and do **not** edit the record. Both are the
     in-place mutation §2 forbids, and the record is the battle's only
     authoritative copy (`REDIS_STATE.md` §2 item 2).
   - Per §6.1, treat this as a rollback trigger for this window, or extend the
     window with operator approval and re-run steps 2–3.
5. **Tooling this phase requires (operator-supplied; not derivable from the
   repository):** read access to the production Redis instance (or its
   portal/monitoring surface), `SCAN`/`TTL`/`GET` permission, a place to record
   each key's classification, and a designated operator who owns the gate
   decision in step 3.

### Phase 3: Coordinated Deployment
1. **Deploy Backend Server:**
   - Deploy post-TASK-313 .NET GameServer backend service.
   - Verify service starts cleanly and binds to configured port.
   - Execute health check: `GET /health` (or `/api/session` health endpoint).
2. **Deploy Frontend Client:**
   - Deploy post-TASK-313 Vite/React/Phaser client bundle.
   - Invalidate CDN edge caches for client index and asset manifests.
   - Verify browser fetch returns new asset hashes (`index-*.js`).

### Phase 4: Verification & Smoke Testing
1. **Internal Verification Test:**
   - Point internal client / developer test harness to production endpoint.
   - Authenticate application session.
   - Call `POST /api/battle/start` to create a fresh battle against Hỏa Long.
   - Connect SignalR `BattleHub` and execute valid Swaps.
   - Verify `SwapStarted`, `SwapResolved`, and `DamageApplied` events arrive.
   - When Boss Power reaches cost (40) and cooldown is 0, verify canonical `BossCardCast` event is delivered and presented in client with `Flame Burst` animation and banner.
   - Complete battle to terminal state (Victory/Defeat) and verify `BattleResultPersisted` flow.
2. **Re-enable Ingress Traffic:**
   - Remove 503 route rule on `POST /api/battle/start`.
   - Clear maintenance notices.

---

## 6. Rollback Procedure & Criteria

### 6.1 Rollback Triggers
Immediately abort the deployment and initiate rollback if any of the following occur:
- Ingress freeze fails or active battles cannot be confirmed drained within the maintenance window.
- Backend server fails to start or health checks fail.
- SignalR negotiation fails or throws unhandled serialization exceptions on connection.
- Smoke test reveals client rendering crashes on `BossCardCast` or missing assets.
- Unhandled error rate exceeds 0.1% of active sessions within 10 minutes of traffic restoration.

### 6.2 Rollback Steps
1. Re-apply ingress freeze (503 on `POST /api/battle/start`).
2. Re-deploy pre-TASK-313 backend server artifact.
3. Re-deploy pre-TASK-313 frontend client assets; purge CDN.
4. Verify pre-TASK-313 health checks.
5. Restore traffic and notify operator.

---

## 7. Sign-off & Audit Record

Rows are filled **only** when the corresponding action is actually executed in
production. Until then they stay as recorded below.

| Role | Name / Target | Date / Time | Decision / Verdict |
| :--- | :--- | :--- | :--- |
| **Lead Operator** | PENDING AUTHORIZATION | UNEXECUTED | PENDING RELEASE WINDOW |
| **Backend Release Engineer** | PENDING AUTHORIZATION | UNEXECUTED | PENDING VERIFIED ZERO-ACTIVE-BATTLE CONFIRMATION |
| **Frontend Release Engineer** | PENDING AUTHORIZATION | UNEXECUTED | PENDING CDN PURGE |

### 7.1 Drain Gate Evidence (completed by the operator at execution time)

This block is the **only** acceptable completion condition for §5 Phase 2. A
duration waited is not evidence and does not belong here.

```text
Ingress freeze confirmed at (UTC):                 __________
Freeze verification output (503 + no new keys):    __________
Scan command used:                                 __________
Scan output at gate decision (UTC):                __________
Keys found / classified in-progress:               __________
Keys found / classified terminal-or-abandoned:     __________   (each listed below)
Per-key classification log:                        __________
Gate decision (PASS = zero in-progress battles):   __________
Operator signing the gate:                         __________
```

---

## 8. Scope of This Document

This runbook is **preparation documentation only** under `OD-316-1 =
PREPARATION ONLY` (`tasks/blocked/TASK-316-breaking-wire-contract-release-preparation.md`).

- No step in this document has been executed.
- No production Redis scan, ingress freeze, battle drain, migration, deployment,
  or CDN operation is authorized by it, and none has occurred.
- It records no deployment result and claims no drain.

**Status:** PENDING OPERATOR AUTHORIZATION AND SCHEDULED MAINTENANCE WINDOW.
