# Slice 5 — Groups: Archive

## Date
2026-05-16

## Specs in scope
- `specs/product-spec.md` §3.3 (Group Detail header, Archive button), §3.2 (archived groups excluded from list), §5 (error handling)
- `specs/technical-spec.md` §2 (project layout), §3 (API integration), §7 (error handling)
- `specs/slice-plan.md` — slice 5 row
- `specs/openapi.json` — `POST /groups/{id}/archive` → 204 No Content

## Lessons cited at start
- L-00 (read spec end-to-end)
- L-H2 (red before green)
- L-H8 (one test per invocation)
- L-H11 (mirror nearest sibling)
- L-FE3 (parse API through Zod)
- L-FE10 (test-writer touches no shared infra)
- L-FE11 (router-param tests need actual Route)
- L-FE12 (atomic values in own elements, use within())

## Acceptance criteria (from @spec-auditor)
1. Group Detail header renders an "Archive" button — ✅
2. Clicking opens a destructive confirmation dialog — ✅
3. Dialog copy distinct from member-remove — ✅
4. Dialog presents confirm and cancel controls — ✅
5. Cancel closes dialog with no API call — ✅
6. Confirm sends POST /groups/{id}/archive with no body — ✅
7. 204 → dialog closes, navigates to /groups — ✅
8. Confirm button disabled while pending — ✅
9. Archive succeeds with non-zero balances (no precheck) — ✅
10. 5xx → error message, user stays on page — ✅
11. 401 → token cleared, redirect to /login?expired=true — ✅
12. Network error → error message shown — ✅
13. Archived group excluded from GroupsList — ✅
14. Route remains auth-guarded — ✅ (inherited)
15. 320px viewport usable — ✅ (mobile-first Tailwind)
16. Real-app reachability via appRoutes — ✅

## Reviewer rounds
- **Round 1:** fail — 1 major (L-H2 process violation, unretroactive), 3 minor (ARIA attributes missing, fragile parentElement scoping, MSW duplication nit)
- **Round 2:** fail — 1 major (8× TS2345: `closest()` returns `Element` not `HTMLElement`), 2 minor (aria-labelledby absent, indentation)
- **Round 3:** pass — all findings resolved

## What the scribe added to LESSONS.md
- L-FE13: `element.closest()` returns `Element | null`, not `HTMLElement` — cast explicitly under strict mode
- L-FE14: Primary must verify test-writer output matches the assigned criterion before accepting green

## Files changed
- `src/features/groups/ArchiveGroup.tsx` — new: confirmation dialog component (modal overlay, ARIA attributes, pending state, distinct copy)
- `src/features/groups/ArchiveGroup.test.tsx` — new: 13 tests covering all acceptance criteria
- `src/features/groups/GroupDetail.tsx` — added `useNavigate`, `archiveMutation`, `showArchive`/`archiveError` state, Archive button in header, error banner, ArchiveGroup modal wiring
- `src/features/groups/RemoveMember.tsx` — added ARIA attributes (`role="dialog"`, `aria-modal="true"`, `aria-labelledby`) for WCAG 2.1 AA consistency

## DoD verification
- `pnpm exec vitest run` — 71/71 passed, 13 test files
- `pnpm build` — clean (zero TS errors)
- `pnpm lint` — clean (zero warnings)
- `scripts/app.sh smoke` — PASS

## Open questions deferred to human
- `Modal.tsx` in `components/` remains an empty stub. Both ArchiveGroup and RemoveMember hand-roll overlay modals. A future slice could implement the shared `Modal` and refactor both consumers.
- GroupDetail still lacks a visible back/parent nav to GroupsList (Global DoD requirement). Predates slice 5.
