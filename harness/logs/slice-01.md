# Slice 1 — Register + Login Session Log

## Specs in scope
- `specs/product-spec.md` — full read
- `specs/technical-spec.md` — full read (sections 1-6, 9)
- `specs/slice-plan.md` — row 1 (Register + Login)
- `DECISIONS.md` — D-01 (plain minimal APIs), D-02 (single project)

## Lessons cited at start
- **L-00:** Read spec end-to-end before writing the first test
- **L-01:** Red before green, always
- **L-H1:** Subagents MUST verify with the tools they have
- **L-H2:** Primary writes no logic before red
- **L-H3:** Thinking-model output budget (≥32K max_tokens)
- **L-02:** One slice's worth of files per session
- **L-03:** Keep subagents within their mandate boundaries
- **L-04:** Document architectural decisions when the spec asks

## What was built
- `src/SplitBook.Api/Domain/User.cs` — User entity (Id, Email, DisplayName, PasswordHash, CreatedAt)
- `src/SplitBook.Api/Infrastructure/Persistence/AppDbContext.cs` — DbContext with Users DbSet, unique email index
- `src/SplitBook.Api/Infrastructure/Auth/PasswordHasher.cs` — BCrypt hash/verify wrapper
- `src/SplitBook.Api/Infrastructure/Auth/JwtTokenService.cs` — JWT creation via JwtPayload (emits iat as numeric claim)
- `src/SplitBook.Api/Infrastructure/Auth/CurrentUserAccessor.cs` — Reads user identity from JWT claims
- `src/SplitBook.Api/Features/Auth/Register/RegisterDtos.cs` — RegisterRequest/RegisterResponse records
- `src/SplitBook.Api/Features/Auth/Register/RegisterValidator.cs` — FluentValidation rules (email, displayName, password)
- `src/SplitBook.Api/Features/Auth/Register/RegisterHandler.cs` — Static handler with validation, duplicate check, persistence
- `src/SplitBook.Api/Features/Auth/Register/RegisterEndpoint.cs` — RouteGroupBuilder extension, one-liner mapping
- `src/SplitBook.Api/Features/Auth/Login/LoginDtos.cs` — LoginRequest/LoginResponse records
- `src/SplitBook.Api/Features/Auth/Login/LoginValidator.cs` — FluentValidation rules (email, password)
- `src/SplitBook.Api/Features/Auth/Login/LoginHandler.cs` — Static handler with validation, credential check, JWT issuance
- `src/SplitBook.Api/Features/Auth/Login/LoginEndpoint.cs` — RouteGroupBuilder extension, one-liner mapping
- `src/SplitBook.Api/Program.cs` — DI wiring, JWT auth config, route groups, middleware order
- `src/SplitBook.Api/appsettings.json` — JWT config, connection string
- `tests/SplitBook.Api.Tests/Infrastructure/AppFactory.cs` — WebApplicationFactory with per-class SQLite isolation
- `tests/SplitBook.Api.Tests/Features/Auth/Register/RegisterEndpointTests.cs` — 8 tests
- `tests/SplitBook.Api.Tests/Features/Auth/Login/LoginEndpointTests.cs` — 6 tests

## Reviewer rounds
- **Round 1:** 2 major findings (D-01 violation — endpoints orchestrated validation; Problem+JSON not used), 7 minor findings (return type mismatch, RouteGroupBuilder, unwired package, missing unique index, middleware order, /groups stub, null-forgiving operator)
- All findings addressed. Round 2: **pass** with 3 minor findings (Unauthorized without body, Location header, /groups stub nit)
- All 3 minor findings addressed. Final: **pass**

## Scribe output
- **L-05 added:** Use TypedResults.Problem() for RFC 7807 error responses
- **L-06 added:** Wire packages you add, or don't add them

## Open questions deferred to human
- Technical spec §9 Q1 (MediatR vs hand-written handlers): Decided hand-written static handlers per D-01 — implemented and working
- Technical spec §9 Q3 (Problem+JSON approach): Decided TypedResults.Problem() per-endpoint — no centralized filter
- `System.IdentityModel.Tokens.Jwt` package not listed in technical-spec §1 but required for JWT construction — conscious decision, not documented in DECISIONS.md yet
