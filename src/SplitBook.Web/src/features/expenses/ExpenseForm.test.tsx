import { http, HttpResponse } from 'msw';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { server } from '../../../test/setup';
import App from '../../App';

describe('ExpenseForm', () => {
  beforeEach(() => {
    localStorage.setItem('splitbook_token', 'fake-jwt-token');
  });

  afterEach(() => {
    localStorage.removeItem('splitbook_token');
  });

  test('route reachable from Group Detail via real <App>', async () => {
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
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);

    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();
  });

  test('AuthGuard redirects unauthenticated users to /login', async () => {
    const testGroupId = 'a2222222-2222-2222-2222-222222222222';

    localStorage.removeItem('splitbook_token');

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);

    render(<App />);

    expect(window.location.pathname).toBe('/login');
  });

  test('form renders all required fields', async () => {
    const testGroupId = 'a3333333-3333-3333-3333-333333333333';
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
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);

    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    expect(screen.getByLabelText(/description/i, { selector: 'input' })).toBeVisible();

    const amountInput = screen.getByRole('spinbutton') || screen.getByLabelText(/amount/i);
    expect(amountInput).toBeVisible();

    const currencyInput = screen.getByLabelText(/currency/i, { selector: 'input' });
    expect(currencyInput).toHaveValue('EUR');
    expect(currencyInput).toHaveAttribute('readonly');

    expect(screen.getByLabelText(/payer/i, { selector: 'select' })).toBeVisible();

    expect(screen.getByLabelText(/date/i, { selector: 'input' })).toBeVisible();

    expect(screen.getByText('Equal')).toBeVisible();

    expect(screen.getByRole('checkbox', { name: /alice/i })).toBeVisible();
    expect(screen.getByRole('checkbox', { name: /bob/i })).toBeVisible();
  });

  test('description required — inline error on submit empty', async () => {
    const user = userEvent.setup();
    const testGroupId = 'a4444444-4444-4444-4444-444444444444';
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
            { userId: '22222222-2222-2222-2222-222222222222', displayName: 'Bob' },
          ],
        });
      }),
      http.post('http://localhost:5000/groups/:groupId/expenses', () => {
        postCalled = true;
        return HttpResponse.json({ id: 'expense-1', amountMinor: 0 }, { status: 201 });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);

    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    const submitButton = screen.getByRole('button', { name: 'Add Expense' });
    await user.click(submitButton);

    expect(screen.getByText('Description is required')).toBeVisible();

    expect(postCalled).toBe(false);
  });

  test('visible back navigation to Group Detail', async () => {
    const user = userEvent.setup();
    const testGroupId = 'a5555555-5555-5555-5555-555555555555';

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
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);

    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    const backLink = screen.getByRole('link', { name: /back/i });
    await expect(backLink).toBeVisible();

    await user.click(backLink);

    expect(window.location.pathname).toBe(`/groups/${testGroupId}`);
  });

  test('submit sends POST with correct shape and navigates on 201', async () => {
    const user = userEvent.setup();
    const testGroupId = 'a6666666-6666-6666-6666-666666666666';
    const aliceId = '11111111-1111-1111-1111-111111111111';
    const bobId = '22222222-2222-2222-2222-222222222222';
    let capturedBody: unknown;

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
      http.post('http://localhost:5000/groups/:groupId/expenses', async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({
          id: 'e1111111-1111-1111-1111-111111111111',
          groupId: testGroupId,
          payerUserId: aliceId,
          amountMinor: 6000,
          currency: 'EUR',
          description: 'Dinner',
          occurredOn: '2024-03-10',
          splitMethod: 'Equal',
          splits: [{ userId: aliceId, amountMinor: 6000 }],
          createdAt: '2024-03-10T20:00:00Z',
          version: 1,
        }, { status: 201 });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    // Fill description
    const descInput = screen.getByLabelText(/description/i, { selector: 'input' });
    await user.type(descInput, 'Dinner');

    // Fill amount
    const amountInput = screen.getByRole('spinbutton');
    await user.clear(amountInput);
    await user.type(amountInput, '60');

    // Select payer (auto-checks Alice as participant)
    const payerSelect = screen.getByLabelText(/payer/i, { selector: 'select' });
    await user.selectOptions(payerSelect, aliceId);

    // Verify Alice is checked as participant (auto-checked by payer selection)
    const aliceCheckbox = screen.getByRole('checkbox', { name: /alice/i });
    expect(aliceCheckbox).toBeChecked();

    // Submit
    const submitButton = screen.getByRole('button', { name: 'Add Expense' });
    await user.click(submitButton);

    // Assert request body shape
    await waitFor(() => {
      expect(capturedBody).not.toBeUndefined();
    });
    const body = capturedBody as Record<string, unknown>;
    expect(body.amountMinor).toBe(6000);
    expect(body.payerUserId).toBe(aliceId);
    expect(body.currency).toBe('EUR');
    expect(body.description).toBe('Dinner');
    expect(body.splitMethod).toBe('Equal');
    expect(Array.isArray(body.splits)).toBe(true);
    expect((body.splits as Array<Record<string, unknown>>).length).toBe(1);
    expect((body.splits as Array<Record<string, unknown>>)[0].userId).toBe(aliceId);

    // Assert navigation
    expect(window.location.pathname).toBe(`/groups/${testGroupId}`);
  });

  test('at least one participant required — inline error when none checked', async () => {
    const user = userEvent.setup();
    const testGroupId = 'a7777777-7777-7777-7777-777777777777';
    const aliceId = '11111111-1111-1111-1111-111111111111';
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
            { userId: aliceId, displayName: 'Alice' },
          ],
        });
      }),
      http.post('http://localhost:5000/groups/:groupId/expenses', () => {
        postCalled = true;
        return HttpResponse.json({ id: 'expense-1', amountMinor: 0 }, { status: 201 });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    // Fill description
    const descInput = screen.getByLabelText(/description/i, { selector: 'input' });
    await user.type(descInput, 'Dinner');

    // Fill amount
    const amountInput = screen.getByRole('spinbutton');
    await user.clear(amountInput);
    await user.type(amountInput, '60');

    // Select payer (auto-checks Alice as participant)
    const payerSelect = screen.getByLabelText(/payer/i, { selector: 'select' });
    await user.selectOptions(payerSelect, aliceId);

    // Uncheck Alice so no participants remain
    const aliceCheckbox = screen.getByRole('checkbox', { name: /alice/i });
    await user.click(aliceCheckbox);

    // Submit with no participants checked
    const submitButton = screen.getByRole('button', { name: 'Add Expense' });
    await user.click(submitButton);

    // Assert inline validation error appears
    expect(screen.getByText('Select at least one participant')).toBeVisible();

    // Assert API was never called
    expect(postCalled).toBe(false);
  });

  test('400 response shows server error detail and preserves form data', async () => {
    const user = userEvent.setup();
    const testGroupId = 'a8888888-8888-8888-8888-888888888888';
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
      http.post('http://localhost:5000/groups/:groupId/expenses', () => {
        return HttpResponse.json({ detail: 'Group not found' }, { status: 400 });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    // Fill description
    const descInput = screen.getByLabelText(/description/i, { selector: 'input' });
    await user.type(descInput, 'Dinner');

    // Fill amount
    const amountInput = screen.getByRole('spinbutton');
    await user.clear(amountInput);
    await user.type(amountInput, '60');

    // Select payer (auto-checks Alice as participant)
    const payerSelect = screen.getByLabelText(/payer/i, { selector: 'select' });
    await user.selectOptions(payerSelect, aliceId);

    // Submit — form is valid so POST fires and gets 400
    const submitButton = screen.getByRole('button', { name: 'Add Expense' });
    await user.click(submitButton);

    // Assert server error detail appears inline
    await expect(screen.findByText('Group not found')).resolves.toBeVisible();

    // Assert form data is preserved (not cleared on error)
    expect(descInput).toHaveValue('Dinner');
    // type="number" inputs report numeric values
    expect(amountInput).toHaveValue(60);
  });

  // App.tsx QueryClient retries by default; give retries time to exhaust.
  test('404 response shows "Group not found or you are not a member"', async () => {
    const testGroupId = 'a9999999-9999-9999-9999-999999999999';

    server.use(
      http.get('http://localhost:5000/groups/:id', () => {
        return HttpResponse.json({ detail: 'Not found' }, { status: 404 });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    // The QueryClient in App.tsx retries by default; wait for retries to exhaust
    // and the error state to render.
    await waitFor(
      () => {
        expect(screen.getByText('Group not found or you are not a member')).toBeVisible();
      },
      { timeout: 10000 },
    );
  }, 15000);

  test('401 response clears token and redirects to /login?expired=true', async () => {
    const user = userEvent.setup();
    const testGroupId = 'ab111111-1111-1111-1111-111111111111';
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
      http.post('http://localhost:5000/groups/:groupId/expenses', () => {
        return HttpResponse.json({ detail: 'Unauthorized' }, { status: 401 });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    // Fill form
    const descInput = screen.getByLabelText(/description/i, { selector: 'input' });
    await user.type(descInput, 'Dinner');

    const amountInput = screen.getByRole('spinbutton');
    await user.clear(amountInput);
    await user.type(amountInput, '60');

    const payerSelect = screen.getByLabelText(/payer/i, { selector: 'select' });
    await user.selectOptions(payerSelect, aliceId);

    // Submit
    const submitButton = screen.getByRole('button', { name: 'Add Expense' });
    await user.click(submitButton);

    // Assert token was cleared from localStorage.
    // Note: jsdom does not implement window.location.href navigation, so we
    // cannot assert the redirect. The client.ts code path (localStorage.removeItem
    // + window.location.href = '/login?expired=true') is exercised by the 401
    // response; the token removal is the verifiable side-effect.
    await waitFor(() => {
      expect(localStorage.getItem('splitbook_token')).toBeNull();
    });
  });

  test('5xx response shows error toast or banner', async () => {
    const user = userEvent.setup();
    const testGroupId = 'ab222222-2222-2222-2222-222222222222';
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
      http.post('http://localhost:5000/groups/:groupId/expenses', () => {
        return HttpResponse.json({ error: 'internal' }, { status: 500 });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    // Fill form
    const descInput = screen.getByLabelText(/description/i, { selector: 'input' });
    await user.type(descInput, 'Dinner');

    const amountInput = screen.getByRole('spinbutton');
    await user.clear(amountInput);
    await user.type(amountInput, '60');

    const payerSelect = screen.getByLabelText(/payer/i, { selector: 'select' });
    await user.selectOptions(payerSelect, aliceId);

    // Submit
    const submitButton = screen.getByRole('button', { name: 'Add Expense' });
    await user.click(submitButton);

    // Assert generic 5xx error message appears
    await expect(screen.findByText('Something went wrong. Please try again.')).resolves.toBeVisible();
  });

  test('Network error shows "Cannot reach the server"', async () => {
    const user = userEvent.setup();
    const testGroupId = 'ab333333-3333-3333-3333-333333333333';
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
      http.post('http://localhost:5000/groups/:groupId/expenses', () => {
        // HttpResponse.error() causes fetch() to reject, producing a non-ApiError
        // that exercises the else branch of the mutation's onError handler.
        return HttpResponse.error();
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    // Fill form
    const descInput = screen.getByLabelText(/description/i, { selector: 'input' });
    await user.type(descInput, 'Dinner');

    const amountInput = screen.getByRole('spinbutton');
    await user.clear(amountInput);
    await user.type(amountInput, '60');

    const payerSelect = screen.getByLabelText(/payer/i, { selector: 'select' });
    await user.selectOptions(payerSelect, aliceId);

    // Submit
    const submitButton = screen.getByRole('button', { name: 'Add Expense' });
    await user.click(submitButton);

    // Assert network error message appears
    await expect(screen.findByText('Cannot reach the server. Check your connection.')).resolves.toBeVisible();
  });

  test('Exact is selectable in the split-method control', async () => {
    const user = userEvent.setup();
    const testGroupId = 'ac111111-1111-1111-1111-111111111111';

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
            { userId: '22222222-2222-2222-2222-222222222222', displayName: 'Bob' },
          ],
        });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    // Equal should be the currently selected method (active styling)
    const equalButton = screen.getByRole('button', { name: /equal/i });
    expect(equalButton.className).toContain('bg-blue-600');

    // Exact should not be selected (inactive styling)
    const exactButton = screen.getByRole('button', { name: /exact/i });
    expect(exactButton.className).not.toContain('bg-blue-600');

    // Click the Exact button to switch split method
    await user.click(exactButton);

    // After clicking Exact, it should be the selected method (active styling)
    expect(exactButton.className).toContain('bg-blue-600');

    // Equal should no longer be selected (inactive styling)
    expect(equalButton.className).not.toContain('bg-blue-600');
  });

  test('Per-participant amount inputs appear for checked participants when Exact is selected', async () => {
    const user = userEvent.setup();
    const testGroupId = 'ac222222-2222-2222-2222-222222222222';

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
            { userId: '22222222-2222-2222-2222-222222222222', displayName: 'Bob' },
          ],
        });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    // Switch to Exact split method
    const exactButton = screen.getByRole('button', { name: /exact/i });
    await user.click(exactButton);

    // Check Alice's participant checkbox
    const aliceCheckbox = screen.getByRole('checkbox', { name: /alice/i });
    await user.click(aliceCheckbox);

    // Assert Alice's row now has a numeric amount input (spinbutton)
    // The participant labels are rendered as <label> elements with the checkbox + display name.
    // After checking Alice, a per-participant amount input should appear adjacent to her name.
    const aliceRow = aliceCheckbox.closest('label');
    expect(aliceRow).not.toBeNull();
    const aliceAmountInput = within(aliceRow!).getByRole('spinbutton');
    expect(aliceAmountInput).toBeVisible();

    // Assert Bob does NOT have an amount input (Bob is unchecked)
    const bobCheckbox = screen.getByRole('checkbox', { name: /bob/i });
    const bobRow = bobCheckbox.closest('label');
    expect(bobRow).not.toBeNull();
    expect(() => within(bobRow!).getByRole('spinbutton')).toThrow();

    // Now check Bob's checkbox
    await user.click(bobCheckbox);

    // Assert Bob now also has an amount input
    const bobAmountInput = within(bobRow!).getByRole('spinbutton');
    expect(bobAmountInput).toBeVisible();
  });

  test('Exact split shows running total of participant amounts', async () => {
    const user = userEvent.setup();
    const testGroupId = 'ac333333-3333-3333-3333-333333333333';

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
            { userId: '22222222-2222-2222-2222-222222222222', displayName: 'Bob' },
          ],
        });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    // Switch to Exact split method
    const exactButton = screen.getByRole('button', { name: /exact/i });
    await user.click(exactButton);

    // Check Alice's participant checkbox
    const aliceCheckbox = screen.getByRole('checkbox', { name: /alice/i });
    await user.click(aliceCheckbox);

    // Type "20" into Alice's amount input
    const aliceRow = aliceCheckbox.closest('label');
    expect(aliceRow).not.toBeNull();
    const aliceAmountInput = within(aliceRow!).getByRole('spinbutton');
    await user.type(aliceAmountInput, '20');

    // Assert a running total is visible showing €20.00
    await expect(screen.findByText(/€20\.00/i)).resolves.toBeVisible();

    // Check Bob's participant checkbox
    const bobCheckbox = screen.getByRole('checkbox', { name: /bob/i });
    await user.click(bobCheckbox);

    // Type "25" into Bob's amount input
    const bobRow = bobCheckbox.closest('label');
    expect(bobRow).not.toBeNull();
    const bobAmountInput = within(bobRow!).getByRole('spinbutton');
    await user.type(bobAmountInput, '25');

    // Assert the running total now shows €45.00
    await expect(screen.findByText(/€45\.00/i)).resolves.toBeVisible();
  });

  test('Exact: participant amounts that do not sum to the expense total show an inline error and do not call the API', async () => {
    const user = userEvent.setup();
    const testGroupId = 'ac444444-4444-4444-4444-444444444444';
    const aliceId = '11111111-1111-1111-1111-111111111111';
    const bobId = '22222222-2222-2222-2222-222222222222';
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
            { userId: aliceId, displayName: 'Alice' },
            { userId: bobId, displayName: 'Bob' },
          ],
        });
      }),
      http.post('http://localhost:5000/groups/:groupId/expenses', () => {
        postCalled = true;
        return HttpResponse.json({ id: 'expense-1', amountMinor: 0 }, { status: 201 });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    // Fill description
    const descInput = screen.getByLabelText(/description/i, { selector: 'input' });
    await user.type(descInput, 'Dinner');

    // Fill amount: 60 (the expense total)
    const amountInput = screen.getByRole('spinbutton');
    await user.clear(amountInput);
    await user.type(amountInput, '60');

    // Select Alice as payer (auto-checks Alice as participant)
    const payerSelect = screen.getByLabelText(/payer/i, { selector: 'select' });
    await user.selectOptions(payerSelect, aliceId);

    // Switch to Exact split method
    const exactButton = screen.getByRole('button', { name: /exact/i });
    await user.click(exactButton);

    // Check Bob as participant too
    const bobCheckbox = screen.getByRole('checkbox', { name: /bob/i });
    await user.click(bobCheckbox);

    // Type "20" into Alice's per-participant amount input
    const aliceCheckbox = screen.getByRole('checkbox', { name: /alice/i });
    const aliceRow = aliceCheckbox.closest('label');
    expect(aliceRow).not.toBeNull();
    const aliceAmountInput = within(aliceRow!).getByRole('spinbutton');
    await user.type(aliceAmountInput, '20');

    // Type "25" into Bob's per-participant amount input
    const bobRow = bobCheckbox.closest('label');
    expect(bobRow).not.toBeNull();
    const bobAmountInput = within(bobRow!).getByRole('spinbutton');
    await user.type(bobAmountInput, '25');

    // Sum is 45, not 60 — submit should show validation error
    const submitButton = screen.getByRole('button', { name: 'Add Expense' });
    await user.click(submitButton);

    // Assert inline error about participant amounts not matching total
    expect(screen.getByText(/participant amounts.*total/i)).toBeVisible();

    // Assert the API was never called
    expect(postCalled).toBe(false);
  });

  test('Submitting Exact with participant amounts that sum to the expense total sends the correct payload', async () => {
    const user = userEvent.setup();
    const testGroupId = 'ac555555-5555-5555-5555-555555555555';
    const aliceId = '11111111-1111-1111-1111-111111111111';
    const bobId = '22222222-2222-2222-2222-222222222222';
    let capturedBody: unknown;

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
      http.post('http://localhost:5000/groups/:groupId/expenses', async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({
          id: 'e2222222-2222-2222-2222-222222222222',
          groupId: testGroupId,
          payerUserId: aliceId,
          amountMinor: 6000,
          currency: 'EUR',
          description: 'Dinner',
          occurredOn: '2024-03-10',
          splitMethod: 'Exact',
          splits: [
            { userId: aliceId, amountMinor: 4000 },
            { userId: bobId, amountMinor: 2000 },
          ],
          createdAt: '2024-03-10T20:00:00Z',
          version: 1,
        }, { status: 201 });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    // Fill description
    const descInput = screen.getByLabelText(/description/i, { selector: 'input' });
    await user.type(descInput, 'Dinner');

    // Fill amount: 60 (the expense total) — only one spinbutton exists at this point
    const amountInput = screen.getByRole('spinbutton');
    await user.clear(amountInput);
    await user.type(amountInput, '60');

    // Select Alice as payer (auto-checks Alice as participant)
    const payerSelect = screen.getByLabelText(/payer/i, { selector: 'select' });
    await user.selectOptions(payerSelect, aliceId);

    // Switch to Exact split method
    const exactButton = screen.getByRole('button', { name: /exact/i });
    await user.click(exactButton);

    // Check Bob as participant too
    const bobCheckbox = screen.getByRole('checkbox', { name: /bob/i });
    await user.click(bobCheckbox);

    // Type "40" into Alice's per-participant amount input
    const aliceCheckbox = screen.getByRole('checkbox', { name: /alice/i });
    const aliceRow = aliceCheckbox.closest('label');
    expect(aliceRow).not.toBeNull();
    const aliceAmountInput = within(aliceRow!).getByRole('spinbutton');
    await user.type(aliceAmountInput, '40');

    // Type "20" into Bob's per-participant amount input
    const bobRow = bobCheckbox.closest('label');
    expect(bobRow).not.toBeNull();
    const bobAmountInput = within(bobRow!).getByRole('spinbutton');
    await user.type(bobAmountInput, '20');

    // Submit — total 40+20=60 matches expense total
    const submitButton = screen.getByRole('button', { name: 'Add Expense' });
    await user.click(submitButton);

    // Assert the captured POST body
    await waitFor(() => {
      expect(capturedBody).not.toBeUndefined();
    });
    const body = capturedBody as Record<string, unknown>;
    expect(body.splitMethod).toBe('Exact');
    expect(body.amountMinor).toBe(6000);
    expect(Array.isArray(body.splits)).toBe(true);
    const splits = body.splits as Array<Record<string, unknown>>;
    expect(splits.length).toBe(2);

    const aliceSplit = splits.find((s) => s.userId === aliceId);
    const bobSplit = splits.find((s) => s.userId === bobId);
    expect(aliceSplit).not.toBeUndefined();
    expect(bobSplit).not.toBeUndefined();
    expect(aliceSplit!.amountMinor).toBe(4000);
    expect(bobSplit!.amountMinor).toBe(2000);

    // Assert navigation back to Group Detail
    expect(window.location.pathname).toBe(`/groups/${testGroupId}`);
  });

  test('Submit button disabled while request is pending', async () => {
    const user = userEvent.setup();
    const testGroupId = 'ab444444-4444-4444-4444-444444444444';
    const aliceId = '11111111-1111-1111-1111-111111111111';
    let resolvePost: (() => void) | undefined;

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
      http.post('http://localhost:5000/groups/:groupId/expenses', async () => {
        // Delay the response so we can assert pending state
        await new Promise<void>((r) => { resolvePost = () => r(); setTimeout(r, 2000); });
        return HttpResponse.json({
          id: 'e1111111-1111-1111-1111-111111111111',
          groupId: testGroupId,
          payerUserId: aliceId,
          amountMinor: 6000,
          currency: 'EUR',
          description: 'Dinner',
          occurredOn: '2024-03-10',
          splitMethod: 'Equal',
          splits: [{ userId: aliceId, amountMinor: 6000 }],
          createdAt: '2024-03-10T20:00:00Z',
          version: 1,
        }, { status: 201 });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    // Fill form
    const descInput = screen.getByLabelText(/description/i, { selector: 'input' });
    await user.type(descInput, 'Dinner');

    const amountInput = screen.getByRole('spinbutton');
    await user.clear(amountInput);
    await user.type(amountInput, '60');

    const payerSelect = screen.getByLabelText(/payer/i, { selector: 'select' });
    await user.selectOptions(payerSelect, aliceId);

    // Submit
    const submitButton = screen.getByRole('button', { name: 'Add Expense' });
    await user.click(submitButton);

    // Assert button is disabled and shows pending text while request is in flight
    await waitFor(() => {
      const btn = screen.getByRole('button', { name: 'Adding...' });
      expect(btn).toBeDisabled();
    });

    // Resolve the delayed response
    resolvePost?.();

    // Assert navigation after response
    await waitFor(() => {
      expect(window.location.pathname).toBe(`/groups/${testGroupId}`);
    });
  });

  test('Equal split path works unchanged — default selected, POST body has splitMethod Equal with null split amounts', async () => {
    const user = userEvent.setup();
    const testGroupId = 'ac777777-7777-7777-7777-777777777777';
    const aliceId = '11111111-1111-1111-1111-111111111111';
    const bobId = '22222222-2222-2222-2222-222222222222';
    let capturedBody: unknown;

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
      http.post('http://localhost:5000/groups/:groupId/expenses', async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({
          id: 'e7777777-7777-7777-7777-777777777777',
          groupId: testGroupId,
          payerUserId: aliceId,
          amountMinor: 5000,
          currency: 'EUR',
          description: 'Lunch',
          occurredOn: '2024-03-10',
          splitMethod: 'Equal',
          splits: [
            { userId: aliceId, amountMinor: 2500 },
            { userId: bobId, amountMinor: 2500 },
          ],
          createdAt: '2024-03-10T20:00:00Z',
          version: 1,
        }, { status: 201 });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    // Assert Equal is the default-selected split method (active styling)
    const equalButton = screen.getByRole('button', { name: /equal/i });
    expect(equalButton.className).toContain('bg-blue-600');

    // Fill description
    const descInput = screen.getByLabelText(/description/i, { selector: 'input' });
    await user.type(descInput, 'Lunch');

    // Fill amount: 50
    const amountInput = screen.getByRole('spinbutton');
    await user.clear(amountInput);
    await user.type(amountInput, '50');

    // Select Alice as payer (auto-checks Alice as participant)
    const payerSelect = screen.getByLabelText(/payer/i, { selector: 'select' });
    await user.selectOptions(payerSelect, aliceId);

    // Verify Alice is auto-checked
    const aliceCheckbox = screen.getByRole('checkbox', { name: /alice/i });
    expect(aliceCheckbox).toBeChecked();

    // Check Bob as participant too
    const bobCheckbox = screen.getByRole('checkbox', { name: /bob/i });
    await user.click(bobCheckbox);

    // Submit (stay on Equal — do not click Exact)
    const submitButton = screen.getByRole('button', { name: 'Add Expense' });
    await user.click(submitButton);

    // Assert the captured POST body
    await waitFor(() => {
      expect(capturedBody).not.toBeUndefined();
    });
    const body = capturedBody as Record<string, unknown>;
    expect(body.splitMethod).toBe('Equal');
    expect(body.amountMinor).toBe(5000);
    expect(body.payerUserId).toBe(aliceId);
    expect(body.currency).toBe('EUR');
    expect(body.description).toBe('Lunch');

    // Assert splits: 2 entries, each with amountMinor: null (Equal split)
    expect(Array.isArray(body.splits)).toBe(true);
    const splits = body.splits as Array<Record<string, unknown>>;
    expect(splits.length).toBe(2);
    expect(splits[0].amountMinor).toBeNull();
    expect(splits[1].amountMinor).toBeNull();

    // Assert navigation back to Group Detail
    expect(window.location.pathname).toBe(`/groups/${testGroupId}`);
  });

  test('Exact split: on successful 201 POST /groups/{id}/expenses, navigate back to Group Detail', async () => {
    const user = userEvent.setup();
    const testGroupId = 'ac666666-6666-6666-6666-666666666666';
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
      http.post('http://localhost:5000/groups/:groupId/expenses', () => {
        return HttpResponse.json({
          id: 'e3333333-3333-3333-3333-333333333333',
          groupId: testGroupId,
          payerUserId: aliceId,
          amountMinor: 6000,
          currency: 'EUR',
          description: 'Dinner',
          occurredOn: '2024-03-10',
          splitMethod: 'Exact',
          splits: [
            { userId: aliceId, amountMinor: 4000 },
            { userId: bobId, amountMinor: 2000 },
          ],
          createdAt: '2024-03-10T20:00:00Z',
          version: 1,
        }, { status: 201 });
      }),
    );

    window.history.pushState({}, '', `/groups/${testGroupId}/expenses/new`);
    render(<App />);

    await expect(screen.findByRole('heading', { name: 'Add Expense' })).resolves.toBeVisible();

    // Fill description
    const descInput = screen.getByLabelText(/description/i, { selector: 'input' });
    await user.type(descInput, 'Dinner');

    // Fill amount: 60
    const amountInput = screen.getByRole('spinbutton');
    await user.clear(amountInput);
    await user.type(amountInput, '60');

    // Select Alice as payer (auto-checks Alice as participant)
    const payerSelect = screen.getByLabelText(/payer/i, { selector: 'select' });
    await user.selectOptions(payerSelect, aliceId);

    // Switch to Exact split method
    const exactButton = screen.getByRole('button', { name: /exact/i });
    await user.click(exactButton);

    // Check Bob as participant
    const bobCheckbox = screen.getByRole('checkbox', { name: /bob/i });
    await user.click(bobCheckbox);

    // Type "40" into Alice's per-participant amount input
    const aliceCheckbox = screen.getByRole('checkbox', { name: /alice/i });
    const aliceRow = aliceCheckbox.closest('label');
    expect(aliceRow).not.toBeNull();
    const aliceAmountInput = within(aliceRow!).getByRole('spinbutton');
    await user.type(aliceAmountInput, '40');

    // Type "20" into Bob's per-participant amount input
    const bobRow = bobCheckbox.closest('label');
    expect(bobRow).not.toBeNull();
    const bobAmountInput = within(bobRow!).getByRole('spinbutton');
    await user.type(bobAmountInput, '20');

    // Submit
    const submitButton = screen.getByRole('button', { name: 'Add Expense' });
    await user.click(submitButton);

    // Assert navigation back to Group Detail
    expect(window.location.pathname).toBe(`/groups/${testGroupId}`);
  });
});
