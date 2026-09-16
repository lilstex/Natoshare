import type { Metadata } from "next";
import { CategoriesScreen } from "./categories-screen";

// A settings-style page behind login, nothing here should show up in search results.
export const metadata: Metadata = {
  title: "Categories",
  robots: { index: false, follow: false },
};

export default function CategoriesPage() {
  return <CategoriesScreen />;
}
