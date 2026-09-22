# TASK-021A — Resolve Damage Events SignalR Contract

**Status:** Done
**Created:** 2026-09-22
**Parent:** TASK-021

---

## Objective

Resolve the contract mismatch between `GAME_EVENTS.md` (which defines `DamageCalculated`, `DamageDealt`, `DamageTaken` as part of the Swap batch) and `SIGNALR_PROTOCOL.md` / `BattleEventWireProjection.cs` (which omitted them).

---

## Problem

`GAME_EVENTS.md` §1 defines 7 event types that cross SignalR in a Swap batch. `SIGNALR_PROTOCOL.md` §3.2.2 listed only 6 discriminators, and `BattleEventWireProjection.cs` only projected 6 event shapes. The 3 Damage events (`DamageCalculated`, `DamageDealt`, `DamageTaken`) were implemented in the Domain layer (`DamagePipeline.Calculate`) and appended by `BattleStateService.ResolveSwap` (lines 540–542), but the wire protocol and projection layer had no corresponding DTOs or switch arms.

This caused 3 integration test failures:
- `Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly` — expected count off by 3 (141 vs 144)
- `Swap_ReceiveEvents_ShouldSerializeAcrossTheSignalRJsonBoundary` — discriminator assertion missing damage types
- `Swap_ReceiveEvents_ShouldCarryNoMemberOutsideTheDocumentedSchema` — allowed-member switch missing damage types

---

## Changes

### Documentation

**`docs/02-technical/SIGNALR_PROTOCOL.md`**

- §3.2.2 discriminator: 6 → 7 values (added `DamageCalculated`, `DamageDealt`, `DamageTaken`)
- §3.2.12 total event count: updated to reflect 7 types
- New §3.2.13 `DamageCalculated` schema: `base` (int), `comboModifier` (double), `elementModifier` (double), `otherModifiers` (double), `defense` (double), `finalDamage` (int)
- New §3.2.14 `DamageDealt` schema: `source` (string), `target` (string), `amount` (int)
- New §3.2.15 `DamageTaken` schema: `source` (string), `target` (string), `amount` (int)
- Added JSON examples and normative notes for each

### Code

**`src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs`**

- Added 3 new DTO factory methods: `DamageCalculated(...)`, `DamageDealt(...)`, `DamageTaken(...)`
- Added 3 new projection methods: `ProjectDamageCalculated`, `ProjectDamageDealt`, `ProjectDamageTaken`
- Extended the `Project` switch to handle `BattleEventType.DamageCalculated`, `.DamageDealt`, `.DamageTaken`
- `DamageParty` enum projected to lowercase string via `.ToString().ToLowerInvariant()`
- C# keyword hazard: `base` reserved → factory parameter named `baseDamage` with `[JsonPropertyName("base")]`

### Tests

**`tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs`**

- `ProjectOne` helper: added switch arms for `DamageCalculated`, `DamageDealt`, `DamageTaken`
- `Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly`: added `DamagePipeline.Calculate` call to build expected damage events and append them to the expected list
- `Swap_ReceiveEvents_ShouldSerializeAcrossTheSignalRJsonBoundary`: added `"DamageCalculated"`, `"DamageDealt"`, `"DamageTaken"` to discriminator `Assert.Contains`
- `Swap_ReceiveEvents_ShouldCarryNoMemberOutsideTheDocumentedSchema`: added 3 damage types to the allowed-member switch with correct field lists; updated assertion to verify damage events are present

---

## Verification

```
dotnet build tests/backend/GameServer.Api.Tests/GameServer.Api.Tests.csproj --no-restore
dotnet test tests/backend/GameServer.Api.Tests/GameServer.Api.Tests.csproj --no-build
```

**Result:** 51 total, 51 passed, 0 failed, 0 warnings.

---

## Files Modified

| File | Change |
|---|---|
| `docs/02-technical/SIGNALR_PROTOCOL.md` | §3.2.2 discriminator, §3.2.12 count, new §3.2.13–§3.2.15 schemas |
| `src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs` | DTO fields, factories, projection methods, switch arms |
| `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` | 3 test fixes + ProjectOne helper |
