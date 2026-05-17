import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { apiRequest } from '../../api/client';
import { ListExpensesResponseSchema, MemberDto } from '../../api/types';
import { DateInput } from '../../components/DateInput';
import { ExpenseItem } from './ExpenseItem';

const PAGE_SIZE = 10;

export function ExpenseList({
  groupId,
  currency,
  members,
}: {
  groupId: string;
  currency: string;
  members: MemberDto[];
}) {
  const [page, setPage] = useState(0);
  const [dateFrom, setDateFrom] = useState<string | null>(null);
  const [dateTo, setDateTo] = useState<string | null>(null);

  const skip = page * PAGE_SIZE;
  const params = new URLSearchParams({ skip: String(skip), take: String(PAGE_SIZE) });
  if (dateFrom) params.set('from', dateFrom);
  if (dateTo) params.set('to', dateTo);

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['expenses', groupId, skip, dateFrom, dateTo],
    queryFn: () =>
      apiRequest(ListExpensesResponseSchema, `/groups/${groupId}/expenses?${params}`),
  });

  const items = data?.items ?? [];
  const total = data?.total ?? 0;
  const hasMore = skip + items.length < total;

  if (isLoading) {
    return <p className="text-gray-500">Loading expenses...</p>;
  }

  if (error) {
    return (
      <div className="space-y-2">
        <p className="text-red-600">Something went wrong. Please try again.</p>
        <button
          type="button"
          onClick={() => refetch()}
          className="rounded border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
        >
          Retry
        </button>
      </div>
    );
  }

  if (items.length === 0) {
    return <p className="text-gray-500">No expenses yet</p>;
  }

  return (
    <div>
      {/* Date filter */}
      <div className="mb-4 flex flex-wrap items-end gap-2">
        <div>
          <label htmlFor={`expense-list-from-${groupId}`} className="mb-1 block text-xs font-medium text-gray-500">
            From
          </label>
          <DateInput
            id={`expense-list-from-${groupId}`}
            value={dateFrom ?? ''}
            onChange={(e) => setDateFrom(e.target.value || null)}
            className="rounded border px-2 py-1 text-sm"
          />
        </div>
        <div>
          <label htmlFor={`expense-list-to-${groupId}`} className="mb-1 block text-xs font-medium text-gray-500">
            To
          </label>
          <DateInput
            id={`expense-list-to-${groupId}`}
            value={dateTo ?? ''}
            onChange={(e) => setDateTo(e.target.value || null)}
            className="rounded border px-2 py-1 text-sm"
          />
        </div>
        {(dateFrom || dateTo) && (
          <button
            type="button"
            onClick={() => {
              setDateFrom(null);
              setDateTo(null);
              setPage(0);
            }}
            className="text-sm text-blue-600 hover:text-blue-700"
          >
            Clear
          </button>
        )}
      </div>

      {/* Expense items */}
      <ul className="space-y-2">
        {items.map((expense) => (
          <li key={expense.id}>
            <ExpenseItem expense={expense} currency={currency} members={members} />
          </li>
        ))}
      </ul>

      {/* Pagination */}
      {total > PAGE_SIZE && (
        <div className="mt-4 flex items-center justify-between">
          <button
            type="button"
            onClick={() => setPage((p) => Math.max(0, p - 1))}
            disabled={page === 0}
            className="rounded border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
          >
            Previous
          </button>
          <span className="text-sm text-gray-500">
            {skip + 1}&ndash;{Math.min(skip + items.length, total)} of {total}
          </span>
          <button
            type="button"
            onClick={() => setPage((p) => p + 1)}
            disabled={!hasMore}
            className="rounded border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
}
