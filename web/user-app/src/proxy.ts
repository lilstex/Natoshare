import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

// The pages someone should not see if they are already logged in.
const AUTH_ONLY_ROUTES = ["/login", "/signup", "/forgot-password", "/reset-password"];

// This runs before a page loads, so we can send a logged-in user away from the login
// page without a flash of it first. Real "you must be logged in" protection for pages
// like /dashboard gets added once those pages exist, from Phase 2 onward.
export function proxy(request: NextRequest) {
  const isLoggedIn = Boolean(request.cookies.get("natoshare-token")?.value);
  const { pathname } = request.nextUrl;

  if (isLoggedIn && AUTH_ONLY_ROUTES.includes(pathname)) {
    return NextResponse.redirect(new URL("/", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/login", "/signup", "/forgot-password", "/reset-password"],
};
