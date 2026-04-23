---
description: Start the next slice from specs/slice-plan.md following the harness protocol
---

We are starting a new slice session for the SplitBook project.

Before doing anything else:

1. Read `specs/product-spec.md`, `specs/technical-spec.md`, and `specs/slice-plan.md` in full.
2. Read `harness/README.md` and `harness/LESSONS.md` in full.
3. Look at `harness/logs/` to see which slices have already been completed. The next slice is the first row in `slice-plan.md` whose log does not exist.

Then, in your first reply, output:

- The slice number and name you are about to work on.
- A short paragraph paraphrasing the lessons from `LESSONS.md` you consider relevant to this slice (do not copy them — show you've internalized them).
- Your plan in three bullets max: what you will ask `@spec-auditor` about, what you expect the red state to look like, and the first production change you'll make after reaching red.

Then follow the per-slice loop documented in `harness/README.md` §2 exactly. Do not skip steps. Do not merge roles. Invoke the subagents with `@spec-auditor`, `@test-writer`, `@reviewer`, and `@lessons-scribe` at the points the protocol specifies.

At the end, write `harness/logs/slice-NN.md` with the session summary described in §5 of the harness README. Do not commit — that is a human decision.
