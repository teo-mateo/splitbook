import { forwardRef, InputHTMLAttributes } from 'react';

export const CurrencyInput = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(
  (props, ref) => (
    <input
      ref={ref}
      type="number"
      step="0.01"
      min="0"
      {...props}
      className={`w-full rounded border px-3 py-2 ${props.className ?? ''}`}
    />
  ),
);
CurrencyInput.displayName = 'CurrencyInput';
