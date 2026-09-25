import type { Metadata } from "next";
import { InvestmentsScreen } from "./investments-screen";

export const metadata: Metadata = {
  title: "Investment log",
  robots: { index: false, follow: false },
};

export default function InvestmentsPage() {
  return <InvestmentsScreen />;
}
