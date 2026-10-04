# TASK-179 — Recovery Blocker Record

**Created by:** the TASK-179 executor session
**Reason:** an external deletion removed three documentation files mid-task; this
record preserves the recovery findings so the next attempt does not repeat them.

---

## 1. What happened

During TASK-179 execution, three files that the task edits were **deleted from
disk between two verification steps**:

```text
docs/02-technical/GAME_STATE.md
docs/02-technical/REDIS_STATE.md
docs/01-game-design/RELIC_RULES.md
```

Git reported all three as deleted (`D`). The deletion was not caused by the
TASK-179 edits — those had been written and read back successfully first.

**Root cause of the data loss.** All three files carried **uncommitted**
increments from TASK-172, TASK-173, TASK-177 and TASK-178. `HEAD` only contains
older revisions:

```text
file                     HEAD revision    pre-deletion working tree
GAME_STATE.md            v2.19            v2.22  (+ TASK-179 → v2.23)
REDIS_STATE.md           v1.10            v1.11  (+ TASK-179 → v1.12)
RELIC_RULES.md           v1.12            v1.12 + TASK-176/178/179 increments
```

## 2. Recovery performed

The files were restored from `HEAD` (so they exist again), and the pre-deletion
content was then reconstructed from the DSH session transcript
(`~/.dsh/sessions/--E-dcacti--/<session>/session.v4.jsonl.zstd`), which is a
multi-frame zstd stream containing the `read` tool results captured earlier in
the same session.

Reconstruction used each read result's structured `meta.lines`
(`[{number, text}]`) rather than the rendered text, and replayed the recorded
`edit` calls (`old_string` → `new_string`) in order.

## 3. Coverage result — measured, not assumed

| File | Read generation | Lines captured | Verdict |
| --- | --- | --- | --- |
| `REDIS_STATE.md` | 669 total | **669 (100%)** | **fully recovered** |
| `GAME_STATE.md` | 2915 total | 1700 (58%) | not recoverable |
| `GAME_STATE.md` | 3301 total | 135 (4%) | not recoverable |
| `RELIC_RULES.md` | 1285 total | 175 (14%) | not recoverable |
| `RELIC_RULES.md` | 1293 total | 50 (4%) | not recoverable |

**Only `REDIS_STATE.md` is fully recoverable.** It has been restored to
**v1.12**, 742 lines, including TASK-179's `§7` item 17
(`PetState.BurnDamageModifiers[]`) and the TASK-178 increment in item 16.

`GAME_STATE.md` and `RELIC_RULES.md` cannot be reconstructed: their whole-file
reads were truncated by the read tool's output cap, the narrow offset reads come
from **two different file generations** whose line numbers refer to different
content (so line-by-line stitching is invalid), and `HEAD` does not contain the
missing regions at all.

A first stitching attempt did merge the two `GAME_STATE.md` generations; it
produced 1,558 `<<<MISSING n>>>` placeholder lines and misaligned numbering. It
was **not** written to the repository.

### 3.1 Why the recovered TASK-179 text cannot simply be written onto HEAD

The TASK-179 `GAME_STATE.md` content was recovered in full (all four edit
payloads: the §2.3 tree entry, §2.3.9, §2.3.10, and the version header), so the
tempting next step is to apply it to the restored v2.19 base. **That is not
safe**, because the v2.19 base predates TASK-178 and three claims in the
recovered text would become false statements about another collection:

```text
claim in the recovered TASK-179 text            v2.19 reality
----------------------------------------------  --------------------------------
"`ATKModifiers[]` ... whose element must         ATKModifier has exactly TWO
 carry one [`Lifetime`] because two lifetimes     members — `SourceIdentity` and
 coexist there"                                   `ATKModifierPercentage`.
                                                  No `Lifetime` member exists.

"§2.3.7 item 11"                                 §2.3.7 has items 1–10 only.

"§2.3.8 item 3" cited as the serialized-         §2.3.8 item 3 is "An element
 lifetime rule                                    serializes exactly these
                                                  members", listing
                                                  `sourceIdentity` and
                                                  `atkModifierPercentage`.
```

Writing the recovered text unchanged would therefore introduce dangling and
false cross-references — a new source-of-truth conflict (`AGENTS.md` §4, §22)
and a violation of TASK-179's own AC-19, which forbids inventing or renaming
anything and requires a reported blocker instead.

The safe part is not separable: the "no `Lifetime` member, and that is
deliberate" argument (AC-02/AC-03) is stated *by contrast* with
`ATKModifiers[]`, so rewording it would mean silently re-authoring TASK-178's
contract inside a TASK-179 edit.

## 4. Consequence for TASK-179

TASK-179's acceptance criteria are **not** all satisfiable:

```text
AC-10, AC-11 (REDIS_STATE.md §7)          PASS  — recovered and verified
AC-01…AC-09, AC-18 (GAME_STATE.md)        BLOCKED
AC-12, AC-18 (RELIC_RULES.md xref)        BLOCKED
AC-13, AC-14, AC-15, AC-16 (scope)        PASS  — no src/**, tests/**, migration
AC-17 (no gameplay decision introduced)   PASS
```

TASK-179 is therefore left in `tasks/backlog/` and **not** marked DONE.

The task's GAME_STATE.md content is fully specified and was written once
(§2.3 tree entry, §2.3.9, §2.3.10, §5.1.5, version header 2.23); it can be
re-authored from the task manifest's "Required Content" section once the
GAME_STATE.md base is restored.

## 5. What unblocks it

1. **Preferred:** restore `GAME_STATE.md` and `RELIC_RULES.md` from an editor
   buffer, backup, snapshot, or another checkout that holds the uncommitted
   TASK-172/173/177/178 revisions.
2. **Alternative:** authorize re-authoring the missing `GAME_STATE.md` /
   `RELIC_RULES.md` content from the task records. This reconstructs other
   tasks' documentation rather than restoring it, so it needs an explicit
   decision — it is outside TASK-179's own authority (`AGENTS.md` §7, §16, §17).
3. Commit early / enable a backup for future documentation tasks, since
   multi-task doc increments currently live only in the working tree.
