import { http, HttpResponse } from 'msw';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { server } from '../../../test/setup';
import { GroupDetail } from './GroupDetail';

describe('AddMember button in GroupDetail', () => {
  beforeEach(() => {
    localStorage.setItem('splitbook_token', 'fake-jwt-token');
  });

  afterEach(() => {
    localStorage.removeItem('splitbook_token');
  });

  test('shows an "Add member" button in the header next to the group name', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';

    server.use(
      http.get('http://localhost:5000/groups/:id', () => {
        return HttpResponse.json({
          id: testGroupId,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [
            { userId: '11111111-1111-1111-1111-111111111111', displayName: 'Alice' },
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

    // Wait for group data to load and name to appear
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Assert the "Add member" button is visible in the header
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /add member/i })).toBeVisible();
    });
  });

  test('clicking "Add member" opens a form with an email input and submit button', async () => {
    const user = userEvent.setup();
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';

    server.use(
      http.get('http://localhost:5000/groups/:id', () => {
        return HttpResponse.json({
          id: testGroupId,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [
            { userId: '11111111-1111-1111-1111-111111111111', displayName: 'Alice' },
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

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the "Add member" button
    const addMemberButton = screen.getByRole('button', { name: /add member/i });
    await user.click(addMemberButton);

    // Assert the form appears with an email input and a submit button
    await expect(
      screen.findByRole('textbox', { name: /email/i }),
    ).resolves.toBeVisible();

    await expect(
      screen.findByRole('button', { name: 'Add' }),
    ).resolves.toBeVisible();
  });

  test('submitting with empty email shows validation error and does not call the API', async () => {
    const user = userEvent.setup();
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    let postCalled = false;

    server.use(
      http.get('http://localhost:5000/groups/:id', () => {
        return HttpResponse.json({
          id: testGroupId,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [
            { userId: '11111111-1111-1111-1111-111111111111', displayName: 'Alice' },
          ],
        });
      }),
      http.get('http://localhost:5000/groups/:id/balances', () => {
        return HttpResponse.json([]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.post('http://localhost:5000/groups/:id/members', () => {
        postCalled = true;
        return HttpResponse.json({ ok: true });
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

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the "Add member" button to open the form
    const addMemberButton = screen.getByRole('button', { name: /add member/i });
    await user.click(addMemberButton);

    // Wait for the form to appear
    await expect(
      screen.findByRole('textbox', { name: /email/i }),
    ).resolves.toBeVisible();

    // Click "Add" without typing anything in the email field
    const addButton = screen.getByRole('button', { name: 'Add' });
    await user.click(addButton);

    // Assert validation error appears
    await expect(
      screen.findByText(/email is required/i),
    ).resolves.toBeVisible();

    // Assert the API was NOT called
    expect(postCalled).toBe(false);
  });

  test('submitting with invalid email format shows validation error and does not call the API', async () => {
    const user = userEvent.setup();
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    let postCalled = false;

    server.use(
      http.get('http://localhost:5000/groups/:id', () => {
        return HttpResponse.json({
          id: testGroupId,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [
            { userId: '11111111-1111-1111-1111-111111111111', displayName: 'Alice' },
          ],
        });
      }),
      http.get('http://localhost:5000/groups/:id/balances', () => {
        return HttpResponse.json([]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.post('http://localhost:5000/groups/:id/members', () => {
        postCalled = true;
        return HttpResponse.json({ ok: true });
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

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the "Add member" button to open the form
    const addMemberButton = screen.getByRole('button', { name: /add member/i });
    await user.click(addMemberButton);

    // Wait for the form to appear
    await expect(
      screen.findByRole('textbox', { name: /email/i }),
    ).resolves.toBeVisible();

    // Type an invalid email address
    const emailInput = screen.getByRole('textbox', { name: /email/i });
    await user.type(emailInput, 'not-an-email');

    // Click "Add" to submit
    const addButton = screen.getByRole('button', { name: 'Add' });
    await user.click(addButton);

    // Assert validation error appears
    await expect(
      screen.findByText(/invalid email address/i),
    ).resolves.toBeVisible();

    // Assert the API was NOT called
    expect(postCalled).toBe(false);
  });

  test('successful add member sends POST /groups/{id}/members with {email} and refetches the group', async () => {
    const user = userEvent.setup();
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    let capturedBody: unknown;
    let getCallCount = 0;

    server.use(
      http.get('http://localhost:5000/groups/:id', () => {
        getCallCount++;
        if (getCallCount === 1) {
          // First call: only Alice
          return HttpResponse.json({
            id: testGroupId,
            name: 'Lisbon Trip',
            currency: 'EUR',
            createdAt: '2024-01-15T10:00:00Z',
            archivedAt: null,
            members: [
              { userId: '11111111-1111-1111-1111-111111111111', displayName: 'Alice' },
            ],
          });
        }
        // Subsequent calls: Alice + Bob (after refetch)
        return HttpResponse.json({
          id: testGroupId,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [
            { userId: '11111111-1111-1111-1111-111111111111', displayName: 'Alice' },
            { userId: '22222222-2222-2222-2222-222222222222', displayName: 'Bob' },
          ],
        });
      }),
      http.get('http://localhost:5000/groups/:id/balances', () => {
        return HttpResponse.json([]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.post('http://localhost:5000/groups/:id/members', async ({ request }) => {
        capturedBody = await request.json();
        return new HttpResponse(null, { status: 204 });
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

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the "Add member" button to open the form
    const addMemberButton = screen.getByRole('button', { name: /add member/i });
    await user.click(addMemberButton);

    // Wait for the form to appear
    await expect(
      screen.findByRole('textbox', { name: /email/i }),
    ).resolves.toBeVisible();

    // Type a valid email
    const emailInput = screen.getByRole('textbox', { name: /email/i });
    await user.type(emailInput, 'bob@example.com');

    // Click "Add" to submit
    const addButton = screen.getByRole('button', { name: 'Add' });
    await user.click(addButton);

    // Assert the POST body contains the correct email
    expect(capturedBody).toEqual({ email: 'bob@example.com' });

    // Assert the form closes (email input is no longer in the DOM)
    await waitFor(() => {
      expect(screen.queryByRole('textbox', { name: /email/i })).toBeNull();
    });

    // Assert the new member "Bob" appears in the member list (proves group refetched)
    await expect(screen.findByText('Bob')).resolves.toBeVisible();
  });

  test('on 409 Conflict, AddMember shows "User already in this group" and form remains open', async () => {
    const user = userEvent.setup();
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';

    server.use(
      http.get('http://localhost:5000/groups/:id', () => {
        return HttpResponse.json({
          id: testGroupId,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [
            { userId: '11111111-1111-1111-1111-111111111111', displayName: 'Alice' },
          ],
        });
      }),
      http.get('http://localhost:5000/groups/:id/balances', () => {
        return HttpResponse.json([]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.post('http://localhost:5000/groups/:id/members', () => {
        return HttpResponse.json(
          {
            type: 'about:blank',
            title: 'Conflict',
            status: 409,
            detail: 'User already in this group',
          },
          { status: 409 },
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

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the "Add member" button to open the form
    const addMemberButton = screen.getByRole('button', { name: /add member/i });
    await user.click(addMemberButton);

    // Wait for the form to appear
    await expect(
      screen.findByRole('textbox', { name: /email/i }),
    ).resolves.toBeVisible();

    // Type a valid email
    const emailInput = screen.getByRole('textbox', { name: /email/i });
    await user.type(emailInput, 'alice@example.com');

    // Click "Add" to submit
    const addButton = screen.getByRole('button', { name: 'Add' });
    await user.click(addButton);

    // Assert the 409 error message appears
    await expect(
      screen.findByText('User already in this group'),
    ).resolves.toBeVisible();

    // Assert the form remains open (email input is still visible)
    await waitFor(() => {
      expect(screen.getByRole('textbox', { name: /email/i })).toBeVisible();
    });
  });

  test('clicking "Cancel" closes the form without making any API request', async () => {
    const user = userEvent.setup();
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    let postCalled = false;

    server.use(
      http.get('http://localhost:5000/groups/:id', () => {
        return HttpResponse.json({
          id: testGroupId,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [
            { userId: '11111111-1111-1111-1111-111111111111', displayName: 'Alice' },
          ],
        });
      }),
      http.get('http://localhost:5000/groups/:id/balances', () => {
        return HttpResponse.json([]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.post('http://localhost:5000/groups/:id/members', () => {
        postCalled = true;
        return HttpResponse.json({ ok: true });
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

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the "Add member" button to open the form
    const addMemberButton = screen.getByRole('button', { name: /add member/i });
    await user.click(addMemberButton);

    // Wait for the form to appear
    await expect(
      screen.findByRole('textbox', { name: /email/i }),
    ).resolves.toBeVisible();

    // Click "Cancel" to dismiss the form
    const cancelButton = screen.getByRole('button', { name: /cancel/i });
    await user.click(cancelButton);

    // Assert the form closes (email input is no longer in the DOM)
    await waitFor(() => {
      expect(screen.queryByRole('textbox', { name: /email/i })).toBeNull();
    });

    // Assert no POST request was made
    expect(postCalled).toBe(false);
  });
});
