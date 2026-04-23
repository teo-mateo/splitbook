# Slice 4 — Groups — Detail Session Log

## Specs in scope
- `specs/product-spec.md` — full read
- `specs/technical-spec.md` — full read (sections 1-6, 9)
- `specs/slice-plan.md` — row 4 (Groups — Detail)
- `DECISIONS.md` — D-01 (plain minimal APIs), D-02 (single project), D-04 (EnsureCreated)

## Lessons cited at start
- **L-00:** Read spec end-to-end before writing the first test
- **L-01 / L-H1:** Tests must be written first and confirmed RED with actual `dotnet test` output
- **L-H2:** No handler logic before test-writer confirms red — scaffolding only
- **L-H8:** One test per @test-writer invocation
- **L-02:** Stay within feature folder scope
- **L-05:** Use TypedResults.Problem() for Problem+JSON errors
- **L-H9:** First slice locks in code style — be aware of precedent propagation

## Context
Slice 4 was interrupted in a prior session. Production code (`GetGroupHandler`, `GetGroupEndpoint`, `GetGroupDtos`) already existed and was wired into `Program.cs`. No tests existed. This session completed the slice by adding tests and fixing a Problem+JSON compliance issue.

## What was built
- `tests/SplitBook.Api.Tests/Features/Groups/GetGroup/GetGroupEndpointTests.cs` — 7 integration tests covering all acceptance criteria
- `src/SplitBook.Api/Features/Groups/GetGroup/GetGroupHandler.cs` — fixed to return `TypedResults.Problem(title: "Not Found", statusCode: 404)` instead of `TypedResults.NotFound()` for both 404 cases; updated return type to `ProblemHttpResult`

## Acceptance criteria coverage
1. Happy path — member reads own group → 200 with detail ✅
2. Creator appears in members array ✅
3. Non-member gets 404 (not 403) ✅
4. Non-existent group gets 404 ✅
5. Unauthenticated request gets 401 ✅
6. Error responses use Problem+JSON ✅ (required production fix)
7. Members array reflects current membership only (removed members excluded) ✅

## Reviewer rounds
- **Round 1:** pass with 1 minor finding (test DTO `CreatedAt` type mismatch — `string` vs `DateTimeOffset`)
- Finding addressed: updated test DTO to use `DateTimeOffset`. No second round needed.

## Scribe output
- **L-05 updated:** broadened from "validation/business-rule errors" to "ALL non-2xx error responses" after slice 4 demonstrated the same `TypedResults.NotFound()` mistake. No new entries added.

## Open questions deferred to human
- None.

## Test count
- 40 tests total (33 from slices 0-3, 7 new for slice 4)
- All passing, build clean with `--warnaserror`
