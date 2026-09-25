import type { InputHTMLAttributes } from "react";

type TextFieldProps = InputHTMLAttributes<HTMLInputElement> & {
  label: string;
};

// A labelled text input in the Natoshare style. See docs/06-design-system.md.
export function TextField({ label, id, className = "", ...props }: TextFieldProps) {
  return (
    <label htmlFor={id} className="flex flex-col gap-1.5 text-left">
      <span className="text-sm font-medium text-muted">{label}</span>
      <input
        id={id}
        className={`h-11 rounded-xl border border-border-strong bg-surface px-3.5 text-[15px] text-text outline-none focus:border-primary focus:ring-4 focus:ring-primary-tint ${className}`}
        {...props}
      />
    </label>
  );
}
