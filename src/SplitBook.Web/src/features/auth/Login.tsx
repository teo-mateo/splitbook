import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { ApiError, apiRequest } from '../../api/client';
import { LoginRequestSchema, LoginResponseSchema } from '../../api/types';

const loginSchema = z.object({
  email: z.string().min(1, 'Email is required'),
  password: z.string().min(1, 'Password is required'),
});

type LoginForm = z.infer<typeof loginSchema>;

export function Login() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const isExpired = searchParams.get('expired') === 'true';
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginForm>({
    resolver: zodResolver(loginSchema),
  });

  const onSubmit = handleSubmit(async (data) => {
    setServerError(null);
    try {
      const request: z.infer<typeof LoginRequestSchema> = {
        email: data.email,
        password: data.password,
      };
      const response = await apiRequest<z.infer<typeof LoginResponseSchema>>(
        LoginResponseSchema,
        '/auth/login',
        {
          method: 'POST',
          body: JSON.stringify(request),
        },
      );
      localStorage.setItem('splitbook_token', response.accessToken);
      navigate('/groups');
    } catch (error) {
      if (error instanceof ApiError) {
        const detail =
          typeof error.problem === 'object' &&
          error.problem !== null &&
          'detail' in error.problem
            ? (error.problem as Record<string, unknown>).detail
            : null;
        setServerError(
          typeof detail === 'string'
            ? detail
            : 'Invalid email or password',
        );
      } else {
        setServerError('Something went wrong. Please try again.');
      }
    }
  });

  return (
    <form onSubmit={onSubmit} className="mx-auto mt-12 max-w-sm space-y-6">
      <h1 className="text-2xl font-bold">Login</h1>
      {isExpired && (
        <p className="text-sm text-amber-600">Session expired. Please log in again.</p>
      )}
      {serverError && (
        <p className="text-sm text-red-600">{serverError}</p>
      )}
      <div>
        <label htmlFor="login-email" className="mb-1 block text-sm font-medium">
          Email
        </label>
        <input
          id="login-email"
          type="email"
          className="w-full rounded border px-3 py-2"
          {...register('email')}
        />
        {errors.email && (
          <p className="mt-1 text-sm text-red-600">{errors.email.message}</p>
        )}
      </div>
      <div>
        <label htmlFor="login-password" className="mb-1 block text-sm font-medium">
          Password
        </label>
        <input
          id="login-password"
          type="password"
          className="w-full rounded border px-3 py-2"
          {...register('password')}
        />
        {errors.password && (
          <p className="mt-1 text-sm text-red-600">{errors.password.message}</p>
        )}
      </div>
      <button
        type="submit"
        className="w-full rounded bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700"
      >
        Login
      </button>
      <p className="text-center text-sm">
        Don't have an account?{' '}
        <Link to="/register" className="font-medium text-blue-600 hover:text-blue-700">
          Register
        </Link>
      </p>
    </form>
  );
}
