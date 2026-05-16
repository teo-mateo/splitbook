import { http, HttpResponse } from 'msw';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { server } from '../../../test/setup';
import { GroupDetail } from './GroupDetail';

function getMembersSection() {
  const membersHeading = screen.getByRole('heading', { name: /members/i });
  return membersHeading.closest('section')!;
}

function getRemoveButtonForMember(displayName: string) {
  const memberName = screen.getByText(displayName, { selector: 'span.font-medium' });
  const memberRow = memberName.closest('li')!;
  return within(memberRow).getByRole('button', { name: /remove/i });
}

function getDialogConfirmButton() {
  const dialogHeading = screen.getByRole('heading', { name: /remove member/i });
  const dialogContainer = dialogHeading.closest('div')!;
  return within(dialogContainer).getByRole('button', { name: 'Remove' });
}

function getDialogCancelButton() {
  const dialogHeading = screen.getByRole('heading', { name: /remove member/i });
  const dialogContainer = dialogHeading.closest('div')!;
  return within(dialogContainer).getByRole('button', { name: 'Cancel' });
}

describe('RemoveMember in GroupDetail', () => {
  beforeEach(() => {
    localStorage.setItem('splitbook_token', 'fake-jwt-token');
  });

  afterEach(() => {
    localStorage.removeItem('splitbook_token');
  });

  test('each member row has a "Remove" button when the group has more than one member', async () => {
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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 3000 },
          { userId: bobId, netAmountMinor: -3000 },
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

    // Both members should be visible
    await expect(screen.findByText('Alice')).resolves.toBeVisible();
    await expect(screen.findByText('Bob')).resolves.toBeVisible();

    // Each member row should have a "Remove" button
    await waitFor(() => {
      const removeButtons = screen.getAllByRole('button', { name: /remove/i });
      expect(removeButtons.length).toBeGreaterThanOrEqual(2);
    });
  });

  test('clicking Remove shows a confirmation dialog and does not call the API until confirmed', async () => {
    const user = userEvent.setup();
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const aliceId = '11111111-1111-1111-1111-111111111111';
    const bobId = '22222222-2222-2222-2222-222222222222';
    let deleteCalled = false;

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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 3000 },
          { userId: bobId, netAmountMinor: -3000 },
        ]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.delete(
        'http://localhost:5000/groups/:id/members/:userId',
        () => {
          deleteCalled = true;
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

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Both members should be visible
    await expect(screen.findByText('Alice')).resolves.toBeVisible();
    await expect(screen.findByText('Bob')).resolves.toBeVisible();

    // Click the "Remove" button next to Alice
    const removeAliceButton = getRemoveButtonForMember('Alice');
    await user.click(removeAliceButton);

    // Assert the confirmation dialog appears with the member's name and group name
    await expect(
      screen.findByText(/Remove Alice from Lisbon Trip\?/),
    ).resolves.toBeVisible();

    // Assert the DELETE endpoint has NOT been called yet (user hasn't confirmed)
    expect(deleteCalled).toBe(false);
  });

  test('confirmed removal sends DELETE /groups/{id}/members/{userId} and refreshes data', async () => {
    const user = userEvent.setup();
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const aliceId = '11111111-1111-1111-1111-111111111111';
    const bobId = '22222222-2222-2222-2222-222222222222';
    let deletedUserId: string | null = null;
    let groupCallCount = 0;

    server.use(
      http.get('http://localhost:5000/groups/:id', () => {
        groupCallCount++;
        // First call: 2 members; after invalidation: 1 member (Alice removed)
        if (groupCallCount === 1) {
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
        }
        // Subsequent calls (after DELETE + invalidation): Alice gone
        return HttpResponse.json({
          id: testGroupId,
          name: 'Lisbon Trip',
          currency: 'EUR',
          createdAt: '2024-01-15T10:00:00Z',
          archivedAt: null,
          members: [
            { userId: bobId, displayName: 'Bob' },
          ],
        });
      }),
      http.get('http://localhost:5000/groups/:id/balances', () => {
        return HttpResponse.json([
          { userId: bobId, netAmountMinor: -3000 },
        ]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.delete(
        'http://localhost:5000/groups/:groupId/members/:userId',
        ({ params }) => {
          deletedUserId = params.userId as string;
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

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Both members visible initially
    await expect(screen.findByText('Alice')).resolves.toBeVisible();
    await expect(screen.findByText('Bob')).resolves.toBeVisible();

    // Click the "Remove" button next to Alice
    const removeAliceButton = getRemoveButtonForMember('Alice');
    await user.click(removeAliceButton);

    // Confirmation dialog appears
    await expect(
      screen.findByText(/Remove Alice from Lisbon Trip\?/),
    ).resolves.toBeVisible();

    // Click the confirm "Remove" button in the dialog
    const confirmButton = getDialogConfirmButton();
    await user.click(confirmButton);

    // Assert DELETE was called with Alice's userId
    expect(deletedUserId).toBe(aliceId);

    // Assert the confirmation dialog closes
    await waitFor(() => {
      expect(
        screen.queryByText(/Remove Alice from Lisbon Trip\?/),
      ).toBeNull();
    });

    // Assert Alice no longer appears in the member list, only Bob remains
    await waitFor(() => {
      const membersSection = getMembersSection();
      expect(within(membersSection).queryByText('Alice')).toBeNull();
      expect(within(membersSection).getByText('Bob')).toBeVisible();
    });
  });

  test('canceling the removal confirmation does nothing', async () => {
    const user = userEvent.setup();
    const testGroupId = 'a1111111-1111-1111-1111-111111111111';
    const aliceId = '11111111-1111-1111-1111-111111111111';
    const bobId = '22222222-2222-2222-2222-222222222222';
    let deleteCalled = false;

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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 3000 },
          { userId: bobId, netAmountMinor: -3000 },
        ]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.delete(
        'http://localhost:5000/groups/:id/members/:userId',
        () => {
          deleteCalled = true;
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

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Both members should be visible
    await expect(screen.findByText('Alice')).resolves.toBeVisible();
    await expect(screen.findByText('Bob')).resolves.toBeVisible();

    // Click the "Remove" button next to Alice
    const removeAliceButton = getRemoveButtonForMember('Alice');
    await user.click(removeAliceButton);

    // Confirmation dialog appears
    await expect(
      screen.findByText(/Remove Alice from Lisbon Trip\?/),
    ).resolves.toBeVisible();

    // Click the "Cancel" button in the dialog
    const cancelButton = getDialogCancelButton();
    await user.click(cancelButton);

    // Assert DELETE was NOT called
    expect(deleteCalled).toBe(false);

    // Assert both Alice and Bob still appear in the member list
    await waitFor(() => {
      const membersSection = getMembersSection();
      expect(within(membersSection).getByText('Alice')).toBeVisible();
      expect(within(membersSection).getByText('Bob')).toBeVisible();
    });

    // Assert the confirmation dialog is closed
    await waitFor(() => {
      expect(
        screen.queryByText(/Remove Alice from Lisbon Trip\?/),
      ).toBeNull();
    });
  });

  test('on API error during removal (non-401) an error message is shown and the member remains', async () => {
    const user = userEvent.setup();
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
        return HttpResponse.json([
          { userId: aliceId, netAmountMinor: 3000 },
          { userId: bobId, netAmountMinor: -3000 },
        ]);
      }),
      http.get('http://localhost:5000/groups/:id/expenses', () => {
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.delete(
        'http://localhost:5000/groups/:id/members/:userId',
        () => {
          return HttpResponse.json(
            { type: 'about:blank', title: 'Server Error', status: 500, detail: 'Internal server error' },
            { status: 500 },
          );
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

    // Wait for group data to load
    await expect(screen.findByText('Lisbon Trip')).resolves.toBeVisible();

    // Both members visible initially
    await expect(screen.findByText('Alice')).resolves.toBeVisible();
    await expect(screen.findByText('Bob')).resolves.toBeVisible();

    // Click the "Remove" button next to Alice
    const removeAliceButton = getRemoveButtonForMember('Alice');
    await user.click(removeAliceButton);

    // Confirmation dialog appears
    await expect(
      screen.findByText(/Remove Alice from Lisbon Trip\?/),
    ).resolves.toBeVisible();

    // Click the confirm "Remove" button in the dialog
    const confirmButton = getDialogConfirmButton();
    await user.click(confirmButton);

    // Assert an error message is shown
    await expect(
      screen.findByText(/something went wrong/i),
    ).resolves.toBeVisible();

    // Assert both Alice and Bob still appear in the member list (removal did not succeed)
    await waitFor(() => {
      const membersSection = getMembersSection();
      expect(within(membersSection).getByText('Alice')).toBeVisible();
      expect(within(membersSection).getByText('Bob')).toBeVisible();
    });
  });
});
