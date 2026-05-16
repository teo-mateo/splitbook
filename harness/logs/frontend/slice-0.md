# Slice 0 — Bootstrap

**Date:** 2026-05-16
**Status:** Complete (DoD met)

## Specs in scope

- `specs/slice-plan.md` row 0: Bootstrap
- `specs/technical-spec.md` §1 (Stack), §2 (Project layout), §4 (Routing), §5 (Styling), §8 (Test strategy), §9 (Definition of done)

## Lessons cited at start

All 14 entries from LESSONS.md read and paraphrased:
- L-00: Read spec end-to-end
- L-H1: Subagents verify with tools
- L-H2: No component logic before red
- L-H3: Thinking-model output budget
- L-H4: Delegate research to @researcher
- L-H7: Tests green ≠ app works — smoke the running app
- L-H8: One test per @test-writer invocation
- L-H10: Test-writer touches no shared test infrastructure
- L-H11: New components mirror siblings
- L-FE1: Vitest non-watch only
- L-FE2: pnpm from src/SplitBook.Web
- L-FE3: Parse API responses through Zod
- L-FE4: pnpm v10 approve-builds
- L-FE5: Triple-slash references
- L-FE6: Testing Library queries from test #1
- L-FE7: Shared infra must be proven by test
- L-FE8: lib/api scaffolded as empty stubs

## Acceptance criteria (from @spec-auditor)

23 criteria produced. Key ones exercised by tests:
- Criterion 10: App renders "SplitBook" heading at `/`
- Criterion 11: Test uses Testing Library accessible queries
- Criterion 13: MSW-mocked API response test proves shared infra works
- All 23 criteria satisfied (config files, structure, stubs verified by build/lint/smoke)

## TDD inner loop

### Criterion 10+11: SplitBook heading
- **@test-writer** wrote `src/App.test.tsx` with `screen.getByRole('heading', { name: /SplitBook/i })`
- **RED:** `Unable to find an accessible element with the role "heading"`
- **GREEN:** Added heading + minimal layout shell to `GroupsList.tsx`

### Criterion 13: MSW-mocked API
- **@test-writer** wrote `test/msw.test.ts` using `server.use()` + `fetch('/api/test')`
- **GREEN immediately:** MSW setup in `test/setup.ts` was correctly self-arming

## DoD verification

| Check | Result |
|-------|--------|
| `pnpm exec vitest run` | 2/2 passed |
| `pnpm build` | Clean (89 modules, 62KB gzipped) |
| `pnpm lint` | Zero warnings |
| `scripts/app.sh smoke` | PASS (dev server on :5173, `/` contains "SplitBook") |

## Reviewer

- **Round 1:** status `pass`
- **Findings addressed:**
  - Deleted `pnpm-workspace.yaml` (misnamed .npmrc content)
  - Removed empty `src/test/` directory
  - Minor note on `lib/constants.ts` having a trivial constant — acceptable

## Scribe

- No new lessons added. Existing lessons (L-FE4 through L-FE8) guided the slice successfully.

## Open questions deferred to human

None. All ambiguities from @spec-auditor resolved pragmatically:
1. `/` renders GroupsList directly (not redirect)
2. E2E files deferred to slice 16
3. ESLint flat config chosen
4. `.env` with `VITE_API_URL` scaffolded
5. `public/manifest.json` created as minimal stub
6. `hooks/useApi.ts` and `useConfirm.ts` scaffolded as empty stubs
7. All component files from §2 layout created as empty placeholders
