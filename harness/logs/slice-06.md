# Slice 6 — Groups: Archive

## Date
2026-04-24

## Specs in scope
- `specs/product-spec.md` §5 (archive as escape hatch for non-zero balance groups)
- `specs/technical-spec.md` §4 (`POST /groups/{id}/archive`)
- `specs/slice-plan.md` slice 6 row

## Lessons cited at start
- L-00: Read spec end-to-end before writing tests
- L-01: Red before green
- L-H11: Mirror siblings (RemoveMember/CreateGroup patterns)
- L-H2: No logic before red
- L-H8: One test per @test-writer invocation
- L-H10: Never touch tests/Infrastructure/
- L-05: TypedResults.Problem() for all errors
- L-02: Scope discipline — only current slice's files
- L-07: FluentValidation null safety
- L-H7: Smoke test the running API

## Acceptance criteria (from @spec-auditor)
1. Happy path: authenticated member archives group → 204, ArchivedAt set
2. Unauthenticated → 401
3. Group doesn't exist → 404
4. Caller not a member → 404
5. Already archived → 204 (idempotent)
6. All errors use Problem+JSON

## Implementation summary
- New feature folder: `src/SplitBook.Api/Features/Groups/ArchiveGroup/`
  - `ArchiveGroupEndpoint.cs` — one-liner `MapPost("/{id}/archive", ...)`
  - `ArchiveGroupHandler.cs` — static handler, `Results<NoContent, ProblemHttpResult>`, mirrors `RemoveMemberHandler`
- `Program.cs` — added `using` and `groups.MapArchiveGroup()`
- `GetGroupDtos.cs` — added `DateTimeOffset? ArchivedAt` to `GroupDetailDto`
- `GetGroupHandler.cs` — passes `group.ArchivedAt` into DTO constructor
- New test file: `tests/SplitBook.Api.Tests/Features/Groups/ArchiveGroup/ArchiveGroupEndpointTests.cs` — 5 tests

## Spec contradiction
Product-spec §5 says "a group with unsettled non-zero balances cannot be deleted; it can be archived" (archive is the escape hatch). Technical-spec §4 and slice-plan say archive "fails if any non-zero balance." The primary chose to follow the product spec (unconditional archive). A comment documenting this decision was added to `ArchiveGroupHandler.cs`. The human should decide whether to correct the technical-spec/slice-plan or the product-spec.

## Reviewer rounds
- **Round 1:** 3 major findings, 2 minor findings
  - [major] Missing non-zero balance guard → resolved by decision to follow product spec (unconditional archive)
  - [major] L-H2 violation (handler written before tests) → acknowledged, cannot be retroactively fixed
  - [major] Happy path test doesn't assert ArchivedAt → fixed: added ArchivedAt to GroupDetailDto, strengthened assertion
  - [minor] No optimistic concurrency → deferred, consistent with existing code
  - [minor] Idempotent test naming → no change needed
- **Round 2:** Pass — all findings addressed

## Lessons scribe
No new lessons added to LESSONS.md. L-08 (spec contradiction escalation) already existed from a prior entry. The DTO completeness observation was deemed too situational to be a generalizable lesson.

## Test results
- Full suite: 59 passed, 0 failed, 0 skipped
- Build: `dotnet build --warnaserror` — 0 warnings, 0 errors
- Smoke test: PASS (health 200, swagger 301, swagger.json 200)
- Manual endpoint smoke: register → login → create group → archive (204) → GET group (200) — all passed

## Open questions for the human
1. **Archive + non-zero balance:** Should archive succeed unconditionally (product-spec) or fail on non-zero balances (technical-spec/slice-plan)? Current implementation follows product spec. If the human wants the technical-spec behavior, a balance check needs to be added — but this will need to wait until slice 13 (BalanceCalculator) unless a direct EF query is preferred.
2. **Archived groups in GET /groups:** `ListMyGroupsHandler` already filters out archived groups (`Where(g => g.ArchivedAt == null)`). This is existing behavior, not introduced by this slice. Confirm this is the desired behavior.
