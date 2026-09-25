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
import type { AuthUser } from "@/store/auth-store";

type SignupResponse = {
  user: AuthUser;
  accessToken: string;
  refreshToken: string;
};

// The sign-up form. A new account starts a 30 day trial automatically, the backend
// takes care of that, this page just has to collect the three fields it needs.
export function SignupForm() {
  const router = useRouter();
  const setSession = useAuthStore((state) => state.setSession);

  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setFieldErrors({});
    setIsSubmitting(true);

    try {
      const result = await apiFetch<SignupResponse>("/auth/signup", {
        method: "POST",
        body: { email, password, displayName },
      });

      setSession(result);
      router.push("/onboarding");
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
        setFieldErrors(err.fieldErrors ?? {});
      } else {
        setError("Something went wrong. Please try again.");
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <AuthCard
      eyebrow="Get started"
      title="Create your account"
      description="Split it. Track it. Keep it. Your 30 day trial starts today."
      footer={
        <>
          Already have an account?{" "}
          <Link href="/login" className="font-semibold text-primary transition-colors hover:text-primary-strong">
            Log in
          </Link>
        </>
      }
    >
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <TextField
          id="displayName"
          label="What should we call you?"
          autoComplete="name"
          required
          value={displayName}
          onChange={(e) => setDisplayName(e.target.value)}
        />
        <TextField
          id="email"
          label="Email"
          type="email"
          autoComplete="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />
        <div>
          <PasswordField
            id="password"
            label="Password"
            autoComplete="new-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
          {fieldErrors.Password && (
            <p className="mt-1 text-xs text-danger">{fieldErrors.Password.join(" ")}</p>
          )}
          {!fieldErrors.Password && <p className="mt-1 text-xs text-subtle">At least 8 characters.</p>}
        </div>

        {error && <p className="rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Creating your account…" : "Create account"}
        </Button>
      </form>
    </AuthCard>
  );
}
