"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import type { FormEvent } from "react";
import { AuthCard } from "@/components/auth-card";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import { apiFetch, ApiError } from "@/lib/api-client";

type ForgotPasswordResponse = {
  resetToken: string | null;
};

// Natoshare does not send emails yet (see docs/01-domain-model.md), so outside of
// development the API never hands back a code here, someone would need an admin's
// help instead (Phase 10 builds that). In development, we show the code straight away
// so the whole reset flow can still be tested end to end.
export function ForgotPasswordForm() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [result, setResult] = useState<ForgotPasswordResponse | null>(null);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      const response = await apiFetch<ForgotPasswordResponse>("/auth/forgot-password", {
        method: "POST",
        body: { email },
      });
      setResult(response);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  if (result) {
    return (
      <AuthCard
        eyebrow="Check your details"
        title="If that account exists…"
        description="We do not want to say whether that email is registered, so this message is the same either way."
        footer={
          <Link href="/login" className="font-semibold text-primary transition-colors hover:text-primary-strong">
            Back to login
          </Link>
        }
      >
        {result.resetToken ? (
          <div className="flex flex-col gap-3 text-sm text-muted">
            <p>
              Natoshare does not send emails yet, so here is your reset code instead of one landing in
              your inbox:
            </p>
            <p className="rounded-xl bg-surface-2 px-3.5 py-2.5 text-center font-mono text-base text-text">
              {result.resetToken}
            </p>
            <Button
              type="button"
              onClick={() => {
                const query = new URLSearchParams({ email, token: result.resetToken ?? "" });
                router.push(`/reset-password?${query.toString()}`);
              }}
            >
              Continue to reset password
            </Button>
          </div>
        ) : (
          <p className="text-sm text-muted">
            If an account exists for that email, someone from Natoshare will be in touch to help you get
            back in.
          </p>
        )}
      </AuthCard>
    );
  }

  return (
    <AuthCard
      eyebrow="Forgot your password?"
      title="Reset your password"
      description="Tell us your email and we will help you get back in."
      footer={
        <Link href="/login" className="font-semibold text-primary transition-colors hover:text-primary-strong">
          Back to login
        </Link>
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

        {error && <p className="rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Sending…" : "Send reset code"}
        </Button>
      </form>
    </AuthCard>
  );
}
