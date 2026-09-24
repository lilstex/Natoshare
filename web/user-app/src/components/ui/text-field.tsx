import type { InputHTMLAttributes, ReactNode } from "react";

type TextFieldProps = InputHTMLAttributes<HTMLInputElement> & {
  label: string;
  // An optional icon or button shown inside the input, on the right (used by
  // PasswordField for the show/hide toggle). Leave it out for a plain text field.
  trailing?: ReactNode;
};

// A labelled text input in the Natoshare style: a label above a rounded box, with the
// focus ring from docs/06-design-system.md. Used across every form in the app.
export function TextField({ label, id, className = "", trailing, ...props }: TextFieldProps) {
  return (
    <label htmlFor={id} className="flex flex-col gap-1.5 text-left">
      <span className="text-sm font-medium text-muted">{label}</span>
      <div className="relative">
        <input
          id={id}
          className={`h-11 w-full rounded-md border border-border-strong bg-surface px-3.5 text-[15px] text-text outline-none focus:border-primary focus:ring-4 focus:ring-primary-tint ${trailing ? "pr-11" : ""} ${className}`}
          {...props}
        />
        {trailing && <div className="absolute inset-y-0 right-1 flex items-center">{trailing}</div>}
      </div>
    </label>
  );
}
