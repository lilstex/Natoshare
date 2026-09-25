"use client";

import type { ReactNode } from "react";
import { useScrollReveal } from "@/hooks/use-scroll-reveal";

type RevealSectionProps = {
  className?: string;
  children: ReactNode;
};

// Wraps a landing page section so it fades and slides up the first time it scrolls
// into view (docs/feedback.md Phase D, decision 2: plain CSS/IntersectionObserver,
// no animation library). Kept as its own small client component so the rest of the
// landing page (page.tsx) can stay a server component for its metadata.
export function RevealSection({ className = "", children }: RevealSectionProps) {
  const { ref, isVisible } = useScrollReveal<HTMLElement>();

  return (
    <section ref={ref} className={`reveal ${isVisible ? "reveal-visible" : ""} ${className}`}>
      {children}
    </section>
  );
}
