import { z } from 'zod';

export const LoginRequestSchema = z.object({
  email: z.string(),
  password: z.string(),
});

export const LoginResponseSchema = z.object({
  accessToken: z.string(),
  expiresAt: z.string().datetime(),
});

export const RegisterRequestSchema = z.object({
  email: z.string(),
  displayName: z.string(),
  password: z.string(),
});

export const RegisterResponseSchema = z.object({
  id: z.string().uuid(),
  email: z.string(),
  displayName: z.string(),
});

export type LoginRequest = z.infer<typeof LoginRequestSchema>;
export type LoginResponse = z.infer<typeof LoginResponseSchema>;
export type RegisterRequest = z.infer<typeof RegisterRequestSchema>;
export type RegisterResponse = z.infer<typeof RegisterResponseSchema>;
