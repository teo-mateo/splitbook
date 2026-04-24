# Slice 5.1 — Patch: Add Swagger/OpenAPI

## Specs in scope
- `specs/slice-plan.md` — Slice 5.1: Add `Swashbuckle.AspNetCore` for OpenAPI spec and Swagger UI
- `specs/technical-spec.md` — Auth scheme (JWT bearer) for security declaration

## Lessons cited at start
- **L-H7** (tests green ≠ app works) — smoke-tested with `scripts/app.sh smoke` in Development environment
- **L-06** (wire packages you add) — Swashbuckle added and wired in same slice
- **L-04** (document decisions) — D-05 recorded in DECISIONS.md
- **L-02** (scope discipline) — only touched `.csproj`, `Program.cs`, `DECISIONS.md`

## What was done

### Package
- Added `Swashbuckle.AspNetCore` 6.9.0 to `SplitBook.Api.csproj`.

### Program.cs wiring
- Called `AddEndpointsApiExplorer()` before `AddSwaggerGen()` so minimal API routes are discovered.
- Configured `AddSwaggerGen()` with:
  - `SwaggerDoc("v1", ...)` for the document metadata.
  - Bearer/JWT security scheme (`AddSecurityDefinition`) so Swagger UI shows an "Authorize" button.
  - Global security requirement (`AddSecurityRequirement`) applying Bearer to all endpoints.
- Added `UseSwagger()` + `UseSwaggerUI()` middleware after `UseAuthentication()`/`UseAuthorization()`, gated behind `app.Environment.IsDevelopment()`.

### DECISIONS.md
- Added D-05 documenting the Swashbuckle choice, version, and wiring decisions.

### Smoke test results
- `/health` → 200
- `/swagger` → 301 (redirect to `/swagger/index.html`)
- `/swagger/v1/swagger.json` → 200
- All 7 required endpoints present in spec: `/health`, `/auth/register`, `/auth/login`, `/groups`, `/groups/{id}`, `/groups/{id}/members`, `/groups/{id}/members/{userId}`.
- Bearer security scheme declared correctly in OpenAPI components.

## Reviewer round count and findings
- **1 round** — reviewer flagged minor out-of-scope edits to harness files (`scripts/app.sh`, `.opencode/skill/`, `opencode.json`, `.opencode/agent/build.md`). These were system/operator artifacts, not slice work. No actionable findings against the slice code.

## Scribe output
- No new lessons added to LESSONS.md. The slice was clean with no novel failure modes.

## Open questions deferred to human
- None.

## DoD checklist
- [x] `dotnet test` green — 54/54 tests pass
- [x] `dotnet build --warnaserror` clean
- [x] `/swagger` returns 200 (via 301 redirect) with HTML content
- [x] `/swagger/v1/swagger.json` returns 200 with valid OpenAPI spec
- [x] All 7 required endpoints listed in spec
- [x] Bearer/JWT security scheme declared
- [x] Diff touches `*.csproj`, `Program.cs`, `DECISIONS.md` only
