import type { ButtonHTMLAttributes } from "react";

type ButtonVariant = "primary" | "secondary";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
};

// The one button style every page should use, so every "primary action" in the app
// looks the same. See docs/06-design-system.md for the button spec this follows.
export function Button({ variant = "primary", className = "", ...props }: ButtonProps) {
  const base =
    "inline-flex h-11 items-center justify-center gap-2 rounded-full px-6 text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-40";

  const variantClasses =
    variant === "primary"
      ? "bg-primary text-white hover:bg-primary-strong"
      : "border border-border-strong bg-surface text-text hover:bg-surface-2";

  return <button className={`${base} ${variantClasses} ${className}`} {...props} />;
}
