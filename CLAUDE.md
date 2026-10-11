# CLAUDE.md

**Role:** orientation only. This file tells an agent where to look — it is
not a source of truth. The binding contract is [`AGENTS.md`](AGENTS.md);
when anything here disagrees with `AGENTS.md` or `docs/`, follow those and
report the conflict per `AGENTS.md` §4.

---

## What this project is

A match-3 + auto-battler game (Discord Activity) with an **authoritative
.NET backend** and a **Phaser 4 / React frontend**. The server computes all
gameplay outcomes; the client only requests actions and renders events.

## Read before coding

1. `AGENTS.md` — behavior contract (always; it is already loaded as project instructions).
2. `docs/00-overview/MVP_SCOPE.md` — IN/OUT/FUTURE scope (check before any new system/content).
3. Domain rules in `docs/01-game-design/` and technical docs in `docs/02-technical/` —
   see the required-reading table in `AGENTS.md` §6; read only what the task touches.
4. ADRs in `docs/03-decisions/ADR/` for *why* a technical choice exists.
5. `.ai/` — agents/skills/workflow execution layer (how to execute, never a source of truth).

## Source-of-truth hierarchy (summary)

```text
docs/00-overview + docs/01-game-design  (game design — most specific domain doc wins)
  > docs/02-technical/  > ADRs  > .ai/  > tasks/  > code  > tests
```

Never invent game rules, mechanics, content, architecture, or scope. Missing
rule or ambiguity = stop and report (`AGENTS.md` §7, §20).

## Repository layout

```text
AGENTS.md            binding AI behavior contract
CLAUDE.md            this file (orientation)
docs/                00-overview, 01-game-design, 02-technical, 03-decisions/ADR
.ai/                 agents/ skills/ workflow/ (execution layer, below docs/)
tasks/               backlog/ active/ blocked/ completed/ + TASK_* templates
src/backend/         GameServer.sln — .NET 10 modular monolith
  GameServer.Api | Application | Domain | Infrastructure
src/frontend/client/ Vite + TypeScript + React 18 + Phaser 4 + SignalR client
tests/backend/       xUnit: Api, Application, Domain, Infrastructure test projects
scripts/             repo-level helper scripts
docker-compose.yml   Postgres + Redis (+ services) for local runs
```

## Common commands

```bash
# Backend
dotnet build src/backend/GameServer.sln
dotnet test  src/backend/GameServer.sln

# Frontend (cwd: src/frontend/client)
npm run dev          # Vite dev server
npm run test:run     # vitest, single pass
npm run build        # tsc && vite build

# Local infra
docker compose up -d
```

## Hard rules (pointer, not restatement)

- **Server-authoritative**: never compute damage/HP/RNG/rewards on the client
  beyond discardable optimistic prediction (`AGENTS.md` §10).
- **Determinism/RNG**: follow `TDD.md` §6 server-seeded strategy only (`AGENTS.md` §11).
- **Boundaries**: PostgreSQL = persistent only; Redis = active battle state only;
  Phaser = battle rendering; React = app UI (`AGENTS.md` §13).
- **Scope**: anything not listed as IN in `MVP_SCOPE.md` §1 is OUT or FUTURE (`AGENTS.md` §8).
- **Process**: Understand → Plan → Implement → Test → Review → Update docs (`AGENTS.md` §14–§17).
- **Report format**: Understanding / Relevant Documentation / Plan / Changes / Tests /
  Documentation / Remaining Issues (`AGENTS.md` §21).
