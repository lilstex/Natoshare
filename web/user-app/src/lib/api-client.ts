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
    const message = data?.title ?? "Something went wrong. Please try again.";
    throw new ApiError(response.status, message, data?.errors);
  }

  return data as T;
}
