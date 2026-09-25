import type { HTMLAttributes } from "react";

// A plain surface panel, the base every admin screen's sections sit on. See
// docs/06-design-system.md.
export function Card({ className = "", ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={`rounded-2xl border border-border bg-surface p-5 shadow-[var(--nato-shadow-sm)] ${className}`}
      {...props}
    />
  );
}
