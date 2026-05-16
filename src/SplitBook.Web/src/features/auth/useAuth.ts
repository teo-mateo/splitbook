const TOKEN_KEY = 'splitbook_token';

export function useAuth() {
  const token = localStorage.getItem(TOKEN_KEY);
  const isAuthenticated = !!token;

  const logout = () => {
    localStorage.removeItem(TOKEN_KEY);
  };

  return { isAuthenticated, logout };
}
