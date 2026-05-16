import { GroupDtoSchema, LoginResponseSchema } from './types';

describe('LoginResponseSchema', () => {
  it('accepts .NET DateTimeOffset format with 7-digit fractional seconds and +00:00 offset', () => {
    const result = LoginResponseSchema.parse({
      accessToken: 'tok',
      expiresAt: '2026-05-17T16:28:52.6134551+00:00',
    });
    expect(result.accessToken).toBe('tok');
    expect(result.expiresAt).toBe('2026-05-17T16:28:52.6134551+00:00');
  });
});

describe('GroupDtoSchema', () => {
  it('accepts .NET DateTimeOffset format with 7-digit fractional seconds and +00:00 offset for createdAt', () => {
    const result = GroupDtoSchema.parse({
      id: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
      name: 'Test',
      currency: 'EUR',
      createdAt: '2026-05-17T16:28:52.6134551+00:00',
    });
    expect(result.name).toBe('Test');
    expect(result.createdAt).toBe('2026-05-17T16:28:52.6134551+00:00');
  });
});

describe('ISO 8601 datetime regression', () => {
  it('LoginResponseSchema and GroupDtoSchema still accept standard ISO 8601 datetimes', () => {
    const loginResult = LoginResponseSchema.parse({
      accessToken: 'tok',
      expiresAt: '2026-05-17T16:28:52Z',
    });
    expect(loginResult.expiresAt).toBe('2026-05-17T16:28:52Z');

    const loginResultMs = LoginResponseSchema.parse({
      accessToken: 'tok',
      expiresAt: '2026-05-17T16:28:52.123Z',
    });
    expect(loginResultMs.expiresAt).toBe('2026-05-17T16:28:52.123Z');

    const groupResult = GroupDtoSchema.parse({
      id: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
      name: 'Test',
      currency: 'EUR',
      createdAt: '2026-05-17T16:28:52Z',
    });
    expect(groupResult.createdAt).toBe('2026-05-17T16:28:52Z');
  });
});
