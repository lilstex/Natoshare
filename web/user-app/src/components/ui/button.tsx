import type { ButtonHTMLAttributes } from "react";

type ButtonVariant = "primary" | "secondary";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
};

// The one button style every page should use, so every "primary action" in the app
// looks the same. See docs/06-design-system.md for the button spec this follows.
// A real rectangle (not a pill, not an oval) with a gradient fill, per the
// 2026-09-24 visual refresh in docs/feedback.md. `rounded-xl` still read as too
// round at this height, `rounded-md` (6px) is the one that actually looks like a
// rectangle rather than a stretched-out oval.
export function Button({ variant = "primary", className = "", ...props }: ButtonProps) {
  const base =
    "inline-flex h-11 items-center justify-center gap-2 rounded-md px-4 text-sm font-semibold shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-md disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:translate-y-0 disabled:hover:shadow-sm sm:px-6";

  const variantClasses =
    variant === "primary"
      ? "bg-gradient-to-r from-cta to-cta-hover text-white hover:brightness-105"
      : "border border-border-strong bg-gradient-to-b from-surface to-surface-2 text-text hover:from-surface-2 hover:to-surface-2";

  return <button className={`${base} ${variantClasses} ${className}`} {...props} />;
}
