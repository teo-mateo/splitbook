import { renderHook } from '@testing-library/react';
import { useAuth } from './useAuth';

describe('useAuth', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  test('isAuthenticated is false when no token is in localStorage', () => {
    const { result } = renderHook(() => useAuth());

    expect(result.current.isAuthenticated).toBe(false);
  });

  test('isAuthenticated is true when splitbook_token exists in localStorage', () => {
    localStorage.setItem('splitbook_token', 'fake-jwt-token');

    const { result } = renderHook(() => useAuth());

    expect(result.current.isAuthenticated).toBe(true);
  });

  test('logout clears splitbook_token from localStorage', () => {
    localStorage.setItem('splitbook_token', 'fake-jwt-token');

    const { result } = renderHook(() => useAuth());
    result.current.logout();

    expect(localStorage.getItem('splitbook_token')).toBeNull();
  });
});
