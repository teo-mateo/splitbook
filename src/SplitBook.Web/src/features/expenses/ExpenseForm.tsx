import { useState } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError, apiRequest } from '../../api/client';
import { AddExpenseRequestSchema, ExpenseDtoSchema, GroupDetailDtoSchema } from '../../api/types';
import { toMinorUnits, formatCurrency } from '../../lib/money';

const today = new Date().toISOString().split('T')[0];

const expenseFormSchema = z.object({
  description: z.string().min(1, 'Description is required'),
  amount: z.string().min(1, 'Amount is required').refine(
    (v) => !isNaN(Number(v)) && Number(v) > 0,
    { message: 'Enter a valid amount' },
  ),
  payerUserId: z.string().uuid('Select a payer'),
  occurredOn: z.string().min(1, 'Date is required'),
  participantIds: z.array(z.string()).min(1, 'Select at least one participant'),
});

type ExpenseFormData = z.infer<typeof expenseFormSchema>;

export function ExpenseForm() {
  const { id } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [serverError, setServerError] = useState<string | null>(null);
  const [splitMethod, setSplitMethod] = useState<'Equal' | 'Exact'>('Equal');
  const [participantAmounts, setParticipantAmounts] = useState<Record<string, string>>({});
  const [splitError, setSplitError] = useState<string | null>(null);

  const { data: group, isLoading, error, refetch } = useQuery({
    queryKey: ['group', id],
    queryFn: () => apiRequest(GroupDetailDtoSchema, `/groups/${id}`),
  });

  const createMutation = useMutation({
    mutationFn: (data: z.infer<typeof AddExpenseRequestSchema>) =>
      apiRequest(ExpenseDtoSchema, `/groups/${id}/expenses`, {
        method: 'POST',
        body: JSON.stringify(data),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['expenses', id] });
      queryClient.invalidateQueries({ queryKey: ['balances', id] });
      navigate(`/groups/${id}`);
    },
    onError: (err) => {
      if (err instanceof ApiError) {
        if (err.status >= 500) {
          setServerError('Something went wrong. Please try again.');
        } else {
          const detail =
            typeof err.problem === 'object' &&
            err.problem !== null &&
            'detail' in err.problem
              ? (err.problem as Record<string, unknown>).detail
              : null;
          setServerError(
            typeof detail === 'string'
              ? detail
              : 'Failed to add expense. Please try again.',
          );
        }
      } else {
        setServerError('Cannot reach the server. Check your connection.');
      }
    },
  });

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors },
  } = useForm<ExpenseFormData>({
    resolver: zodResolver(expenseFormSchema),
    defaultValues: {
      description: '',
      amount: '',
      payerUserId: '',
      occurredOn: today,
      participantIds: [],
    },
  });

  const selectedPayer = watch('payerUserId');
  const participantIds = watch('participantIds');

  const handlePayerChange = (userId: string) => {
    setValue('payerUserId', userId, { shouldValidate: true });
    if (!participantIds.includes(userId)) {
      setValue(
        'participantIds',
        [...participantIds, userId],
        { shouldValidate: true },
      );
    }
  };

  const onSubmit = handleSubmit(async (data) => {
    setServerError(null);
    setSplitError(null);
    if (!group) return;

    // Validate Exact split amounts sum to total
    if (splitMethod === 'Exact') {
      const participantSum = Object.values(participantAmounts).reduce(
        (sum, v) => sum + (Number(v) || 0),
        0,
      );
      if (Math.abs(participantSum - Number(data.amount)) > 0.001) {
        setSplitError('Participant amounts must equal the total amount');
        return;
      }
    }

    const amountMinor = toMinorUnits(Number(data.amount));

    const request: z.infer<typeof AddExpenseRequestSchema> = {
      payerUserId: data.payerUserId,
      amountMinor,
      currency: group.currency,
      description: data.description,
      occurredOn: data.occurredOn,
      splitMethod,
      splits: data.participantIds.map((userId) => ({
        userId,
        amountMinor: splitMethod === 'Exact'
          ? toMinorUnits(Number(participantAmounts[userId]) || 0)
          : null,
        percentage: null,
        shares: null,
      })),
    };

    createMutation.mutate(request);
  });

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gray-50">
        <main className="mx-auto max-w-2xl px-4 py-6">
          <p className="text-gray-500">Loading...</p>
        </main>
      </div>
    );
  }

  if (error) {
    const isNotFound = (error as { status?: number }).status === 404;
    return (
      <div className="min-h-screen bg-gray-50">
        <main className="mx-auto max-w-2xl px-4 py-6">
          <div className="space-y-2">
            <p className="text-red-600">
              {isNotFound
                ? 'Group not found or you are not a member'
                : 'Something went wrong. Please try again.'}
            </p>
            {!isNotFound && (
              <button
                type="button"
                onClick={() => refetch()}
                className="rounded border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
              >
                Retry
              </button>
            )}
          </div>
        </main>
      </div>
    );
  }

  if (!group) {
    return null;
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <main className="mx-auto max-w-2xl px-4 py-6">
        <Link
          to={`/groups/${id}`}
          className="mb-4 inline-block text-sm text-blue-600 hover:text-blue-700"
        >
          ← Back to group
        </Link>
        <h1 className="mb-6 text-2xl font-bold text-gray-900">Add Expense</h1>

        {serverError && (
          <p className="mb-4 text-sm text-red-600">{serverError}</p>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          {/* Description */}
          <div>
            <label htmlFor="expense-description" className="mb-1 block text-sm font-medium">
              Description
            </label>
            <input
              id="expense-description"
              className="w-full rounded border px-3 py-2"
              {...register('description')}
            />
            {errors.description && (
              <p className="mt-1 text-sm text-red-600">{errors.description.message}</p>
            )}
          </div>

          {/* Amount */}
          <div>
            <label htmlFor="expense-amount" className="mb-1 block text-sm font-medium">
              Amount
            </label>
            <input
              id="expense-amount"
              type="number"
              step="0.01"
              min="0"
              className="w-full rounded border px-3 py-2"
              {...register('amount')}
            />
            {errors.amount && (
              <p className="mt-1 text-sm text-red-600">{errors.amount.message}</p>
            )}
          </div>

          {/* Currency (read-only) */}
          <div>
            <label htmlFor="expense-currency" className="mb-1 block text-sm font-medium">
              Currency
            </label>
            <input
              id="expense-currency"
              type="text"
              className="w-full rounded border bg-gray-100 px-3 py-2 text-gray-500"
              value={group.currency}
              readOnly
            />
          </div>

          {/* Payer */}
          <div>
            <label htmlFor="expense-payer" className="mb-1 block text-sm font-medium">
              Payer
            </label>
            <select
              id="expense-payer"
              className="w-full rounded border px-3 py-2"
              value={selectedPayer}
              onChange={(e) => handlePayerChange(e.target.value)}
            >
              <option value="">Select payer</option>
              {group.members.map((member) => (
                <option key={member.userId} value={member.userId}>
                  {member.displayName}
                </option>
              ))}
            </select>
            {errors.payerUserId && (
              <p className="mt-1 text-sm text-red-600">{errors.payerUserId.message}</p>
            )}
          </div>

          {/* Date */}
          <div>
            <label htmlFor="expense-date" className="mb-1 block text-sm font-medium">
              Date
            </label>
            <input
              id="expense-date"
              type="date"
              className="w-full rounded border px-3 py-2"
              {...register('occurredOn')}
            />
            {errors.occurredOn && (
              <p className="mt-1 text-sm text-red-600">{errors.occurredOn.message}</p>
            )}
          </div>

          {/* Split method */}
          <div>
            <span className="mb-1 block text-sm font-medium">Split Method</span>
            <div className="flex rounded border overflow-hidden">
              <button
                type="button"
                onClick={() => setSplitMethod('Equal')}
                className={`flex-1 px-4 py-2 text-sm font-medium ${
                  splitMethod === 'Equal'
                    ? 'bg-blue-600 text-white'
                    : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                }`}
              >
                Equal
              </button>
              <button
                type="button"
                onClick={() => setSplitMethod('Exact')}
                className={`flex-1 px-4 py-2 text-sm font-medium ${
                  splitMethod === 'Exact'
                    ? 'bg-blue-600 text-white'
                    : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                }`}
              >
                Exact
              </button>
              <button
                type="button"
                className="flex-1 px-4 py-2 text-sm font-medium bg-gray-100 text-gray-400"
                disabled
              >
                %
              </button>
              <button
                type="button"
                className="flex-1 px-4 py-2 text-sm font-medium bg-gray-100 text-gray-400"
                disabled
              >
                Shares
              </button>
            </div>
          </div>

          {/* Participants */}
          <div>
            <span className="mb-2 block text-sm font-medium">Participants</span>
            <div className="space-y-2">
              {group.members.map((member) => {
                const isChecked = participantIds.includes(member.userId);
                return (
                  <label
                    key={member.userId}
                    className="flex items-center gap-2"
                  >
                    <input
                      type="checkbox"
                      checked={isChecked}
                      onChange={(e) => {
                        if (e.target.checked) {
                          setValue(
                            'participantIds',
                            [...participantIds, member.userId],
                            { shouldValidate: true },
                          );
                        } else {
                          setValue(
                            'participantIds',
                            participantIds.filter((id) => id !== member.userId),
                            { shouldValidate: true },
                          );
                        }
                      }}
                    />
                    <span className="text-sm">{member.displayName}</span>
                    {splitMethod === 'Exact' && isChecked && (
                      <input
                        type="number"
                        step="0.01"
                        min="0"
                        id={`exact-amount-${member.userId}`}
                        className="w-24 rounded border px-2 py-1 text-sm"
                        value={participantAmounts[member.userId] ?? ''}
                        onChange={(e) => {
                          setParticipantAmounts((prev) => ({
                            ...prev,
                            [member.userId]: e.target.value,
                          }));
                        }}
                      />
                    )}
                  </label>
                );
              })}
            </div>
            {splitMethod === 'Exact' && (
              <p className="mt-2 text-sm font-medium text-gray-700">
                Total: {formatCurrency(
                  toMinorUnits(
                    Object.values(participantAmounts).reduce(
                      (sum, v) => sum + (Number(v) || 0),
                      0,
                    ),
                  ),
                  group.currency,
                )}
              </p>
            )}
            {splitError && (
              <p className="mt-1 text-sm text-red-600">{splitError}</p>
            )}
            {errors.participantIds && (
              <p className="mt-1 text-sm text-red-600">{errors.participantIds.message}</p>
            )}
          </div>

          {/* Submit */}
          <div>
            <button
              type="submit"
              disabled={createMutation.isPending}
              className="rounded bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {createMutation.isPending ? 'Adding...' : 'Add Expense'}
            </button>
          </div>
        </form>
      </main>
    </div>
  );
}
