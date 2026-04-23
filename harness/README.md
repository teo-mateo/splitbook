# Harness Design

This document defines the iterative loop under which the local model (Qwen3.6-27B FP8 via vLLM, driven by opencode) will build the SplitBook backend.

## 0. Principles (non-negotiable)

1. **Spec is the ground truth.** `specs/*.md` is the contract. When the spec and the code disagree, the code is wrong until the spec is revised with a human in the loop.
2. **One slice at a time.** A session always targets exactly one slice from `specs/slice-plan.md`. No parallel work across slices.
3. **TDD cadence.** Red → green → refactor. Tests are written before the implementation they test, not after.
4. **Lessons are curated.** `harness/LESSONS.md` is the model's external memory. It is capped (~2K tokens), pruned, and written by the `lessons-scribe` subagent only — never by the primary builder.
5. **The human is the meta-reviewer.** Between slices, a human (you) reads the diff, the updated LESSONS.md, and the reviewer's final report. You may inject corrections before the next slice starts.

## 1. Agents

All subagents live in `.opencode/agent/` and are invoked via `@<name>` or auto-delegation from the primary agent.

| Name | Mode | Purpose | Tools |
|------|------|---------|-------|
| **primary** (default opencode agent) | primary | Implements the slice: writes tests, writes code, runs `dotnet test`, iterates until green | full |
| `spec-auditor` | subagent (plan mode — read-only) | Before a slice starts: reads the slice brief and the specs, produces the slice's acceptance criteria as a checklist. Blocks on any spec ambiguity it finds | read, grep |
| `test-writer` | subagent | Given the checklist from `spec-auditor`, writes the xUnit integration + unit tests for the slice. Does NOT write production code. Runs `dotnet test` and confirms **all new tests fail** (pure red) before returning | read, write, bash (scoped to `dotnet test`) |
| `reviewer` | subagent (plan mode — read-only) | After primary reports green: reads the diff, the specs, and LESSONS.md. Emits a structured report (`pass` / `findings[]`). Each finding has severity, file:line, and a one-line fix hint | read, grep, bash (scoped to `git diff`, `dotnet test`) |
| `lessons-scribe` | subagent | End of slice: reads the reviewer report and the session transcript, distills ≤3 new lessons, merges into LESSONS.md respecting the cap. Deletes lessons that newer ones supersede | read, write (LESSONS.md only) |
| `researcher` | subagent (read-only + web) | On demand: invoked by ANY other agent (primary, test-writer, reviewer) with a focused library/API/pattern question. Looks it up via webfetch/websearch/Context7 and returns a distilled answer + one example + sources. Keeps the caller's main context clean. | read, webfetch, websearch |
| `smoke-tester` | subagent | After primary's xUnit is green, extends `scripts/smoke.sh` with curl assertions for the slice's new endpoints, starts the real API, runs the smoke, returns pass/fail with quoted HTTP codes. Blocks the `@reviewer` step until smoke is green. Protects against the "tests green, `dotnet run` broken" failure (L-H7). | write/edit (`scripts/smoke.sh` only), dotnet build/run, curl, kill, pgrep |

## 2. Per-slice loop

**Primary does NOT write production logic before `@test-writer` returns RED.** Pre-test edits by the primary are limited to compile-enabling scaffolding: `.csproj`, `Program.cs` (DI wiring and route mapping only), `appsettings.json`, and empty placeholder types. Handler bodies, domain logic, hashing, token generation, EF configuration, mapping — all of that waits until red is verified. See `LESSONS.md` **L-H2**.

```
  ┌─ human starts a slice session in opencode ─┐
  │                                             │
  ▼                                             │
  primary reads: specs/*, LESSONS.md, slice #   │
  │                                             │
  ▼                                             │
  primary invokes @spec-auditor                 │
  │  → returns acceptance-criteria checklist    │
  ▼                                             │
  primary invokes @test-writer                  │
  │  → writes failing tests, confirms RED       │
  ▼                                             │
  primary implements production code            │
  │  → runs `dotnet test` until GREEN           │
  │  → refactors, tests still GREEN             │
  ▼                                             │
  primary invokes @smoke-tester                 │
  │  → extends scripts/smoke.sh                 │
  │  → runs API + curl assertions               │
  │  → returns pass/fail                        │
  │  if fail → primary fixes → @smoke-tester ──┘
  ▼
  primary invokes @reviewer                     │
  │  → report: {status, findings[]}             │
  │                                             │
  ├─ if findings → primary fixes → @reviewer ──┘
  │
  ▼
  primary invokes @lessons-scribe
  │  → LESSONS.md updated (or explicitly noted "no new")
  ▼
  human reads the diff + LESSONS.md + reviewer report,
  decides to accept / request rework / adjust specs,
  then moves to next slice.
```

Maximum **3 reviewer→fix rounds** per slice before the human is pulled in. This prevents loops where the model thrashes on an issue it cannot see.

## 3. Lessons memory protocol

- **File:** `harness/LESSONS.md`
- **Cap:** ~2000 tokens (roughly 280 lines). Scribe must enforce.
- **Shape:** one entry = one lesson. Each entry has:

  ```
  ### L-NN: <short title>
  - **Observed in:** slice #X
  - **Lesson:** <one or two sentences, imperative voice>
  - **Why:** <the failure or near-miss that triggered it>
  ```

- **Scribe rules:**
  - Merge near-duplicates. If a new lesson restates an existing one more precisely, replace the old one and keep the same ID.
  - Never keep more than the 20 most-recent or most-generalizable lessons.
  - Do not record fix recipes tied to a specific file/function — those rot fast. Record the principle.
  - If nothing new was learned, append nothing; state "no new lessons" in the session log instead.

- **Primary's contract:** at slice start, read LESSONS.md in full and paraphrase relevant entries back in its first message ("I am keeping in mind L-04 (invariants must be asserted) and L-07 (…)"). This forces use of the memory rather than performative reading.

## 4. Human oversight protocol (your role)

Between slices you:

1. **Scan the diff.** `git diff main...HEAD` (or `git log -p` for the last slice's commits). Look for: scope creep, dead code, abstractions that aren't earned, disabled tests.
2. **Read LESSONS.md.** Is the scribe distilling genuine lessons, or just summarizing what happened?
3. **Read the reviewer report from the session log** (`harness/logs/slice-NN.md`, see §5).
4. **Intervene cheaply if needed.** Your levers, cheapest first:
   - Add a line to LESSONS.md by hand, prefixed `[HUMAN]`. Everyone treats these as top priority.
   - Amend a spec file (product-spec / technical-spec / slice-plan). This is the "strong" correction.
   - Add an `.opencode/agent/*.md` subagent or tighten an existing one's prompt.
5. **Only the human** decides a slice is truly done and the next may begin.

## 5. Session logs

Every slice session must end with the primary writing `harness/logs/slice-NN.md`:

- which specs were in scope,
- which lessons were cited at start,
- reviewer round count and findings,
- what the scribe added to LESSONS.md,
- any open questions deferred to the human.

These logs are the record of the experiment. They are the data you'll analyze to decide whether the harness is good.

## 6. Known risks & mitigations (for this specific model)

- **Thinking-model token budget.** The model emits long reasoning traces in the `reasoning` field before producing `content`. In opencode, ensure `max_tokens` ≥ 8192 per call and that the provider config does not truncate reasoning deltas.
- **Tool-call fidelity on 27B.** Keep each subagent's tool surface ≤6 tools. Prefer `qwen3_coder` vLLM tool parser over generic.
- **Long-context degradation past ~32K per turn.** If a session grows beyond that, stop and summarize into a fresh session — do not push through.
- **Over-refactoring.** Smaller models love to "clean up" unrelated code. Reviewer's checklist must explicitly flag out-of-scope edits.
- **Hallucinated APIs.** The model will invent EF Core / MediatR method names. Tests that actually run catch this; that's why TDD is non-negotiable here.

## 7. What "success" looks like for THIS experiment

Forget the app for a moment — the experiment succeeds if, at the end, you can point to:

1. A reproducible harness (configs + agent prompts) that a fresh operator could rerun.
2. A LESSONS.md that is *predictive* of failures, not merely descriptive of the last one.
3. Concrete data on where this model class breaks: which slice, which kind of task, how many reviewer rounds, whether lessons actually helped.

The app being finished is a bonus. The harness and the findings are the deliverable.
