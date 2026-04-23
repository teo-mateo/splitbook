# SplitBook — Technical Specification

## 1. Stack

- **.NET 8**, C# 12, nullable reference types enabled, `TreatWarningsAsErrors=true`.
- **ASP.NET Core Minimal API** endpoints (preferred over MVC controllers for this style).
- **Entity Framework Core 8** + **SQLite** provider (`Microsoft.EntityFrameworkCore.Sqlite`).
- **MediatR** for the handler-per-slice pattern — optional; hand-written handlers are acceptable if MediatR adds friction.
- **FluentValidation** for request validation.
- **xUnit** + **FluentAssertions** + **Microsoft.AspNetCore.Mvc.Testing** (`WebApplicationFactory`) for integration tests. **Respawn** or simple in-memory-recreate for DB reset between tests.
- **Serilog** for structured logging.
- **JWT Bearer** via `Microsoft.AspNetCore.Authentication.JwtBearer`.
- **BCrypt.Net-Next** for password hashing.

No other runtime dependencies without discussion.

## 2. Solution layout (vertical slice)

```
src/
  SplitBook.Api/
    Program.cs
    Features/
      Auth/
        Register/               RegisterEndpoint.cs  RegisterHandler.cs  RegisterValidator.cs  RegisterTests.cs?
        Login/                  ...
      Groups/
        CreateGroup/
        ListMyGroups/
        GetGroup/
        AddMember/
        RemoveMember/
        ArchiveGroup/
      Expenses/
        AddExpense/
        ListExpenses/
        EditExpense/
        DeleteExpense/
      Settlements/
        RecordSettlement/
        ListSettlements/
      Balances/
        GetGroupBalances/
        GetSimplifiedDebts/
      Reports/
        GetUserSummary/
    Domain/
      User.cs  Group.cs  Membership.cs  Expense.cs  ExpenseSplit.cs  Settlement.cs
      SplitMethod.cs  Money.cs  BalanceCalculator.cs  DebtSimplifier.cs
    Infrastructure/
      Persistence/  AppDbContext.cs  Migrations/
      Auth/         JwtTokenService.cs  PasswordHasher.cs  CurrentUserAccessor.cs
      Http/         ProblemDetailsMiddleware.cs  CorrelationIdMiddleware.cs  IdempotencyStore.cs
    appsettings.json
tests/
  SplitBook.Api.Tests/          # integration, mirrors Features/
  SplitBook.Domain.Tests/       # pure unit tests for Money/BalanceCalculator/DebtSimplifier
SplitBook.sln
```

**Slice rule:** a feature folder contains everything that feature needs — endpoint registration, request/response DTOs, validator, handler, and its tests. Cross-slice sharing is limited to `Domain/` and `Infrastructure/`. No shared "Services" layer.

## 3. Domain model (ERD in prose)

- `User`: `Id (Guid)`, `Email (unique, lowercase)`, `DisplayName`, `PasswordHash`, `CreatedAt`.
- `Group`: `Id`, `Name`, `Currency (3-letter ISO)`, `CreatedByUserId → User`, `CreatedAt`, `ArchivedAt?`, `RowVersion`.
- `Membership`: `(GroupId, UserId)` composite PK, `JoinedAt`, `RemovedAt?`.
- `Expense`: `Id`, `GroupId → Group`, `PayerUserId → User`, `AmountMinor (long)`, `Currency`, `Description`, `OccurredOn (DateOnly)`, `SplitMethod (enum)`, `CreatedAt`, `DeletedAt?`, `RowVersion`.
- `ExpenseSplit`: `(ExpenseId, UserId)` PK, `AmountMinor`, `Percentage?`, `Shares?` — which fields are populated depends on `SplitMethod`.
- `Settlement`: `Id`, `GroupId`, `FromUserId`, `ToUserId`, `AmountMinor`, `Currency`, `OccurredOn`, `CreatedAt`, `DeletedAt?`.

`Money` is a value type wrapping `(AmountMinor, Currency)` with arithmetic and a guard that prevents mixing currencies.

## 4. REST contract

All responses are JSON; auth required except `/auth/register` and `/auth/login`. Errors use Problem+JSON.

### Auth
- `POST /auth/register` — `{email, displayName, password}` → `201 {id, email, displayName}`.
- `POST /auth/login` — `{email, password}` → `200 {accessToken, expiresAt}`.

### Groups
- `POST /groups` — `{name, currency}` → `201 GroupDto`.
- `GET /groups` — list groups the caller belongs to → `200 [GroupDto]`.
- `GET /groups/{id}` — group detail incl. members → `200 GroupDetailDto`.
- `POST /groups/{id}/members` — `{email}` adds a user by email → `204`.
- `DELETE /groups/{id}/members/{userId}` → `204` (fails if user has non-zero balance).
- `POST /groups/{id}/archive` → `204` (fails if any non-zero balance).

### Expenses
- `POST /groups/{groupId}/expenses` — headers: `Idempotency-Key` (optional). Body: `{payerUserId, amountMinor, currency, description, occurredOn, splitMethod, splits: [{userId, amountMinor?, percentage?, shares?}]}` → `201 ExpenseDto`.
- `GET /groups/{groupId}/expenses?skip=&take=&from=&to=` → `200 {items, total}`.
- `PUT /groups/{groupId}/expenses/{id}` — same body; requires `If-Match: <rowVersion>` → `200 ExpenseDto`.
- `DELETE /groups/{groupId}/expenses/{id}` → `204` (soft delete).

### Settlements
- `POST /groups/{groupId}/settlements` — headers: `Idempotency-Key`. Body: `{fromUserId, toUserId, amountMinor, currency, occurredOn}` → `201 SettlementDto`.
- `GET /groups/{groupId}/settlements` → `200 [SettlementDto]`.

### Balances / reports
- `GET /groups/{groupId}/balances` → `200 [{userId, netAmountMinor}]` (sums to 0).
- `GET /groups/{groupId}/simplified-debts` → `200 [{fromUserId, toUserId, amountMinor}]`.
- `GET /users/me/summary` → `200 {groups: [{groupId, netAmountMinor, grossAmountMinor}]}`.

### Status/health
- `GET /health` → `200 {status: "ok", version}`.

## 5. Auth, identity, and authorization

- JWT claims: `sub` (user id), `email`, `name`, `exp`, `iat`, `iss`, `aud`.
- `CurrentUserAccessor` reads `HttpContext.User` and exposes a strongly typed `CurrentUser`.
- Group-scoped endpoints enforce `caller ∈ group.Members` in the handler (not via policy) — makes the check explicit and testable within the slice.
- Return `404` (not `403`) when the caller is not a member of a group, to avoid leaking existence.

## 6. Persistence conventions

- All timestamps stored as UTC `DateTimeOffset`.
- All money stored as `long` (minor units) + `string Currency` (3 chars, ISO).
- `RowVersion` is a `uint` with `[Timestamp]`/`IsRowVersion()` mapped to SQLite via a manual trigger-emulation or a stored `long Ticks` if needed (acceptable simplification: use `long Version` and increment in `SaveChanges`).
- Soft-deletes via `DeletedAt?`. Global query filter excludes soft-deleted rows.
- Migrations committed into `Infrastructure/Persistence/Migrations/`.

## 7. Test strategy

**Per slice:**
- At least one **integration test** hitting the HTTP endpoint through `WebApplicationFactory`, covering the happy path.
- At least one integration test per documented failure mode (validation, auth, not-found, concurrency).
- **Pure unit tests** for any non-trivial logic (`BalanceCalculator`, `DebtSimplifier`, split-rounding) in `SplitBook.Domain.Tests`.

**Invariants that must be asserted somewhere:**
- Balances sum to 0 per group after any expense or settlement.
- `DebtSimplifier` produces ≤ N−1 transfers for N members with non-zero balance.
- Split-rounding: the sum of all split amounts equals the expense total, exactly, for every split method.
- Idempotency: two `POST` calls with same `Idempotency-Key` within 24h return identical bodies and create only one row.

**Test DB:** use a unique SQLite file per test class (`$"Data Source={Guid}.db"`) or an in-memory shared connection — whichever is more reliable under xUnit parallelism.

## 8. Definition of done (per slice)

- All new/modified tests pass: `dotnet test` green.
- `dotnet build --warnaserror` green.
- New endpoints documented in this spec (update in place).
- LESSONS.md updated with any new lesson learned this slice (or explicitly "no new lessons").

## 9. Open questions for the implementing model to decide

These are deliberately underspecified so the model exercises judgment — it should document its decision in code comments or a `DECISIONS.md`:

1. MediatR vs hand-written handlers.
2. How to express `RowVersion` in SQLite (concurrency token shape).
3. Whether to use `Results.Problem(...)` helpers everywhere or a centralized error filter.
4. Project reference boundaries: one project (`SplitBook.Api`) with folders, or split `SplitBook.Domain` into its own class library.

The reviewer subagent should check these decisions are (a) made consciously, (b) consistent across slices.
