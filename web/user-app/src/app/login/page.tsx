import type { Metadata } from "next";
import { LoginForm } from "./login-form";

// This page does not need a login itself (it is how you get one), so unlike the
// dashboard and other pages behind auth, this one is fine to leave indexable.
export const metadata: Metadata = {
  title: "Log in",
  description: "Log in to your Natoshare account.",
};

export default function LoginPage() {
  return <LoginForm />;
}
