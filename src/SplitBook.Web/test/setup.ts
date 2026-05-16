import '@testing-library/jest-dom/vitest';
import { setupServer } from 'msw/node';
import { http, HttpResponse } from 'msw';

// Self-arming MSW server — runs at module load, not behind an exported function.
// Default handlers catch all requests to prevent unhandled request errors.
export const server = setupServer(
  http.all('*', ({ request }) => {
    // Pass through to actual handlers defined in tests
    return HttpResponse.json({ error: 'No handler for ' + request.method + ' ' + request.url });
  }),
);

server.listen({ onUnhandledRequest: 'bypass' });

afterAll(() => server.close());
afterEach(() => server.resetHandlers());
