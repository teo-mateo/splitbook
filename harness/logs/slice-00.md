# Slice 0 — Bootstrap Session Log

## Specs in scope
- `specs/product-spec.md` — full read
- `specs/technical-spec.md` — full read
- `specs/slice-plan.md` — row 0 (Bootstrap)

## Lessons cited at start
- **L-00:** Read spec end-to-end before writing the first test
- **L-01:** Red before green, always
- **L-02:** One slice's worth of files per session

## What was built
- `SplitBook.sln` — solution with two projects
- `src/SplitBook.Api/` — .NET 8 Web API project, nullable, TreatWarningsAsErrors, Serilog.AspNetCore
- `src/SplitBook.Api/Program.cs` — minimal host with `GET /health` returning `{status: "ok", version: "0.1.0"}`
- `src/SplitBook.Api/appsettings.json` + `appsettings.Development.json`
- `tests/SplitBook.Api.Tests/` — xUnit + FluentAssertions + WebApplicationFactory
- `tests/SplitBook.Api.Tests/Features/Health/HealthEndpointTests.cs` — 2 integration tests
- `scripts/test.sh` — simple test runner

## Reviewer rounds
- **Round 1:** pass with 3 minor findings (test file placement, `--no-restore` in script, undocumented health endpoint location decision)
- All findings addressed. No second round needed.

## Scribe output
- **L-03 added:** Keep subagents within their mandate boundaries (test-writer wrote Program.cs and hallucinated Serilog API)
- **L-04 added:** Document architectural decisions when the spec asks (health endpoint location not recorded)

## Open questions deferred to human
- Technical spec §9 Q1: MediatR vs hand-written handlers — deferred to slice 1 when handlers are needed
- Technical spec §9 Q4: Single project vs split Domain library — deferred; current structure is single project with folders
