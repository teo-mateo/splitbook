# Slice 2.1 — Auth: fix datetime schema validation (hotfix)

**Date:** 2026-05-16
**Specs in scope:** `specs/slice-plan.md` row 2.1, `specs/technical-spec.md` §3.2

## Lessons cited at start

- **L-00:** Read all specs end-to-end before writing tests.
- **L-H2:** No component logic before red — only schema relaxation after test fails.
- **L-H1:** Subagents must verify with actual tool output (vitest run).
- **L-H8:** One test per @test-writer invocation.
- **L-H10:** Test-writer touches no shared test infrastructure.
- **L-FE2:** pnpm runs from `src/SplitBook.Web/`.
- **L-FE3:** Parse every API response through its Zod schema.
- **L-FE5:** Triple-slash references for framework globals.
- **L-FE9:** Cross-cutting concerns break previous slice tests.

## What was done

### Root cause
`LoginResponseSchema.expiresAt` and `GroupDtoSchema.createdAt` used `z.string().datetime()`, which rejects the .NET `DateTimeOffset` wire format (`2026-05-17T16:28:52.6134551+00:00`) due to 7-digit fractional seconds and timezone offset.

### Changes
- **`src/api/types.ts`:** Relaxed `expiresAt` and `createdAt` from `z.string().datetime()` to `z.string()`.
- **`src/api/types.test.ts`:** New file with 3 tests:
  1. `LoginResponseSchema` accepts .NET datetime format
  2. `GroupDtoSchema` accepts .NET datetime format
  3. Regression: both schemas still accept standard ISO 8601 datetimes
- **`api/client.ts`:** No changes needed — `parseOrThrow` correctly delegates to `schema.parse()`.

### Acceptance criteria (from @spec-auditor)
1. `LoginResponseSchema` parses .NET datetime — **PASS**
2. `GroupDtoSchema` parses .NET datetime — **PASS**
3. Standard ISO 8601 datetimes still accepted — **PASS**
4. Regression test exists — **PASS**
5. `pnpm build` clean — **PASS**
6. Full test suite passes — **PASS** (9 files, 36 tests, 0 failures)

## Reviewer round count and findings

- **Round 1:** `@reviewer` returned **pass** with no findings.

## What the scribe added to LESSONS.md

- **L-FE10:** Zod `z.string().datetime()` rejects .NET DateTimeOffset wire format — use `z.string()` for datetime fields. Cross-cutting interop lesson for all future slices with datetime fields.

## Open questions deferred to the human

None. The spec-auditor flagged `api/client.ts` as listed in scope but requiring no changes — confirmed correct.
