---
description: Start the next frontend slice from specs/slice-plan.md following the harness protocol
---

We are starting a new slice session for the **SplitBook web frontend** (React + Vite + TypeScript). The web package lives at `src/SplitBook.Web/` — slice 0 scaffolds it; later slices extend it. All `pnpm` commands run from that directory, never the repo root (L-FE2).

## Preflight — backend MUST be running (hard gate)

**Very first action of the session**, before reading any spec or invoking any subagent. Run:

```bash
bash scripts/fe-backend-check.sh
```

If it exits non-zero: **FULL STOP.** Do not read specs, invoke subagents, write code, or write a slice log. Report its output as your only reply — the operator must start the backend. There is no "plan from prose" fallback. (Only exception: a `slice-plan.md` row with zero API surface — pure styling/layout — may proceed; state explicitly you verified it touches no endpoint.)

On success the script has written `specs/openapi.json` — the authoritative contract the steps below and `@spec-auditor` use.

Before doing anything else:

1. Read `specs/product-spec.md`, `specs/technical-spec.md`, and `specs/slice-plan.md` in full.
2. Read `harness/README.md` and `harness/LESSONS.md` in full.
3. Look at `harness/logs/frontend/` to see which slices are already completed. The next slice is the first row in `slice-plan.md` whose `harness/logs/frontend/slice-NN.md` does not exist. (Frontend logs live under `harness/logs/frontend/`, NOT `harness/logs/` — that directory holds the unrelated backend run's logs and both plans number 0..16.)
4. The live backend API contract is already at `specs/openapi.json` (saved by the preflight above). Read it and, for the current slice, identify **which specific endpoint(s) from the contract this slice needs** — list them (method + path) in your first reply. A slice uses only the endpoints its feature requires; do not pull in unrelated ones.

**API data strategy (applies to every slice that touches the API):** the *application code* calls the **real backend** — `api/client.ts` targets `VITE_API_URL` (default `http://localhost:5000`), and every endpoint path, HTTP method, request body, and response shape MUST match `specs/openapi.json` exactly (not the prose in the specs — the live contract wins; if they disagree, that is an ambiguity for `@spec-auditor` to flag). The *tests* never hit the network: MSW handlers stand in for the backend, and each handler's URL/method/payload MUST mirror the same `specs/openapi.json` entry it doubles for. A test passing against an MSW handler that doesn't match the real contract is a false green — treat contract-fidelity of the mocks as part of every API acceptance criterion.

Then, in your first reply, output:

- The slice number and name you are about to work on.
- A short paragraph paraphrasing the lessons from `LESSONS.md` you consider relevant to this slice (do not copy them — show you've internalized them).
- Your plan in three bullets max: what you will ask `@spec-auditor` about, what you expect the red state to look like, and the first production change you'll make after reaching red.

Then follow the per-slice loop documented in `harness/README.md` §2 exactly. Do not skip steps. Do not merge roles. Invoke the subagents with `@spec-auditor`, `@test-writer`, `@reviewer`, and `@lessons-scribe` at the points the protocol specifies.

**IMPORTANT — test-writer is invoked once per acceptance criterion, not once per slice.** After `@spec-auditor` returns N criteria, run an inner loop: for each criterion, invoke `@test-writer` with that single criterion, get one failing test (verified red via `pnpm exec vitest run` filtered), implement the minimal code to make it pass, then the next criterion. See `harness/README.md` §2 and `LESSONS.md` L-H8. Do NOT ask `@test-writer` to write "all tests for this slice" — it WILL fail.

**Never run bare `pnpm test`, `pnpm dev`, or `vite` from the Bash tool** — they don't detach and you will hang (L-FE1). Tests run as `pnpm exec vitest run` (non-watch). The dev server runs only via `scripts/app.sh`.

At the end, write `harness/logs/frontend/slice-NN.md` with the session summary described in §5 of the harness README. Do not commit — that is a human decision.

**Git is forbidden in this run.** The autopilot (an out-of-band controller) owns all git operations. Never invoke `git` — the permission system rejects it. The reviewer reads the slice diff from `harness/logs/runs/slice-context/` (staged by the autopilot), not from `git diff`. If you need the previous commit, read `harness/logs/runs/slice-context/last-commit.txt`.
