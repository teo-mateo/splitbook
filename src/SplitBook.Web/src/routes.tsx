import type { ReactNode } from 'react';
import { Route } from 'react-router-dom';
import { AuthGuard } from './features/auth/AuthGuard';

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
    <Route path="/login" element={<Login />} />
    <Route path="/register" element={<Register />} />
    <Route path="/" element={<AuthGuard><GroupsList /></AuthGuard>} />
    <Route path="/groups" element={<AuthGuard><GroupsList /></AuthGuard>} />
    <Route path="/groups/:id" element={<AuthGuard><GroupDetail /></AuthGuard>} />
    <Route path="/groups/:id/expenses/new" element={<AuthGuard><ExpenseForm /></AuthGuard>} />
    <Route path="/groups/:id/expenses/:expenseId/edit" element={<AuthGuard><ExpenseForm /></AuthGuard>} />
    <Route path="/groups/:id/settlements/new" element={<AuthGuard><SettlementForm /></AuthGuard>} />
    <Route path="/profile" element={<AuthGuard><Profile /></AuthGuard>} />
  </>
);
