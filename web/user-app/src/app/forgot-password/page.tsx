import type { Metadata } from "next";
import { ForgotPasswordForm } from "./forgot-password-form";

// This page only matters to someone who is already trying to get back into their own
// account, there is nothing here worth a search engine sending strangers to.
export const metadata: Metadata = {
  title: "Reset your password",
  robots: { index: false, follow: false },
};

export default function ForgotPasswordPage() {
  return <ForgotPasswordForm />;
}
