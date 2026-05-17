import { forwardRef, SelectHTMLAttributes } from 'react';

export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(
  (props, ref) => (
    <select
      ref={ref}
      {...props}
      className={`w-full rounded border px-3 py-2 ${props.className ?? ''}`}
    />
  ),
);
Select.displayName = 'Select';
