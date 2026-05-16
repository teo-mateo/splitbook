import { http, HttpResponse } from 'msw';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { server } from '../../../test/setup';
import { Login } from './Login';
import { renderWithProviders } from '../../../test/render';

describe('Login', () => {
  beforeEach(() => {
    Object.defineProperty(window, 'location', {
      value: { ...window.location, pathname: '/login' },
      writable: true,
      configurable: true,
    });
  });

  test('renders email input, password input, and submit button', () => {
    renderWithProviders(<Login />);

    expect(screen.getByRole('textbox', { name: /email/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /login/i })).toBeInTheDocument();
  });

  test('shows validation errors when submitting with empty email and password', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Login />);

    await user.click(screen.getByRole('button', { name: /login/i }));

    expect(screen.getByText(/email.*required/i)).toBeInTheDocument();
    expect(screen.getByText(/password.*required/i)).toBeInTheDocument();
  });

  test('calls POST /auth/login with correct payload on submit', async () => {
    const user = userEvent.setup();
    let receivedBody: unknown;

    server.use(
      http.post('http://localhost:5000/auth/login', async ({ request }) => {
        receivedBody = await request.json();
        return HttpResponse.json({ accessToken: 'fake-jwt-token', expiresAt: '2025-01-02T00:00:00Z' });
      }),
    );

    renderWithProviders(<Login />);

    await user.type(screen.getByRole('textbox', { name: /email/i }), 'test@example.com');
    await user.type(screen.getByLabelText(/password/i), 'password123');
    await user.click(screen.getByRole('button', { name: /login/i }));

    expect(receivedBody).toEqual({
      email: 'test@example.com',
      password: 'password123',
    });
  });

  test('login success stores JWT in localStorage and redirects to /groups', async () => {
    const user = userEvent.setup();
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });

    let currentPath = '/login';
    function LocationTracker() {
      const location = useLocation();
      currentPath = location.pathname;
      return null;
    }

    server.use(
      http.post('http://localhost:5000/auth/login', () => {
        return HttpResponse.json({ accessToken: 'test-jwt-token', expiresAt: '2025-01-02T00:00:00Z' });
      }),
    );

    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={['/login']}>
          <Login />
          <LocationTracker />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await user.type(screen.getByRole('textbox', { name: /email/i }), 'alice@example.com');
    await user.type(screen.getByLabelText(/password/i), 'password123');
    await user.click(screen.getByRole('button', { name: /login/i }));

    await waitFor(() => {
      expect(localStorage.getItem('splitbook_token')).toBe('test-jwt-token');
    });

    expect(currentPath).toBe('/groups');
  });

  test('provides a link to navigate to the Register page', async () => {
    const user = userEvent.setup();
    let currentPath = '/login';
    function LocationTracker() {
      const location = useLocation();
      currentPath = location.pathname;
      return null;
    }

    render(
      <MemoryRouter initialEntries={['/login']}>
        <Login />
        <LocationTracker />
      </MemoryRouter>,
    );

    const registerLink = screen.getByRole('link', { name: /register/i });
    expect(registerLink).toHaveAttribute('href', '/register');

    await user.click(registerLink);
    await waitFor(() => {
      expect(currentPath).toBe('/register');
    });
  });

  test('shows session expired message when ?expired=true query param is present', () => {
    render(
      <MemoryRouter initialEntries={['/login?expired=true']}>
        <Login />
      </MemoryRouter>,
    );

    expect(screen.getByText(/session expired/i)).toBeInTheDocument();
  });

  test('login failure shows inline error and does not redirect', async () => {
    const user = userEvent.setup();
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });

    let currentPath = '/login';
    function LocationTracker() {
      const location = useLocation();
      currentPath = location.pathname;
      return null;
    }

    server.use(
      http.post('http://localhost:5000/auth/login', () => {
        return HttpResponse.json(
          { type: 'about:blank', title: 'Unauthorized', status: 401, detail: 'Invalid email or password' },
          { status: 401 },
        );
      }),
    );

    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={['/login']}>
          <Login />
          <LocationTracker />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await user.type(screen.getByRole('textbox', { name: /email/i }), 'alice@example.com');
    await user.type(screen.getByLabelText(/password/i), 'wrongpassword');
    await user.click(screen.getByRole('button', { name: /login/i }));

    await waitFor(() => {
      expect(screen.getByText(/invalid email or password/i)).toBeInTheDocument();
    });

    expect(currentPath).toBe('/login');
  });
});
