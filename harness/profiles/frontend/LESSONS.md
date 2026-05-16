# LESSONS.md — Frontend Profile

Curated lessons for the SplitBook **web frontend** harness. Max ~20 entries. Only the `lessons-scribe` subagent writes here, except `[HUMAN]` entries which the operator adds directly and which take priority. This file is independent of the backend profile's LESSONS.md.

Read this file in full at the start of every slice. Paraphrase the entries you consider relevant, in your first message.

---

### L-00: Read the spec end-to-end before writing the first test
- **Observed in:** seed
- **Lesson:** Before a slice starts, read `specs/product-spec.md`, `specs/technical-spec.md`, and the current row of `specs/slice-plan.md` in full. Do not rely on remembered summaries.
- **Why:** Medium-context models confabulate requirements when they skim. Every skipped read is a likely defect.

### L-H2 [HUMAN]: Primary writes no component logic before red
- **Observed in:** seed (carried from backend harness)
- **Lesson:** Between reading the specs and `@test-writer` returning RED, the primary's edits are limited to: `package.json` (deps), `vite.config.ts` / `tsconfig.json` / `tailwind.config.ts` / `postcss.config.js`, route registration in `routes.tsx`/`App.tsx`, and **empty placeholder components** (`export function CreateGroup() { return null }`) just sufficient for imports to resolve. Hooks, Zod schemas, React Hook Form wiring, TanStack Query queries/mutations, fetch calls, rendering/JSX with behavior — NONE of these before red is confirmed.
- **Why:** Writing components first and then tests-to-match inverts TDD and destroys the design-pressure signal the harness depends on.

### L-H1 [HUMAN]: Subagents MUST verify with the tools they have
- **Observed in:** seed (carried from backend harness)
- **Lesson:** `test-writer` must actually run `pnpm exec vitest run` (filtered) and quote the failing output before returning — never infer red from "I wrote a test." `reviewer` must actually run `pnpm exec vitest run`, `pnpm build`, and `pnpm lint` and quote results. If a command is unavailable or rejected, STOP and surface it — do not silently fall back to reading files and claim verification.
- **Why:** Unverified red/green is the core safety signal of the harness. Skipping it breaks everything downstream.

### L-H3 [HUMAN]: Thinking-model output budget
- **Observed in:** seed (carried from backend harness)
- **Lesson:** `max_tokens` must stay ≥ 32K (set in `opencode.json`). Multi-step subagent tasks routinely burn 10K–20K reasoning tokens before the first tool call. A low ceiling makes subagents return empty silently (`finish_reason=length` mid-think).
- **Why:** An empty subagent return forces the primary to improvise — a protocol violation caused entirely by a client-side setting.

### L-H4 [HUMAN]: Delegate research to @researcher
- **Observed in:** seed (carried from backend harness)
- **Lesson:** When stuck on a library API or framework behavior (TanStack Query cache invalidation, React Hook Form + zodResolver wiring, MSW 2 handler syntax, React Router v6 loaders/guards, Vitest/RTL queries, Playwright selectors) — invoke `@researcher` with a focused question; do not webfetch/websearch inline. Preferred order when stuck: (1) grep existing code for in-repo precedent, (2) `@researcher`, (3) guess and let the test cycle correct you.
- **Why:** Inline webfetch bloats the primary's context for every later turn; the researcher absorbs that cost in a disposable session and returns ~300 tokens.

### L-H7 [HUMAN]: Tests green ≠ app works — smoke the running app
- **Observed in:** seed (carried from backend harness)
- **Lesson:** Vitest runs components under jsdom with MSW-mocked network — it does not exercise the real Vite build, router, or bundling. For any slice that touches build config, routing, providers, or entry wiring, the slice is NOT done until `pnpm build` is clean AND `scripts/app.sh smoke` passes (dev server answers on `:5173` and `/` contains `SplitBook`).
- **Why:** The test harness is not the production harness. A slice can be 100% green under jsdom and 100% broken at `pnpm build` / in the browser — the worst failure mode because it looks fine until a human opens the page.

### L-H8 [HUMAN]: One test per @test-writer invocation
- **Observed in:** seed (carried from backend harness)
- **Lesson:** The primary invokes `@test-writer` **once per acceptance criterion**, never once per slice. Test-writer writes a single failing test, runs a filtered `pnpm exec vitest run`, returns. The primary then writes the minimum code to green that one test, then invokes `@test-writer` for the next criterion. Never ask for "all tests for this slice."
- **Why:** Batch test-writing produces multi-K-token reasoning spirals and malformed tool-call streams on this model class. One-test-per-invocation keeps traces short and contains failures.

### L-H10 [HUMAN]: Test-writer touches no shared test infrastructure + runs the full suite
- **Observed in:** seed (carried from backend harness)
- **Lesson:** `@test-writer` must NOT edit shared test infrastructure — the MSW server/handlers setup, `setupTests.ts`/`vitest.setup.ts`, the test render helper/`renderWithProviders`, or test `tsconfig`. Code there that looks redundant is load-bearing until proven otherwise; if it seems wrong, STOP and delegate to `@researcher` or return a blocked report. Always run BOTH the full `pnpm exec vitest run` (detect fleet-wide breakage) AND the filtered run (confirm your one new test's red). A crash in shared setup is never a valid "red."
- **Why:** Silent "helpful" edits to shared MSW/render setup break every other test while the filtered run still looks red-for-the-right-reason; the primary then loops fixing the wrong file.

### L-H11 [HUMAN]: New components must be stylistically homogeneous with existing ones
- **Observed in:** seed (carried from backend harness; reinforced by slice-plan "Mirror the nearest sibling")
- **Lesson:** Before writing a new component, `cat` the closest existing sibling in the same feature family and match its silhouette exactly: React Hook Form + `zodResolver` usage, Zod schema shape, error-display pattern (inline field errors below inputs), TanStack Query query/mutation pattern and cache-invalidation keys, the shared `components/` primitives (`Button`, `Input`, `Modal`…). Do not introduce a second form library, a second data-fetching style, or a second error-handling convention mid-project. If you believe the existing convention is wrong, STOP and raise it — do not silently fork the codebase.
- **Why:** This model imitates surface (folder layout, JSX skeleton) but re-invents deeper philosophy (state management, error flow) when unchecked. One divergent slice propagates and the codebase bifurcates within two or three slices. The reviewer gates this; the primary front-runs it by reading the neighbor first.

### L-FE1 [HUMAN]: Vitest runs non-watch; never start dev/watch from the Bash tool
- **Observed in:** seed
- **Lesson:** Always run tests as `pnpm exec vitest run` (one-shot). Bare `pnpm test`, `pnpm dev`, or `vite` started from the opencode Bash tool never returns (watch/long-lived process) and hangs the agent until the autopilot kills it — the frontend analogue of "never `dotnet run &`". The dev server is started only via `scripts/app.sh start` (it handles nohup/disown). Smoke = one HTTP probe at a time, never a bundled multi-step script (the permission gate auto-rejects large compound scripts and the agent freezes on the error).
- **Why:** Watch-mode hang plus silent-on-tool-error is the highest-frequency hard-stuck failure for this model class. Codify the non-watch invocation and surface every `status=error` within one turn.

### L-FE2 [HUMAN]: pnpm runs from src/SplitBook.Web, not the repo root
- **Observed in:** seed
- **Lesson:** The web package lives at `src/SplitBook.Web/` (created by slice 0). Every `pnpm install / build / lint / exec vitest / exec playwright` must run with that as the working directory (`cd src/SplitBook.Web && …`). The repo root has no `package.json`.
- **Why:** Running pnpm at the repo root fails confusingly ("no package.json") and the model tends to "fix" it by scaffolding a second project. There is exactly one frontend package and it is `src/SplitBook.Web`.

### L-FE3 [HUMAN]: Parse every API response through its Zod schema
- **Observed in:** seed (technical-spec §3.2)
- **Lesson:** Every API call parses its response through the matching Zod schema in `api/types.ts`. A parse failure is treated as a 500-class error (the API broke its contract), not swallowed. Do not hand-cast `as SomeType`; do not skip the parse "because the test mock is already shaped right." Money fields are `z.number()` (safe-integer range per spec) — no BigInt.
- **Why:** Defensive parsing at the boundary is a spec requirement and the only thing that catches contract drift between this frontend and the separately-built backend. Skipping it moves failures from a clear parse error to a confusing render-time crash.

### L-FE4: pnpm v10 requires `pnpm approve-builds` in non-interactive environments
- **Observed in:** slice 0 (Bootstrap)
- **Lesson:** When a package with build scripts (like `msw`) is newly added under pnpm v10, `pnpm install` prompts interactively to approve the build. In non-interactive shells (CI, agent tooling, scripts), this hangs and kills the session. Pre-approve with `pnpm approve-builds` or set `enablePrePostScripts=true` and `requiredScripts=allow-scope` in `.npmrc` to bypass the prompt.
- **Why:** This is the frontend equivalent of "interactive prompts hang non-interactive shells" — a class of failure that silently stalls the agent. Any future slice that adds a new dependency with lifecycle scripts will hit it again.

### L-FE5: TypeScript triple-slash references are required for framework global types
- **Observed in:** slice 0 (Bootstrap)
- **Lesson:** When using `strict: true`, TypeScript does not implicitly know about framework-provided globals. `import.meta.env` requires `/// <reference types="vite/client" />` in a `.d.ts` file included by `tsconfig`. Vitest globals (`describe`, `it`, `expect`) require `/// <reference types="vitest/globals" />`. Without these, `pnpm build` (which runs `tsc -b`) fails on test files even though `vitest run` works fine. Keep a single `src/vite-env.d.ts` (or equivalent) that collects all triple-slash references.
- **Why:** The divergence between "tests run" and "build fails" is the worst failure mode — it looks green until the DoD gate catches it. This pattern applies to any framework that injects globals (Vite, Vitest, Jest, Cypress) and will recur whenever a new tool with global types is added.

### L-FE6 [HUMAN]: The first test's query style is a propagating precedent — use Testing Library queries from test #1
- **Observed in:** slice 0 (Bootstrap) — `App.test.tsx` asserted with `document.querySelector('h1,...')` + `heading?.textContent` instead of `screen.getByRole('heading', { name: /SplitBook/i })`.
- **Lesson:** Every component test asserts via Testing Library accessible queries (`screen.getByRole/getByText/getByLabelText`) and `@testing-library/user-event` for interaction — never `document.querySelector`, never raw DOM traversal, never `?.textContent` chains. This is non-negotiable from the FIRST test, because L-H11 ("mirror the nearest sibling") makes the first test's style propagate verbatim into every later slice. If slice 0's test is wrong, fixing the lesson is not enough — the sibling test itself must be corrected or redone, or L-H11 will fight L-FE6 and the antipattern wins.
- **Why:** Direct frontend recurrence of the backend L-H9 failure (slice-1 `JsonDocument.Parse` antipattern propagating through every slice). This model imitates in-repo precedent with no quality filter; the precedent must be correct before slice 1 mirrors it.

### L-FE7 [HUMAN]: Bootstrap-slice shared infrastructure must be proven by a test that exercises it, not assumed from its shape
- **Observed in:** slice 0 (Bootstrap) — `src/test/setup.ts` exported a tidy `setupTestServer()` (MSW `beforeAll/afterEach/afterAll`) that nothing ever called; `vite.config` `setupFiles` loaded the module but the MSW lifecycle was never armed. Invisible because slice 0's only test didn't use MSW.
- **Lesson:** Anything later slices depend on and L-H10 freezes — the test setup file, the render helper, the API client, global providers — must have at least one slice-0 test that actually *drives* it (e.g. a test asserting an MSW-mocked response resolves, not merely that the app renders). Plausible-looking-but-unwired scaffolding passes green and detonates in the first slice that relies on it, where L-H10 forbids fixing it. Shared test infra must self-arm at module top level, never behind an exported function that nothing invokes.
- **Why:** Compounds L-H7 (green ≠ works) and is strictly worse in the bootstrap slice: it is the only slice allowed to author shared infra, so an inert-but-green piece is unrecoverable later without an operator override. The failure class generalizes (provider defined but not wrapped, route declared but not mounted, env var read but never set).

### L-FE8 [HUMAN]: Early slices scaffold lib/ and api/ as empty stubs, never implemented logic
- **Observed in:** slice 0 (Bootstrap) — `lib/money.ts` (incl. `Math.round` float money rounding) and `lib/dates.ts` shipped as real, untested logic with no failing test driving them, while `api/client.ts`/`api/types.ts` were correctly left as empty `export {}` stubs.
- **Lesson:** "A utility I will need later" with no failing test that requires it is an L-H2 violation and scope creep, regardless of how small. Scaffold `lib/`/`api/`/hooks the same way placeholder components are scaffolded — empty stub only — until a slice's test forces the implementation. Money/rounding logic in particular must never exist before a test pins its exact behavior.
- **Why:** Untested utility logic written ahead of need carries silent bugs (float rounding) into every slice that later imports it, and inverts the red-before-green design pressure. The scaffold contradicting its own discipline (api stubbed, lib implemented) shows the model will partially apply L-H2 unless told the rule is total.

### L-FE9: Cross-cutting concerns break previous slice tests — fix them proactively
- **Observed in:** slice 1 (Auth)
- **Lesson:** When a slice introduces global behavior (auth guards, route protection, error interceptors, providers), existing tests from previous slices will break because they were written without that context. Run the full `pnpm exec vitest run` immediately after adding global behavior, and fix regressions in previous slice tests before proceeding. Do not defer the fix to a later slice.
- **Why:** AuthGuard redirecting unauthenticated users broke `App.test.tsx` from slice 0. This pattern recurs whenever cross-cutting concerns are added (error boundaries, themes, i18n, global toasts). Deferring the fix creates technical debt and makes it harder to isolate failures in future slices.
