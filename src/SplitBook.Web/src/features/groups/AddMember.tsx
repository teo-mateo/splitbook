import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { ApiError, apiRequest } from '../../api/client';

const addMemberSchema = z.object({
  email: z.string().min(1, 'Email is required').email('Invalid email address'),
});

type AddMemberForm = z.infer<typeof addMemberSchema>;

export function AddMember({ onClose }: { onClose: () => void }) {
  const { id } = useParams();
  const queryClient = useQueryClient();
  const [serverError, setServerError] = useState<string | null>(null);

  const addMutation = useMutation({
    mutationFn: (email: string) =>
      apiRequest(z.void(), `/groups/${id}/members`, {
        method: 'POST',
        body: JSON.stringify({ email }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['group', id] });
      queryClient.invalidateQueries({ queryKey: ['balances', id] });
      onClose();
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) {
        setServerError('User already in this group');
      } else if (error instanceof ApiError) {
        setServerError('Something went wrong. Please try again.');
      } else {
        setServerError('Something went wrong. Please try again.');
      }
    },
  });

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<AddMemberForm>({
    resolver: zodResolver(addMemberSchema),
    defaultValues: {
      email: '',
    },
  });

  const onSubmit = handleSubmit((data) => {
    setServerError(null);
    addMutation.mutate(data.email);
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="w-full max-w-sm rounded-lg bg-white p-6 shadow-xl">
        <h2 className="mb-4 text-xl font-bold">Add Member</h2>
        {serverError && (
          <p className="mb-4 text-sm text-red-600">{serverError}</p>
        )}
        <form onSubmit={onSubmit} className="space-y-4">
          <div>
            <label htmlFor="add-member-email" className="mb-1 block text-sm font-medium">
              Email
            </label>
            <input
              id="add-member-email"
              className="w-full rounded border px-3 py-2"
              {...register('email')}
            />
            {errors.email && (
              <p className="mt-1 text-sm text-red-600">{errors.email.message}</p>
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
              disabled={addMutation.isPending}
              className="rounded bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {addMutation.isPending ? 'Adding...' : 'Add'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
