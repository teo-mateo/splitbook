import { z } from 'zod';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { apiRequest } from '../../api/client';
import { BalanceDtoSchema, GroupDetailDtoSchema, ListExpensesResponseSchema } from '../../api/types';

export function GroupDetail() {
  const { id } = useParams();

  const { data: group, isLoading, error, refetch } = useQuery({
    queryKey: ['group', id],
    queryFn: () => apiRequest(GroupDetailDtoSchema, `/groups/${id}`),
  });

  const { data: balances } = useQuery({
    queryKey: ['balances', id],
    queryFn: () => apiRequest(z.array(BalanceDtoSchema), `/groups/${id}/balances`),
    enabled: !!group,
  });

  const { data: expensesData } = useQuery({
    queryKey: ['expenses', id],
    queryFn: () => apiRequest(ListExpensesResponseSchema, `/groups/${id}/expenses`),
    enabled: !!group,
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

  const balanceMap = new Map(
    balances?.map((b) => [b.userId, b.netAmountMinor]) ?? [],
  );

  const formatBalance = (minorUnits: number): string => {
    const major = minorUnits / 100;
    const symbol = group.currency === 'EUR' ? '€' : group.currency;
    if (major >= 0) {
      return `${symbol}${Math.abs(major).toFixed(2)}`;
    }
    return `-${symbol}${Math.abs(major).toFixed(2)}`;
  };

  const balanceColor = (minorUnits: number): string => {
    if (minorUnits > 0) return 'text-green-600';
    if (minorUnits < 0) return 'text-red-600';
    return 'text-gray-500';
  };

  const memberNameMap = new Map(
    group.members.map((m) => [m.userId, m.displayName]),
  );

  const formatExpenseAmount = (minorUnits: number): string => {
    const major = minorUnits / 100;
    const symbol = group.currency === 'EUR' ? '€' : group.currency;
    return `${symbol}${major.toFixed(2)}`;
  };

  const expenses = expensesData?.items ?? [];

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white shadow-sm">
        <div className="mx-auto max-w-2xl px-4 py-4">
          <div className="flex items-center justify-between">
            <h1 className="text-2xl font-bold text-gray-900">{group.name}</h1>
            <span className="text-sm text-gray-500">{group.currency}</span>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-2xl px-4 py-6">
        <section className="mb-6">
          <h2 className="mb-2 text-lg font-semibold text-gray-900">Members</h2>
          {group.members.length === 0 ? (
            <p className="text-gray-500">No members</p>
          ) : (
            <ul className="space-y-2">
              {group.members.map((member) => {
                const minor = balanceMap.get(member.userId) ?? 0;
                return (
                  <li key={member.userId} className="rounded-lg bg-white px-4 py-3 shadow-sm">
                    <div className="flex items-center justify-between">
                      <span className="font-medium text-gray-900">{member.displayName}</span>
                      <span className={balanceColor(minor)}>{formatBalance(minor)}</span>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </section>
        <section className="mb-6">
          <h2 className="mb-2 text-lg font-semibold text-gray-900">Expenses</h2>
          {expenses.length === 0 ? (
            <p className="text-gray-500">No expenses yet</p>
          ) : (
            <ul className="space-y-2">
              {expenses.map((expense) => {
                const payerName = memberNameMap.get(expense.payerUserId) ?? 'Unknown';
                const participantCount = expense.splits?.length ?? 0;
                return (
                  <li key={expense.id} className="rounded-lg bg-white px-4 py-3 shadow-sm">
                    <div className="flex items-center justify-between">
                      <div>
                        <p className="font-medium text-gray-900">{expense.description}</p>
                        <p className="text-sm text-gray-500">
                          <span>{payerName}</span>
                          {' paid '}
                          <span>{formatExpenseAmount(expense.amountMinor)}</span>
                          {' · '}
                          <span>{expense.occurredOn}</span>
                          {' · '}
                          <span>{participantCount} people</span>
                        </p>
                      </div>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </section>
      </main>
    </div>
  );
}
