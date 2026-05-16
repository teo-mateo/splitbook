# SplitBook Frontend — Technical Specification

## 1. Stack

- **React 18**, TypeScript 5, strict mode enabled (`strict: true` in `tsconfig.json`).
- **Vite 5** as the build tool and dev server.
- **React Router v6** for client-side routing.
- **TanStack Query (React Query v5)** for server state (data fetching, caching, mutations).
- **Zod** for runtime validation of API responses (defensive parsing, not client-side form validation).
- **React Hook Form** + **Zod resolver** for form validation.
- **Tailwind CSS 3** for styling. No component library (no shadcn, no MUI, no Chakra).
- **Vitest** + **@testing-library/react** for unit and component tests.
- **Playwright** for end-to-end tests against a real running API instance.
- **msw (MSW 2)** for API mocking in unit/component tests.

No other runtime dependencies without discussion.

## 2. Project layout

```
src/
  SplitBook.Web/
    index.html
    package.json
    tsconfig.json
    vite.config.ts
    tailwind.config.ts
    postcss.config.js
    src/
      main.tsx                 # entry point
      App.tsx                  # router setup, auth guard
      routes.tsx               # route definitions
      api/
        client.ts              # fetch wrapper, JWT injection, error handling
        types.ts               # API response types (Zod schemas)
      features/
        auth/
          Login.tsx
          Register.tsx
          useAuth.ts           # hook: token storage, user info, logout
          AuthGuard.tsx        # route guard component
        groups/
          GroupsList.tsx
          GroupDetail.tsx
          CreateGroup.tsx
          AddMember.tsx
          ArchiveGroup.tsx
          RemoveMember.tsx
        expenses/
          ExpenseForm.tsx      # shared add/edit form
          ExpenseList.tsx
          ExpenseItem.tsx
          SplitSelector.tsx    # Equal/Exact/Percentage/Shares UI
        settlements/
          SettlementForm.tsx
          SettlementList.tsx
        balances/
          BalancesDisplay.tsx
          SimplifiedDebts.tsx
        profile/
          Profile.tsx
      components/
        Button.tsx
        Input.tsx
        Select.tsx
        Modal.tsx
        Toast.tsx
        CurrencyInput.tsx
        DateInput.tsx
      hooks/
        useApi.ts              # generic data-fetching hook (thin wrapper)
        useConfirm.ts          # confirmation dialog hook
      lib/
        money.ts               # minor↔major conversion, formatting
        dates.ts               # DateOnly helpers
        constants.ts           # API base URL, currency symbols
    e2e/
      fixtures/
      auth.spec.ts
      groups.spec.ts
      expenses.spec.ts
      settlements.spec.ts
    public/
      manifest.json
```

**Slice rule:** a feature folder contains everything that feature needs — components, hooks, tests. Cross-feature sharing is limited to `components/`, `hooks/`, `lib/`, and `api/`. No shared "services" layer.

## 3. API integration

### 3.1 HTTP client

`api/client.ts` wraps `fetch` with:
- Base URL from `VITE_API_URL` env var (defaults to `http://localhost:5000`).
- Automatic JWT injection from `localStorage` via `Authorization: Bearer <token>`.
- Response parsing: 2xx responses return parsed JSON; non-2xx return a typed error with status + Problem+JSON body.
- Retry: none in v1. Transient failures surface to the user.

### 3.2 Type safety

API response shapes are defined as Zod schemas in `api/types.ts`. Every API call parses its response through the matching schema. A parse failure is treated as a 500-level error (the API broke its contract).

Money fields (`amountMinor`, `netAmountMinor`, `grossAmountMinor`) are sent as JSON numbers. The maximum realistic value (€1 billion = 100,000,000,000 minor units) is well within JavaScript's safe integer range (±2⁵³ ≈ 9 quadrillion), so `z.number()` is sufficient — no BigInt needed.

Example:
```typescript
const GroupDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  currency: z.string().length(3),
  memberCount: z.number().int().nonnegative(),
});
```

### 3.3 Server state

TanStack Query handles:
- Cache invalidation on mutations (e.g., after adding expense, invalidate `expenses` and `balances` queries for that group).
- Background refetch on window focus (enabled globally).
- Optimistic updates where the response is deterministic (e.g., settlement recording).

### 3.4 Auth flow

- JWT stored in `localStorage` under key `splitbook_token`.
- `useAuth` hook exposes `user`, `login`, `register`, `logout`, `isAuthenticated`.
- `AuthGuard` wrapper component: if no valid token, redirect to `/login`.
- On 401 from any API call, clear token and redirect to `/login?expired=true`.

## 4. Routing

```
/                    → GroupsList (guarded)
/login               → Login
/register            → Register
/groups               → GroupsList (alias, guarded)
/groups/:id           → GroupDetail (guarded)
/groups/:id/expenses/new       → ExpenseForm (guarded)
/groups/:id/expenses/:id/edit  → ExpenseForm (guarded)
/groups/:id/settlements/new    → SettlementForm (guarded)
/profile              → Profile (guarded)
```

## 5. Styling conventions

- **Tailwind CSS** with the default theme. No custom design system beyond utility classes.
- **Mobile-first**: base styles target 320px width. Use `md:` and `lg:` breakpoints sparingly.
- **Color coding for balances**: green (`text-green-600`) for positive (owed), red (`text-red-600`) for negative (owe), gray for zero.
- **Spacing**: use Tailwind's default scale (0.25rem = 1 unit). No arbitrary values unless justified.
- **Typography**: system font stack (Tailwind default). No external fonts.

## 6. Form handling

All forms use React Hook Form with Zod resolver. Pattern:

```typescript
const schema = z.object({ /* shape */ });
type FormData = z.infer<typeof schema>;

function ExpenseForm() {
  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });
  // ...
}
```

Form validation runs on blur and on submit. Server-side validation errors (from Problem+JSON) are mapped to field-level errors after a failed submit.

## 7. Error handling

- **API errors**: `client.ts` throws `ApiError` with `status` and `problem` (parsed Problem+JSON). TanStack Query's `onError` callback handles per-mutation errors.
- **Global 401 interceptor**: `client.ts` detects 401, clears token, triggers redirect.
- **Toast notifications**: success toasts for mutations (e.g., "Expense added"), error toasts for failures.
- **Inline form errors**: field-level, rendered below the input.
- **Page-level errors**: if a query fails and the page has no data, show an error banner with retry button.

## 8. Test strategy

**Per slice:**
- At least one **component test** (Vitest + React Testing Library) covering the happy path of the main component.
- At least one test per documented failure mode (validation, empty state, error state).
- **E2E test** (Playwright) for the golden-path user journey of the slice, running against a real API instance.

**Test conventions:**
- Component tests use MSW to mock API responses — no network calls.
- E2E tests run against `VITE_API_URL` pointing to a real (or dockerized) SplitBook.Api instance.
- Use `@testing-library/user-event` for realistic user interactions.
- Assert on visible text and DOM structure, not on internal React state or implementation details.

**E2E test environment:**
- API runs on `http://localhost:5000` (configured via `.env.e2e`).
- Playwright starts a fresh browser context per test (no shared state).
- Tests clean up after themselves (delete created resources or use unique names).

## 9. Definition of done (per slice)

- All tests pass: `pnpm test` green (Vitest) and `pnpm test:e2e` green (Playwright, if API available).
- `pnpm build` produces a clean production bundle with no TypeScript errors.
- `pnpm lint` passes (ESLint with TypeScript ESLint, no warnings).
- The slice's screen is navigable and functional in the browser at `http://localhost:5173`.
- LESSONS.md updated with any new lesson learned this slice (or explicitly "no new lessons").

## 10. Open questions for the implementing model to decide

1. **Toast library**: use `sonner` (lightweight, popular) or hand-roll a minimal toast with React state and CSS transitions?
2. **Modal implementation**: use `@headlessui/react` Dialog or hand-roll with portal + overlay?
3. **Currency formatting**: use `Intl.NumberFormat` directly or a thin wrapper in `lib/money.ts`?
4. **Route-level code splitting**: use `React.lazy` + `Suspense` per route, or keep it simple with eager imports?

The reviewer subagent should check these decisions are (a) made consciously, (b) consistent across slices.
