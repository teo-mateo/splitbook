import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { apiRequest } from '../../api/client';
import { ListGroupsResponseSchema } from '../../api/types';
import { CreateGroup } from './CreateGroup';

export function GroupsList() {
  const [showCreate, setShowCreate] = useState(false);
  const { data: groups, isLoading, error, refetch } = useQuery({
    queryKey: ['groups'],
    queryFn: () => apiRequest(ListGroupsResponseSchema, '/groups'),
  });

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white shadow-sm">
        <div className="mx-auto max-w-2xl px-4 py-4">
          <h1 className="text-2xl font-bold text-gray-900">SplitBook</h1>
        </div>
      </header>
      <main className="mx-auto max-w-2xl px-4 py-6">
        <div className="mb-4 flex justify-end">
          <button
            type="button"
            onClick={() => setShowCreate(true)}
            className="rounded bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700"
          >
            Create group
          </button>
        </div>
        {showCreate && (
          <CreateGroup onClose={() => setShowCreate(false)} />
        )}
        {isLoading && <p className="text-gray-500">Loading...</p>}
        {error && (
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
        )}
        {groups !== undefined && (
          <>
            {groups.length === 0 ? (
              <p className="text-gray-500">No groups yet</p>
            ) : (
              <ul className="space-y-3">
                {groups.map((group) => (
                  <li key={group.id}>
                    <Link
                      to={`/groups/${group.id}`}
                      className="block rounded-lg bg-white px-4 py-3 shadow-sm hover:bg-gray-50"
                    >
                      <div className="flex items-center justify-between">
                        <span className="font-medium text-gray-900">{group.name}</span>
                        <span className="text-sm text-gray-500">{group.currency}</span>
                      </div>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </main>
    </div>
  );
}
