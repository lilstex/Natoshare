import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

// The pages someone should not see if they are already logged in.
const AUTH_ONLY_ROUTES = ["/login", "/signup", "/forgot-password", "/reset-password"];

// The pages that need a login. Whether onboarding itself is finished is checked inside
// the page, not here, the cookie only tells us "someone is logged in on this device".
const LOGIN_REQUIRED_ROUTES = [
  "/onboarding", "/categories", "/dashboard", "/notifications", "/close-month",
  "/people-money", "/investments", "/obligations", "/recurring", "/reports",
];

// A JWT's middle segment is a base64url JSON payload with an "exp" claim (seconds
// since epoch). We only read it here to decide whether to bother showing someone a
// protected page at all, this is not what actually protects anyone's data, the API
// checks the token's real signature and expiry on every request regardless, and
// returns its own 401 if this ever disagrees with it. That real 401 is what
// api-client.ts listens for to log someone out for good, this is just so an
// obviously-expired session does not get a page to look logged in on for even a
// moment before that happens.
function isExpired(token: string): boolean {
  try {
    const payload = token.split(".")[1];
    const json = atob(payload.replace(/-/g, "+").replace(/_/g, "/"));
    const exp = JSON.parse(json).exp;
    return typeof exp !== "number" || Date.now() >= exp * 1000;
  } catch {
    return true;
  }
}

// This runs before a page loads, so we can send a logged-in user away from the login
// page, and send a logged-out visitor (or one whose token has since expired) away
// from pages that need an account, without a flash of the wrong page first.
export function proxy(request: NextRequest) {
  const token = request.cookies.get("natoshare-token")?.value;
  const isLoggedIn = Boolean(token) && !isExpired(token!);
  const { pathname } = request.nextUrl;

  if (isLoggedIn && AUTH_ONLY_ROUTES.includes(pathname)) {
    return NextResponse.redirect(new URL("/", request.url));
  }

  if (!isLoggedIn && LOGIN_REQUIRED_ROUTES.includes(pathname)) {
    const response = NextResponse.redirect(new URL("/login", request.url));
    if (token) {
      // It was there but expired, clear it so this check is cheap (and honest)
      // next time instead of re-decoding a dead token on every request.
      response.cookies.delete("natoshare-token");
    }
    return response;
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    "/login", "/signup", "/forgot-password", "/reset-password", "/onboarding", "/categories", "/dashboard", "/notifications", "/close-month",
    "/people-money", "/investments", "/obligations", "/recurring", "/reports",
  ],
};
