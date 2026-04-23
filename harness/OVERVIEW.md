# Harness Overview

A one-page explanation of how this project's AI coding harness works. Designed to be read cold.

## What we're doing

A **local open-weight model** (Qwen3.6-27B-FP8 on our own vLLM server) is building a medium-sized .NET REST API. The goal isn't the app; it's the **harness** — a setup that makes a small-ish thinking model reliably deliver correct code by pacing it, constraining it, and giving it feedback that grounds its reasoning.

We run the model through **opencode** (an open-source, Claude-Code-style CLI). opencode supports custom **subagents** defined as markdown files with YAML frontmatter, each with its own system prompt and its own scoped toolset.

## The actors

```
                    ┌────────────────┐
                    │     human      │   meta-reviewer; adjusts prompts/specs
                    └────────┬───────┘
                             │ between slices
                             ▼
┌──────────────────────────────────────────────────┐
│                   primary                         │   implements the slice
│  (default opencode agent — reads everything)      │
└────┬──────────┬──────────┬──────────┬─────────────┘
     │          │          │          │
     │ on demand for any uncertainty  │
     │                                 │
     ▼          ▼          ▼           ▼            ▼
  spec-     test-      researcher   reviewer     lessons-
  auditor   writer     (webfetch    (read only)  scribe
  (read     (tests     /search)                  (LESSONS.md
   only)    only)                                 only)
```

### Who does what

| Agent | Purpose | Can do | Cannot do |
|---|---|---|---|
| **primary** | Writes the production code. The "builder." | everything | — |
| **spec-auditor** | Turns the slice's spec rows into an acceptance-criteria checklist. Flags ambiguities. | `read`, `grep` | anything else |
| **test-writer** | Given the checklist, writes failing xUnit tests and runs `dotnet test` to confirm red state. | `write` (tests only), `dotnet test/build` | write production code |
| **researcher** | On-demand lookup. When *any* agent is uncertain about a library API or framework behavior, it delegates here. Returns a distilled answer + one code example + source URLs. | `read`, `webfetch`, `websearch` (MCP) | write, edit, bash |
| **reviewer** | Audits the slice's diff against spec + lessons. Verifies tests green. Emits structured `{status, findings[]}` report. | `read`, `git diff/log`, `dotnet test` | write, edit |
| **lessons-scribe** | At end of slice, distills ≤3 new lessons into LESSONS.md. | write LESSONS.md only | touch anything else |

## The inputs (fixed ground truth)

```
specs/product-spec.md      ← what the app does (stable)
specs/technical-spec.md    ← stack, endpoints, test strategy (stable)
specs/slice-plan.md        ← ordered 17 slices, 1 endpoint each (stable)
DECISIONS.md               ← architectural decisions (pinned by human)
```

All four files are loaded as `instructions` at every session start. They evolve only by the human's hand. The model grades against a moving-but-not-drifting target.

## The curated memory (evolves, model-written)

```
harness/LESSONS.md         ← capped at ~20 entries; scribe-curated
                             [HUMAN]-prefixed entries take priority
```

Read in full by every new primary at slice start. The primary must paraphrase the lessons it considers relevant in its first message — a forcing function to actually use the memory rather than performatively list it.

## Per-slice loop

```
  human: /next-slice
              │
              ▼
      primary reads specs + DECISIONS + LESSONS
              │
              ▼
      @spec-auditor → acceptance-criteria checklist
              │
              ▼
      primary writes SCAFFOLDING ONLY (csproj, DI wiring, empty placeholders)
              │                           ▲
              ▼                           │ (anyone can call
      @test-writer ─────────────────────── @researcher for
              │  writes failing tests      library/API questions)
              │  runs `dotnet test`
              │  confirms RED
              ▼
      primary IMPLEMENTS production code until GREEN
              │
              ▼
      @reviewer (up to 3 rounds)
              │
              │  if `fail` → primary fixes → @reviewer again
              │  if `pass` → continue
              ▼
      @lessons-scribe → appends ≤3 new lessons to LESSONS.md
              │
              ▼
      primary writes harness/logs/slice-NN.md (session summary)
              │
              ▼
      human reviews diff + LESSONS.md + slice log,
      commits, decides to start next slice or adjust harness
```

## What makes this work (or not)

These are the non-obvious design choices discovered through failure:

1. **Scaffolding-only before test-writer.** The primary can write `.csproj`, DI wiring, empty placeholder types — nothing with a method body. Method bodies only come after tests exist and are red. Prevents tests-written-to-match-existing-code, the #1 small-model TDD failure.

2. **Curated, not append-only lessons memory.** Max 20 entries, scribe-pruned, `[HUMAN]`-entries are sacred. Prior art consensus (Reflexion, MemGPT, Aider) is that unbounded logs poison context and the model ignores them.

3. **Research as a subagent, not inline.** When the primary is uncertain about a library API, it calls `@researcher`, which pulls docs, distills to ~300 tokens, and returns an answer. An inline `webfetch` by the primary pulls 10K tokens of HTML into its conversation and bloats every subsequent turn. Delegation keeps the primary lean.

4. **Anti-deliberation protocols in subagent prompts.** Thinking models spiral. Subagent prompts include hard rules: "if you wrote 'OK let me write now', your NEXT action is a write tool call — not more reasoning." Fixed thinking budgets (~5K–6K reasoning tokens). Decide-once: no re-validating a classification later.

5. **One endpoint per slice.** Bigger slices mean more tests in one batch, more deliberation, bigger blast radius when something goes wrong. Current plan has 17 slices, each 1–2 endpoints.

6. **Output budget matters for thinking models.** vLLM `max_tokens` must be ≥32K. Qwen3's `<think>` blocks routinely eat 10K–20K before the first tool call. A low ceiling → subagent returns empty, harness protocol breaks.

## Human's role

Between slices, the human:

1. Reviews `git diff` of the slice.
2. Reads the new LESSONS.md entries — is the scribe distilling or just narrating?
3. Reads `harness/logs/slice-NN.md`.
4. Intervenes, cheapest lever first: add a `[HUMAN]` line to LESSONS.md → amend a spec → tighten a subagent prompt.
5. Commits and releases the next slice.

The specs stay stable across the experiment (they're what the model is graded against). The harness — subagent prompts, LESSONS.md, DECISIONS.md — is what evolves and what's being learned about.

## The experiment's output

Not the app. The app is a byproduct. What we care about:

- A reproducible harness a fresh operator could rerun.
- A LESSONS.md that's *predictive* of failures, not merely *descriptive* of the last one.
- Concrete data on where this model class breaks: which slices, which kinds of tasks, how many reviewer rounds, whether new lessons reduce future failures.
