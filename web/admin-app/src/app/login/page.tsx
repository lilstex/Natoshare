import type { Metadata } from "next";
import { LoginForm } from "./login-form";

// The whole admin app is noindex already (see the root layout), this page does not
// need anything extra on top of that.
export const metadata: Metadata = {
  title: "Log in",
};

export default function LoginPage() {
  return <LoginForm />;
}
