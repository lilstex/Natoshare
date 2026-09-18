"use client";

import { useEffect, useState } from "react";
import type { ObligationItem } from "@natoshare/shared-types";
import { SiteHeader } from "@/components/site-header";
import { apiFetch, ApiError } from "@/lib/api-client";
import { formatMoney } from "@/lib/format";
import { useAuthStore } from "@/store/auth-store";

type MeResponse = { user: { currencyCode: string; locale: string } };

const TYPE_LABELS: Record<string, string> = {
  DebtDue: "Debt due",
  LoanReturn: "Loan expected back",
  PromiseReminder: "Promise",
  RecurringItem: "Recurring item",
  FixedAccountConfirm: "Confirm transfer",
  MonthClose: "Month needs closing",
  CarriedDeficit: "Carried deficit",
};

const SEVERITY_STYLES: Record<string, string> = {
  Info: "bg-surface-2 text-subtle",
  Warning: "bg-warning-tint text-warning",
  Critical: "bg-danger-tint text-danger",
};

// A calendar of upcoming dates worth keeping an eye on: debts and loans coming due,
// promises still open, and anything month-lifecycle related still needing
// attention. See docs/02-api-surface.md section 11.
export function ObligationsScreen() {
  const accessToken = useAuthStore((state) => state.accessToken);
  const [accountLocale, setAccountLocale] = useState({ currencyCode: "USD", locale: "en" });
  const [items, setItems] = useState<ObligationItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    apiFetch<MeResponse>("/me", { token: accessToken })
      .then((me) => setAccountLocale({ currencyCode: me.user.currencyCode, locale: me.user.locale }))
      .catch(() => {});

    apiFetch<ObligationItem[]>("/obligations?days=30", { token: accessToken })
      .then(setItems)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not load your obligations."))
      .finally(() => setIsLoading(false));
  }, [accessToken]);

  return (
    <div>
      <SiteHeader />

      <main className="mx-auto max-w-3xl px-4 pb-16">
        <h1 className="text-2xl font-bold text-text">Obligations, next 30 days</h1>

        {error && <p className="mt-4 rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

        {isLoading ? (
          <p className="mt-8 text-sm text-muted">Loading…</p>
        ) : items.length === 0 ? (
          <p className="mt-8 text-sm text-muted">Nothing coming up, you are all clear.</p>
        ) : (
          <div className="mt-5 flex flex-col gap-2">
            {items.map((item, index) => (
              <div key={index} className="flex items-center justify-between rounded-xl border border-border bg-surface px-4 py-3">
                <div>
                  <div className="flex items-center gap-2">
                    <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${SEVERITY_STYLES[item.severity]}`}>
                      {TYPE_LABELS[item.type] ?? item.type}
                    </span>
                    <span className="text-xs text-muted">{item.date}</span>
                  </div>
                  <p className="mt-1 text-sm text-text">{item.title}</p>
                </div>
                {item.amount !== null && (
                  <span className="text-sm font-semibold text-text">{formatMoney(item.amount, accountLocale.currencyCode, accountLocale.locale)}</span>
                )}
              </div>
            ))}
          </div>
        )}
      </main>
    </div>
  );
}
