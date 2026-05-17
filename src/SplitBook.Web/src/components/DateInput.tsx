import { forwardRef, InputHTMLAttributes } from 'react';

export const DateInput = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(
  (props, ref) => (
    <input
      ref={ref}
      type="date"
      {...props}
      className={`w-full rounded border px-3 py-2 ${props.className ?? ''}`}
    />
  ),
);
DateInput.displayName = 'DateInput';
