import '@testing-library/jest-dom/vitest';
import { setupServer } from 'msw/node';
import { http, HttpResponse } from 'msw';

// Self-arming MSW server — runs at module load, not behind an exported function.
//
// The catch-all returns HTTP 500 (NOT 200) for any request a test did not
// explicitly mock. A 200 with a junk body silently feeds bad data into the
// app's Zod parsing and produces a confusing downstream "component is in its
// error state" symptom far from the real cause (the classic example: a
// router-param test missing its <Route> so the component requests
// `/groups/undefined`, which a 200 catch-all would mask — see L-FE11). A loud
// 500 naming the unrouted method+URL makes the misroute the first thing the
// failure output shows.
export const server = setupServer(
  http.all('*', ({ request }) => {
    const msg = `[test/setup] Unmocked request: ${request.method} ${request.url} — ` +
      `the test did not register a handler for this URL. If the URL contains ` +
      `"undefined"/"null", a route param did not resolve (render through ` +
      `<Routes><Route path="...">, not bare MemoryRouter — see L-FE11).`;
    console.error(msg);
    return HttpResponse.json({ error: msg }, { status: 500 });
  }),
);

server.listen({ onUnhandledRequest: 'bypass' });

afterAll(() => server.close());
afterEach(() => server.resetHandlers());
