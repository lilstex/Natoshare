import type { Metadata } from "next";
import { RecurringItemsScreen } from "./recurring-items-screen";

// A user's own recurring items, nothing here should show up in search results.
export const metadata: Metadata = {
  title: "Recurring items",
  robots: { index: false, follow: false },
};

export default function RecurringItemsPage() {
  return <RecurringItemsScreen />;
}
