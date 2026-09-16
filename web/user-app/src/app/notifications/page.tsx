import type { Metadata } from "next";
import { NotificationsScreen } from "./notifications-screen";

// A logged-in user's own notifications, nothing here should show up in search results.
export const metadata: Metadata = {
  title: "Notifications",
  robots: { index: false, follow: false },
};

export default function NotificationsPage() {
  return <NotificationsScreen />;
}
