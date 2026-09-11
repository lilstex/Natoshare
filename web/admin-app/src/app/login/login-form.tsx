"use client";

import { useState } from "react";
import type { FormEvent } from "react";
import { BrandMark } from "@/components/brand-mark";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import { apiFetch, ApiError } from "@/lib/api-client";
import { useAdminAuthStore } from "@/store/auth-store";
import type { AdminUser } from "@/store/auth-store";

type LoginResponse = {
  user: AdminUser;
  accessToken: string;
  refreshToken: string;
};

// The only screen this app has for now. The real admin screens (users, audit log,
// monitoring, and so on) get built in Phase 10, this just proves an admin can log in.
export function LoginForm() {
  const setSession = useAdminAuthStore((state) => state.setSession);
  const user = useAdminAuthStore((state) => state.user);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      const result = await apiFetch<LoginResponse>("/auth/login", {
        method: "POST",
        body: { email, password },
      });

      // This app is only for the Natoshare team. A normal user's login still works
      // against the API (there is no separate login endpoint for admins), but we do
      // not let them into this app.
      if (result.user.role !== "Admin") {
        setError("This account does not have access to the admin app.");
        return;
      }

      setSession(result);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  if (user) {
    return (
      <main className="flex min-h-screen flex-col items-center justify-center gap-3 bg-bg px-4 text-center">
        <BrandMark size={56} />
        <h1 className="text-2xl font-bold text-text">Welcome, {user.displayName}</h1>
        <p className="max-w-sm text-sm text-muted">
          You are logged in. The real admin screens (users, audit log, monitoring) are coming in Phase
          10.
        </p>
      </main>
    );
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-6 bg-bg px-4">
      <div className="flex items-center gap-2.5">
        <BrandMark size={36} />
        <span className="font-display text-lg font-semibold text-text">Natoshare Admin</span>
      </div>

      <div className="w-full max-w-sm rounded-[26px] border border-border bg-surface p-8 shadow-lg">
        <h1 className="text-xl font-bold text-text">Log in</h1>
        <p className="mt-1 text-sm text-muted">Natoshare team members only.</p>

        <form onSubmit={handleSubmit} className="mt-6 flex flex-col gap-4">
          <TextField
            id="email"
            label="Email"
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
          <TextField
            id="password"
            label="Password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />

          {error && <p className="rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Logging in…" : "Log in"}
          </Button>
        </form>
      </div>
    </main>
  );
}
