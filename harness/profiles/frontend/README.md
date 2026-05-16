# Harness Design — Frontend Profile

This document defines the iterative loop under which the local model (Qwen3.6-27B FP8 via vLLM, driven by opencode) builds the **SplitBook web frontend** (React + Vite + TypeScript). It is the frontend twin of the backend harness; the protocol is identical, only the toolchain differs.

> **Profile note.** This file is the active `harness/README.md` only while the **frontend** profile is selected (`scripts/harness-switch.sh frontend`). The backend profile ships its own copy. Switching profiles repoints the symlink — it does not merge the two.

## 0. Principles (non-negotiable)

1. **Spec is the ground truth.** `specs/*.md` is the contract. When spec and code disagree, the code is wrong until the spec is revised with a human in the loop.
2. **One slice at a time.** A session targets exactly one row of `specs/slice-plan.md`. No parallel work across slices.
3. **TDD cadence.** Red → green → refactor. The failing component test is written before the component it tests.
4. **Lessons are curated.** `harness/LESSONS.md` is the model's external memory — capped (~2K tokens), pruned, written only by `lessons-scribe`. The frontend profile keeps its **own** LESSONS.md, separate from the backend's.
5. **The human is the meta-reviewer.** Between slices a human reads the diff, the updated LESSONS.md, and the reviewer's final report, and may inject corrections before the next slice.

## 1. Agents

All subagents live in `.opencode/agent/` and are invoked via `@<name>`.

| Name | Mode | Purpose | Tools |
|------|------|---------|-------|
| **primary** (default opencode agent) | primary | Implements the slice: writes the failing test, writes components, runs `pnpm exec vitest run`, iterates until green | full |
| `spec-auditor` | subagent (read-only) | Before a slice: turns the slice row + specs into a flat acceptance-criteria checklist. Blocks on spec ambiguity | read, grep |
| `test-writer` | subagent | Given ONE criterion, writes ONE failing Vitest + React Testing Library test (MSW-mocked). No production code. Confirms red via `pnpm exec vitest run` | read, write, bash (scoped) |
| `reviewer` | subagent (read-only) | After green: reviews the slice diff against spec + lessons. Structured `pass`/`findings[]` report | read, grep, bash (scoped) |
| `lessons-scribe` | subagent | End of slice: distils ≤3 lessons into LESSONS.md within the cap | write (LESSONS.md only) |
| `researcher` | subagent (read-only + web) | On demand: focused React/Vite/TanStack/RHF/Zod/MSW/Playwright question → distilled answer + one example + sources | read, webfetch, websearch |

## 2. Per-slice loop

**Primary does NOT write component logic before `@test-writer` returns RED.** Pre-test edits by the primary are limited to compile-enabling scaffolding: `package.json` / `vite.config.ts` / `tsconfig.json` / `tailwind.config.ts`, route registration, and **empty placeholder components** (`export function Foo() { return null }`). Hooks, form schemas, query/mutation wiring, rendering logic, API calls — all wait until red is verified. See `LESSONS.md` **L-H2**.

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
  primary writes pre-test scaffolding           │
  │  → package.json, config, empty components   │
  │                                             │
  ▼                                             │
  for each acceptance criterion:  ◄─────────────┐
  │                                             │
  ├─ primary invokes @test-writer               │
  │    with ONE criterion                       │
  │    → writes ONE failing component test       │
  │    → pnpm exec vitest run (filtered) RED     │
  │                                             │
  ├─ primary implements minimal code            │
  │    for that one test only                   │
  │    → vitest run (filtered) GREEN             │
  │                                             │
  └─ next criterion ────────────────────────────┤
                                                │
  (all criteria green)                          │
  ▼                                             │
  primary runs FULL `pnpm exec vitest run`,     │
  `pnpm build`, `pnpm lint`, then               │
  `scripts/app.sh smoke` (L-H7)                 │
  ▼                                             │
  primary invokes @reviewer                     │
  │  → report: {status, findings[]}             │
  ├─ if findings → primary fixes → @reviewer ──┘
  │
  ▼
  primary invokes @lessons-scribe
  ▼
  primary writes harness/logs/frontend/slice-NN.md
  ▼
  human reviews, then releases the next slice.
```

**Inner loop — criterion-level TDD.** `@test-writer` is invoked ONCE PER ACCEPTANCE CRITERION, never once per slice. See **L-H8**.

Maximum **3 reviewer→fix rounds** per slice before the human is pulled in.

## 2.1 Toolchain commands (frontend)

All `pnpm` commands run **from `src/SplitBook.Web/`**, not the repo root (that directory is created by slice 0).

- Run tests (always non-watch): `pnpm exec vitest run` — full suite. Filtered: `pnpm exec vitest run -t "<test name>"` or by path.
- **Never** run bare `pnpm test` / `pnpm dev` / `vite` from the Bash tool — watch/dev processes do not detach and the caller hangs (frontend analogue of "never `dotnet run &`"). Use `scripts/app.sh` for the dev server.
- Type-check / build: `pnpm build` (must be clean, zero TS errors).
- Lint: `pnpm lint` (ESLint + typescript-eslint, **zero warnings**).
- App liveness: `scripts/app.sh smoke` — clean build + dev server on `:5173` + probes `/` contains `SplitBook`. Green tests ≠ working app.
- E2E (slice 16 only): `pnpm exec playwright test` against a real API per technical-spec §8.

## 3. Lessons memory protocol

- **File:** `harness/LESSONS.md` (frontend profile's own copy).
- **Cap:** ~2000 tokens (~280 lines). Scribe enforces. Hard cap 20 entries.
- **Shape:** `### L-NN: <title>` + `Observed in` / `Lesson` / `Why` bullets.
- **Scribe rules:** merge near-duplicates (keep ID), drop file-specific recipes (record the principle), never touch `[HUMAN]` entries, append nothing if nothing was learned.
- **Primary's contract:** at slice start, read LESSONS.md in full and paraphrase the relevant entries back in the first message.

## 4. Human oversight protocol

Between slices the human: (1) scans the diff for scope creep / dead code / disabled tests, (2) reads LESSONS.md for genuine distillation vs narration, (3) reads `harness/logs/frontend/slice-NN.md`, (4) intervenes cheaply — a `[HUMAN]` line in LESSONS.md (top priority), a spec amendment (strong), or a subagent-prompt tightening, (5) decides when the slice is done and the next may begin.

## 5. Session logs

Every slice ends with the primary writing `harness/logs/frontend/slice-NN.md` (note the `frontend/` subdir — the backend profile writes to `harness/logs/slice-NN.md`; keeping them separate avoids the slice-number collision since both plans number 0..16):

- which specs were in scope,
- which lessons were cited at start,
- reviewer round count and findings,
- what the scribe added to LESSONS.md,
- open questions deferred to the human.

## 6. Known risks & mitigations (this model)

- **Thinking-model token budget.** `max_tokens` ≥ 32K (set in `opencode.json`). Low ceilings make subagents return empty mid-think.
- **Tool-call fidelity on 27B.** Subagent tool surface ≤6 tools.
- **Long-context degradation past ~32K/turn.** Stop and summarise into a fresh session rather than push through.
- **Over-refactoring.** Small models "tidy" unrelated code — the reviewer must flag out-of-scope edits.
- **Hallucinated APIs.** The model invents TanStack Query / RHF / Zod / MSW method names. Tests that actually run catch this — TDD is non-negotiable.
- **Watch-mode hang.** Bare `pnpm test`/`pnpm dev` never returns under the Bash tool. Codified in L-FE1.

## 7. What "success" looks like

Same as the backend experiment: a reproducible harness, a *predictive* LESSONS.md, and concrete data on where this model class breaks on frontend work (which slice, which task kind, reviewer rounds, whether lessons helped). The app shipping is a bonus; the harness and findings are the deliverable.
