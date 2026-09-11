"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";

export type AdminUser = {
  id: string;
  email: string;
  displayName: string;
  role: string;
};

type Session = {
  user: AdminUser;
  accessToken: string;
  refreshToken: string;
};

type AuthState = {
  user: AdminUser | null;
  accessToken: string | null;
  refreshToken: string | null;
  setSession: (session: Session) => void;
  clearSession: () => void;
};

// Keeps track of which admin is logged in on this device. The real screens that use
// this (users, audit log, and so on) get built in Phase 10, this is just the login
// piece for now.
export const useAdminAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      accessToken: null,
      refreshToken: null,
      setSession: (session) => set(session),
      clearSession: () => set({ user: null, accessToken: null, refreshToken: null }),
    }),
    { name: "natoshare-admin-auth" },
  ),
);
