# SplitBook — Product Specification

## 1. Purpose

SplitBook is a lightweight group-expense tracker: friends or roommates record shared costs, and the app keeps a running picture of who owes whom. It is a backend-first project; this document defines the product behavior that the REST API must implement to support a future 4–5 screen frontend.

## 2. Personas

- **Organizer** — creates a group (e.g. "Lisbon Trip"), invites members, enters expenses on behalf of others.
- **Member** — participates in a group, adds their own expenses, records payments they make to settle up.
- **Casual user** — opens the app to check "do I owe anyone anything right now?".

All three are the same account type; the distinction is behavioral.

## 3. Screens to support (frontend, out of scope for this phase)

1. **Groups list** — all groups the signed-in user belongs to, with their net balance in each.
2. **Group detail** — members, expense feed (newest first), current balances per member, "simplified debts" suggestion, button to add expense or settle.
3. **Add/edit expense** — amount, currency, payer, participants, split method (equal / exact amounts / percentages / shares), description, date.
4. **Settle up** — record a payment from one member to another; updates balances.
5. **Profile / history** — user info, list of groups, cross-group totals, personal expense history with filters.

## 4. Core domain concepts

- **User** — identified by email, authenticates with password, holds a display name.
- **Group** — named collection of users; has a default currency; has a creator; is immutable in members only by add/remove operations.
- **Membership** — link between user and group; a user can be in many groups.
- **Expense** — one payment made by one member (the *payer*), shared among any subset of members (the *participants*) using one of four split methods:
  - `Equal` — divided evenly among participants.
  - `Exact` — participants each have a specified exact amount; must sum to expense total.
  - `Percentage` — participants each have a percentage; must sum to 100.
  - `Shares` — participants each have an integer share count; amount is proportional to shares.
- **Settlement** — a direct payment recorded between two members (payer → payee) of the same group for a given amount; it is *not* an expense and is not split, but it moves balances.
- **Balance** — derived, per group per member: `sum(paid) − sum(owed share)`. Across the whole group, balances sum to zero (invariant).
- **Simplified debts** — given balances, the minimum set of payer→payee transfers that clears them (classic greedy min-cashflow algorithm).

## 5. Business rules (must hold)

- An expense's participants must all be members of the same group as the payer.
- Expense total is stored in the smallest currency unit (integer cents) to avoid float drift.
- For `Exact`: sum of participant amounts must equal the expense total.
- For `Percentage`: sum of percentages must equal exactly 100 (use two-decimal precision, stored as integer basis-points if needed).
- For `Shares`: total shares must be ≥ 1; each participant's share ≥ 1.
- Rounding: when a split does not divide evenly, the remainder is assigned deterministically (first N participants get one extra cent; N = remainder).
- Settlements and expenses are append-only; edits create a new version linked to the original (soft edit via replacement record) — simple "last-write-wins" is acceptable for v1, but deletes must be soft.
- A user may only see groups they are a member of. A user may only create expenses in groups they are a member of. The expense payer must be a group member (not necessarily the caller — you can pay on someone else's behalf as long as both are members).
- Balances sum to zero per group at all times (invariant — tests must assert this).
- A group with unsettled non-zero balances cannot be deleted; it can be archived.

## 6. Non-functional requirements

- **Stateless API**: no server-side session state other than what's in the database.
- **Auth**: JWT bearer tokens; 24h access token expiry; no refresh tokens in v1.
- **Storage**: EF Core + SQLite (file-based) is sufficient; migrations via EF tooling.
- **Correctness over performance**: no pagination optimization required beyond basic skip/take on list endpoints; no caching layer.
- **Error shape**: RFC 7807 Problem+JSON for all non-2xx responses.
- **Idempotency**: `POST /expenses` and `POST /settlements` must accept an `Idempotency-Key` header; same key within 24h returns the original response.
- **Concurrency**: optimistic concurrency on `Group` and `Expense` mutations via a `rowVersion` field.
- **Observability**: structured logging (Serilog console sink), request correlation ID middleware.
- **Testability**: every slice ships with tests (unit for pure logic, integration for endpoint behavior).

## 7. Out of scope (explicit)

- Multi-currency FX — every group has one currency; cross-currency expenses not supported.
- Receipt uploads, comments, reactions.
- Push notifications, email.
- OAuth / social login.
- Real-time sync / websockets.
- Multi-tenant / org accounts.

## 8. Acceptance criteria (happy paths, high level)

Every slice includes its own detailed ACs in `slice-plan.md`. At the product level, the API is "done" when an end-to-end scenario works:

1. Two users register, log in.
2. User A creates a group, invites user B (by email).
3. User A adds an expense: €60 equal split, paid by A, for both A and B.
4. GET balances: B owes A €30; A has +€30; sum is 0.
5. Simplified debts: one transfer B→A €30.
6. User B records a settlement of €30 to A.
7. Balances now zero.
8. Group report: each user sees €30 gross activity, €0 net.

The integration test suite for the final slice must cover this scenario end-to-end through HTTP.
