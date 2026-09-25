type BadgeTone = "neutral" | "success" | "warning" | "danger" | "info";

type BadgeProps = {
  tone?: BadgeTone;
  children: React.ReactNode;
};

const toneClasses: Record<BadgeTone, string> = {
  neutral: "bg-surface-2 text-muted",
  success: "bg-success-tint text-success",
  warning: "bg-warning-tint text-warning",
  danger: "bg-danger-tint text-danger",
  info: "bg-info-tint text-info",
};

// A small status pill, for example a user's Active/Suspended status or a
// subscription's Pending/Active status. See docs/06-design-system.md.
export function Badge({ tone = "neutral", children }: BadgeProps) {
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-semibold ${toneClasses[tone]}`}>
      {children}
    </span>
  );
}

// Picks a sensible tone for a user's account status, used in a few places (the
// users list, a user's detail page) so the mapping only lives in one spot.
export function statusTone(status: string): BadgeTone {
  if (status === "Active") return "success";
  if (status === "Suspended") return "danger";
  if (status === "PendingDeletion") return "warning";
  return "neutral";
}
