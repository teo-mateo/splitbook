import { http, HttpResponse } from 'msw';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, useLocation } from 'react-router-dom';
import userEvent from '@testing-library/user-event';
import { server } from '../../../test/setup';
import { renderWithProviders } from '../../../test/render';
import { Register } from './Register';

describe('Register', () => {
  test('renders email, display name, password, password confirmation inputs and submit button', () => {
    renderWithProviders(<Register />);

    expect(screen.getByRole('textbox', { name: /email/i })).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /display name/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/^Password$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/confirm/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /register/i })).toBeInTheDocument();
  });

  test('empty required fields show validation errors and do not call the API', async () => {
    const user = userEvent.setup();
    let apiCalled = false;

    server.use(
      http.post('http://localhost:5000/auth/register', () => {
        apiCalled = true;
        return HttpResponse.json({}, { status: 201 });
      }),
    );

    renderWithProviders(<Register />);

    await user.click(screen.getByRole('button', { name: /register/i }));

    expect(screen.getByText('Email is required')).toBeInTheDocument();
    expect(screen.getByText('Display name is required')).toBeInTheDocument();
    expect(screen.getByText(/password.*8/i)).toBeInTheDocument();
    expect(screen.getByText(/confirm your password/i)).toBeInTheDocument();

    expect(apiCalled).toBe(false);
  });

  test('shows validation errors when submitting with short password and mismatched confirmation', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Register />);

    await user.type(screen.getByRole('textbox', { name: /email/i }), 'alice@example.com');
    await user.type(screen.getByRole('textbox', { name: /display name/i }), 'Alice');
    await user.type(screen.getByLabelText(/^Password$/i), '123');
    await user.type(screen.getByLabelText(/confirm/i), '456');
    await user.click(screen.getByRole('button', { name: /register/i }));

    expect(screen.getByText(/password.*8/i)).toBeInTheDocument();
    expect(screen.getByText(/passwords?.*match/i)).toBeInTheDocument();
  });

  test('calls POST /auth/register with correct payload (no passwordConfirm)', async () => {
    const user = userEvent.setup();
    let capturedBody: unknown;

    server.use(
      http.post('http://localhost:5000/auth/register', async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(
          { id: '550e8400-e29b-41d4-a716-446655440000', email: 'alice@example.com', displayName: 'Alice' },
          { status: 201 },
        );
      }),
      http.post('http://localhost:5000/auth/login', () => {
        return HttpResponse.json({ accessToken: 'test-jwt-token', expiresAt: '2025-01-02T00:00:00Z' });
      }),
    );

    renderWithProviders(<Register />);

    await user.type(screen.getByRole('textbox', { name: /email/i }), 'alice@example.com');
    await user.type(screen.getByRole('textbox', { name: /display name/i }), 'Alice');
    await user.type(screen.getByLabelText(/^Password$/i), 'securepassword123');
    await user.type(screen.getByLabelText(/confirm/i), 'securepassword123');
    await user.click(screen.getByRole('button', { name: /register/i }));

    const body = capturedBody as Record<string, unknown> | undefined;
    expect(body).not.toBeUndefined();
    expect(body!.email).toBe('alice@example.com');
    expect(body!.displayName).toBe('Alice');
    expect(body!.password).toBe('securepassword123');
    expect(body!).not.toHaveProperty('passwordConfirm');
  });

  test('on successful register, auto-logs in and redirects to /groups', async () => {
    const user = userEvent.setup();
    let loginBody: unknown;
    let currentPath = '/register';

    function LocationTracker() {
      const location = useLocation();
      currentPath = location.pathname;
      return null;
    }

    server.use(
      http.post('http://localhost:5000/auth/register', () => {
        return HttpResponse.json(
          { id: '550e8400-e29b-41d4-a716-446655440000', email: 'alice@example.com', displayName: 'Alice' },
          { status: 201 },
        );
      }),
      http.post('http://localhost:5000/auth/login', async ({ request }) => {
        loginBody = await request.json();
        return HttpResponse.json({ accessToken: 'test-jwt-token', expiresAt: '2025-01-02T00:00:00Z' });
      }),
    );

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });

    render(
      <MemoryRouter initialEntries={['/register']}>
        <QueryClientProvider client={queryClient}>
          <Register />
          <LocationTracker />
        </QueryClientProvider>
      </MemoryRouter>,
    );

    await user.type(screen.getByRole('textbox', { name: /email/i }), 'alice@example.com');
    await user.type(screen.getByRole('textbox', { name: /display name/i }), 'Alice');
    await user.type(screen.getByLabelText(/^Password$/i), 'securepassword123');
    await user.type(screen.getByLabelText(/confirm/i), 'securepassword123');
    await user.click(screen.getByRole('button', { name: /register/i }));

    const loginPayload = loginBody as Record<string, unknown> | undefined;
    expect(loginPayload).not.toBeUndefined();
    expect(loginPayload!.email).toBe('alice@example.com');
    expect(loginPayload!.password).toBe('securepassword123');

    expect(localStorage.getItem('splitbook_token')).toBe('test-jwt-token');

    expect(currentPath).toBe('/groups');
  });

  test('provides a link to navigate to the Login page', async () => {
    const user = userEvent.setup();
    let currentPath = '/register';
    function LocationTracker() {
      const location = useLocation();
      currentPath = location.pathname;
      return null;
    }

    render(
      <MemoryRouter initialEntries={['/register']}>
        <Register />
        <LocationTracker />
      </MemoryRouter>,
    );

    const loginLink = screen.getByRole('link', { name: /login/i });
    expect(loginLink).toHaveAttribute('href', '/login');

    await user.click(loginLink);
    await waitFor(() => {
      expect(currentPath).toBe('/login');
    });
  });
});
