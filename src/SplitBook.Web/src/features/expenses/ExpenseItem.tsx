import { formatCurrency } from '../../lib/money';
import { ExpenseDto, MemberDto } from '../../api/types';

export function ExpenseItem({
  expense,
  currency,
  members,
}: {
  expense: ExpenseDto;
  currency: string;
  members: MemberDto[];
}) {
  const payer = members.find((m) => m.userId === expense.payerUserId);
  const payerName = payer?.displayName ?? 'Unknown';
  const participantCount = expense.splits?.length ?? 0;

  return (
    <div className="rounded-lg bg-white px-4 py-3 shadow-sm">
      <div className="flex items-center justify-between">
        <div>
          <p className="font-medium text-gray-900">{expense.description}</p>
          <p className="text-sm text-gray-500">
            <span>{payerName}</span>
            {' paid '}
            <span>{formatCurrency(expense.amountMinor, currency)}</span>
            {' · '}
            <span>{expense.occurredOn}</span>
            {' · '}
            <span>{participantCount} people</span>
          </p>
        </div>
      </div>
    </div>
  );
}
