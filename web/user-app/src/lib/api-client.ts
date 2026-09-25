import { useAuthStore } from "@/store/auth-store";

// Exported so a page that needs a plain, non-fetch URL (like a report export a
// browser downloads directly with window.open) can build one against the same base.
export const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5001/api/v1";

// This is what we throw whenever the API says something went wrong. It carries the
// status code and, when the problem is bad input, the field-by-field errors too, so a
// form can show them next to the right input.
export class ApiError extends Error {
  status: number;
  fieldErrors?: Record<string, string[]>;

  constructor(status: number, message: string, fieldErrors?: Record<string, string[]>) {
    super(message);
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

type ApiFetchOptions = {
  method?: "GET" | "POST" | "PATCH" | "DELETE";
  body?: unknown;
  token?: string | null;
};

// This is the one place in the app that knows how to call the Natoshare API. Every
// page should call this instead of using fetch directly, so headers and errors are
// only handled in one spot, not copied into every form.
export async function apiFetch<T>(path: string, options: ApiFetchOptions = {}): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    method: options.method ?? "GET",
    headers: {
      "Content-Type": "application/json",
      ...(options.token ? { Authorization: `Bearer ${options.token}` } : {}),
    },
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  });

  // 204 No Content never has a body, do not try to parse one.
  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  const data = text ? JSON.parse(text) : undefined;

  if (!response.ok) {
    // A 401 on a call that sent a token means the session itself is no good
    // anymore (expired, or revoked by a logout elsewhere), not "bad input", so
    // there is nothing a form on the current page can usefully do with it. A 401
    // with no token, like a wrong password on /auth/login, is a completely
    // different thing (bad credentials, not a dead session), and must not trigger
    // this, or a failed login attempt would bounce someone off their own login
    // form. `proxy.ts` stops most of this before a protected page even loads, but
    // a token can still expire while someone is already sitting on one, this is
    // what catches that: sign them out for real (clearing the store and the
    // cookie proxy.ts reads) instead of leaving a stale "logged in" header up
    // over a page that cannot actually load anything anymore.
    if (response.status === 401 && options.token) {
      useAuthStore.getState().clearSession();
      if (typeof window !== "undefined") {
        // A full reload, not router.push: this file is a plain module, not a
        // component, so there is no router instance to call here. A hard
        // navigation is also the right call anyway, it throws away every other
        // component's in-memory state along with the dead session instead of
        // leaving some of it behind for a client-side transition to trip over.
        // eslint-disable-next-line @next/next/no-location-assign-relative-destination
        window.location.href = "/login";
      }
    }

    const message = data?.title ?? "Something went wrong. Please try again.";
    throw new ApiError(response.status, message, data?.errors);
  }

  return data as T;
}
