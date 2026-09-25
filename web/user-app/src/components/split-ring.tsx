const SEGMENT_COLORS = [
  "var(--cat-rent)",
  "var(--cat-feeding)",
  "var(--cat-transport)",
  "var(--cat-utility)",
  "var(--cat-subs)",
  "var(--cat-invest)",
];

type SplitRingSegment = {
  percentage: number;
};

type SplitRingProps = {
  segments: SplitRingSegment[];
};

// The donut ring from the onboarding design: one ring, one arc per category, colored
// in the order the categories are listed. If the percentages do not add up to 100 yet
// (still mid-edit), the ring just shows however much is filled so far, it does not
// pretend the split is finished.
export function SplitRing({ segments }: SplitRingProps) {
  const radius = 52;
  const circumference = 2 * Math.PI * radius;
  const total = segments.reduce((sum, s) => sum + s.percentage, 0);

  // Worked out before render, not mutated during it, so where each arc starts is
  // just "how much of the ring every earlier arc already used up".
  const startPercentages = segments.reduce<number[]>((offsets, segment, index) => {
    offsets.push(index === 0 ? 0 : offsets[index - 1] + segments[index - 1].percentage);
    return offsets;
  }, []);

  return (
    <svg width="132" height="132" viewBox="0 0 132 132" className="shrink-0">
      <circle cx="66" cy="66" r={radius} fill="none" stroke="var(--surface-2)" strokeWidth="16" />
      {segments.map((segment, index) => {
        const dash = (segment.percentage / 100) * circumference;
        const offset = -((startPercentages[index] / 100) * circumference);

        return (
          <circle
            key={index}
            cx="66"
            cy="66"
            r={radius}
            fill="none"
            stroke={SEGMENT_COLORS[index % SEGMENT_COLORS.length]}
            strokeWidth="16"
            strokeDasharray={`${dash} ${circumference}`}
            strokeDashoffset={offset}
            transform="rotate(-90 66 66)"
          />
        );
      })}
      <text x="66" y="63" textAnchor="middle" className="fill-text font-display text-[17px] font-bold">
        {Math.round(total)}%
      </text>
      <text x="66" y="79" textAnchor="middle" className="fill-subtle text-[9px]">
        allocated
      </text>
    </svg>
  );
}

export { SEGMENT_COLORS };
