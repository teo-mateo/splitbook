import { http, HttpResponse } from 'msw';
import { screen, waitFor } from '@testing-library/react';
import { render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { useEffect } from 'react';
import { server } from '../../../test/setup';
import { CreateGroup } from './CreateGroup';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';

/**
 * Spy component that records the current router location pathname on a
 * mutable object so the test can assert navigation without touching shared infra.
 */
function LocationSpy({ locationRef }: { locationRef: { current: string } }) {
  const location = useLocation();
  useEffect(() => {
    locationRef.current = location.pathname;
  }, [location.pathname, locationRef]);
  return null;
}

describe('CreateGroup', () => {
  beforeEach(() => {
    localStorage.setItem('splitbook_token', 'fake-jwt-token');
  });

  afterEach(() => {
    localStorage.removeItem('splitbook_token');
  });

  test('after successful POST /groups, navigates to /groups/<id>', async () => {
    const user = userEvent.setup();
    const testGroupId = '550e8400-e29b-41d4-a716-446655440000';
    const onCloseMock = vi.fn();
    const locationRef = { current: '/' };

    server.use(
      http.post('http://localhost:5000/groups', () => {
        return HttpResponse.json(
          {
            id: testGroupId,
            name: 'Test Group',
            currency: 'EUR',
            createdAt: '2024-01-01T00:00:00Z',
          },
          { status: 201 },
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
        <MemoryRouter initialEntries={['/']}>
          <LocationSpy locationRef={locationRef} />
          <CreateGroup onClose={onCloseMock} />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    // Fill in the group name
    const nameInput = screen.getByRole('textbox', { name: /name/i });
    await user.type(nameInput, 'Test Group');

    // Submit the form
    const submitButton = screen.getByRole('button', { name: 'Create' });
    await user.click(submitButton);

    // Assert navigation to /groups/<id>
    await waitFor(() => {
      expect(locationRef.current).toBe(`/groups/${testGroupId}`);
    });
  });

  test('on POST /groups 400, shows error message and does not close the form', async () => {
    const user = userEvent.setup();
    const onCloseMock = vi.fn();

    server.use(
      http.post('http://localhost:5000/groups', () => {
        return HttpResponse.json(
          {
            type: 'about:blank',
            title: 'Bad Request',
            status: 400,
            detail: 'Group name already exists',
          },
          { status: 400 },
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
        <MemoryRouter initialEntries={['/']}>
          <CreateGroup onClose={onCloseMock} />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    // Fill in the group name
    const nameInput = screen.getByRole('textbox', { name: /name/i });
    await user.type(nameInput, 'Duplicate Group');

    // Submit the form
    const submitButton = screen.getByRole('button', { name: 'Create' });
    await user.click(submitButton);

    // Assert error message is displayed in the form
    await waitFor(() => {
      expect(screen.getByText(/Group name already exists/i)).toBeInTheDocument();
    });

    // Assert the form did NOT close
    expect(onCloseMock).not.toHaveBeenCalled();
  });
});
