"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAdminAuthStore } from "@/store/auth-store";

// Zustand's persist middleware reads localStorage asynchronously, so on the very
// first render accessToken is still null even for someone already logged in (the
// same rehydration timing the user app already works around on its dashboard, see
// its dashboard-screen.tsx). We wait one client-only render (a plain useEffect
// only ever runs in the browser, never during server prerendering) before trusting
// accessToken enough to redirect, so a real admin never gets bounced by mistake and
// `next build`'s static prerendering never touches localStorage at all.
export function useAdminGuard() {
  const accessToken = useAdminAuthStore((state) => state.accessToken);
  const user = useAdminAuthStore((state) => state.user);
  const router = useRouter();
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    // This is the standard "wait for the client" trick, not state syncing, so the
    // set-state-in-effect lint rule's usual advice does not apply here (same
    // precedent as the user app's dashboard screen for the same reason).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setMounted(true);
  }, []);

  useEffect(() => {
    if (mounted && !accessToken) {
      router.replace("/login");
    }
  }, [mounted, accessToken, router]);

  return { accessToken, user, ready: mounted && !!accessToken };
}
