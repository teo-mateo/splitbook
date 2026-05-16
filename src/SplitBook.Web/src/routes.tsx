import type { ReactNode } from 'react';
import { Route } from 'react-router-dom';

// Feature components (placeholders)
import { GroupsList } from './features/groups/GroupsList';
import { GroupDetail } from './features/groups/GroupDetail';
import { Login } from './features/auth/Login';
import { Register } from './features/auth/Register';
import { ExpenseForm } from './features/expenses/ExpenseForm';
import { SettlementForm } from './features/settlements/SettlementForm';
import { Profile } from './features/profile/Profile';

export const appRoutes: ReactNode = (
  <>
    <Route path="/" element={<GroupsList />} />
    <Route path="/login" element={<Login />} />
    <Route path="/register" element={<Register />} />
    <Route path="/groups" element={<GroupsList />} />
    <Route path="/groups/:id" element={<GroupDetail />} />
    <Route path="/groups/:id/expenses/new" element={<ExpenseForm />} />
    <Route path="/groups/:id/expenses/:expenseId/edit" element={<ExpenseForm />} />
    <Route path="/groups/:id/settlements/new" element={<SettlementForm />} />
    <Route path="/profile" element={<Profile />} />
  </>
);
