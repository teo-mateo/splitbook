import { z } from 'zod';

export const LoginRequestSchema = z.object({
  email: z.string(),
  password: z.string(),
});

export const LoginResponseSchema = z.object({
  accessToken: z.string(),
  expiresAt: z.string(),
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

export const GroupDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  currency: z.string(),
  createdAt: z.string(),
});

export const ListGroupsResponseSchema = z.array(GroupDtoSchema);

export const CreateGroupRequestSchema = z.object({
  name: z.string(),
  currency: z.string(),
});

export const MemberDtoSchema = z.object({
  userId: z.string().uuid(),
  displayName: z.string(),
});

export const GroupDetailDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  currency: z.string(),
  createdAt: z.string(),
  archivedAt: z.string().nullable(),
  members: z.array(MemberDtoSchema),
});

export const BalanceDtoSchema = z.object({
  userId: z.string().uuid(),
  netAmountMinor: z.number(),
});

export const ExpenseSplitDtoSchema = z.object({
  userId: z.string().uuid(),
  amountMinor: z.number(),
});

export const ExpenseDtoSchema = z.object({
  id: z.string().uuid(),
  groupId: z.string().uuid(),
  payerUserId: z.string().uuid(),
  amountMinor: z.number(),
  currency: z.string().nullable(),
  description: z.string().nullable(),
  occurredOn: z.string(),
  splitMethod: z.string().nullable(),
  splits: z.array(ExpenseSplitDtoSchema).nullable(),
  createdAt: z.string(),
  version: z.number(),
});

export const ListExpensesResponseSchema = z.object({
  items: z.array(ExpenseDtoSchema).nullable(),
  total: z.number(),
});

export type LoginRequest = z.infer<typeof LoginRequestSchema>;
export type LoginResponse = z.infer<typeof LoginResponseSchema>;
export type RegisterRequest = z.infer<typeof RegisterRequestSchema>;
export type RegisterResponse = z.infer<typeof RegisterResponseSchema>;
export type GroupDto = z.infer<typeof GroupDtoSchema>;
export type CreateGroupRequest = z.infer<typeof CreateGroupRequestSchema>;
export type MemberDto = z.infer<typeof MemberDtoSchema>;
export type GroupDetailDto = z.infer<typeof GroupDetailDtoSchema>;
export type BalanceDto = z.infer<typeof BalanceDtoSchema>;
export type ExpenseSplitDto = z.infer<typeof ExpenseSplitDtoSchema>;
export type ExpenseDto = z.infer<typeof ExpenseDtoSchema>;
export const ExpenseSplitRequestSchema = z.object({
  userId: z.string().uuid(),
  amountMinor: z.number().nullable(),
  percentage: z.number().nullable(),
  shares: z.number().nullable(),
});

export const AddExpenseRequestSchema = z.object({
  payerUserId: z.string().uuid(),
  amountMinor: z.number(),
  currency: z.string().nullable(),
  description: z.string().nullable(),
  occurredOn: z.string(),
  splitMethod: z.string().nullable(),
  splits: z.array(ExpenseSplitRequestSchema).nullable(),
});

export type ListExpensesResponse = z.infer<typeof ListExpensesResponseSchema>;
export type ExpenseSplitRequest = z.infer<typeof ExpenseSplitRequestSchema>;
export type AddExpenseRequest = z.infer<typeof AddExpenseRequestSchema>;
