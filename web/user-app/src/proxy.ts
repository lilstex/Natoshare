import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

// The pages someone should not see if they are already logged in.
const AUTH_ONLY_ROUTES = ["/login", "/signup", "/forgot-password", "/reset-password"];

// The pages that need a login. Whether onboarding itself is finished is checked inside
// the page, not here, the cookie only tells us "someone is logged in on this device".
const LOGIN_REQUIRED_ROUTES = ["/onboarding", "/categories", "/dashboard", "/notifications", "/close-month"];

// This runs before a page loads, so we can send a logged-in user away from the login
// page, and send a logged-out visitor away from pages that need an account, without a
// flash of the wrong page first.
export function proxy(request: NextRequest) {
  const isLoggedIn = Boolean(request.cookies.get("natoshare-token")?.value);
  const { pathname } = request.nextUrl;

  if (isLoggedIn && AUTH_ONLY_ROUTES.includes(pathname)) {
    return NextResponse.redirect(new URL("/", request.url));
  }

  if (!isLoggedIn && LOGIN_REQUIRED_ROUTES.includes(pathname)) {
    return NextResponse.redirect(new URL("/login", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    "/login", "/signup", "/forgot-password", "/reset-password", "/onboarding", "/categories", "/dashboard", "/notifications", "/close-month",
  ],
};
