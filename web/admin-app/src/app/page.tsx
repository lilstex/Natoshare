import { redirect } from "next/navigation";

// There is nothing to see at the root of the admin app yet, so we send people
// straight to the login page. The real dashboard comes in Phase 10.
export default function Home() {
  redirect("/login");
}
