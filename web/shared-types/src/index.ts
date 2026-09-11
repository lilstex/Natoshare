// This package holds TypeScript types that both the user app and the admin app need.
// We write them once here instead of copying the same type into both apps.
// Real types get added as we build each feature (see docs/02-api-surface.md for the
// shape of every endpoint response).

// Every money amount coming back from the API is a plain decimal string with 2 decimal
// places, for example "125000.00". It is never negative, a shortfall shows up as a
// separate "deficit" field instead. Always format this with Intl.NumberFormat and the
// account's own currency and locale, do not just print the raw string.
export type MoneyAmount = string;

// This is the currency, timezone and locale a Natoshare account is set up with.
// It comes back from GET /me and drives how we format every money amount, date and
// number in both apps.
export type AccountLocale = {
  currencyCode: string;
  currencySymbol: string;
  timeZoneId: string;
  locale: string;
};
