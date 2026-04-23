# Slice Plan — Execution Order

Each slice is a self-contained TDD cycle. Do not advance until the current slice's Definition of Done (see technical-spec §8) is met.

| # | Slice | Why it goes here | DoD signal |
|---|-------|------------------|------------|
| 0 | **Bootstrap** — solution, Api project, Tests project, `GET /health`, CI via a `dotnet test` script, `appsettings.Development.json` | Establishes the skeleton without domain noise. Reveals toolchain issues early. | `/health` returns 200 in an integration test; `dotnet build -warnaserror` clean |
| 1 | **Register + Login** — `Auth/Register`, `Auth/Login`, `AppDbContext` with `Users` table, JWT config, `CurrentUserAccessor` | Every other slice needs auth. | Can register then login and receive a working JWT; attempting to hit a protected endpoint without token returns 401 |
| 2 | **Create group + list my groups** — `Groups/CreateGroup`, `Groups/ListMyGroups`, `Group` and `Membership` entities, creator auto-added as member | Minimal group lifecycle. | Caller creates a group, sees it in their list, a second user does not see it |
| 3 | **Get group + add/remove member + archive** — rounds out the group feature | Needed before expenses so we have multi-member groups. | All endpoints from §4.Groups covered; member cannot be removed if any expense references them (stub rule — becomes real in slice 5) |
| 4 | **Add expense (Equal + Exact) + list expenses** — `Expense`, `ExpenseSplit`, split-rounding logic, idempotency store, RFC 7807 errors | Core value. Keep Percentage/Shares out to limit scope. | Integration test: create group, add €60 equal split between 2 members, list it back, split sums to €60 exactly |
| 5 | **Percentage + Shares split methods + Edit/Delete expense** | Rounds out expenses; introduces concurrency (`RowVersion`) for edits | Edit with stale `If-Match` returns 412; percentages must sum to 100 enforced |
| 6 | **Balances** — `BalanceCalculator`, `GetGroupBalances` endpoint | Derived read model; tests must assert zero-sum invariant | Invariant test: after N random expenses, sum of balances is 0 |
| 7 | **Settlements + Simplified debts** — `Settlements/Record`, `Balances/GetSimplifiedDebts`, `DebtSimplifier` algorithm | Closes the loop — balances can go to zero | End-to-end scenario from product-spec §8 passes |
| 8 | **Reports** — `GetUserSummary` across groups | Cross-group aggregation | Summary matches sum of balances per group for the user |

## Notes for the implementer

- **Do not skip ahead.** Do not add a `Settlements` table in slice 2 because you "know it's coming." Each slice introduces only what its tests require.
- **Red first.** Start each slice by writing integration tests that fail because the endpoint 404s. Commit the red state. Then implement.
- **One open question per slice, max.** If a slice surfaces more than one architectural decision, stop and escalate to the reviewer subagent rather than guessing.
- **Refactor is part of the slice.** The slice is not done until the code you just wrote is clean. Dead code, placeholder names, duplicated mapping — clean them before DoD.
