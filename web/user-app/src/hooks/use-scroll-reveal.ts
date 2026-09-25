"use client";

import { useEffect, useRef, useState } from "react";

// A small helper for the landing page's "fade and slide up as you scroll to it"
// effect (see docs/feedback.md Phase A, decision 2: plain CSS/IntersectionObserver
// instead of a new animation library). Attach the returned ref to any section, then
// toggle the "reveal-visible" class (see globals.css) with the returned isVisible
// value. We do not need to special-case "prefers-reduced-motion" here, globals.css
// already forces ".reveal" to sit at full opacity with no transition for anyone who
// asked for that, whether or not this observer has fired yet.
export function useScrollReveal<T extends HTMLElement>() {
  const ref = useRef<T | null>(null);
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    const node = ref.current;
    if (!node) {
      return;
    }

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setIsVisible(true);
          observer.disconnect();
        }
      },
      { threshold: 0.15 },
    );

    observer.observe(node);
    return () => observer.disconnect();
  }, []);

  return { ref, isVisible };
}
