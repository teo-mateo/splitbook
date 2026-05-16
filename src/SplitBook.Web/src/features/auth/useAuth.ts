export function useAuth() {
  return {
    user: null,
    isAuthenticated: false,
    login: async () => {},
    register: async () => {},
    logout: () => {},
  };
}
