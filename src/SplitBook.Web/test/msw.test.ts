import { http, HttpResponse } from 'msw';
import { server } from './setup';

describe('MSW setup', () => {
  test('intercepts fetch calls and returns mocked responses', async () => {
    server.use(
      http.get('/api/test', () => {
        return HttpResponse.json({ ok: true });
      }),
    );

    const response = await fetch('/api/test');
    const data = await response.json();

    expect(data).toEqual({ ok: true });
  });
});
