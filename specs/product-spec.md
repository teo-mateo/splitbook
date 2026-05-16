# SplitBook Frontend — Product Specification

## 1. Purpose

SplitBook Frontend is a single-page application that consumes the SplitBook REST API. It provides a clean, mobile-first interface for the five screens outlined in the backend product spec. This document defines the product behavior the frontend must implement.

## 2. Personas

Same as backend product spec §2: **Organizer**, **Member**, **Casual user**. The frontend does not distinguish account types — all UI is available to any authenticated user.

## 3. Screens

### 3.1 Auth (Register / Login)

Two views, toggled or on separate routes. After successful login the user is redirected to Groups List.

- **Register** — email, display name, password (min 8 chars), password confirmation (client-side validation only — not sent to API). On success, auto-login and redirect.
- **Login** — email, password. On success, store JWT and redirect. On failure, show inline error.
- Token is stored in `localStorage`. On app load, if a valid token exists, skip auth screens.
- Token expiry (24h) is handled by redirecting to Login with a "session expired" message. No refresh-token flow in v1.

### 3.2 Groups List (`/groups`)

The default landing page after auth.

- Displays all groups the signed-in user belongs to, as cards or list items.
- Each entry shows: group name, currency symbol, net balance for the current user (positive = others owe you, negative = you owe), member count. *Backend requirement: `GET /groups` must return per-group net balance and member count in `GroupDto`, or the frontend must accept N+1 fetches (lazy on card click).*
- "Create group" button opens a modal or navigates to a form.
- Create group form: name (required), currency (3-letter ISO, default EUR). On success, navigate to Group Detail.
- Archived groups are excluded from the list.

### 3.3 Group Detail (`/groups/:id`)

The central screen. Four logical sections:

1. **Header** — group name, currency, "Add member" button, "Archive" button (destructive confirmation). Archive always succeeds regardless of outstanding balances (see D-06).
2. **Members** — list of member display names with their net balance (color-coded: green for positive, red for negative, neutral for zero).
3. **Simplified debts** — if any member has non-zero balance, show the minimum set of transfers that clears them. Each transfer is "X owes Y €Z". Collapsible if all balances are zero.
4. **Expense feed** — expenses newest first, showing payer, amount, description, date, and participant count. "Add expense" button (floating or in header).
5. **Settlements** — settlements listed below expenses or in a separate tab. "Record settlement" button.

### 3.4 Add / Edit Expense (`/groups/:id/expenses/new` or `/groups/:id/expenses/:id/edit`)

A form with these fields:

- **Description** — free text, required.
- **Amount** — numeric input. The UI displays major units (€30.00) but sends `amountMinor` (3000) to the API.
- **Currency** — pre-filled from group default, read-only (single-currency per group).
- **Payer** — dropdown of group members, required.
- **Date** — date picker, defaults to today.
- **Split method** — radio or segmented control: Equal / Exact / Percentage / Shares.
- **Participants** — changes based on split method:
  - *Equal*: checkboxes for each member (payer pre-checked). At least one required.
  - *Exact*: each checked participant gets an amount input. UI shows running total; validates sum equals expense total.
  - *Percentage*: each checked participant gets a percentage input. UI shows running total; validates sum equals 100%.
  - *Shares*: each checked participant gets an integer share input (min 1). UI shows computed amount per share.
- **Submit** — validates client-side before sending. On success, navigate back to Group Detail.

### 3.5 Record Settlement (`/groups/:id/settlements/new`)

A form with:

- **From** — dropdown of group members (who is paying).
- **To** — dropdown of group members (who is receiving), cannot be same as "From".
- **Amount** — numeric input (major units in UI, minor units in API).
- **Date** — date picker, defaults to today.
- **Submit** — on success, navigate back to Group Detail (balances refresh).

### 3.6 Profile / Summary (`/profile`)

- User display name and email.
- "Logout" button — clears JWT, redirects to Login.
- **Groups summary** — list of all groups with net balance and gross activity per group (from `GET /users/me/summary`).
- Total across all groups: how much the user is owed in total, how much they owe.

## 4. Navigation model

- Unauthenticated user → Login (or Register).
- Authenticated user → Groups List.
- Group Detail is the hub; Add Expense, Record Settlement, and Add Member are modal dialogs or separate routes that return to Group Detail.
- Breadcrumbs or back button on sub-screens.

## 5. Error handling

- **401 (Unauthorized)** — clear JWT, redirect to Login with "Session expired" message.
- **404 (Not Found)** — show "Group not found or you are not a member" on group-scoped routes.
- **400 (Validation error)** — display Problem+JSON `detail` or field-level errors inline on the form.
- **409 (Conflict)** — show "User already in this group" (from `POST /groups/{id}/members`).
- **412 (Precondition Failed)** — on expense edit: "This expense was modified by someone else. Reload and try again."
- **5xx** — generic "Something went wrong. Please try again." toast or banner.
- **Network error** — "Cannot reach the server. Check your connection."

## 6. Non-functional requirements

- **Mobile-first responsive** — primary target is phone portrait; desktop is a stretched version, not a separate layout.
- **Single currency per group** — the UI never offers currency conversion or multi-currency expense entry.
- **No server-side rendering** — pure client-side SPA. The API is the single source of truth.
- **Offline tolerance** — no offline mode required. If the API is unreachable, show an error.
- **Accessibility** — WCAG 2.1 AA for color contrast, focus management, and form labels. All interactive elements are keyboard-navigable.
- **Performance** — initial bundle < 200KB gzipped. No lazy-loading required beyond route-level code splitting.
- **i18n** — English only for v1. String externalization is not required, but avoid hard-coding currency formatting (use `Intl.NumberFormat`).

## 7. Out of scope (explicit)

- Push notifications, email, or any background sync.
- Receipt photo upload.
- Real-time updates (websockets, SSE). Users refresh or navigate away and back to see changes.
- Dark mode toggle (acceptable to ship with one theme).
- Admin panel, group owner privileges beyond "creator" metadata.
- Export to CSV/PDF.
- Search across groups.

## 8. Acceptance criteria (happy paths, high level)

The frontend is "done" when this end-to-end scenario works in the browser:

1. User A registers, lands on Groups List (empty).
2. User A creates "Lisbon Trip" (EUR), lands on Group Detail.
3. User A adds member (User B's email). Member list shows 2 people.
4. User A adds expense: €60, equal split, paid by A, for A and B. Expense feed shows it.
5. Balances show A: +€30, B: −€30. Simplified debts show "B owes A €30".
6. User A logs out. User B logs in.
7. User B sees "Lisbon Trip", opens it, sees the same balances and expense.
8. User B records settlement: €30 to A. Balances now zero. Simplified debts section collapses.
9. User B navigates to Profile, sees €30 gross activity in "Lisbon Trip", €0 net.
