# Frontend Slice Plan — Execution Order

Each slice is a self-contained TDD cycle. Do not advance until the current slice's Definition of Done (see frontend-technical-spec §9) is met.

## Sizing rule

**One screen or feature per slice. At most two, and only if they are tightly coupled** (share components or state that isn't otherwise justified, e.g. Login + Register share auth hooks and form patterns; Add Member + Remove Member share member list UI). Bigger slices burn the model — it spirals in deliberation, tests get written in huge batches with no feedback, and a single wrong guess contaminates many files. Small slices give per-screen red→green cycles and commit-worthy checkpoints.

If you feel a slice is growing to three components, split it.

## Slices

| # | Slice | Screen(s) / Component | DoD signal |
|---|-------|------------------------|------------|
| 0 | **Bootstrap** — Vite project, TypeScript config, Tailwind, React Router, `pnpm` setup, basic layout shell | App shell renders | `pnpm build` clean; app loads at `:5173` with a placeholder "SplitBook" heading |
| 1 | **Auth — Login + Register** (two coupled: share `useAuth`, JWT storage, form patterns) | `/login`, `/register` | Can register and login via mocked API; JWT stored in `localStorage`; `AuthGuard` redirects unauthenticated users |
| 2 | **Groups — List + Create** (two coupled: Create navigates back to List) | `/groups`, CreateGroup modal/form | List renders groups from mocked API; Create form validates and submits; empty state shows "No groups yet" |
| 3 | **Groups — Detail** | `/groups/:id` | Shows group name, members with balances, expense feed; 404 handling for non-member access |
| 4 | **Groups — Add + Remove member** (two coupled: share member list UI) | AddMember form, RemoveMember confirmation | Add by email with validation; Remove with destructive confirmation; balances update on member removal |
| 5 | **Groups — Archive** | ArchiveGroup confirmation | Destructive action with confirmation dialog; always succeeds per D-06 (no balance check) |
| 6 | **Expenses — Add (Equal split only)** | `/groups/:id/expenses/new` with Equal split | Form validates, submits, navigates back; expense appears in feed; idempotency key sent |
| 7 | **Expenses — Exact split** (extends slice 6, same form) | Exact split UI and validation | Participant amount inputs; running total; validates sum equals expense total client-side |
| 8 | **Expenses — List + Pagination** | ExpenseList component with skip/take | Renders expenses newest first; pagination controls; date filter UI |
| 9 | **Expenses — Percentage + Shares** (two coupled: both are proportional splits) | Percentage and Shares split UI | Percentage inputs with running total (must sum to 100%); Share inputs with computed per-share amount |
| 10 | **Expenses — Edit** | `/groups/:id/expenses/:id/edit` | Pre-fills form from API; handles 412 (stale edit) with user-friendly message; optimistic UI |
| 11 | **Expenses — Delete** | Delete button in ExpenseItem | Soft delete with confirmation; optimistic removal from list; rollback on failure |
| 12 | **Balances — Display** | BalancesDisplay, SimplifiedDebts | Shows per-member balances color-coded; simplified debts as actionable list; collapses when all zero |
| 13 | **Settlements — Record** | `/groups/:id/settlements/new` | From/To dropdowns (can't be same member); amount validation; navigates back with refreshed balances |
| 14 | **Settlements — List** | SettlementList component | Renders settlements newest first; shows from→to, amount, date |
| 15 | **Profile / Summary** | `/profile` | Shows user info, logout, groups summary with net/gross; cross-group totals |
| 16 | **E2E — Full scenario** | Playwright tests | Product-spec §8 end-to-end scenario passes against real API: register, group, expense, balance, settlement, profile |

## Notes for the implementer

- **Do not skip ahead.** Do not build the expense form with all four split methods in slice 6 because you "know they're coming." Each slice introduces only what its tests require.
- **Red first.** Start each slice by writing component tests that fail because the component doesn't exist or renders nothing. The test-writer subagent is responsible for this. Confirm red by quoting `pnpm test` output.
- **Mock the API.** Use MSW in component tests. The API is not required to be running for unit/component tests. Only E2E tests (slice 16) need a real API.
- **One open question per slice, max.** If a slice surfaces more than one architectural decision, stop and escalate to the reviewer subagent rather than guessing.
- **Refactor is part of the slice.** The slice is not done until the code you just wrote is clean — dead code, placeholder names, duplicated logic, unused imports, etc., all go before DoD.
- **The definition of "done" is strict.** If you find yourself thinking "we'll clean that up in a later slice," either make that an explicit follow-up noted in the slice log OR clean it up now. Both are acceptable; silent deferral is not.
- **One test per test-writer invocation (L-H8).** Invoke the test-writer subagent once per acceptance criterion, producing one failing test at a time. Never batch-write all tests for a slice in one invocation.
- **Mirror the nearest sibling.** Before writing a new component, read the closest existing peer in the same feature family. Match its structure: React Hook Form usage, Zod schema shape, error display pattern, TanStack Query mutation/query pattern. Do not introduce a new form library or error-handling style mid-project.
