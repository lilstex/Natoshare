import type { ButtonHTMLAttributes } from "react";

type ButtonVariant = "primary" | "secondary" | "danger";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
};

// Each variant is a complete, self-contained class list, not a base style with
// bits overridden through className. Appending a background/text override onto the
// primary button's own classes (bg-primary, text-white and so on all still in the
// string) produced a half-primary, half-secondary looking button, since Tailwind
// does not let a later class in the string reliably beat an earlier one with the
// same specificity.
const variantClasses: Record<ButtonVariant, string> = {
  primary: "bg-cta text-white hover:bg-cta-hover",
  secondary: "bg-surface-2 text-text hover:bg-border",
  danger: "bg-danger text-white hover:opacity-90",
};

// The button styles the admin app uses. See docs/06-design-system.md.
export function Button({ variant = "primary", className = "", ...props }: ButtonProps) {
  return (
    <button
      className={`inline-flex h-11 items-center justify-center gap-2 rounded-full px-6 text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-40 ${variantClasses[variant]} ${className}`}
      {...props}
    />
  );
}
