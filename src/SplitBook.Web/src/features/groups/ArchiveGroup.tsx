export function ArchiveGroup({
  groupName,
  onClose,
  onConfirm,
  isPending,
}: {
  groupName: string;
  onClose: () => void;
  onConfirm: () => void;
  isPending: boolean;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" role="dialog" aria-modal="true" aria-labelledby="archive-dialog-title">
      <div className="w-full max-w-sm rounded-lg bg-white p-6 shadow-xl" role="document">
        <h2 id="archive-dialog-title" className="mb-2 text-xl font-bold">Archive Group</h2>
        <p className="mb-4 text-gray-700">
          Are you sure you want to archive {groupName}? This will hide the
          group from your list.
        </p>
        <div className="flex justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            className="rounded border px-4 py-2 text-gray-700 hover:bg-gray-50 disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={isPending}
            className="rounded bg-red-600 px-4 py-2 font-medium text-white hover:bg-red-700 disabled:opacity-50"
          >
            {isPending ? 'Archiving...' : 'Archive'}
          </button>
        </div>
      </div>
    </div>
  );
}
