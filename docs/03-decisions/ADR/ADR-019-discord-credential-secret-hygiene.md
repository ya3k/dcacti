# ADR-019: Discord Credential Secret Hygiene

**Status:** Accepted
**Date:** 2026-10-04

## Context

`ADR-007` item 3 establishes the *boundary* for the Discord credential: the
`Discord Client Secret` is held by the backend "via server-side
environment/secret configuration" and is never exposed via Vite client bundles,
environment variables exposed to browsers, React config, Phaser code, or Git.
`TDD.md` §2.1 item 3 and `ARCHITECTURE.md` §2.3 item 2 restate that boundary,
and `ADR-013` item 11 fixes the *keys* it is bound from (`Discord:ClientId` /
`Discord:ClientSecret`) as configuration, "never hard-coded and never
committed".

None of those documents decides the **mechanism** that satisfies the boundary.
Specifically, no document decided:

1. the developer-local channel that supplies the Client Secret,
2. the deployment channel that supplies it,
3. the shape of the tracked `appsettings.json` placeholder,
4. what the host does when the credential is absent,
5. who rotates the credential, on what trigger, and with what operational cost.

This gap is why **TASK-036 — Discord Credential Secret Hygiene** was created
`BLOCKED`: it owns credential storage and hygiene only, and its remediation
could not be chosen by an implementing agent (`AGENTS.md` §7, §18, §20;
`.ai/README.md` §13 "behavior is ambiguous (multiple valid implementations)").
`ADR-015` deliberately does **not** fill the gap: its D7–D11 are scoped to the
**JWT signing key only** and say so explicitly, leaving Discord credential
hygiene to TASK-036.

The gap was not theoretical. Two tracked artifacts disagreed with the host's
actual behavior:

- `src/backend/.env.example` instructed developers to *"Copy to:
  `src/backend/GameServer.Api/.env`"* and listed
  `Discord__ClientSecret=YOUR_DISCORD_CLIENT_SECRET`, while the same file
  correctly states that **a `.env` file is not one of the host's configuration
  providers**. No `.env` loader is packaged, so the host cannot read that file.
  Worse, `.gitignore` ignores `src/backend/.env` — **not** the
  `src/backend/GameServer.Api/.env` path the template names — so following the
  tracked instruction would have produced a file that is neither consumed nor
  ignored.
- The tracked `appsettings.json` carried an empty `Discord:ClientSecret`, and
  the only channel that actually worked locally was the gitignored
  `appsettings.Development.json` — a real credential in plaintext in the working
  tree, protected only by a `.gitignore` entry. A `.gitignore` entry is a commit
  guard, not a secrets-management mechanism.

Current code state: no code reads either key, no Discord OAuth client exists,
and `UnconfiguredDiscordIdentityResolver` returns the `API_CONTRACTS.md` §2.6
transient `503 DISCORD_UNAVAILABLE` rather than inventing an identity. The
exchange client itself is TASK-035's downstream implementation and is **not**
decided here.

The decision below was taken explicitly by the human (Product Owner / security
decision session, 2026-10-04, decisions **D1–D6**) and is recorded here (the
*why*). It does not supersede `ADR-007`, `ADR-013`, or `ADR-015` — it fills the
one area those documents deliberately left open.

## Decision

1. **D1 — the developer-local channel is the .NET User Secrets store; the
   deployment channel is a host environment variable.**
   In local development the Discord credentials are supplied from the
   `GameServer.Api` project's user-secrets store, addressed by the project's
   existing `UserSecretsId` (`GameServer.Api.csproj`), under the keys
   `Discord:ClientId` / `Discord:ClientSecret`. The store lives outside the
   working tree, so no credential value is ever in a tracked file or a build
   output. In deployment they are supplied from host environment variables
   (`Discord__ClientId` / `Discord__ClientSecret`), which is the same channel
   `ADR-013` item 11 and `ADR-015` D10 already use for server-side secrets.
   **No `.env` file is loaded by the host, and none is to be introduced.**

2. **D2 — `src/backend/.env.example` keeps a Discord section, redirected to the
   approved mechanism.** The file is a reference for configuration **key
   names**, not a place values are read from, because a `.env` file is not one
   of the host's providers. It must not instruct developers to create
   `src/backend/GameServer.Api/.env`, and it must name the approved channels
   (user-secrets locally, environment variables in deployment) rather than a
   file the host does not read. The same correction applies to the file's
   header instruction.

3. **D3 — the tracked `appsettings.json` placeholder stays an empty string.**

   ```json
   "Discord": {
     "ClientId": "<public client id>",
     "ClientSecret": ""
   }
   ```

   The tracked file carries no credential and no value that could ever
   authenticate. For D4's purposes, **"absent" means null, empty, or
   whitespace-only** — the same test `ApplicationSessionSigningKeys` already
   applies to the session signing secret (`string.IsNullOrWhiteSpace`). An empty
   tracked placeholder therefore cannot be mistaken for a usable credential.

4. **D4 — missing-credential behavior is environment-sensitive.**
   In **Production** (and any environment that is not Development) the host
   **fails to start** when `Discord:ClientId` or `Discord:ClientSecret` is
   absent: Discord is production's only identity path, so a host that cannot
   perform the exchange must not serve requests as if it could. In
   **Development** the host starts without them, and `POST /api/auth/discord`
   keeps the **unchanged** `503 DISCORD_UNAVAILABLE` (`API_CONTRACTS.md` §2.6),
   so no Player is created and no session is issued. TASK-181's development
   authentication is untouched and remains independent: it still requires both a
   Development host **and** the explicit `DevelopmentAuthentication:Enabled`
   opt-in, and it is unavailable in Production. No anonymous fallback and no
   second authentication path is introduced.

5. **D5 — rotation is manual, operator-owned, and event-triggered; there is no
   scheduled cadence in MVP.** The operator who administers the Discord
   application rotates the credential in the Discord Developer Portal, then
   updates the approved configuration channel (D1). Because configuration is
   read during host composition, a rotation takes effect only after the backend
   is **restarted or redeployed**. Rotation is required when:

   ```text
   suspected or confirmed compromise
   accidental exposure (committed, logged, shared, or captured in a backup,
     archive, IDE index, or an artifact)
   developer offboarding or loss of custody of the credential
   provider-side credential regeneration
   loss of confidence in the value for any other reason
   ```

   No automated rotation, no KMS, and no secrets-manager infrastructure is
   introduced, matching `ADR-015` D11's manual, operator-driven MVP stance. The
   backend consumes exactly **one** configured credential and assumes no overlap
   window — unlike D11's `kid`-based JWT key overlap, a Discord credential
   rotation is not absorbed by a second accepted value.

6. **D6 — the existing local credential is rotated once, as a precaution.**
   The credential currently in `appsettings.Development.json` is rotated once
   and then stored through the approved channel (D1). It was never committed and
   appears nowhere in git history, but it has sat in plaintext in the working
   tree, so its exposure surface beyond the file itself is not knowable from
   this repository. Rotation is an **operator action**: TASK-036 records it and
   verifies it was performed, and does not perform a rotation by invention.
   **The credential value is never reproduced** in this ADR, in any task file,
   in a commit message, in a log, or in a report.

## Security invariants

These hold in every environment and are not weakenable by implementation
choice. They restate `ADR-007` item 3, `ADR-013` items 11–12, and
`API_CONTRACTS.md` §2.7, and they are the acceptance basis for TASK-036:

```text
The Discord ClientSecret is never committed — no tracked file, no build
  output, no git history.
The Discord ClientSecret is never sent to the frontend — not in a Vite
  variable, a client bundle, React config, Phaser code, or a public asset.
The Discord ClientSecret is never returned by any API response, header, or
  error envelope (API_CONTRACTS.md §2.6 / §6).
The Discord ClientSecret is never logged — not in logs, telemetry, or error
  messages (API_CONTRACTS.md §2.7 item 5).
The Discord ClientSecret is supplied only through an approved server-side
  configuration provider (D1) and read by the backend alone.
The tracked appsettings.json carries no secret (D3).
The repository's existing .gitignore secrets entries remain effective and are
  not weakened or removed.
Only the Discord ClientId — a public identifier — may reach the frontend.
```

## Why

- **The boundary already existed; only the mechanism was missing.** `ADR-007`
  item 3 fixed where the secret lives and where it must never go. This ADR
  supplies the channel that satisfies it without reopening that boundary.
- **User secrets is already this project's local channel, and needs no new
  code.** `GameServer.Api.csproj` declares a `UserSecretsId`, and the session
  signing secret is already documented as living there. ASP.NET Core reads the
  store in Development by default, so D1 introduces **no** configuration loader
  and no custom provider — the smallest change that closes the gap
  (`ARCHITECTURE.md` §5 anti-overengineering, `AGENTS.md` §9).
- **The status quo is the defect.** Keeping the credential in the gitignored
  `appsettings.Development.json` would leave TASK-036 with nothing to
  remediate: it is exactly the plaintext-at-rest condition the task exists to
  remove, and it is the channel `ADR-015` D10 forbids for the signing secret.
- **A `.env` file is not a provider.** Choosing it would require inventing a
  loader to justify a template, and it currently points at a path that is not
  even git-ignored — trading a commit guard for a false sense of safety.
- **Production must fail closed.** Discord identity is production's only login
  path; a host that starts without the credential can only serve an
  authentication boundary that is permanently broken. Failing at composition
  surfaces the misconfiguration before the service accepts traffic.
- **Local development must stay permissive.** The Discord exchange client is
  not implemented yet, and TASK-181's local browser flow is a working,
  deliberate development path. Failing startup everywhere would break it for no
  security gain, because Development imposes no production exposure.
- **Manual rotation matches the MVP's existing security posture.** `ADR-015`
  D11 already chose operator-driven keys with no KMS; introducing automated
  rotation or a secrets manager would be a new infrastructure decision that
  deserves its own ADR, not a side effect of this one.

## Consequences

### Positive

- **TASK-036 is unblocked.** Every decision its remediation depended on is now
  recorded, so its implementation can proceed without guessing.
- The credential leaves the working tree: with D1, the gitignored
  `appsettings.Development.json` is no longer a required secret channel.
- `src/backend/.env.example` becomes truthful: it names the channels that
  actually supply values, and it no longer points at an unconsumed,
  un-ignored path.
- Production misconfiguration is caught at startup rather than at first login.
- No storage or runtime boundary moves: `ADR-005`, `ADR-006`, `ADR-013`, and
  `ADR-015` are all unaffected, and no new infrastructure is introduced.

### Negative

- **Local setup is per-developer and not shared.** A user-secrets store is
  machine-local, so each developer must populate their own store; there is no
  repository-supplied local credential, by design.
- **Rotation costs a restart.** Because configuration is read at composition, a
  rotated credential is not live until the backend is restarted or redeployed.
- **No scheduled cadence means no forced hygiene.** With D5, a credential that
  is never suspected of exposure may live indefinitely; that residual risk is
  accepted for MVP.
- **Production now has one more way to fail to start**, which is intended but
  must be reflected in deployment runbooks and environment configuration.

### Trade-offs

- **User secrets over a gitignored file:** the value leaves the tree entirely at
  the cost of per-developer setup.
- **Environment variables over a `.env` file in deployment:** the native
  provider is used at the cost of losing a single copy-pasteable file — the
  `.env.example` template remains only as key-name documentation.
- **Empty tracked placeholder over a marker string:** the tracked file stays
  byte-minimal and carries no value that could be mistaken for a credential, at
  the cost of the file not self-documenting; D2's template is where the
  documentation lives.

## Alternatives Considered

### Option A — Keep the gitignored `appsettings.Development.json` (status quo)

Rejected. It is the plaintext-at-rest gap TASK-036 was created to remediate: a
real credential in the working tree whose only protection is a `.gitignore`
entry, which guards against accidental commit but not against a filesystem
reader, a backup or sync tool, an IDE index, a shared machine, or an archive.
`ADR-015` D10 explicitly forbids this file as a secret channel for the session
signing key, so retaining it for the Discord credential would make the
repository's own secret rule inconsistent between two server-side secrets.

### Option B — An uncommitted `.env` file plus a custom loader

Rejected. The host has no `.env` provider, so this requires **adding** a
configuration loader purely to keep a template's current wording — new
code and a new dependency for no security gain over D1
(`ARCHITECTURE.md` §5, `AGENTS.md` §9). It also currently points at
`src/backend/GameServer.Api/.env`, a path `.gitignore` does not cover, so
following the instruction would create an un-ignored file containing the
credential in plaintext.

### Option C — Fail startup when the credential is absent, in every environment

Rejected. It would break TASK-181's local browser development path, which is
deliberate, explicitly opt-in, and unavailable in Production, and it would
prevent the backend from starting at all while the exchange client is still
unimplemented. D4 gets the production guarantee without either cost.

### Option D — Always start, and let the endpoint answer `503 DISCORD_UNAVAILABLE`

Rejected as the *whole* policy. It is the current behavior and is correct for a
not-yet-implemented exchange in Development, but in Production it lets a host
run indefinitely with Discord authentication permanently unusable while
appearing healthy to a liveness probe. D4 keeps this behavior in Development
only.

### Option E — Automated rotation, a KMS, or a secrets manager

Rejected. No such infrastructure exists in this repository or its deployment
(`docker-compose.yml` provisions PostgreSQL and Redis only), and `ADR-015` D11
set the MVP precedent of manual, operator-driven, no-KMS key handling.
Introducing a secrets-management service is an infrastructure decision that
needs its own ADR and its own scope check against
`docs/00-overview/MVP_SCOPE.md`.

### Option F — Environment variables as the canonical local channel too

Rejected as the *local* channel (it remains the deployment channel, D1). It
works and is native, but it requires every local shell, IDE, and test runner to
export the values per session, whereas the project's user-secrets store is
already addressable and already used. The two channels are complementary rather
than competing: user-secrets for a developer machine, environment variables for
deployment.

## Related Documents

- `docs/03-decisions/ADR/ADR-007-discord-activity-authentication.md` item 3 —
  the Client Secret boundary this ADR satisfies; unchanged
- `docs/03-decisions/ADR/ADR-013-discord-identity-exchange-contract.md` items
  11–13 — the canonical configuration keys and the exchange that consumes the
  credential; unchanged
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md`
  D7–D11 — the JWT signing key's own channel and rotation, whose scope excludes
  this decision; the precedent D5 follows
- `docs/02-technical/API_CONTRACTS.md` §2.6 (failure contract), §2.7 (security
  requirements: backend-only configuration, never committed, never logged), §6
  (error envelope)
- `docs/02-technical/TDD.md` §2.1 item 3 and
  `docs/02-technical/ARCHITECTURE.md` §2.3 item 2 — the boundary restated for
  the backend
- `src/backend/.env.example` (§D2), `.gitignore`,
  `src/backend/GameServer.Api/GameServer.Api.csproj` (the `UserSecretsId` D1
  addresses), `src/backend/GameServer.Api/Authentication/ApplicationSessionSigningKeys.cs`
  (the existing `IsNullOrWhiteSpace` absence test cited in D3)
- `tasks/backlog/TASK-036-discord-credential-secret-hygiene.md` — the task this
  decision unblocks (moved from `tasks/blocked/` when this ADR resolved its
  blocker)
- `tasks/completed/TASK-035-discord-identity-exchange-contract.md` — the
  exchange contract, which deliberately left credential storage to TASK-036
- `tasks/backlog/TASK-181-local-web-development-authentication-and-playability.md`
  — the development authentication path D4 must not disturb
