# 30-app-benchmark — SplitBook under a local-model harness

An experiment: can Qwen3.6-27B-FP8, running on a self-hosted vLLM endpoint and driven by **opencode**, build a medium-sized C#/.NET REST API correctly, under a subagent-based harness with persistent curated "lessons" memory?

The app being built is **SplitBook**, a group-expense tracker (Splitwise-lite). The code is the side effect; the harness and the lessons data are the deliverable.

## Layout

```
specs/
  product-spec.md        # what SplitBook does, business rules, screens
  technical-spec.md      # stack, solution layout, domain, REST contract, test strategy
  slice-plan.md          # ordered vertical slices, one per session
harness/
  README.md              # iteration loop, subagent roles, oversight protocol
  LESSONS.md             # curated, capped, lessons-scribe-only (+ [HUMAN] entries)
  logs/                  # one session log per slice
.opencode/
  agent/                 # spec-auditor, test-writer, reviewer, lessons-scribe
  command/               # slash commands (e.g. /next-slice)
opencode.json            # provider (vLLM at heapzilla), model, permissions, instructions
```

## Prerequisites

- opencode installed (`npm i -g opencode-ai` or via their installer — check opencode.ai/docs).
- .NET 8 SDK.
- Env var set:

  ```bash
  export HEAPZILLA_API_KEY=llmd-...
  ```

## Running it

```bash
cd 30-app-benchmark
opencode          # pick up opencode.json automatically
> /next-slice     # in the opencode prompt
```

The slash command points the primary agent at the protocol in `harness/README.md` and it will invoke subagents as specified there.

## Your job as operator

Between slices, follow the protocol in `harness/README.md` §4:
1. Read the diff.
2. Read `harness/LESSONS.md` — is the scribe distilling or just narrating?
3. Read `harness/logs/slice-NN.md`.
4. If needed, inject a `[HUMAN]` line into LESSONS.md, amend a spec, or tighten a subagent prompt. Then release the next slice.

## The specs are fixed — the harness is not

Expect the specs to stay stable across the experiment (that's the point — the model is graded against a fixed target). Expect `harness/` to evolve: prompts get tightened, subagents get added or merged, LESSONS.md grows and is pruned. Keep track of these changes — they *are* the experiment's findings.

## What we are measuring

For each slice, from `harness/logs/slice-NN.md`:
- reviewer rounds to reach pass (ideal: 1)
- reviewer finding severities (blocker/major/minor/nit)
- out-of-scope edits caught
- invariants missed
- whether the slice's initial message cited applicable lessons, and whether the implementation honored them
- human interventions and their shape

A harness that works for this model will show these numbers improving across slices as LESSONS.md matures.
