import type { SelectHTMLAttributes } from "react";

type SelectFieldProps = SelectHTMLAttributes<HTMLSelectElement> & {
  label: string;
};

// A labelled dropdown that matches TextField's look, so a form mixing text inputs and
// dropdowns (like currency + timezone in onboarding) still looks like one form.
export function SelectField({ label, id, className = "", children, ...props }: SelectFieldProps) {
  return (
    <label htmlFor={id} className="flex flex-col gap-1.5 text-left">
      <span className="text-sm font-medium text-muted">{label}</span>
      <select
        id={id}
        className={`h-11 rounded-xl border border-border-strong bg-surface px-3.5 text-[15px] text-text outline-none focus:border-primary focus:ring-4 focus:ring-primary-tint ${className}`}
        {...props}
      >
        {children}
      </select>
    </label>
  );
}
