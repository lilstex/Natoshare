import type { Metadata } from "next";
import { DashboardScreen } from "./dashboard-screen";

// A logged-in user's own money, nothing here should show up in search results.
export const metadata: Metadata = {
  title: "Dashboard",
  robots: { index: false, follow: false },
};

export default function DashboardPage() {
  return <DashboardScreen />;
}
