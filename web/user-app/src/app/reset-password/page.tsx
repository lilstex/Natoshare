import type { Metadata } from "next";
import { ResetPasswordForm } from "./reset-password-form";

// This page can carry a one-time reset code in its URL, we do not want that showing
// up in search results or being cached anywhere.
export const metadata: Metadata = {
  title: "Set a new password",
  robots: { index: false, follow: false },
};

type ResetPasswordPageProps = {
  searchParams: Promise<{ email?: string; token?: string }>;
};

export default async function ResetPasswordPage({ searchParams }: ResetPasswordPageProps) {
  const params = await searchParams;

  return <ResetPasswordForm initialEmail={params.email ?? ""} initialToken={params.token ?? ""} />;
}
