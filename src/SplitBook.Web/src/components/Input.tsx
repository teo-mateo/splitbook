import { forwardRef, InputHTMLAttributes } from 'react';

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(
  (props, ref) => (
    <input
      ref={ref}
      {...props}
      className={`w-full rounded border px-3 py-2 ${props.className ?? ''}`}
    />
  ),
);
Input.displayName = 'Input';
