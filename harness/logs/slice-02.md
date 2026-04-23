# Slice 2 — Groups — Create Session Log

## Specs in scope
- `specs/product-spec.md` — full read
- `specs/technical-spec.md` — full read (sections 1-6, 9)
- `specs/slice-plan.md` — row 2 (Groups — Create)
- `DECISIONS.md` — D-01 (plain minimal APIs), D-02 (single project), D-04 (EnsureCreated)

## Lessons cited at start
- **L-00:** Read spec end-to-end before writing the first test
- **L-01 / L-H1:** Tests must be written first and confirmed RED with actual `dotnet test` output
- **L-H2:** No handler logic before test-writer confirms red — scaffolding only
- **L-H4:** Delegate research to @researcher when stuck on API shapes
- **L-02:** Stay within feature folder scope
- **L-05:** Use TypedResults.Problem() for Problem+JSON errors
- **L-06:** Wire packages you add, or don't add them
- **L-H6:** Single-endpoint slice — manageable scope

## What was built
- `src/SplitBook.Api/Domain/Group.cs` — Group entity (Id, Name, Currency, CreatedByUserId, CreatedAt, ArchivedAt?, Version)
- `src/SplitBook.Api/Domain/Membership.cs` — Membership entity (GroupId, UserId composite PK, JoinedAt, RemovedAt?)
- `src/SplitBook.Api/Features/Groups/CreateGroup/CreateGroupDtos.cs` — CreateGroupRequest/CreateGroupResponse records
- `src/SplitBook.Api/Features/Groups/CreateGroup/CreateGroupValidator.cs` — FluentValidation rules (name not empty/whitespace, currency 3 alpha chars)
- `src/SplitBook.Api/Features/Groups/CreateGroup/CreateGroupHandler.cs` — Static handler with validation, Group + Membership persistence, auto-membership for creator
- `src/SplitBook.Api/Features/Groups/CreateGroup/CreateGroupEndpoint.cs` — RouteGroupBuilder extension, one-liner mapping
- `src/SplitBook.Api/Infrastructure/Persistence/AppDbContext.cs` — Added Groups/Memberships DbSets, composite key, Currency max length
- `src/SplitBook.Api/Program.cs` — Added CreateGroup route mapping, removed temporary GET /groups stub
- `tests/SplitBook.Api.Tests/Features/Groups/CreateGroup/CreateGroupEndpointTests.cs` — 9 tests (7 from test-writer + 2 added after reviewer findings)
- `tests/SplitBook.Api.Tests/Features/Auth/Login/LoginEndpointTests.cs` — Fixed ProtectedEndpoint test to use POST /groups instead of removed GET stub

## Reviewer rounds
- **Round 1:** pass with 4 minor findings (default! vs string.Empty, untested alphabetic currency rule, redundant .NotEmpty(), missing whitespace name test)
- All findings addressed. No second round needed.

## Scribe output
- **L-07 already existed** — FluentValidation `.Must()` null-safety lesson was already recorded from a prior observation. No new lessons added.

## Open questions deferred to human
- None. All decisions made within existing DECISIONS.md framework.

## Test count
- 25 tests total (16 from slices 0-1, 9 new for slice 2)
- All passing, build clean with `--warnaserror`
