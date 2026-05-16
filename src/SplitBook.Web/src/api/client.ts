const BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';

export class ApiError extends Error {
  constructor(
    public status: number,
    public problem?: unknown,
    message?: string,
  ) {
    super(message || `API error ${status}`);
    this.name = 'ApiError';
  }
}

async function parseOrThrow<T>(schema: { parse: (data: unknown) => T }, data: unknown): Promise<T> {
  try {
    return schema.parse(data);
  } catch {
    throw new ApiError(500, undefined, 'Response failed schema validation');
  }
}

export async function apiRequest<T>(
  schema: { parse: (data: unknown) => T },
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const token = localStorage.getItem('splitbook_token');
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(init.headers as Record<string, string> || {}),
  };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const response = await fetch(`${BASE_URL}${path}`, { ...init, headers });

  if (!response.ok) {
    const body = await response.json().catch(() => undefined);
    if (response.status === 401) {
      localStorage.removeItem('splitbook_token');
      if (window.location.pathname !== '/login') {
        window.location.href = '/login?expired=true';
      }
    }
    throw new ApiError(response.status, body);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const json = await response.json();
  return parseOrThrow(schema, json);
}
