import { http, HttpResponse } from 'msw';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { server } from '../../../test/setup';
import { GroupDetail } from './GroupDetail';

describe('GroupDetail', () => {
  beforeEach(() => {
    localStorage.setItem('splitbook_token', 'fake-jwt-token');
  });

  afterEach(() => {
    localStorage.removeItem('splitbook_token');
  });

  test('fires GET /groups/{id} request with group id from URL params', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    let requestReceived = false;

    server.use(
      http.get('http://localhost:5000/groups/:id', ({ params }) => {
        requestReceived = true;
        return HttpResponse.json({
          id: params.id,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [],
        });
      }),
      http.get('http://localhost:5000/groups/:id/balances', () => {
        return HttpResponse.json([]);
      }),
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
        <MemoryRouter initialEntries={[`/groups/${testGroupId}`]}>
          <Routes>
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await waitFor(() => {
      expect(requestReceived).toBe(true);
    });
  });

  test('shows each member display name with formatted net balance', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const user1Id = '11111111-1111-1111-1111-111111111111';
    const user2Id = '22222222-2222-2222-2222-222222222222';

    server.use(
      http.get('http://localhost:5000/groups/:id', () => {
        return HttpResponse.json({
          id: testGroupId,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [
            { userId: user1Id, displayName: 'Alice' },
            { userId: user2Id, displayName: 'Bob' },
          ],
        });
      }),
      http.get('http://localhost:5000/groups/:id/balances', () => {
        return HttpResponse.json([
          { userId: user1Id, netAmountMinor: 3000 },
          { userId: user2Id, netAmountMinor: -3000 },
        ]);
      }),
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
        <MemoryRouter initialEntries={[`/groups/${testGroupId}`]}>
          <Routes>
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    await expect(screen.findByText('Alice')).resolves.toBeVisible();
    await expect(screen.findByText('Bob')).resolves.toBeVisible();

    await expect(screen.findByText('€30.00')).resolves.toBeVisible();
    await expect(screen.findByText('-€30.00')).resolves.toBeVisible();
  });

  test('color-codes member balances: green for positive, red for negative, gray for zero', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const user1Id = '11111111-1111-1111-1111-111111111111';
    const user2Id = '22222222-2222-2222-2222-222222222222';
    const user3Id = '33333333-3333-3333-3333-333333333333';

    server.use(
      http.get('http://localhost:5000/groups/:id', () => {
        return HttpResponse.json({
          id: testGroupId,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [
            { userId: user1Id, displayName: 'Alice' },
            { userId: user2Id, displayName: 'Bob' },
            { userId: user3Id, displayName: 'Charlie' },
          ],
        });
      }),
      http.get('http://localhost:5000/groups/:id/balances', () => {
        return HttpResponse.json([
          { userId: user1Id, netAmountMinor: 5000 },
          { userId: user2Id, netAmountMinor: -5000 },
          { userId: user3Id, netAmountMinor: 0 },
        ]);
      }),
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
        <MemoryRouter initialEntries={[`/groups/${testGroupId}`]}>
          <Routes>
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Positive balance (Alice, +€50.00) should be green
    const positiveBalance = await screen.findByText('€50.00');
    expect(positiveBalance).toHaveClass('text-green-600');

    // Negative balance (Bob, -€50.00) should be red
    const negativeBalance = await screen.findByText('-€50.00');
    expect(negativeBalance).toHaveClass('text-red-600');

    // Zero balance (Charlie, €0.00) should be gray
    const zeroBalance = await screen.findByText('€0.00');
    expect(zeroBalance).toHaveClass('text-gray-500');
  });

  test('fires GET /groups/{id}/balances request when rendering', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    let balancesRequestReceived = false;

    server.use(
      http.get('http://localhost:5000/groups/:id', ({ params }) => {
        return HttpResponse.json({
          id: params.id,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [],
        });
      }),
      http.get('http://localhost:5000/groups/:id/balances', () => {
        balancesRequestReceived = true;
        return HttpResponse.json([
          { userId: 'user-1', netAmountMinor: 3000 },
          { userId: 'user-2', netAmountMinor: -3000 },
        ]);
      }),
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
        <MemoryRouter initialEntries={[`/groups/${testGroupId}`]}>
          <Routes>
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await waitFor(() => {
      expect(balancesRequestReceived).toBe(true);
    });
  });

  test('fires GET /groups/{id}/expenses request when rendering', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    let expensesRequestReceived = false;

    server.use(
      http.get('http://localhost:5000/groups/:id', ({ params }) => {
        return HttpResponse.json({
          id: params.id,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [],
        });
      }),
      http.get('http://localhost:5000/groups/:id/balances', () => {
        return HttpResponse.json([]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        expensesRequestReceived = true;
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
        <MemoryRouter initialEntries={[`/groups/${testGroupId}`]}>
          <Routes>
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await waitFor(() => {
      expect(expensesRequestReceived).toBe(true);
    });
  });

  test('shows "No expenses yet" empty-state when the group has zero expenses', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const aliceId = '11111111-1111-1111-1111-111111111111';

    server.use(
      http.get('http://localhost:5000/groups/:id', () => {
        return HttpResponse.json({
          id: testGroupId,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [
            { userId: aliceId, displayName: 'Alice' },
          ],
        });
      }),
      http.get('http://localhost:5000/groups/:id/balances', () => {
        return HttpResponse.json([]);
      }),
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
        <MemoryRouter initialEntries={[`/groups/${testGroupId}`]}>
          <Routes>
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();
    await expect(screen.findByText('No expenses yet')).resolves.toBeVisible();
  });

  test('renders expense feed with description, formatted amount, payer name, date, and participant count', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const aliceId = '11111111-1111-1111-1111-111111111111';
    const bobId = '22222222-2222-2222-2222-222222222222';

    server.use(
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
        return HttpResponse.json([]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({
          items: [
            {
              id: 'e1111111-1111-1111-1111-111111111111',
              groupId: testGroupId,
              payerUserId: aliceId,
              amountMinor: 6000,
              currency: null,
              description: 'Dinner at Time Out Market',
              occurredOn: '2024-03-10',
              splitMethod: 'Equal',
              splits: [
                { userId: aliceId, amountMinor: 3000 },
                { userId: bobId, amountMinor: 3000 },
              ],
              createdAt: '2024-03-10T20:00:00Z',
              version: 1,
            },
            {
              id: 'e2222222-2222-2222-2222-222222222222',
              groupId: testGroupId,
              payerUserId: bobId,
              amountMinor: 2500,
              currency: null,
              description: 'Metro tickets',
              occurredOn: '2024-03-11',
              splitMethod: 'Equal',
              splits: [
                { userId: aliceId, amountMinor: 1250 },
                { userId: bobId, amountMinor: 1250 },
              ],
              createdAt: '2024-03-11T09:00:00Z',
              version: 1,
            },
          ],
          total: 2,
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
        <MemoryRouter initialEntries={[`/groups/${testGroupId}`]}>
          <Routes>
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // First expense: "Dinner at Time Out Market" — €60.00, paid by Alice, 2024-03-10, 2 people
    await expect(screen.findByText('Dinner at Time Out Market')).resolves.toBeVisible();
    await expect(screen.findByText('€60.00')).resolves.toBeVisible();
    const aliceElements = await screen.findAllByText('Alice');
    expect(aliceElements.length).toBeGreaterThanOrEqual(1);
    await expect(screen.findByText('2024-03-10')).resolves.toBeVisible();

    // Second expense: "Metro tickets" — €25.00, paid by Bob, 2024-03-11, 2 people
    await expect(screen.findByText('Metro tickets')).resolves.toBeVisible();
    await expect(screen.findByText('€25.00')).resolves.toBeVisible();
    const bobElements = await screen.findAllByText('Bob');
    expect(bobElements.length).toBeGreaterThanOrEqual(1);
    await expect(screen.findByText('2024-03-11')).resolves.toBeVisible();

    // Both expenses show participant count
    const peopleElements = await screen.findAllByText('2 people');
    expect(peopleElements.length).toBe(2);
  });

  test('expenses render in the order returned by the API (newest first)', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const aliceId = '11111111-1111-1111-1111-111111111111';
    const bobId = '22222222-2222-2222-2222-222222222222';

    server.use(
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
        return HttpResponse.json([]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({
          items: [
            {
              id: 'e3333333-3333-3333-3333-333333333333',
              groupId: testGroupId,
              payerUserId: aliceId,
              amountMinor: 6000,
              currency: null,
              description: 'Third',
              occurredOn: '2024-01-03',
              splitMethod: 'Equal',
              splits: [
                { userId: aliceId, amountMinor: 3000 },
                { userId: bobId, amountMinor: 3000 },
              ],
              createdAt: '2024-01-03T20:00:00Z',
              version: 1,
            },
            {
              id: 'e1111111-1111-1111-1111-111111111111',
              groupId: testGroupId,
              payerUserId: bobId,
              amountMinor: 2500,
              currency: null,
              description: 'First',
              occurredOn: '2024-01-01',
              splitMethod: 'Equal',
              splits: [
                { userId: aliceId, amountMinor: 1250 },
                { userId: bobId, amountMinor: 1250 },
              ],
              createdAt: '2024-01-01T09:00:00Z',
              version: 1,
            },
            {
              id: 'e2222222-2222-2222-2222-222222222222',
              groupId: testGroupId,
              payerUserId: aliceId,
              amountMinor: 4000,
              currency: null,
              description: 'Second',
              occurredOn: '2024-01-02',
              splitMethod: 'Equal',
              splits: [
                { userId: aliceId, amountMinor: 2000 },
                { userId: bobId, amountMinor: 2000 },
              ],
              createdAt: '2024-01-02T14:00:00Z',
              version: 1,
            },
          ],
          total: 3,
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
        <MemoryRouter initialEntries={[`/groups/${testGroupId}`]}>
          <Routes>
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Expenses should appear in DOM in the same order as the API returned them:
    // Third (Jan 3), First (Jan 1), Second (Jan 2) — newest first
    const descriptions = await screen.findAllByText((content) => {
      return content === 'Third' || content === 'First' || content === 'Second';
    });
    expect(descriptions.map((el) => el.textContent)).toEqual(['Third', 'First', 'Second']);
  });

  test('shows "Group not found or you are not a member" on HTTP 404 without a retry button', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';

    server.use(
      http.get('http://localhost:5000/groups/:id', () => {
        return HttpResponse.json(
          { type: 'about:blank', title: 'Not Found', status: 404 },
          { status: 404 },
        );
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
        <MemoryRouter initialEntries={[`/groups/${testGroupId}`]}>
          <Routes>
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await expect(
      screen.findByText('Group not found or you are not a member'),
    ).resolves.toBeVisible();

    expect(screen.queryByText('Retry')).toBeNull();
  });

  test('shows generic error message with Retry button on non-404 error, and Retry re-fires the request', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const user = userEvent.setup();
    let requestCount = 0;

    server.use(
      http.get('http://localhost:5000/groups/:id', () => {
        requestCount++;
        return HttpResponse.json(
          { type: 'about:blank', title: 'Internal Server Error', status: 500 },
          { status: 500 },
        );
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
        <MemoryRouter initialEntries={[`/groups/${testGroupId}`]}>
          <Routes>
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    // Assert error message appears
    await expect(
      screen.findByText('Something went wrong. Please try again.'),
    ).resolves.toBeVisible();

    // Assert the initial request was made
    expect(requestCount).toBe(1);

    // Assert Retry button is visible
    const retryButton = await screen.findByRole('button', { name: /Retry/i });
    expect(retryButton).toBeVisible();

    // Click Retry and assert the request fires again
    await user.click(retryButton);
    await waitFor(() => {
      expect(requestCount).toBe(2);
    });
  });
});
