"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import type { FormEvent } from "react";
import { AuthCard } from "@/components/auth-card";
import { Button } from "@/components/ui/button";
import { PasswordField } from "@/components/ui/password-field";
import { TextField } from "@/components/ui/text-field";
import { apiFetch, ApiError } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";

type LoginResponse = {
  user: {
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
  accessToken: string;
  refreshToken: string;
};

// The actual login form. This is a client component because it needs to hold what
// the person has typed and react to the submit button, the page.tsx around it stays a
// server component so it can still set the page title.
export function LoginForm() {
  const router = useRouter();
  const setSession = useAuthStore((state) => state.setSession);

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

      setSession(result);

      // Someone who never finished setting up their account gets sent back to the
      // wizard instead of a home page that will not make sense without it. The login
      // itself already succeeded at this point, so if this check fails for any reason
      // we still send them somewhere useful instead of showing a login error.
      const onboardingPath = await apiFetch<{ done: boolean }>("/onboarding/state", { token: result.accessToken })
        .then((state) => (state.done ? "/dashboard" : "/onboarding"))
        .catch(() => "/onboarding");
      router.push(onboardingPath);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <AuthCard
      eyebrow="Welcome back"
      title="Log in"
      description="Pick up right where you left off."
      footer={
        <>
          New to Natoshare?{" "}
          <Link href="/signup" className="font-semibold text-primary transition-colors hover:text-primary-strong">
            Create an account
          </Link>
        </>
      }
    >
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <TextField
          id="email"
          label="Email"
          type="email"
          autoComplete="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />
        <PasswordField
          id="password"
          label="Password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />

        <div className="text-right text-sm">
          <Link href="/forgot-password" className="font-medium text-primary transition-colors hover:text-primary-strong">
            Forgot your password?
          </Link>
        </div>

        {error && <p className="rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Logging in…" : "Log in"}
        </Button>
      </form>
    </AuthCard>
  );
}
