# Slice 5 — Groups: Add + Remove Member

## Specs in scope
- `specs/slice-plan.md` — Slice 5: `POST /groups/{id}/members`, `DELETE /groups/{id}/members/{userId}`
- `specs/technical-spec.md` — REST contract for groups members endpoints
- `specs/product-spec.md` — Membership concept, group membership rules

## Lessons cited at start
- **L-H11** (mirror the sibling) — AddMember/RemoveMember mirror CreateGroup silhouette: typed `Results<TOk, ProblemHttpResult>`, inline `TypedResults.Problem()`, one-liner endpoint map, static handler class, paired validator
- **L-H10** (test-writer boundaries) — never touch `tests/Infrastructure/`
- **L-H8** (one test per invocation) — one criterion, one test at a time
- **L-H2** (no logic before red) — scaffolding only before red confirmed
- **L-05** (TypedResults.Problem for all errors) — all non-2xx use `TypedResults.Problem()`
- **L-02** (scope discipline) — only touch current slice files

## What was done
This slice was already implemented in a prior session. The code and tests were verified in this session:

### AddMember (`POST /groups/{id}/members`)
- **AddMemberHandler.cs** — validates request, checks caller membership (404 if not member), looks up target user by email, checks for existing membership (409), inserts `Membership` row, returns 204.
- **AddMemberValidator.cs** — FluentValidation: email not empty, valid email format.
- **AddMemberDtos.cs** — `AddMemberRequest(string Email)`, public.
- **AddMemberEndpoint.cs** — one-liner `MapPost("/{id}/members", ...)`.

### RemoveMember (`DELETE /groups/{id}/members/{userId}`)
- **RemoveMemberHandler.cs** — checks caller membership (404), checks target membership exists (404), soft-removes by setting `RemovedAt`, returns 204.
- **RemoveMemberEndpoint.cs** — one-liner `MapDelete("/{id}/members/{userId:guid}, ...)`.

### Test coverage (14 tests total)
- **AddMemberEndpointTests.cs** (8 tests): happy path, caller not member (404), group not found (404), email not found (404), already member (409), no token (401), invalid email (400), Problem+JSON shape.
- **RemoveMemberEndpointTests.cs** (6 tests): happy path, caller not member (404), group not found (404), user not in group (404), no token (401), remove self (204).

## Reviewer round count and findings
- **0 rounds** — slice was already complete; no review cycle needed in this session.

## Scribe output
- No new lessons added to LESSONS.md.

## Open questions deferred to human
- RemoveMember does not yet enforce the "non-zero balance" guard (slice plan notes this is a stub made real in slice 11/Balances). This is by design per the slice plan.

## DoD checklist
- [x] `dotnet test` green — 54/54 tests pass
- [x] `dotnet build --warnaserror` clean
- [x] AddMember: lookup by email, 204 on success, 409 on duplicate, 404 on missing user/group/not-member, 400 on invalid email, 401 without token
- [x] RemoveMember: soft delete via `RemovedAt`, 204 on success, 404 on missing group/not-member/not-in-group, 401 without token
- [x] Both endpoints mirror CreateGroup silhouette (L-H11)
- [x] All error responses use `TypedResults.Problem()` (L-05)
