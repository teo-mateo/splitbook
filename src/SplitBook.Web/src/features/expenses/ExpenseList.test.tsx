import { http, HttpResponse } from 'msw';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { server } from '../../../test/setup';
import { ExpenseList } from './ExpenseList';
import App from '../../App';

describe('ExpenseList', () => {
  beforeEach(() => {
    localStorage.setItem('splitbook_token', 'fake-jwt-token');
  });

  afterEach(() => {
    localStorage.removeItem('splitbook_token');
  });

  test('expense row shows all required fields', async () => {
    const payerId = 'b2222222-2222-2222-2222-222222222222';
    const memberId = 'c3333333-3333-3333-3333-333333333333';
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const members = [
      { userId: payerId, displayName: 'Alice' },
      { userId: memberId, displayName: 'Bob' },
    ];

    server.use(
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({
          items: [
            {
              id: 'e4444444-4444-4444-4444-444444444444',
              groupId: testGroupId,
              payerUserId: payerId,
              amountMinor: 6000,
              currency: 'EUR',
              description: 'Dinner at Mario\'s',
              occurredOn: '2024-03-10',
              splitMethod: 'Equal',
              splits: [
                { userId: payerId, amountMinor: 3000 },
                { userId: memberId, amountMinor: 3000 },
              ],
              createdAt: '2024-03-10T12:00:00Z',
              version: 1,
            },
          ],
          total: 1,
        });
      }),
    );

    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });

    render(
      <QueryClientProvider client={queryClient}>
        <ExpenseList groupId={testGroupId} currency="EUR" members={members} />
      </QueryClientProvider>,
    );

    // Description
    await waitFor(() => {
      expect(screen.getByText("Dinner at Mario's")).toBeInTheDocument();
    });

    // Payer name
    expect(screen.getByText('Alice')).toBeInTheDocument();

    // Formatted amount (€60.00 via Intl.NumberFormat)
    expect(screen.getByText('€60.00')).toBeInTheDocument();

    // Date
    expect(screen.getByText('2024-03-10')).toBeInTheDocument();

    // Participant count
    expect(screen.getByText('2 people')).toBeInTheDocument();
  });

  test('initial query includes skip and take params', async () => {
    let requestUrl: string | null = null;

    server.use(
      http.get('http://localhost:5000/groups/:id/expenses', ({ request }) => {
        requestUrl = request.url;
        return HttpResponse.json({ items: [], total: 0 });
      }),
    );

    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });

    const testGroupId = 'a1111111-1111-1111-1111-111111111111';

    render(
      <QueryClientProvider client={queryClient}>
        <ExpenseList groupId={testGroupId} currency="EUR" members={[]} />
      </QueryClientProvider>,
    );

    await waitFor(() => {
      expect(requestUrl).not.toBeNull();
      expect(requestUrl).toContain('skip=0');
      expect(requestUrl).toContain('take=10');
    });
  });

  test('shows "No expenses yet" when the expense list is empty', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';

    server.use(
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
    );

    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });

    render(
      <QueryClientProvider client={queryClient}>
        <ExpenseList groupId={testGroupId} currency="EUR" members={[]} />
      </QueryClientProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText('No expenses yet')).toBeInTheDocument();
    });
  });

  test('shows loading indicator while the expense query is in flight', async () => {
    const testGroupId = 'f6666666-6666-6666-6666-666666666666';

    server.use(
      http.get('http://localhost:5000/groups/:id/expenses', async () => {
        // Delay so the loading state is observable before data arrives
        await new Promise((r) => setTimeout(r, 500));
        return HttpResponse.json({ items: [], total: 0 });
      }),
    );

    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });

    render(
      <QueryClientProvider client={queryClient}>
        <ExpenseList groupId={testGroupId} currency="EUR" members={[]} />
      </QueryClientProvider>,
    );

    // Assert the loading indicator appears before the response arrives
    expect(screen.getByText('Loading expenses...')).toBeInTheDocument();
  });

  test('Next-page control increments skip and updates the visible list', async () => {
    const user = userEvent.setup();

    const payerId = 'b2222222-2222-2222-2222-222222222222';
    const memberId = 'c3333333-3333-3333-3333-333333333333';
    const testGroupId = 'd5555555-5555-5555-5555-555555555555';
    const members = [
      { userId: payerId, displayName: 'Alice' },
      { userId: memberId, displayName: 'Bob' },
    ];

    let lastRequestUrl: string | null = null;

    server.use(
      http.get('http://localhost:5000/groups/:id/expenses', ({ request }) => {
        const url = new URL(request.url);
        lastRequestUrl = request.url;
        const skip = parseInt(url.searchParams.get('skip') ?? '0', 10);

        if (skip === 0) {
          // Page 0: first 10 expenses (descriptions "Expense 1" .. "Expense 10")
          return HttpResponse.json({
            items: Array.from({ length: 10 }, (_, i) => ({
              id: `e${String(i + 1).padStart(3, '0')}0000-0000-0000-0000-000000000000`,
              groupId: testGroupId,
              payerUserId: payerId,
              amountMinor: (i + 1) * 1000,
              currency: 'EUR',
              description: `Expense ${i + 1}`,
              occurredOn: '2024-03-10',
              splitMethod: 'Equal' as const,
              splits: [
                { userId: payerId, amountMinor: Math.round((i + 1) * 500) },
                { userId: memberId, amountMinor: Math.round((i + 1) * 500) },
              ],
              createdAt: '2024-03-10T12:00:00Z',
              version: 1,
            })),
            total: 25,
          });
        }

        // Page 1 (skip=10): next 10 expenses ("Expense 11" .. "Expense 20")
        return HttpResponse.json({
          items: Array.from({ length: 10 }, (_, i) => ({
            id: `e${String(i + 11).padStart(3, '0')}0000-0000-0000-0000-000000000000`,
            groupId: testGroupId,
            payerUserId: payerId,
            amountMinor: (i + 11) * 1000,
            currency: 'EUR',
            description: `Expense ${i + 11}`,
            occurredOn: '2024-03-15',
            splitMethod: 'Equal' as const,
            splits: [
              { userId: payerId, amountMinor: Math.round((i + 11) * 500) },
              { userId: memberId, amountMinor: Math.round((i + 11) * 500) },
            ],
            createdAt: '2024-03-15T12:00:00Z',
            version: 1,
          })),
          total: 25,
        });
      }),
    );

    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });

    render(
      <QueryClientProvider client={queryClient}>
        <ExpenseList groupId={testGroupId} currency="EUR" members={members} />
      </QueryClientProvider>,
    );

    // Wait for page 0 to load — verify first batch is visible
    await waitFor(() => {
      expect(screen.getByText('Expense 1')).toBeInTheDocument();
    });
    expect(screen.getByText('Expense 10')).toBeInTheDocument();
    // Page 0 should NOT show page-1 items yet
    expect(screen.queryByText('Expense 11')).not.toBeInTheDocument();

    // Click "Next"
    await user.click(screen.getByRole('button', { name: /next/i }));

    // The new request should have skip=10
    await waitFor(() => {
      expect(lastRequestUrl).toContain('skip=10');
    });

    // The visible list should now show page-1 items
    await waitFor(() => {
      expect(screen.getByText('Expense 11')).toBeInTheDocument();
    });
    expect(screen.getByText('Expense 20')).toBeInTheDocument();
    // Page 0 items should no longer be visible
    expect(screen.queryByText('Expense 1')).not.toBeInTheDocument();
  });

  test('date filter sends from/to query params when dates are set', async () => {
    const payerId = 'b2222222-2222-2222-2222-222222222222';
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const members = [
      { userId: payerId, displayName: 'Alice' },
    ];

    let lastRequestUrl: string | null = null;

    server.use(
      http.get('http://localhost:5000/groups/:id/expenses', ({ request }) => {
        lastRequestUrl = request.url;
        return HttpResponse.json({
          items: [
            {
              id: 'e4444444-4444-4444-4444-444444444444',
              groupId: testGroupId,
              payerUserId: payerId,
              amountMinor: 6000,
              currency: 'EUR',
              description: 'Dinner at Mario\'s',
              occurredOn: '2024-03-10',
              splitMethod: 'Equal',
              splits: [
                { userId: payerId, amountMinor: 6000 },
              ],
              createdAt: '2024-03-10T12:00:00Z',
              version: 1,
            },
          ],
          total: 1,
        });
      }),
    );

    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });

    render(
      <QueryClientProvider client={queryClient}>
        <ExpenseList groupId={testGroupId} currency="EUR" members={members} />
      </QueryClientProvider>,
    );

    // Wait for initial data to load so the filter controls are visible
    await waitFor(() => {
      expect(screen.getByText("Dinner at Mario's")).toBeInTheDocument();
    });

    // Set dates on the From and To inputs (native value setter bypass for jsdom date inputs)
    const setNativeValue = (el: HTMLInputElement, value: string) => {
      const valueSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
      valueSetter?.call(el, value);
    };

    const fromInput = screen.getByLabelText('From') as HTMLInputElement;
    setNativeValue(fromInput, '2024-03-01');
    fireEvent.change(fromInput);

    // Wait for data to reappear after the refetch triggered by the first change
    await waitFor(() => {
      expect(screen.getByText("Dinner at Mario's")).toBeInTheDocument();
    });

    // Re-query toInput after re-render
    const toInput = screen.getByLabelText('To') as HTMLInputElement;
    setNativeValue(toInput, '2024-03-31');
    fireEvent.change(toInput);

    // Assert the request URL includes from and to query params
    await waitFor(() => {
      expect(lastRequestUrl).not.toBeNull();
      expect(lastRequestUrl).toContain('from=2024-03-01');
      expect(lastRequestUrl).toContain('to=2024-03-31');
    });
  });

  test('clearing date filter removes from/to params and restores full list', async () => {
    const user = userEvent.setup();

    const payerId = 'b2222222-2222-2222-2222-222222222222';
    const testGroupId = 'b2222222-aaaa-bbbb-cccc-dddddddddddd';
    const members = [
      { userId: payerId, displayName: 'Alice' },
    ];

    let lastRequestUrl: string | null = null;

    server.use(
      http.get('http://localhost:5000/groups/:id/expenses', ({ request }) => {
        lastRequestUrl = request.url;
        return HttpResponse.json({
          items: [
            {
              id: 'e4444444-4444-4444-4444-444444444444',
              groupId: testGroupId,
              payerUserId: payerId,
              amountMinor: 6000,
              currency: 'EUR',
              description: 'Dinner at Mario\'s',
              occurredOn: '2024-03-10',
              splitMethod: 'Equal',
              splits: [
                { userId: payerId, amountMinor: 6000 },
              ],
              createdAt: '2024-03-10T12:00:00Z',
              version: 1,
            },
          ],
          total: 1,
        });
      }),
    );

    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });

    render(
      <QueryClientProvider client={queryClient}>
        <ExpenseList groupId={testGroupId} currency="EUR" members={members} />
      </QueryClientProvider>,
    );

    // Wait for initial data to load
    await waitFor(() => {
      expect(screen.getByText("Dinner at Mario's")).toBeInTheDocument();
    });

    // Set date range (native value setter bypass for jsdom date inputs)
    const setNativeValue = (el: HTMLInputElement, value: string) => {
      const valueSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
      valueSetter?.call(el, value);
    };

    const fromInput = screen.getByLabelText('From') as HTMLInputElement;
    setNativeValue(fromInput, '2024-03-01');
    fireEvent.change(fromInput);

    // Wait for data to reappear after the refetch triggered by the first change
    await waitFor(() => {
      expect(screen.getByText("Dinner at Mario's")).toBeInTheDocument();
    });

    // Re-query toInput after re-render
    const toInput = screen.getByLabelText('To') as HTMLInputElement;
    setNativeValue(toInput, '2024-03-31');
    fireEvent.change(toInput);

    // Confirm filter params are present
    await waitFor(() => {
      expect(lastRequestUrl).toContain('from=2024-03-01');
      expect(lastRequestUrl).toContain('to=2024-03-31');
    });

    // Click Clear button
    await user.click(screen.getByRole('button', { name: /clear/i }));

    // Assert the new request has no from/to params
    await waitFor(() => {
      expect(lastRequestUrl).not.toBeNull();
      const url = new URL(lastRequestUrl!);
      expect(url.searchParams.has('from')).toBe(false);
      expect(url.searchParams.has('to')).toBe(false);
    });

    // Assert the full list is still visible
    await waitFor(() => {
      expect(screen.getByText("Dinner at Mario's")).toBeInTheDocument();
    });
  });

  test('error state shows message and Retry button, and clicking Retry re-fetches data', async () => {
    const user = userEvent.setup();

    const payerId = 'b2222222-2222-2222-2222-222222222222';
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const members = [
      { userId: payerId, displayName: 'Alice' },
    ];

    let callCount = 0;

    server.use(
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        callCount++;
        if (callCount === 1) {
          return HttpResponse.json(
            { type: 'about:blank', title: 'Internal Server Error', status: 500, detail: 'Internal server error' },
            { status: 500 },
          );
        }
        return HttpResponse.json({
          items: [
            {
              id: 'e4444444-4444-4444-4444-444444444444',
              groupId: testGroupId,
              payerUserId: payerId,
              amountMinor: 6000,
              currency: 'EUR',
              description: 'Dinner at Mario\'s',
              occurredOn: '2024-03-10',
              splitMethod: 'Equal',
              splits: [
                { userId: payerId, amountMinor: 6000 },
              ],
              createdAt: '2024-03-10T12:00:00Z',
              version: 1,
            },
          ],
          total: 1,
        });
      }),
    );

    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });

    render(
      <QueryClientProvider client={queryClient}>
        <ExpenseList groupId={testGroupId} currency="EUR" members={members} />
      </QueryClientProvider>,
    );

    // Assert error message is visible
    await waitFor(() => {
      expect(screen.getByText('Something went wrong. Please try again.')).toBeInTheDocument();
    });

    // Assert Retry button is visible
    const retryButton = screen.getByRole('button', { name: /retry/i });
    expect(retryButton).toBeInTheDocument();

    // Click Retry
    await user.click(retryButton);

    // Assert expense data appears after retry
    await waitFor(() => {
      expect(screen.getByText("Dinner at Mario's")).toBeInTheDocument();
    });
  });

  test('reachability through real <App>: from /groups click card then see expense list in Group Detail', async () => {
    const user = userEvent.setup();
    const testGroupId = 'e1777777-7777-7777-7777-777777777777';
    const aliceId = '11111111-1111-1111-1111-111111111111';
    const bobId = '22222222-2222-2222-2222-222222222222';

    server.use(
      // GroupsList queries this
      http.get('http://localhost:5000/groups', () => {
        return HttpResponse.json([
          {
            id: testGroupId,
            name: 'Lisbon Trip',
            currency: 'EUR',
            createdAt: '2024-01-15T10:00:00Z',
          },
        ]);
      }),
      // GroupDetail queries this
      http.get('http://localhost:5000/groups/:id', () => {
        return HttpResponse.json({
          id: testGroupId,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [
            { userId: aliceId, displayName: 'Alice' },
            { userId: bobId, displayName: 'Bob' },
          ],
        });
      }),
      http.get('http://localhost:5000/groups/:id/balances', () => {
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 3000 },
          { userId: bobId, netAmountMinor: -3000 },
        ]);
      }),
      // ExpenseList queries this — returns one expense
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({
          items: [
            {
              id: 'e8888888-8888-8888-8888-888888888888',
              groupId: testGroupId,
              payerUserId: aliceId,
              amountMinor: 6000,
              currency: 'EUR',
              description: 'Dinner at Mario\'s',
              occurredOn: '2024-03-10',
              splitMethod: 'Equal',
              splits: [
                { userId: aliceId, amountMinor: 3000 },
                { userId: bobId, amountMinor: 3000 },
              ],
              createdAt: '2024-03-10T20:00:00Z',
              version: 1,
            },
          ],
          total: 1,
        });
      }),
    );

    // Start at /groups (the post-auth landing page)
    window.history.pushState({}, '', '/groups');
    render(<App />);

    // 1. GroupsList loads — find the group card link by its visible name
    const groupCard = await screen.findByRole('link', { name: /Lisbon Trip/i });
    await user.click(groupCard);

    // 2. GroupDetail loads — wait for group name heading
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // 3. ExpenseList inside GroupDetail renders — assert the expense description appears
    await waitFor(
      () => {
        expect(screen.getByText("Dinner at Mario's")).toBeVisible();
      },
      { timeout: 10000 },
    );
  });
});
