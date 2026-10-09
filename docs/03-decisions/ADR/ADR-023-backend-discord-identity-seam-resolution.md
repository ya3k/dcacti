# ADR-023: Backend Discord Identity Seam Resolution

**Status:** Accepted
**Date:** 2026-10-09

---

## 1. Context

Under `ADR-020`, the project transitioned to a 100% Web-First model with standalone web account authentication (Username/Password), superseding `ADR-007`, `ADR-013`, and `ADR-019`, and retiring the Discord Embedded App SDK and OAuth dependency.

However, a dormant backend dependency injection seam remained in the codebase:
- Interface `IDiscordIdentityResolver` and contract models in `src/backend/GameServer.Application/Identity/DiscordIdentityResolution.cs`.
- Resolver implementations `UnconfiguredDiscordIdentityResolver.cs` and `DevelopmentDiscordIdentityResolver.cs` under `src/backend/GameServer.Infrastructure/Discord/`.
- Configuration option classes `DiscordCredentialOptions.cs` and `DevelopmentAuthenticationOptions.cs` under `src/backend/GameServer.Infrastructure/Discord/`.
- DI container registrations in `src/backend/GameServer.Infrastructure/DependencyInjection.cs` (`AddSingleton<IDiscordIdentityResolver, UnconfiguredDiscordIdentityResolver>()` and `AddDevelopmentDiscordIdentityResolver()`).

`TASK-212` §A-20 and `TASK-217A` §10 R-6 identified this residue as requiring an explicit architectural decision: whether to formally delete the dormant seam or retain it as an intentional extension point.

A technical repository audit established the following evidence:
1. **Zero Consumers:** `IDiscordIdentityResolver` has zero live consumers in application services, controllers, or hubs. No route or handler invokes `ResolveAsync`.
2. **Unreachable Development Path:** `AddDevelopmentDiscordIdentityResolver()` has zero callers repository-wide. `DevelopmentAuthentication:Enabled` is not configured in any environment.
3. **Unused Configuration:** `DiscordCredentialOptions` is not read or validated by any active composition root.
4. **Blast Radius:** Physical removal entails deleting 5 source files, editing 1 DI registration file, and updating 2 test files in `GameServer.Api.Tests` where test fixtures referenced the types as substitution hooks.

Because selecting between retiring or preserving the seam constitutes an architectural policy choice, the decision was submitted to the Product Owner / Architecture Owner.

---

## 2. Decision

The Product Owner explicitly authorized **`REMOVE`** (Option 1):

1. **Retirement of Backend Seam:** The dormant backend Discord identity dependency injection seam (`IDiscordIdentityResolver`), resolver classes (`UnconfiguredDiscordIdentityResolver`, `DevelopmentDiscordIdentityResolver`), configuration options (`DiscordCredentialOptions`, `DevelopmentAuthenticationOptions`), and contract definitions (`DiscordIdentity`, `DiscordIdentityResolution`) are formally retired.
2. **Architecture Alignment:** The backend architecture is declared 100% decoupled from Discord identity abstractions, fully aligning backend architectural intent with `ADR-020` D1.
3. **Implementation Boundary:**
   - This ADR records the **architectural decision** to retire the backend Discord identity seam.
   - **Physical deletion** of the runtime C# source files, DI registrations, configuration classes, and test fixture compatibility refactoring is **deferred to future implementation work** and is explicitly **not performed within TASK-236**.
   - No runtime C# code, configuration, or tests are modified by this decision record.

---

## 3. Rejected Alternatives

### Option 2: RETAIN (Preserve as Intentional Dormant Extension Seam)

Rejected. Retaining the dormant seam would:
- Preserve dead, unexecuted wiring in the active ASP.NET Core dependency injection container.
- Create ongoing documentation drift and ambiguity against `ADR-020`, requiring permanent explanatory documentation in `ARCHITECTURE.md` for an unused platform integration.
- Introduce unnecessary maintenance overhead for interfaces and options with zero live consumers.

---

## 4. Consequences

- **Positive:**
  - Establishes a clean, unambiguous architectural direction: backend identity is exclusively owned by the standalone web account model (`ADR-020`) and stateless application session tokens (`ADR-015`).
  - Eliminates architectural ambiguity regarding whether Discord authentication might be partially supported in the backend.
  - Prepares the backend codebase for dead-code removal in downstream implementation work.
- **Implementation & Downstream Impact:**
  - Physical removal of the 5 C# files, removal of the DI registration in `DependencyInjection.cs`, and cleanup of affected test references in `GameServer.Api.Tests` will be executed in separate implementation work.
  - Until that implementation work lands, the runtime source files remain physically present in the repository but are architecturally retired per this ADR.

---

## 5. Related Documents

- `docs/00-overview/MVP_SCOPE.md` §1 — Standalone web account model (Discord unlisted / FUTURE)
- `docs/02-technical/ARCHITECTURE.md` §2.3, §3 — Authentication boundaries and component catalog
- `docs/02-technical/TDD.md` — Technical direction
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` — Application session token contract
- `docs/03-decisions/ADR/ADR-020-standalone-web-account-authentication.md` — Standalone web account authentication and Discord retirement
- `tasks/completed/TASK-212-post-task-211-product-audit.md` §A-20 — Gap identification for Discord DI residue
- `tasks/completed/TASK-217A-architecture-component-directory-reconciliation.md` §10 R-6 — Documentation audit of undecided DI seam
