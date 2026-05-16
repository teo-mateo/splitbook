import { render, screen } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthGuard } from './AuthGuard';

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
}

describe('AuthGuard', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  test('redirects unauthenticated users to /login', () => {
    let currentPath = '/protected';
    function LocationTracker() {
      const location = useLocation();
      currentPath = location.pathname;
      return null;
    }

    const queryClient = createQueryClient();

    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={['/protected']}>
          <AuthGuard>
            <div>Protected</div>
          </AuthGuard>
          <LocationTracker />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    expect(currentPath).toBe('/login');
  });

  test('allows authenticated users through without redirecting', () => {
    localStorage.setItem('splitbook_token', 'fake-jwt-token');

    let currentPath = '/protected';
    function LocationTracker() {
      const location = useLocation();
      currentPath = location.pathname;
      return null;
    }

    const queryClient = createQueryClient();

    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={['/protected']}>
          <AuthGuard>
            <div>Protected</div>
          </AuthGuard>
          <LocationTracker />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    expect(screen.getByText('Protected')).toBeInTheDocument();
    expect(currentPath).toBe('/protected');
  });
});
