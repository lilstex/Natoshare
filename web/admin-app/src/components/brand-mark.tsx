type BrandMarkProps = {
  size?: number;
  className?: string;
};

// This draws the Natoshare mark for the admin app. The admin app does not use the
// gradient tile that the user app uses, it sits on a plain surface-2 tile instead, so
// the two apps still look related but you can always tell them apart at a glance.
export function BrandMark({ size = 40, className }: BrandMarkProps) {
  return (
    <span
      className={className}
      style={{
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        width: size,
        height: size,
        borderRadius: size * 0.28,
        background: "var(--nato-surface-2)",
      }}
    >
      <svg
        width={size * 0.6}
        height={size * 0.6}
        viewBox="0 0 64 64"
        role="img"
        aria-label="Natoshare Admin"
      >
        <path
          d="M21 45V19l22 26V19"
          fill="none"
          style={{ stroke: "var(--nato-primary)" }}
          strokeWidth={6.5}
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        <circle cx={45.5} cy={19.5} r={4.6} style={{ fill: "var(--nato-primary)" }} />
      </svg>
    </span>
  );
}
