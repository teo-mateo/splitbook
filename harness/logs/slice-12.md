# Slice 12 — Expenses — Delete

## Specs in scope
- `specs/product-spec.md` §5 (soft deletes), §4 (Expense domain model)
- `specs/technical-spec.md` §4 (REST contract: `DELETE /groups/{groupId}/expenses/{id}` → 204), §6 (soft-deletes via `DeletedAt?`, global query filter)
- `specs/slice-plan.md` row 12: "Soft delete — row remains with `DeletedAt`, excluded from queries"

## Lessons cited at start
- L-00 (read spec end-to-end)
- L-H2 (no logic before red — scaffolding only)
- L-H11 (mirror nearest sibling: EditExpense)
- L-H8 (one test per @test-writer invocation)
- L-H10 (never touch tests/Infrastructure/)
- L-05 (TypedResults.Problem for all errors)
- L-H7 (smoke-test the running API)
- L-12 (verbose mode for 5xx)

## Acceptance criteria (from @spec-auditor)
9 criteria produced by spec-auditor:
1. Happy path — 204 No Content
2. Soft delete — `DeletedAt` is set
3. Soft delete — `ExpenseSplit` rows are retained
4. Excluded from expense list
5. 404 — caller not a group member
6. 404 — expense not found
7. 404 — expense already deleted
8. 401 — unauthenticated
9. 404 — expense belongs to a different group

## Test results
- Criterion 1: RED → GREEN (handler implemented after red confirmed)
- Criteria 2–9: GREEN immediately (behavior already correct from criterion 1 implementation)
- Full suite: 107/107 passed, 0 failed, 0 skipped

## Smoke test
- `scripts/app.sh smoke`: PASS (/health 200, /swagger 301, /swagger/v1/swagger.json 200)
- End-to-end DELETE smoke: register → login → create group → create expense → DELETE → 204 → list expenses → empty. All passed.

## Reviewer round count and findings
- **Round 1:** Status `fail` with 2 findings:
  - [major] L-H2 violation — **false positive**. Handler was first written with a 501 stub (scaffolding), then filled in after criterion 1 RED. File mtime only shows the final edit time. L-H5 already documents this blind spot.
  - [minor] `scripts/app.sh` DB_FILE fix (`app.db` → `splitbook.db`) — out-of-scope corrective edit. Necessary to fix L-H7 smoke test that was silently targeting the wrong database file.
- No further review rounds needed.

## What the scribe added to LESSONS.md
- **L-16** (new): Smoke-test infrastructure must match production config — verify before trusting. Triggered by the `scripts/app.sh` DB filename mismatch.
- **L-H5** (sharpened): Added the mtime blind spot caveat — file modification times cannot distinguish scaffolding stub from full implementation when both edits land in the same file.

## Out-of-scope changes
- `scripts/app.sh:33` — `DB_FILE` changed from `app.db` to `splitbook.db` to match `appsettings.json`'s connection string. This was discovered during smoke testing when `EnsureCreated()` never ran because the stale `splitbook.db` (created before Expense table existed) was never removed by the reset script.

## Open questions deferred to the human
- Whether soft-deleted expenses should be excluded from balance calculations (slice 13). The current design (global query filter on `DeletedAt == null`) means `BalanceCalculator` will naturally exclude them — which seems correct per spec ("deletes must be soft" implies they should no longer affect balances). If this is wrong, the human should clarify before slice 13.
