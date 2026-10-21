# Coordinated Release Runbook: Breaking Wire-Contract Deployment (BossSkillCast Retirement)

**Version:** 1.0
**Status:** DRAFT / PREPARATION ONLY (TASK-316, OD-316-1)
**Governing ADR:** ADR-029 Slice 5 / TASK-313
**Applicable Documents:** `SIGNALR_PROTOCOL.md` §3.2.18 / §3.2.30 / §8 item 10, `REDIS_STATE.md` §2.1 item 7 / §3.10, `DATABASE.md` §1 / §5

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
2. **Sole Ability Event:** `BattleEventType.BossCardCast` (value 13) is the canonical event; wire schema carries `cardId`, `sourceId`, and `powerCost`.
3. **Redis Deserialization Tolerance:** `BattleStateSerializer.cs` uses default `System.Text.Json` deserialization (no unmapped member disallow; no `[JsonRequired]`). Existing Redis snapshots containing legacy `"skillCharge"` deserialize into `BossState` without error, discarding the value (`REDIS_STATE.md` §2.1).
4. **PostgreSQL Compatibility:** `BossDefinitionConfiguration.cs` reads only `skillId` and `cooldownTurns`, writing `0` for legacy `baseDamage` and `chargeRequirement`. Live schema requires no migration.
5. **Frontend Runtime:** Phaser/React client recognizes only `BossCardCast`. Any incoming `BossSkillCast` discriminator would be unhandled.

### 3.2 Operational Assumptions Requiring Operator Verification
1. **Ingress / Gateway Route Control:** The operational mechanism for freezing `/api/battle/start` (e.g. reverse proxy Nginx configuration, Traefik middleware, Cloudflare WAF rule, or API Gateway route disabling) must be verified on the target hosting environment.
2. **Redis Production Connectivity:** Direct administrative/monitoring access (`redis-cli` or administrative portal) to observe battle keys during the drain.
3. **Maintenance Window Scheduling:** Adequate off-peak maintenance window (minimum 30 minutes recommended based on 15-minute battle sliding TTL).
4. **Asset CDN Cache Invalidation:** The exact procedure to purge or invalidate cached client frontend bundles (`index-*.js`, `index-*.css`) on the delivery CDN.

---

## 4. Release Preconditions & Authorization Gate

Prior to initiating any deployment activity, the executing operator must verify:

```text
[ ] Written Production Authorization obtained from Product Owner / Lead Operator.
[ ] Scheduled maintenance window announced to users / Discord Activity platform.
[ ] Release candidate commit SHA verified on master (HEAD matches audited state).
[ ] Backend test suite 100% passing (3363 tests passed, 0 failed, 0 skipped).
[ ] Frontend test suite 100% passing (1267 tests passed, 0 failed).
[ ] Frontend production bundle built cleanly (tsc && vite build).
[ ] Target server and client release artifacts tagged with identical release tag.
[ ] Rollback release artifacts (pre-TASK-313 stable build) available for immediate redeployment.
```

---

## 5. Step-by-Step Deployment Runbook

### Phase 1: Ingress Freeze (Initiate Drain)
1. **Freeze Battle Creation:**
   - Configure edge ingress / reverse proxy to return `HTTP 503 Service Unavailable` with `Retry-After: 300` for `POST /api/battle/start`.
   - Leave `POST /api/auth/*` and `GET /api/collection/*` active if non-disruptive, or route all Discord Activity traffic to maintenance notice.
2. **Verify Freeze:**
   - Execute curl test against `POST /api/battle/start`: confirm 503 response.
   - Confirm zero new battles are created in Redis (`MONITOR` or keyspace events).

### Phase 2: Active Battle Drain
1. **Monitor Active Battle Count:**
   - Run administrative Redis query to enumerate active battle state keys:
     ```bash
     redis-cli SCAN 0 MATCH "battle:*:state" COUNT 100
     ```
2. **Wait for Natural Battle Drain:**
   - Allow in-flight battles to conclude naturally through player actions or expire via Redis TTL.
   - Standard battle sliding TTL is 15 minutes (`REDIS_STATE.md` §3).
   - If an active battle remains near the end of the maintenance window, inspect its `Turn` and idle duration:
     ```bash
     redis-cli TTL "battle:{battleId}:state"
     ```
3. **Confirm Zero In-Flight Battles:**
   - Re-run `SCAN 0 MATCH "battle:*:state"`.
   - Ensure the scan returns zero keys (or only expired/terminal battle keys).

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

| Role | Name / Target | Date / Time | Decision / Verdict |
| :--- | :--- | :--- | :--- |
| **Lead Operator** | PENDING AUTHORIZATION | UNEXECUTED | PENDING RELEASE WINDOW |
| **Backend Release Engineer** | PENDING AUTHORIZATION | UNEXECUTED | PENDING DRAIN CONFIRMATION |
| **Frontend Release Engineer** | PENDING AUTHORIZATION | UNEXECUTED | PENDING CDN PURGE |

**Status:** PENDING OPERATOR AUTHORIZATION AND SCHEDULED MAINTENANCE WINDOW.
