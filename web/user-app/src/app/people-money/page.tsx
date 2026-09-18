import type { Metadata } from "next";
import { PeopleMoneyScreen } from "./people-money-screen";

// Loans, debts and promises are a user's own private records, none of this should
// show up in search results.
export const metadata: Metadata = {
  title: "Loans, debts & promises",
  robots: { index: false, follow: false },
};

export default function PeopleMoneyPage() {
  return <PeopleMoneyScreen />;
}
