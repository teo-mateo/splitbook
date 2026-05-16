import { http, HttpResponse } from 'msw';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { server } from '../../../test/setup';
import { appRoutes } from '../../routes';
import { GroupDetail } from './GroupDetail';
import { GroupsList } from './GroupsList';

describe('ArchiveGroup', () => {
  beforeEach(() => {
    localStorage.setItem('splitbook_token', 'fake-jwt-token');
  });

  afterEach(() => {
    localStorage.removeItem('splitbook_token');
  });

  test('GroupDetail header shows an "Archive" button alongside header controls', async () => {
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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 0 },
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

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // The header should contain an "Archive" button
    const archiveButton = screen.getByRole('button', { name: /archive/i });
    expect(archiveButton).toBeVisible();
  });

  test('clicking Archive opens a confirmation dialog that overlays the page', async () => {
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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 0 },
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

    const user = userEvent.setup();

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the Archive button
    const archiveButton = screen.getByRole('button', { name: /archive/i });
    await user.click(archiveButton);

    // A confirmation dialog heading should appear
    await expect(
      screen.findByRole('heading', { name: /archive/i }),
    ).resolves.toBeVisible();

    // The page content behind the overlay should still be present
    await waitFor(() => {
      expect(screen.getByText('Lisbon Trip')).toBeInTheDocument();
    });
  });

  test('Archive confirmation dialog copy is distinct from RemoveMember dialog', async () => {
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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 0 },
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

    const user = userEvent.setup();

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the Archive button
    const archiveButton = screen.getByRole('button', { name: /archive/i });
    await user.click(archiveButton);

    // Wait for dialog to appear
    await expect(
      screen.findByRole('heading', { name: /archive group/i }),
    ).resolves.toBeVisible();

    // Find the dialog body text (unique to archive dialog, not on page)
    const dialogBody = await screen.findByText(/sure.*archive/i);

    // Assert dialog contains the group name
    expect(dialogBody).toHaveTextContent('Lisbon Trip');

    // Assert dialog contains "archive" (case-insensitive)
    expect(dialogBody).toHaveTextContent(/archive/i);

    // Assert dialog does NOT contain "remove" — distinct from RemoveMember
    expect(dialogBody).not.toHaveTextContent(/remove/i);
  });

  test('clicking Cancel closes the dialog without making an API call', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const aliceId = '11111111-1111-1111-1111-111111111111';

    let archiveCalled = false;

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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 0 },
        ]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.post('http://localhost:5000/groups/:id/archive', () => {
        archiveCalled = true;
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

    const user = userEvent.setup();

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the Archive button to open the dialog
    const archiveButton = screen.getByRole('button', { name: /archive/i });
    await user.click(archiveButton);

    // Wait for the dialog to appear
    await expect(
      screen.findByRole('heading', { name: /archive group/i }),
    ).resolves.toBeVisible();

    // Click Cancel
    const cancelButton = screen.getByRole('button', { name: /cancel/i });
    await user.click(cancelButton);

    // The dialog should close (heading no longer found)
    await waitFor(() => {
      expect(
        screen.queryByRole('heading', { name: /archive group/i }),
      ).not.toBeInTheDocument();
    });

    // The archive API must NOT have been called
    expect(archiveCalled).toBe(false);

    // The group name and member should still be visible on the page
    expect(screen.getByText('Lisbon Trip')).toBeInTheDocument();
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  test('clicking the Archive confirm button sends POST /groups/{id}/archive with no body', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const aliceId = '11111111-1111-1111-1111-111111111111';

    let archiveCalled = false;
    let archiveGroupId: string | null = null;

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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 0 },
        ]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.post(
        'http://localhost:5000/groups/:id/archive',
        async ({ params, request }) => {
          archiveCalled = true;
          archiveGroupId = params.id as string;
          const bodyText = await request.text();
          expect(bodyText).toBe('');
          return new HttpResponse(null, { status: 204 });
        },
      ),
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

    const user = userEvent.setup();

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the Archive button in the header to open the dialog
    const headerArchiveButton = screen.getByRole('button', { name: /archive/i });
    await user.click(headerArchiveButton);

    // Wait for the dialog to appear
    await expect(
      screen.findByRole('heading', { name: /archive group/i }),
    ).resolves.toBeVisible();

    // Scope confirm button to the dialog using within() — see L-FE12
    const dialogHeading = screen.getByRole('heading', { name: /archive group/i });
    const dialogCard = dialogHeading.closest('div[role="dialog"]') as HTMLElement;
    const confirmButton = within(dialogCard).getByRole('button', {
      name: /archive/i,
    });
    await user.click(confirmButton);

    // Assert the POST was called with the correct group ID and no body
    await waitFor(() => {
      expect(archiveCalled).toBe(true);
      expect(archiveGroupId).toBe(testGroupId);
    });
  });

  test('on 204 No Content the dialog closes and navigates to /groups', async () => {
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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 0 },
        ]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.post('http://localhost:5000/groups/:id/archive', () => {
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
            <Route path="/groups" element={<div data-testid="groups-list">Groups List</div>} />
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    const user = userEvent.setup();

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the Archive button in the header to open the dialog
    const headerArchiveButton = screen.getByRole('button', { name: /archive/i });
    await user.click(headerArchiveButton);

    // Wait for the dialog to appear
    await expect(
      screen.findByRole('heading', { name: /archive group/i }),
    ).resolves.toBeVisible();

    // Click the Archive confirm button in the dialog (scoped via within) — L-FE12
    const dialogHeading = screen.getByRole('heading', { name: /archive group/i });
    const dialogCard = dialogHeading.closest('div[role="dialog"]') as HTMLElement;
    const confirmButton = within(dialogCard).getByRole('button', {
      name: /archive/i,
    });
    await user.click(confirmButton);

    // The dialog should close
    await waitFor(() => {
      expect(
        screen.queryByRole('heading', { name: /archive group/i }),
      ).not.toBeInTheDocument();
    });

    // The user should be navigated to /groups
    await waitFor(() => {
      expect(screen.getByTestId('groups-list')).toBeInTheDocument();
    });
  });

  test('on 5xx the dialog shows an error message and the user stays on Group Detail', async () => {
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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 0 },
        ]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.post('http://localhost:5000/groups/:id/archive', () => {
        return HttpResponse.json(
          { type: 'about:blank', title: 'Server Error', status: 500, detail: 'Internal server error' },
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
            <Route path="/groups" element={<div data-testid="groups-list">Groups List</div>} />
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    const user = userEvent.setup();

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the Archive button in the header to open the dialog
    const headerArchiveButton = screen.getByRole('button', { name: /archive/i });
    await user.click(headerArchiveButton);

    // Wait for the dialog to appear
    await expect(
      screen.findByRole('heading', { name: /archive group/i }),
    ).resolves.toBeVisible();

    // Click the Archive confirm button in the dialog (scoped via within) — L-FE12
    const dialogHeading = screen.getByRole('heading', { name: /archive group/i });
    const dialogCard = dialogHeading.closest('div[role="dialog"]') as HTMLElement;
    const confirmButton = within(dialogCard).getByRole('button', {
      name: /archive/i,
    });
    await user.click(confirmButton);

    // An error message should appear
    await expect(
      screen.findByText(/something went wrong/i),
    ).resolves.toBeVisible();

    // The user should still be on Group Detail (group name visible)
    await waitFor(() => {
      expect(screen.getByText('Lisbon Trip')).toBeInTheDocument();
    });

    // The dialog should have closed
    await waitFor(() => {
      expect(
        screen.queryByRole('heading', { name: /archive group/i }),
      ).not.toBeInTheDocument();
    });
  });

  test('on network error the dialog shows an error message and the user stays on Group Detail', async () => {
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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 0 },
        ]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.post('http://localhost:5000/groups/:id/archive', () => {
        return HttpResponse.error();
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
            <Route path="/groups" element={<div data-testid="groups-list">Groups List</div>} />
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    const user = userEvent.setup();

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the Archive button in the header to open the dialog
    const headerArchiveButton = screen.getByRole('button', { name: /archive/i });
    await user.click(headerArchiveButton);

    // Wait for the dialog to appear
    await expect(
      screen.findByRole('heading', { name: /archive group/i }),
    ).resolves.toBeVisible();

    // Click the Archive confirm button in the dialog (scoped via within) — L-FE12
    const dialogHeading = screen.getByRole('heading', { name: /archive group/i });
    const dialogCard = dialogHeading.closest('div[role="dialog"]') as HTMLElement;
    const confirmButton = within(dialogCard).getByRole('button', {
      name: /archive/i,
    });
    await user.click(confirmButton);

    // An error message should appear (current handler shows generic message for all errors)
    await expect(
      screen.findByText(/something went wrong/i),
    ).resolves.toBeVisible();

    // The user should still be on Group Detail (group name visible)
    await waitFor(() => {
      expect(screen.getByText('Lisbon Trip')).toBeInTheDocument();
    });

    // The dialog should have closed
    await waitFor(() => {
      expect(
        screen.queryByRole('heading', { name: /archive group/i }),
      ).not.toBeInTheDocument();
    });
  });

  test('archive succeeds with non-zero outstanding balances — no client-side balance precheck', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const aliceId = '11111111-1111-1111-1111-111111111111';
    const bobId = '22222222-2222-2222-2222-222222222222';

    let archiveCalled = false;

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
        // Non-zero balances: Alice is owed €30, Bob owes €30
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 3000 },
          { userId: bobId, netAmountMinor: -3000 },
        ]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.post('http://localhost:5000/groups/:id/archive', () => {
        archiveCalled = true;
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
            <Route path="/groups" element={<div data-testid="groups-list">Groups List</div>} />
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    const user = userEvent.setup();

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the Archive button in the header to open the dialog
    const headerArchiveButton = screen.getByRole('button', { name: /archive/i });
    await user.click(headerArchiveButton);

    // The dialog should open normally — no blocking message about outstanding balances
    await expect(
      screen.findByRole('heading', { name: /archive group/i }),
    ).resolves.toBeVisible();

    // The dialog body should NOT mention balances, owing, or any blocking reason
    const dialogBody = await screen.findByText(/sure.*archive/i);
    expect(dialogBody).not.toHaveTextContent(/balance/i);
    expect(dialogBody).not.toHaveTextContent(/owe/i);
    expect(dialogBody).not.toHaveTextContent(/outstanding/i);

    // Click the Archive confirm button in the dialog (scoped via within) — L-FE12
    const dialogHeading = screen.getByRole('heading', { name: /archive group/i });
    const dialogCard = dialogHeading.closest('div[role="dialog"]') as HTMLElement;
    const confirmButton = within(dialogCard).getByRole('button', {
      name: /archive/i,
    });
    await user.click(confirmButton);

    // The archive POST should have been called (not blocked by balances)
    await waitFor(() => {
      expect(archiveCalled).toBe(true);
    });

    // The dialog should close and navigate to /groups
    await waitFor(() => {
      expect(
        screen.queryByRole('heading', { name: /archive group/i }),
      ).not.toBeInTheDocument();
    });
    await waitFor(() => {
      expect(screen.getByTestId('groups-list')).toBeInTheDocument();
    });
  });

  test('while the archive request is in flight the confirm button is disabled and shows "Archiving..."', async () => {
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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 0 },
        ]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.post('http://localhost:5000/groups/:id/archive', async () => {
        await new Promise(r => setTimeout(r, 500));
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
            <Route path="/groups" element={<div data-testid="groups-list">Groups List</div>} />
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    const user = userEvent.setup();

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the Archive button in the header to open the dialog
    const headerArchiveButton = screen.getByRole('button', { name: /archive/i });
    await user.click(headerArchiveButton);

    // Wait for the dialog to appear
    await expect(
      screen.findByRole('heading', { name: /archive group/i }),
    ).resolves.toBeVisible();

    // Click the Archive confirm button in the dialog (scoped via within) — L-FE12
    const dialogHeading = screen.getByRole('heading', { name: /archive group/i });
    const dialogCard = dialogHeading.closest('div[role="dialog"]') as HTMLElement;
    const confirmButton = within(dialogCard).getByRole('button', {
      name: /archive/i,
    });
    await user.click(confirmButton);

    // The confirm button should now show "Archiving..." and be disabled
    await expect(
      screen.findByRole('button', { name: /archiving/i }),
    ).resolves.toBeDisabled();

    // After the delayed response the dialog should close
    await waitFor(() => {
      expect(
        screen.queryByRole('heading', { name: /archive group/i }),
      ).not.toBeInTheDocument();
    });
  });

  test('on 401 the JWT is cleared and the user is redirected to /login?expired=true', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const aliceId = '11111111-1111-1111-1111-111111111111';

    // Replace window.location with a plain mock so jsdom doesn't throw on href assignment
    const originalLocation = window.location;
    const mockLocation = {
      href: '',
      pathname: '/groups/test',
    } as unknown as Location;
    Object.defineProperty(window, 'location', {
      value: mockLocation,
      writable: true,
      configurable: true,
    });

    try {
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
          return HttpResponse.json([
            { userId: aliceId, netAmountMinor: 0 },
          ]);
        }),
        http.get('http://localhost:5000/groups/:id/expenses', () => {
          return HttpResponse.json({ items: [], total: 0 });
        }),
        http.post('http://localhost:5000/groups/:id/archive', () => {
          return new HttpResponse(null, { status: 401 });
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
              <Route path="/groups" element={<div data-testid="groups-list">Groups List</div>} />
              <Route path="/groups/:id" element={<GroupDetail />} />
            </Routes>
          </MemoryRouter>
        </QueryClientProvider>,
      );

      const user = userEvent.setup();

      // Wait for group data to load
      await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

      // Click the Archive button to open the dialog
      const archiveButton = screen.getByRole('button', { name: /archive/i });
      await user.click(archiveButton);

      // Wait for the dialog to appear
      await expect(
        screen.findByRole('heading', { name: /archive group/i }),
      ).resolves.toBeVisible();

    // Click the Archive confirm button in the dialog (scoped via within) — L-FE12
    const dialogHeading = screen.getByRole('heading', { name: /archive group/i });
    const dialogCard = dialogHeading.closest('div[role="dialog"]') as HTMLElement;
    const confirmButton = within(dialogCard).getByRole('button', {
      name: /archive/i,
    });
      await user.click(confirmButton);

      // The JWT should be cleared from localStorage
      await waitFor(() => {
        expect(localStorage.getItem('splitbook_token')).toBeNull();
      });

      // The user should be redirected to /login?expired=true
      await waitFor(() => {
        expect((mockLocation as { href: string }).href).toContain('/login?expired=true');
      });
    } finally {
      Object.defineProperty(window, 'location', {
        value: originalLocation,
        writable: true,
        configurable: true,
      });
    }
  });

  test('archived group no longer appears in GroupsList after successful archive and navigation back', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const aliceId = '11111111-1111-1111-1111-111111111111';

    server.use(
      http.get('http://localhost:5000/groups', () => {
        // GroupsList only mounts after archive + navigation, so the group
        // should already be gone from the list
        return HttpResponse.json([]);
      }),
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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 3000 },
        ]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.post('http://localhost:5000/groups/:id/archive', () => {
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
            <Route path="/groups" element={<GroupsList />} />
            <Route path="/groups/:id" element={<GroupDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    const user = userEvent.setup();

    // Wait for group data to load on GroupDetail
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Click the Archive button in the header to open the dialog
    const headerArchiveButton = screen.getByRole('button', { name: /archive/i });
    await user.click(headerArchiveButton);

    // Wait for the dialog to appear
    await expect(
      screen.findByRole('heading', { name: /archive group/i }),
    ).resolves.toBeVisible();

    // Click the Archive confirm button in the dialog (scoped via within) — L-FE12
    const dialogHeading = screen.getByRole('heading', { name: /archive group/i });
    const dialogCard = dialogHeading.closest('div[role="dialog"]') as HTMLElement;
    const confirmButton = within(dialogCard).getByRole('button', {
      name: /archive/i,
    });
    await user.click(confirmButton);

    // The dialog should close
    await waitFor(() => {
      expect(
        screen.queryByRole('heading', { name: /archive group/i }),
      ).not.toBeInTheDocument();
    });

    // The user should be navigated to /groups and the GroupsList should render
    // After archive, GET /groups returns empty, so "No groups yet" should appear
    // and "Lisbon Trip" should NOT appear
    await expect(screen.findByText('No groups yet')).resolves.toBeVisible();
  });

  test('full archive flow through real appRoutes: GroupDetail → Archive → /groups (real router)', async () => {
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const aliceId = '11111111-1111-1111-1111-111111111111';

    server.use(
      http.get('http://localhost:5000/groups', () => {
        // GroupsList only mounts after archive + navigation to /groups,
        // so the archived group should already be gone from the list
        return HttpResponse.json([]);
      }),
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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 0 },
        ]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.post('http://localhost:5000/groups/:id/archive', () => {
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });

    // Render with the REAL appRoutes (same routes App.tsx uses), not hand-built Routes
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[`/groups/${testGroupId}`]}>
          <Routes>{appRoutes}</Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    const user = userEvent.setup();

    // 1. GroupDetail loads via real route /groups/:id
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // 2. Click the Archive button
    const archiveButton = screen.getByRole('button', { name: /archive/i });
    await user.click(archiveButton);

    // 3. Wait for the confirmation dialog to appear
    await expect(
      screen.findByRole('heading', { name: /archive group/i }),
    ).resolves.toBeVisible();

    // 4. Click the Archive confirm button in the dialog (scoped via within) — L-FE12
    const dialogHeading = screen.getByRole('heading', { name: /archive group/i });
    const dialogCard = dialogHeading.closest('div[role="dialog"]') as HTMLElement;
    const confirmButton = within(dialogCard).getByRole('button', {
      name: /archive/i,
    });
    await user.click(confirmButton);

    // 5. The dialog should close
    await waitFor(() => {
      expect(
        screen.queryByRole('heading', { name: /archive group/i }),
      ).not.toBeInTheDocument();
    });

    // 6. Navigation to /groups should happen — GroupsList renders with "No groups yet"
    // (because the second GET /groups call returns an empty array)
    await expect(screen.findByText('No groups yet')).resolves.toBeVisible();

    // 7. The archived group should NOT appear in the list
    await waitFor(() => {
      expect(screen.queryByText('Lisbon Trip')).not.toBeInTheDocument();
    });
  });
});
