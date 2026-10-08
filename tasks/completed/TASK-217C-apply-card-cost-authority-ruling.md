# TASK-217C — Apply the Card-Cost Authority Ruling to `SIGNALR_PROTOCOL.md` §3.2.20 Item 2

> **Retrospective reconstruction authored under TASK-224, Product Owner Decision 2 =
> A (evidence-only).** The original TASK-217C file was never authored, so this record
> reconstructs the task from artifacts only: documents, git history, and the task
> records that cite the id. Anything no artifact proves is labelled `NOT PROVEN`
> (`tasks/active/TASK-224-commit-step-and-task-209-record.md:359–375`). This record
> writes exactly one file — itself. No existing file was edited, and no writing git
> command was issued (`TASK-223-commit-worktree-ownership.md` §6.2 P-6).

---

## Metadata

```text
Task ID:            TASK-217C
Type:               DOCUMENTATION
Status:             PARTIAL / NOT CLOSED — the commissioned change (the Card-cost
                    ruling in SIGNALR_PROTOCOL.md §3.2.20 item 2) is ABSENT; four
                    reconciliations the version prose attributes to the same id are
                    PRESENT in the working tree (uncommitted)
Risk:               LOW (documentation prose only; no artifact claims a code, schema,
                    wire, or rule change)
Priority:           P3 (documentation) — TASK-213:611; reaffirmed unchanged by
                    TASK-219A-post-implementation-audit.md:851,965 and
                    TASK-221A-post-implementation-audit.md:1101
Primary Agent:      documentation (per TASK-213:611 "DOC"; not otherwise recorded)
Supporting Agents:  N/A
Workflow:           .ai/workflow/documentation/documentation-change.md (inferred)
Skills:             NOT RECORDED — the task file was never authored. Existing
                    entries: .ai/skills/quality/documentation-consistency.md,
                    .ai/skills/discovery/documentation-discovery.md
Dependencies:       TASK-213 (§8 ruling, §9 item 7 assignment); TASK-224
                    (retrospective-record authorization, Decision 1 = A / 2 = A)
Evidence status:    PARTIALLY PROVEN
Lifecycle note:     reconstructed retrospectively; the original task file was never
                    authored, so tasks/TASK_LIFECYCLE.md §3 cannot be applied as
                    written: this is neither an `IN REVIEW → DONE` record nor a
                    `→ SUPERSEDED` record.
```

All line numbers are the working-tree state read at `HEAD = 9b5c5fe`
(`9b5c5fe8e9c3b0e2b7025e558ae3f8a32a23f527`). The tree is shared and was being modified
by other tasks while this record was written; nothing in the declared file set is owned
by this record except this record itself.

---

## 1. Objective

Apply the Card-cost authority ruling decided by TASK-213 to
`docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.20 (`CardCast`) item 2 — correcting the
superseded sentence *"A client that must show the spent Cost reads the Card's
definition"* — and land the documentation reconciliations that the repository's own
revision-history headers attribute to the TASK-217C id (`TDD.md` v1.6,
`DATABASE.md` v1.38, `REDIS_STATE.md` v1.13, `ARCHITECTURE.md` v1.8).

**Reconstructed finding.** The first half is **ABSENT**: the superseded sentence is
still present, verbatim and unaltered, at `SIGNALR_PROTOCOL.md:1131–1133` (decisive
clause on `:1132`). The second half is **PRESENT but uncommitted**, and its attribution
to TASK-217C rests on revision-history prose alone.

---

## 2. Authoritative References

- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.20 item 2 (`:1124–1133`); §4 item 15
  (`:1536–1561`) and §4.3 item 15 (`:2049–2054`) — governing text; §3.2.24 item 4
  (`:1312–1315`) — the surviving "no cost member" cross-reference.
- `docs/01-game-design/CARD_RULES.md` §3.6 — owner of `EffectiveCardCost`, cited by
  `TASK-213:552–556,562–563`. Cited, not restated; not modified.
- `tasks/completed/TASK-213-content-reachability-decision.md` §8, §8.1, §8.2, §8.3,
  §9 item 7, §9.1, §11.2 (N-05); `TASK-219A-post-implementation-audit.md` §9, §10
  (P-5); `TASK-221A-post-implementation-audit.md` §9.5, §10;
  `TASK-218A-relic-trigger-presentation-audit.md` §9.4;
  `TASK-223-commit-worktree-ownership.md` §6.2, §10 F-5;
  `TASK-217B-post-implementation-audit.md` §1–§3;
  `tasks/active/TASK-224-commit-step-and-task-209-record.md` Decision 2 = A
  (`:359–375`).
- `tasks/TASK_LIFECYCLE.md` §3, §4. Revision-history headers naming the decision
  source: `TDD.md:3–10`, `DATABASE.md:3–13`, `REDIS_STATE.md:3–14`,
  `ARCHITECTURE.md:15–26`.
- `AGENTS.md` §2, §17 and `docs/AGENTS.md` §2, §17 (precedence; documentation change
  rule).

**Precision note on the process policy.** The brief describes P-3 as requiring an
exact declared file set per record. Read literally, `TASK-223…md` §6.2 records P-3 as
the *ownership precondition* ("no file may be committed under a task that has no record
in `tasks/completed/`"); the exact-declared-file-set requirement is P-2 (`:515–521`)
and P-5 (`:531–533`). §7 satisfies P-2/P-5, and this record exists so that P-3 is
satisfied for these paths.

---

## 3. Scope

**In scope** (as commissioned by the citing records): correcting the superseded
sentence in `SIGNALR_PROTOCOL.md` §3.2.20 item 2 (`TASK-213:586–588` "a
DOCUMENTATION-ONLY change to one document's wording"; `TASK-213:611` "One sentence."),
and recording with evidence the four reconciliations the revision-history headers name
TASK-217C as decision source for.

**Out of scope:** any wire member, member set, event, hub method, or schema element
(`TASK-213:579–585`); re-deciding the ruling (`TASK-213:588`); Card cost/affordability
display (TASK-212B stays DEFERRED — `TASK-221A…md:1096,1101`); rewriting any completed
task record (`tasks/TASK_LIFECYCLE.md:218`); any item OUT in `MVP_SCOPE.md` §2.

---

## 4. Evidence Base (`path:line`)

```text
CURRENT DOCUMENT TEXT (read directly during this reconstruction)
  SIGNALR_PROTOCOL.md  :1099 §3.2.20 heading; :1124-1133 item 2 incl. the
      superseded sentence (clause :1132); :1312-1315 §3.2.24 item 4;
      :1536-1561 §4 item 15; :2049-2054 §4.3 item 15
  TDD.md               :3-10 v1.6 (TASK-217C); :358-367 §4 item 2
  DATABASE.md          :3-13 v1.38 (TASK-217C); :911-926 §1 resolver note;
      :1262-1335 §1 atomicity block incl. item 7
  REDIS_STATE.md       :3-14 v1.13 (TASK-217C); :188-201 §3 lifecycle;
      :555-563 §7 item 11 superseding note
  ARCHITECTURE.md      :3-14 v1.9 (TASK-217A); :15-26 v1.8 (TASK-217C);
      :689-697 §3 component table; :724-733 §4 item 4

CITING RECORDS  (per-change detail in §6)
  TASK-213…md :533-556, :559-575 (superseded text :573-574), :579-592
      (assignment :586-588), :611, :638-644, :735 (N-05); TASK-219A…audit.md
      :851, :915, :965; TASK-221A…audit.md :988, :1025-1029, :1101;
      TASK-218A…audit.md :641-653; TASK-223…md :504-538 (P-1…P-6), :786-788;
      TASK-224…md :134-136, :359-375; TASK-217B…audit.md :1-3
```

`afe5b14` and `9b5c5fe` carry a **byte-identical** §3.2.20 item-2 paragraph
(normalised hash `6BDE143B2EF629B3`, length 733 both): its line number moved
`:1074 → :1119 → :1132`, its text never did.

---

## 5. The §3.2.20 item 2 sentence, quoted and adjudicated

`SIGNALR_PROTOCOL.md:1124–1133` (item 2, abridged) still reads:

```text
   … The Card's Cost is a definition value owned by `CARD_RULES.md` §2 — not a wire
   member — and the authoritative record of what a cast did to `PetState.Power` is the
   state value itself, delivered by the §4 push (`GAME_STATE.md` §2.3). A client that
   must show the spent Cost reads the Card's definition; a client must not recompute
   it from the event (`GAME_RULES.md` §18, ADR-001).
```

**The superseded sentence is STILL THERE.** Its decisive clause is on
`SIGNALR_PROTOCOL.md:1132`: *"that must show the spent Cost reads the Card's
definition; a client must not"*. TASK-213 §8.2 declared that sentence superseded
(`:573–574`); the governing text is §4 item 15 / §4.3 item 15 (`:1536–1561`,
`:2049–2054`). The intra-document contradiction is therefore still live: §4 item 15
(`:1557–1560`) forbids reconstructing a Card-cost value "from a Card's definition",
which is exactly what §3.2.20 item 2 instructs a client to do.

**Escape margin, recorded so the correction is not over-widened.** §3.2.24 item 4
(`:1312–1315`) cross-references §3.2.20 item 2 for the *surviving* TASK-104 **A-2C**
ruling (no cost member on `CardCast`). Only the "reads the Card's definition" clause is
superseded; the no-cost-member ruling is not, and the correction must not delete it.

---

## 6. Per-change disposition (REQUIRED)

| # | Change | Citing-record requirement | Current state | Current line numbers | Evidence |
|---|---|---|---|---|---|
| C1 | `SIGNALR_PROTOCOL.md` §3.2.20 item 2 — replace "A client that must show the spent Cost reads the Card's definition" with the TASK-213 §8.2 authority rule | `TASK-213:586–588`, `:611` ("One sentence"), `:735`; `TASK-219A…md:851`; `TASK-221A…md:1025–1029`; `TASK-224…md:367` | **ABSENT — superseded sentence present verbatim** | `:1124–1133`; clause `:1132` | Current read; identical normalised paragraph at `afe5b14` and `9b5c5fe`; `git show 9b5c5fe -- …SIGNALR_PROTOCOL.md` has no §3.2.20 hunk; the string is in HEAD **and** the working tree |
| C2 | `TDD.md` — §4 item 2 synchronised with the battle-end persistence/atomicity contract | Version prose only: `TDD.md:3–10`, "Decision source: TASK-217C" (`:10`) | **PRESENT (uncommitted)** | `:358–367` | Current read; `git log -1 -- TDD.md` = `afe5b14` (v1.5) and TDD is not in `9b5c5fe`; `git status` = ` M` |
| C3a | `DATABASE.md` — §1 battle-end atomicity block gains item 7 (explicit scope statement) | `DATABASE.md:3–5` (v1.38 block) | **PRESENT (uncommitted)** | `:1328–1335` (block `:1262–1335`) | Current read; the block is credited inline to TASK-217A (`:1263`), item 7 to TASK-217C (`:3–5`) |
| C3b | `DATABASE.md` — §1 `BossDefinition` resolution note reconciled to the existing `BossDefinitionLookup` boundary instead of `PersistenceRepository (Postgres)` | `DATABASE.md:8–11` (v1.38: "reconciled in the same change under TASK-217C") | **PRESENT (uncommitted)** | live note `:911–926` (naming `:916–919`) | Current read; the old name survives only inside historical v1.11 prose at `:271` (version log, not a live statement) |
| C4a | `REDIS_STATE.md` — §3 lifecycle entry: delete only after the battle-end unit of work has committed | `REDIS_STATE.md:3–8`, `:14` (v1.13 block) | **PRESENT (uncommitted)** | `:188–201` | Current read; `git log -1 -- REDIS_STATE.md` = `e8f149b` (v1.12); not in `9b5c5fe`; ` M` |
| C4b | `REDIS_STATE.md` — §7 item 11 gains a concise superseding reference (TASK-040/TASK-041 sequencing retained) | `REDIS_STATE.md:9–10` (v1.13 block) | **PRESENT (uncommitted); attribution split** | `:555–563` | Current read; the inline note credits the current contract to **TASK-217A** (`:555`) while the version block credits the edit to TASK-217C (`:14`) |
| C5a | `ARCHITECTURE.md` — §3 component table no longer names types that do not exist; `PersistenceRepository (Postgres)` replaced by the boundaries that perform the battle-end writes | `ARCHITECTURE.md:15–26` (v1.8 block, "Decision source: TASK-217C" at `:26`) | **PRESENT (uncommitted)** | `:689–697` | Current read; `git show HEAD:…ARCHITECTURE.md` (v1.7) names `PersistenceRepository (Postgres)` in the §3 table and §4 item 4; the working tree keeps the name only inside the v1.8 prose (`:20`) |
| C5b | `ARCHITECTURE.md` — §4 item 4 states the ordering rule and cites the owning contract instead of naming `PersistenceRepository` | `ARCHITECTURE.md:15–26` (v1.8 block) | **PRESENT (uncommitted); region rewritten twice** | `:724–733` | Current read; the same region is also claimed by the v1.9/TASK-217A block (`:3–14`), and the single uncommitted diff cannot separate v1.8 from v1.9 by artifact alone |

**Disposition summary:** the chartered deliverable (C1) is **ABSENT** (1 of 8
changes); the attributed reconciliations (C2, C3a/b, C4a/b, C5a/b) are **PRESENT**
and uncommitted (7 of 8).

**What the evidence proves, and does not.** A revision-history block naming a decision
source is evidence that *an edit landed under that id in that document*; it is **not**
evidence of the edit's content. For C2–C5 the content is verifiable because the
current text matches what the block describes — but authorship of that text by
TASK-217C is asserted by prose only. In C5b one text region is claimed by two
different ids (v1.8/TASK-217C and v1.9/TASK-217A), and no artifact separates them.

---

## 7. Declared file set (REQUIRED)

Markers: `COMMITTED-IN-9b5c5fe` = listed by `git show --name-only 9b5c5fe`;
`WORKING-TREE-MODIFIED` = ` M` in `git status --porcelain`;
`WORKING-TREE-UNTRACKED` = `??`; `ABSENT` = the commissioned change is not in the
file.

| Path | Evidence | Marker |
|---|---|---|
| `docs/02-technical/SIGNALR_PROTOCOL.md` | Commissioned target (C1). No TASK-217C marker in the file; superseded sentence at `:1132`; HEAD already contains it (`:1119`) | **ABSENT** (change); the file itself is `COMMITTED-IN-9b5c5fe` at v2.18 and `WORKING-TREE-MODIFIED` at v2.19 for another task's content |
| `docs/02-technical/TDD.md` | `:3–10` names TASK-217C; §4 item 2 present at `:358–367`; `git log -1` = `afe5b14`; not in `9b5c5fe`; ` M` | **WORKING-TREE-MODIFIED** |
| `docs/02-technical/DATABASE.md` | `:3–13` names TASK-217C; item 7 and the resolver note present; the file is in `9b5c5fe` but at v1.36 (TASK-221 content), not this content; ` M` | **WORKING-TREE-MODIFIED** |
| `docs/02-technical/REDIS_STATE.md` | `:3–14` names TASK-217C; `:188–201` and `:555–563` present; `git log -1` = `e8f149b`; not in `9b5c5fe`; ` M` | **WORKING-TREE-MODIFIED** |
| `docs/02-technical/ARCHITECTURE.md` | `:15–26` names TASK-217C; `:689–697` and `:724–733` present; `git log -1` = `afe5b14`; not in `9b5c5fe`; ` M` | **WORKING-TREE-MODIFIED** |
| `tasks/completed/TASK-217C-apply-card-cost-authority-ruling.md` | This reconstruction record, created by this task; `??` | **WORKING-TREE-UNTRACKED** |

```text
git show --name-only --oneline 9b5c5fe | grep docs/02-technical
    API_CONTRACTS.md, DATABASE.md, GAME_STATE.md, SIGNALR_PROTOCOL.md
    (no TDD.md, no REDIS_STATE.md, no ARCHITECTURE.md)
git status --porcelain -- docs   M ROADMAP.md, BOSS_RULES.md (not this task);
    M ARCHITECTURE.md, DATABASE.md (TASK-217A + TASK-217C); M REDIS_STATE.md,
    TDD.md (TASK-217C); M SIGNALR_PROTOCOL.md (TASK-226, NOT TASK-217C)
git log --all --grep=217   ->  NO MATCH
```

No commit hash can be declared for TASK-217C: no commit message in the 41-commit
history names the id, and no path's TASK-217C content is committed. The path set
above is the set this record declares; because `ARCHITECTURE.md`, `DATABASE.md` and
`REDIS_STATE.md` also carry TASK-217A's uncommitted content, a future commit must be
an explicitly named group commit (P-2), not a per-task slice.

---

## 8. Acceptance criteria and verification status

TASK-217C has **no authored acceptance criteria** — the file was never written
(`NOT FOUND`: `glob **/*217C*` → no files; `tasks/completed/`, `tasks/active/`,
`tasks/backlog/`, `tasks/blocked/` listed; repo-wide `grep 217C` → citations only).
The criteria below are reconstructed from the citing records.

| # | Criterion (source) | Status |
|---|---|---|
| AC-1 | `SIGNALR_PROTOCOL.md` §3.2.20 item 2 no longer instructs a client to read the Card's definition for a spent cost, and states that any shown cost is a server-delivered value (`TASK-213:559–575`, `:586–588`) | **NOT SATISFIED** — sentence present at `:1131–1133` |
| AC-2 | The correction leaves the surviving TASK-104 **A-2C** "no cost member" ruling intact (`SIGNALR_PROTOCOL.md:1124–1125`, cross-referenced at `:1312–1313`) | **NOT APPLICABLE** — no correction exists to constrain |
| AC-3 | The four reconciliations the version prose attributes to TASK-217C are present in `TDD.md`, `DATABASE.md`, `REDIS_STATE.md`, `ARCHITECTURE.md` | **SATISFIED** (content present; each read directly, §6) |
| AC-4 | Documentation-only: no wire member, member set, event, hub method, schema element, or gameplay value added or removed (`TASK-213:579–585`) | **PARTIALLY VERIFIED** — the changed passages read as prose-only and every version block asserts it, but the complete uncommitted hunks were not re-reviewed line by line |
| AC-5 | The record declares an exact file set (P-2 / P-5; the brief's P-3 framing) | **SATISFIED** — §7 |
| AC-6 | Tests unchanged and passing | **NOT APPLICABLE** — see §9 |

---

## 9. Tests / Validation

```text
No test applies to this task and no test run is recorded for it.
```

- TASK-217C is a documentation task (`TASK-213:611` classifies it `DOC`); no source,
  test, migration, fixture, or contract file is claimed by its scope.
- **No test evidence exists for TASK-217C anywhere**: no record file exists, no commit
  message names the id, and no citing record reports a suite, a command, or a result
  for it (`TASK-219A…md:851`, `TASK-221A…md:1025–1029` record only that the work is
  *unmoved*).
- The only commands executed were read-only git and file reads. `git diff --check --
  docs` reported **PASS (exit 0)** — whitespace hygiene of the current tree only, and
  **not** evidence that any TASK-217C change was made.

---

## 10. Remaining issues

```text
R-1  OPEN — the superseded sentence is still present. SIGNALR_PROTOCOL.md:1132 still
     instructs a client to read the Card's definition for the spent cost,
     contradicting §4 item 15 (:1557-1560) and §4.3 item 15 (:2049-2054) in the same
     document — the state TASK-219A…md:851 and TASK-221A…md:1025-1029 recorded as
     "unchanged" (TASK-213:611's stated reason for the task).

     OWNERSHIP: no record owns this correction once this reconstruction exists.
     TASK-224 Decision 2 = A (:359-375) commissioned six evidence-only records and
     this is TASK-217C's; TASK-217C had no executing record, and this record may not
     perform the edit (it writes one file and edits nothing). The correction needs a
     newly assigned documentation task id; none is invented here. Priority stays P3
     while TASK-212B is deferred (TASK-221A…md:1096,1101).

R-2  OPEN (attribution conflict) — the id carries two non-identical scopes. The
     revision-history prose credits TASK-217C with the battle-end atomicity
     documentation synchronisation; TASK-213:611 and TASK-224:367 commission
     TASK-217C for the Card-cost ruling only. TASK-217B…audit.md:1-3 identifies the
     family's actual use as TASK-217A = "Atomic Battle Reward Persistence" and
     TASK-217B = its audit — not TASK-213 §9.1's proposed split (:638-644). Which
     scope TASK-217C executed cannot be settled from artifacts; a record owner or the
     Product Owner must.

R-3  FLAGGED, NOT SCOPED — the neighbouring clause (:1128-1130, "The Card's Cost is a
     definition value owned by CARD_RULES.md §2"). TASK-213:549-556 records that the
     spent value is EffectiveCardCost, "a runtime value, not stored state", and that a
     definition read yields a datum that is "not even the right value". TASK-213's
     assignment is "One sentence" (:611) and §8.2 names only the read-the-definition
     sentence as superseded (:573-574); whether the definition-value clause must also
     change is NOT DECIDED and is NOT added to the declared scope here.

R-4  DISCLOSURE — every C2-C5 edit is uncommitted working-tree text shared with other
     tasks' content in the same files; this record neither stages nor claims it
     (P-2/P-3/P-5; TASK-223:786-788 F-5).

NOT PROVEN (per TASK-224 Decision 2 = A): that TASK-217C executed anything (its only
footprint is the id inside four version-history blocks); the content of those edits as
authored work; any date, agent, effort, skill list, or acceptance criterion of the
original task (no task file was ever authored); any commit hash for TASK-217C; any test
or validation result for it.
```

---

## 11. Revision History

```text
1.0  Retrospective reconstruction of TASK-217C, authored under TASK-224 Product Owner
     Decision 2 = A (evidence-only) and Decision 1 = A (TASK-223 §6.2 P-1…P-6
     binding). Writes one file. No existing file edited, no git writing command
     issued, no commit hash or date invented for the reconstructed task. Status:
     PARTIAL / NOT CLOSED, not DONE — the commissioned Card-cost correction is ABSENT
     and remains open (R-1).

TASK-217C — RECONSTRUCTED (evidence-only)
Chartered deliverable (SIGNALR §3.2.20 item 2 correction)      ABSENT
Attributed reconciliations (TDD/DATABASE/REDIS/ARCHITECTURE)    PRESENT, UNCOMMITTED
Attribution of those reconciliations to TASK-217C              PROSE ONLY
Evidence status                                               PARTIALLY PROVEN
Files written by this task                                    1 (this record)
Git write operations performed                                0
```
