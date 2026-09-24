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

type ResetPasswordFormProps = {
  initialEmail: string;
  initialToken: string;
};

// The last step of "forgot my password": email, the reset code from the previous
// step, and the new password. If you came from that page, the first two are already
// filled in for you.
export function ResetPasswordForm({ initialEmail, initialToken }: ResetPasswordFormProps) {
  const router = useRouter();

  const [email, setEmail] = useState(initialEmail);
  const [resetToken, setResetToken] = useState(initialToken);
  const [newPassword, setNewPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      await apiFetch("/auth/reset-password", {
        method: "POST",
        body: { email, resetToken, newPassword },
      });
      router.push("/login");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <AuthCard
      eyebrow="Almost there"
      title="Set a new password"
      description="Enter the code you were given, along with a new password."
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
        <TextField
          id="resetToken"
          label="Reset code"
          required
          value={resetToken}
          onChange={(e) => setResetToken(e.target.value)}
        />
        <PasswordField
          id="newPassword"
          label="New password"
          autoComplete="new-password"
          required
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
        />

        {error && <p className="rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Saving…" : "Set new password"}
        </Button>
      </form>
    </AuthCard>
  );
}
