import type { Metadata } from "next";
import { OnboardingWizard } from "./onboarding-wizard";

// This page only makes sense to someone who is already logged in and mid-signup,
// there is nothing here for a search engine to send a stranger to.
export const metadata: Metadata = {
  title: "Set up your account",
  robots: { index: false, follow: false },
};

export default function OnboardingPage() {
  return <OnboardingWizard />;
}
