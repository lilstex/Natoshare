import type { Metadata } from "next";
import { SignupForm } from "./signup-form";

export const metadata: Metadata = {
  title: "Create your account",
  description: "Split your income into categories, track your spending, and see what you saved, in any currency.",
};

export default function SignupPage() {
  return <SignupForm />;
}
