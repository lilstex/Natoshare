import type { Metadata } from "next";
import { ObligationsScreen } from "./obligations-screen";

export const metadata: Metadata = {
  title: "Obligations",
  robots: { index: false, follow: false },
};

export default function ObligationsPage() {
  return <ObligationsScreen />;
}
