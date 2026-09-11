type BrandMarkProps = {
  size?: number;
  className?: string;
};

// This draws the Natoshare logo mark, the gradient squircle with the "N" inside.
// The size prop lets you use it small (in a nav bar) or big (on the landing page)
// without keeping several copies of this file around.
export function BrandMark({ size = 40, className }: BrandMarkProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 64 64"
      role="img"
      aria-label="Natoshare"
      className={className}
    >
      <defs>
        <linearGradient id="natoshare-mark-gradient" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#7A4BE6" />
          <stop offset=".52" stopColor="#5B3AD1" />
          <stop offset="1" stopColor="#4668DB" />
        </linearGradient>
      </defs>
      <path
        d="M32 3c13.2 0 18.6 1.2 23.1 5.9C59.8 13.4 61 18.8 61 32s-1.2 18.6-5.9 23.1C50.6 59.8 45.2 61 32 61S13.4 59.8 8.9 55.1C4.2 50.6 3 45.2 3 32S4.2 13.4 8.9 8.9 18.8 3 32 3Z"
        fill="url(#natoshare-mark-gradient)"
      />
      <path
        d="M21 45V19l22 26V19"
        fill="none"
        stroke="#fff"
        strokeWidth={6.5}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <circle cx={45.5} cy={19.5} r={4.6} fill="#fff" />
    </svg>
  );
}
