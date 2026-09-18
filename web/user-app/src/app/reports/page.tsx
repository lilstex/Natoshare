import type { Metadata } from "next";
import { ReportsScreen } from "./reports-screen";

export const metadata: Metadata = {
  title: "Reports",
  robots: { index: false, follow: false },
};

export default function ReportsPage() {
  return <ReportsScreen />;
}
