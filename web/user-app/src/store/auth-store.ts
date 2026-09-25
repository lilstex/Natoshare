"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";

export type AuthUser = {
  id: string;
  email: string;
  displayName: string;
  role: string;
  currencyCode: string;
  currencySymbol: string;
  timeZoneId: string;
  locale: string;
  trialEndsAt: string;
};

type Session = {
  user: AuthUser;
  accessToken: string;
  refreshToken: string;
};

type AuthState = {
  user: AuthUser | null;
  accessToken: string | null;
  refreshToken: string | null;
  setSession: (session: Session) => void;
  clearSession: () => void;
};

// Keeps track of who is logged in on this device. We save it to localStorage so a
// page refresh does not log someone out, and we also copy the access token into a
// plain cookie so our middleware can tell if someone is logged in before a page even
// starts loading.
export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      accessToken: null,
      refreshToken: null,
      setSession: (session) => {
        set(session);
        setAuthCookie(session.accessToken);
      },
      clearSession: () => {
        set({ user: null, accessToken: null, refreshToken: null });
        setAuthCookie(null);
      },
    }),
    { name: "natoshare-auth" },
  ),
);

// Writes (or clears) the cookie our middleware reads. This is not how we prove who the
// user is to the API, that is always the access token in the Authorization header, this
// cookie only tells page routing "someone is logged in, do not show them the login page".
function setAuthCookie(token: string | null) {
  if (token) {
    document.cookie = `natoshare-token=${token}; path=/; max-age=${60 * 60 * 24 * 30}; samesite=lax`;
  } else {
    document.cookie = "natoshare-token=; path=/; max-age=0";
  }
}
