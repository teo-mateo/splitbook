import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate } from 'react-router-dom';
import { ApiError, apiRequest } from '../../api/client';
import { LoginRequestSchema, LoginResponseSchema, RegisterRequestSchema, RegisterResponseSchema } from '../../api/types';

const registerSchema = z
  .object({
    email: z.string().min(1, 'Email is required'),
    displayName: z.string().min(1, 'Display name is required'),
    password: z.string().min(8, 'Password must be at least 8 characters'),
    passwordConfirm: z.string().min(1, 'Please confirm your password'),
  })
  .refine((data) => data.password === data.passwordConfirm, {
    message: 'Passwords do not match',
    path: ['passwordConfirm'],
  });

type RegisterForm = z.infer<typeof registerSchema>;

export function Register() {
  const navigate = useNavigate();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RegisterForm>({
    resolver: zodResolver(registerSchema),
  });

  const onSubmit = handleSubmit(async (data) => {
    setServerError(null);
    try {
      const registerRequest: z.infer<typeof RegisterRequestSchema> = {
        email: data.email,
        displayName: data.displayName,
        password: data.password,
      };
      await apiRequest<z.infer<typeof RegisterResponseSchema>>(
        RegisterResponseSchema,
        '/auth/register',
        {
          method: 'POST',
          body: JSON.stringify(registerRequest),
        },
      );

      // Auto-login after successful registration
      const loginRequest: z.infer<typeof LoginRequestSchema> = {
        email: data.email,
        password: data.password,
      };
      const loginResponse = await apiRequest<z.infer<typeof LoginResponseSchema>>(
        LoginResponseSchema,
        '/auth/login',
        {
          method: 'POST',
          body: JSON.stringify(loginRequest),
        },
      );
      localStorage.setItem('splitbook_token', loginResponse.accessToken);
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
            : 'Registration failed. Please try again.',
        );
      } else {
        setServerError('Something went wrong. Please try again.');
      }
    }
  });

  return (
    <form onSubmit={onSubmit} className="mx-auto mt-12 max-w-sm space-y-6">
      <h1 className="text-2xl font-bold">Register</h1>
      {serverError && (
        <p className="text-sm text-red-600">{serverError}</p>
      )}
      <div>
        <label htmlFor="register-email" className="mb-1 block text-sm font-medium">
          Email
        </label>
        <input
          id="register-email"
          type="email"
          className="w-full rounded border px-3 py-2"
          {...register('email')}
        />
        {errors.email && (
          <p className="mt-1 text-sm text-red-600">{errors.email.message}</p>
        )}
      </div>
      <div>
        <label htmlFor="register-displayName" className="mb-1 block text-sm font-medium">
          Display Name
        </label>
        <input
          id="register-displayName"
          type="text"
          className="w-full rounded border px-3 py-2"
          {...register('displayName')}
        />
        {errors.displayName && (
          <p className="mt-1 text-sm text-red-600">{errors.displayName.message}</p>
        )}
      </div>
      <div>
        <label htmlFor="register-password" className="mb-1 block text-sm font-medium">
          Password
        </label>
        <input
          id="register-password"
          type="password"
          className="w-full rounded border px-3 py-2"
          {...register('password')}
        />
        {errors.password && (
          <p className="mt-1 text-sm text-red-600">{errors.password.message}</p>
        )}
      </div>
      <div>
        <label htmlFor="register-passwordConfirm" className="mb-1 block text-sm font-medium">
          Confirm Password
        </label>
        <input
          id="register-passwordConfirm"
          type="password"
          className="w-full rounded border px-3 py-2"
          {...register('passwordConfirm')}
        />
        {errors.passwordConfirm && (
          <p className="mt-1 text-sm text-red-600">
            {errors.passwordConfirm.message}
          </p>
        )}
      </div>
      <button
        type="submit"
        className="w-full rounded bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700"
      >
        Register
      </button>
      <p className="text-center text-sm">
        Already have an account?{' '}
        <Link to="/login" className="font-medium text-blue-600 hover:text-blue-700">
          Login
        </Link>
      </p>
    </form>
  );
}
