import type { Metadata } from "next";
import { CloseMonthScreen } from "./close-month-screen";

// The close-month ritual, only makes sense to a logged-in user closing their own
// month, nothing here should show up in search results.
export const metadata: Metadata = {
  title: "Close month",
  robots: { index: false, follow: false },
};

export default function CloseMonthPage() {
  return <CloseMonthScreen />;
}
