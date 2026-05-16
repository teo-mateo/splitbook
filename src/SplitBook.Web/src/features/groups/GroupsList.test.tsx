import { http, HttpResponse } from 'msw';
import { screen, waitFor } from '@testing-library/react';
import { server } from '../../../test/setup';
import { GroupsList } from './GroupsList';
import { renderWithProviders } from '../../../test/render';
import userEvent from '@testing-library/user-event';

describe('GroupsList', () => {
  beforeEach(() => {
    localStorage.setItem('splitbook_token', 'fake-jwt-token');
  });

  afterEach(() => {
    localStorage.removeItem('splitbook_token');
  });

  test('shows empty state when GET /groups returns an empty array', async () => {
    server.use(
      http.get('http://localhost:5000/groups', () => {
        return HttpResponse.json([]);
      }),
    );

    renderWithProviders(<GroupsList />);

    await waitFor(() => {
      expect(screen.getByText(/no groups yet/i)).toBeInTheDocument();
    });
  });

  test('renders each group with name and currency from GET /groups', async () => {
    server.use(
      http.get('http://localhost:5000/groups', () => {
        return HttpResponse.json([
          {
            id: 'a1111111-1111-1111-1111-111111111111',
            name: 'Lisbon Trip',
            currency: 'EUR',
            createdAt: '2024-01-15T10:00:00Z',
          },
          {
            id: 'b2222222-2222-2222-2222-222222222222',
            name: 'NYC Wedding',
            currency: 'USD',
            createdAt: '2024-02-20T14:30:00Z',
          },
        ]);
      }),
    );

    renderWithProviders(<GroupsList />);

    await waitFor(() => {
      expect(screen.getByText('Lisbon Trip')).toBeInTheDocument();
      expect(screen.getByText('EUR')).toBeInTheDocument();
      expect(screen.getByText('NYC Wedding')).toBeInTheDocument();
      expect(screen.getByText('USD')).toBeInTheDocument();
    });
  });

  test('sends JWT Authorization header on GET /groups', async () => {
    const TEST_TOKEN = 'test-jwt-123';
    localStorage.setItem('splitbook_token', TEST_TOKEN);

    let capturedAuthHeader: string | null = null;

    server.use(
      http.get('http://localhost:5000/groups', ({ request }) => {
        capturedAuthHeader = request.headers.get('Authorization');
        return HttpResponse.json([]);
      }),
    );

    renderWithProviders(<GroupsList />);

    await waitFor(() => {
      expect(capturedAuthHeader).toBe(`Bearer ${TEST_TOKEN}`);
    });
  });

  test('shows a "Create group" button', async () => {
    server.use(
      http.get('http://localhost:5000/groups', () => {
        return HttpResponse.json([]);
      }),
    );

    renderWithProviders(<GroupsList />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /create group/i })).toBeInTheDocument();
    });
  });

  test('clicking "Create group" shows form with name and currency inputs (EUR default)', async () => {
    const user = userEvent.setup();

    server.use(
      http.get('http://localhost:5000/groups', () => {
        return HttpResponse.json([]);
      }),
    );

    renderWithProviders(<GroupsList />);

    // Wait for the list to load and the button to appear
    const createButton = await screen.findByRole('button', { name: /create group/i });
    await user.click(createButton);

    // Assert the form inputs appear
    expect(screen.getByRole('textbox', { name: /name/i })).toBeInTheDocument();
    expect(screen.getByDisplayValue('EUR')).toBeInTheDocument();
  });

  test('submitting with empty name shows "Name is required" error and does not call POST /groups', async () => {
    const user = userEvent.setup();

    let postCalled = false;

    server.use(
      http.get('http://localhost:5000/groups', () => {
        return HttpResponse.json([]);
      }),
      http.post('http://localhost:5000/groups', () => {
        postCalled = true;
        return HttpResponse.json({ error: 'should not be called' });
      }),
    );

    renderWithProviders(<GroupsList />);

    const createButton = await screen.findByRole('button', { name: /create group/i });
    await user.click(createButton);

    // Leave name empty, click Create submit button (exact match to avoid "Create group" button)
    const submitButton = screen.getByRole('button', { name: 'Create' });
    await user.click(submitButton);

    // Assert validation error appears
    await waitFor(() => {
      expect(screen.getByText('Name is required')).toBeInTheDocument();
    });

    // Assert no POST request was made
    expect(postCalled).toBe(false);
  });

  test('submitting with invalid currency length shows "Currency must be 3 characters" error and does not call POST /groups', async () => {
    const user = userEvent.setup();

    let postCalled = false;

    server.use(
      http.get('http://localhost:5000/groups', () => {
        return HttpResponse.json([]);
      }),
      http.post('http://localhost:5000/groups', () => {
        postCalled = true;
        return HttpResponse.json({ error: 'should not be called' });
      }),
    );

    renderWithProviders(<GroupsList />);

    const createButton = await screen.findByRole('button', { name: /create group/i });
    await user.click(createButton);

    // Fill in a valid name
    const nameInput = screen.getByRole('textbox', { name: /name/i });
    await user.type(nameInput, 'Test Group');

    // Clear the currency field (default EUR) and type a 2-character value
    const currencyInput = screen.getByRole('textbox', { name: /currency/i });
    await user.clear(currencyInput);
    await user.type(currencyInput, 'EU');

    // Click submit
    const submitButton = screen.getByRole('button', { name: 'Create' });
    await user.click(submitButton);

    // Assert validation error appears
    await waitFor(() => {
      expect(screen.getByText('Currency must be 3 characters')).toBeInTheDocument();
    });

    // Assert no POST request was made
    expect(postCalled).toBe(false);
  });

  test('valid submit calls POST /groups with name and currency', async () => {
    const user = userEvent.setup();

    let capturedBody: unknown = null;

    server.use(
      http.get('http://localhost:5000/groups', () => {
        return HttpResponse.json([]);
      }),
      http.post('http://localhost:5000/groups', async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(
          {
            id: 'c3333333-3333-3333-3333-333333333333',
            name: 'Lisbon Trip',
            currency: 'GBP',
            createdAt: '2024-03-01T12:00:00Z',
          },
          { status: 201 },
        );
      }),
    );

    renderWithProviders(<GroupsList />);

    const createButton = await screen.findByRole('button', { name: /create group/i });
    await user.click(createButton);

    const nameInput = screen.getByRole('textbox', { name: /name/i });
    await user.type(nameInput, 'Lisbon Trip');

    const currencyInput = screen.getByRole('textbox', { name: /currency/i });
    await user.clear(currencyInput);
    await user.type(currencyInput, 'GBP');

    const submitButton = screen.getByRole('button', { name: 'Create' });
    await user.click(submitButton);

    await waitFor(() => {
      expect(capturedBody).toEqual({ name: 'Lisbon Trip', currency: 'GBP' });
    });
  });

  test('POST /groups request includes JWT Authorization header', async () => {
    const user = userEvent.setup();
    const TEST_TOKEN = 'test-jwt-post-123';
    localStorage.setItem('splitbook_token', TEST_TOKEN);

    let capturedAuthHeader: string | null = null;

    server.use(
      http.get('http://localhost:5000/groups', () => {
        return HttpResponse.json([]);
      }),
      http.post('http://localhost:5000/groups', ({ request }) => {
        capturedAuthHeader = request.headers.get('Authorization');
        return HttpResponse.json(
          {
            id: 'd4444444-4444-4444-4444-444444444444',
            name: 'Test Group',
            currency: 'USD',
            createdAt: '2024-04-01T09:00:00Z',
          },
          { status: 201 },
        );
      }),
    );

    renderWithProviders(<GroupsList />);

    const createButton = await screen.findByRole('button', { name: /create group/i });
    await user.click(createButton);

    const nameInput = screen.getByRole('textbox', { name: /name/i });
    await user.type(nameInput, 'Test Group');

    const currencyInput = screen.getByRole('textbox', { name: /currency/i });
    await user.clear(currencyInput);
    await user.type(currencyInput, 'USD');

    const submitButton = screen.getByRole('button', { name: 'Create' });
    await user.click(submitButton);

    await waitFor(() => {
      expect(capturedAuthHeader).toBe(`Bearer ${TEST_TOKEN}`);
    });
  });

  test('shows loading indicator while GET /groups is in flight', async () => {
    server.use(
      http.get('http://localhost:5000/groups', () => {
        return new Promise((resolve) => {
          setTimeout(() => {
            resolve(HttpResponse.json([]));
          }, 500);
        });
      }),
    );

    renderWithProviders(<GroupsList />);

    // Loading text should appear immediately (before the delayed response resolves)
    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  test('shows error message and retry button when GET /groups fails', async () => {
    const user = userEvent.setup();

    let requestCount = 0;

    server.use(
      http.get('http://localhost:5000/groups', () => {
        requestCount++;
        return HttpResponse.json({ error: 'Internal server error' }, { status: 500 });
      }),
    );

    renderWithProviders(<GroupsList />);

    // Assert error message appears
    await waitFor(() => {
      expect(screen.getByText(/something went wrong/i)).toBeInTheDocument();
    });

    // Assert retry button exists and clicking it triggers a second request
    const retryButton = screen.getByRole('button', { name: /retry/i });
    await user.click(retryButton);

    await waitFor(() => {
      expect(requestCount).toBe(2);
    });
  });
});
