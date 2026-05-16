import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { ApiError, apiRequest } from '../../api/client';
import { CreateGroupRequestSchema, GroupDtoSchema } from '../../api/types';

const createGroupSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  currency: z.string().length(3, 'Currency must be 3 characters'),
});

type CreateGroupForm = z.infer<typeof createGroupSchema>;

export function CreateGroup({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [serverError, setServerError] = useState<string | null>(null);

  const createMutation = useMutation({
    mutationFn: (data: z.infer<typeof CreateGroupRequestSchema>) =>
      apiRequest(GroupDtoSchema, '/groups', {
        method: 'POST',
        body: JSON.stringify(data),
      }),
    onSuccess: (group) => {
      queryClient.invalidateQueries({ queryKey: ['groups'] });
      onClose();
      navigate(`/groups/${group.id}`);
    },
    onError: (error) => {
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
            : 'Creation failed. Please try again.',
        );
      } else {
        setServerError('Something went wrong. Please try again.');
      }
    },
  });

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CreateGroupForm>({
    resolver: zodResolver(createGroupSchema),
    defaultValues: {
      name: '',
      currency: 'EUR',
    },
  });

  const onSubmit = handleSubmit(async (data) => {
    setServerError(null);
    createMutation.mutate({ name: data.name, currency: data.currency });
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="w-full max-w-sm rounded-lg bg-white p-6 shadow-xl">
        <h2 className="mb-4 text-xl font-bold">Create Group</h2>
        {serverError && (
          <p className="mb-4 text-sm text-red-600">{serverError}</p>
        )}
        <form onSubmit={onSubmit} className="space-y-4">
          <div>
            <label htmlFor="create-group-name" className="mb-1 block text-sm font-medium">
              Name
            </label>
            <input
              id="create-group-name"
              className="w-full rounded border px-3 py-2"
              {...register('name')}
            />
            {errors.name && (
              <p className="mt-1 text-sm text-red-600">{errors.name.message}</p>
            )}
          </div>
          <div>
            <label htmlFor="create-group-currency" className="mb-1 block text-sm font-medium">
              Currency
            </label>
            <input
              id="create-group-currency"
              className="w-full rounded border px-3 py-2"
              {...register('currency')}
            />
            {errors.currency && (
              <p className="mt-1 text-sm text-red-600">{errors.currency.message}</p>
            )}
          </div>
          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded border px-4 py-2 text-gray-700 hover:bg-gray-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="rounded bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700"
            >
              Create
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
