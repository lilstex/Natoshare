import { redirect } from "next/navigation";

// The dashboard's own AdminShell guard sends anyone not logged in straight to
// /login, so it is always safe to point the bare root here.
export default function Home() {
  redirect("/dashboard");
}
