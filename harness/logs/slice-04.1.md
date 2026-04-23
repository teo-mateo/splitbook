# Slice 4.1 — Patch: Share DTOs between API and tests

## Specs in scope
- `specs/slice-plan.md` — Slice 4.1 section (refactor-only, no new endpoints)
- `specs/technical-spec.md` — REST contract DTO shapes

## Lessons cited at start
- **L-00** (read specs end-to-end) — read all specs before proceeding
- **L-02** (one slice's files only) — only DTOs and test files touched
- **L-H9** (first slice locks in code style) — this slice fixes the bad precedent of JsonDocument.Parse and duplicated test-local DTOs

## What was done
1. Created `tests/SplitBook.Api.Tests/Infrastructure/TestOnlyDtos.cs` with `ProblemDetailsDto` (the only legitimate test-only DTO).
2. Removed all test-local DTO records from `GetGroupEndpointTests.cs` (`RegisterDto`, `GroupCreateDto`, `GroupDetailDto`, `MemberDto`, `ProblemDetailsDto`) and replaced with `using` directives referencing API types.
3. Replaced `JsonDocument.Parse` with typed `ReadFromJsonAsync<T>` in `CreateGroupEndpointTests.cs` and `ListMyGroupsEndpointTests.cs`.
4. Replaced anonymous request objects with typed DTOs (`CreateGroupRequest`, `RegisterRequest`, `LoginRequest`) in test helpers.
5. `JsonDocument.Parse` retained only for single-field lookups: login token extraction and JWT payload claim inspection.

## Reviewer round count and findings
- **1 round** — status: `pass`, no findings.

## Scribe output
- No new lessons added to LESSONS.md. Principles exercised (L-H9, L-02) already covered.

## Open questions deferred to human
None.

## DoD checklist
- [x] `dotnet test` green — 40/40 tests pass
- [x] `grep -rn 'record [A-Z][a-zA-Z]*Dto' tests/` — only `ProblemDetailsDto` in `TestOnlyDtos.cs`
- [x] All API DTOs are `public`
- [x] Diff touches only test files and one new `TestOnlyDtos.cs` — no production code changes
