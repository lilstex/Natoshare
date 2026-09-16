// Turns a plain number into money the way the user's own currency and locale expect
// it to look, for example 125000 becomes "₦125,000.00" for an NGN/en-NG account or
// "€1,200.00" for a EUR/de-DE account. We never hardcode a currency symbol ourselves,
// Intl already knows how every currency is supposed to be written.
export function formatMoney(amount: number, currencyCode: string, locale: string): string {
  try {
    return new Intl.NumberFormat(locale || "en", {
      style: "currency",
      currency: currencyCode || "USD",
    }).format(amount);
  } catch {
    // A locale or currency code that Intl does not recognise should not crash the
    // page, it should just fall back to something plain.
    return `${currencyCode} ${amount.toFixed(2)}`;
  }
}

// Gives us every IANA timezone name the browser knows about, for the timezone picker.
// Older browsers do not have Intl.supportedValuesOf, so we fall back to a short list
// of common zones covering Africa, Europe and the Americas rather than an empty box.
export function getSupportedTimeZones(): string[] {
  const withSupportedValues = Intl as unknown as {
    supportedValuesOf?: (key: string) => string[];
  };

  if (typeof withSupportedValues.supportedValuesOf === "function") {
    return withSupportedValues.supportedValuesOf("timeZone");
  }

  return [
    "Africa/Lagos",
    "Africa/Accra",
    "Africa/Nairobi",
    "Africa/Johannesburg",
    "Europe/London",
    "Europe/Berlin",
    "Europe/Paris",
    "America/New_York",
    "America/Chicago",
    "America/Los_Angeles",
    "Asia/Dubai",
    "UTC",
  ];
}

// Works out "what month is it right now" for a given timezone, as plain year/month
// numbers the way the API expects. This is how the onboarding wizard knows which
// month the very first income split should start from, in the user's own timezone,
// not the server's.
export function currentMonthInTimeZone(timeZoneId: string): { year: number; month: number } {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: timeZoneId,
    year: "numeric",
    month: "numeric",
  }).formatToParts(new Date());

  const year = Number(parts.find((p) => p.type === "year")?.value);
  const month = Number(parts.find((p) => p.type === "month")?.value);

  return { year, month };
}
