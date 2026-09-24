"use client";

import { useEffect, useState } from "react";
import type { CreateInvestmentLogRequest, InvestmentLog, InvestmentSummary } from "@natoshare/shared-types";
import { SiteHeader } from "@/components/site-header";
import { Button } from "@/components/ui/button";
import { TrendingUpIcon } from "@/components/ui/icons";
import { TextField } from "@/components/ui/text-field";
import { apiFetch, ApiError } from "@/lib/api-client";
import { formatMoney } from "@/lib/format";
import { useAuthStore } from "@/store/auth-store";

type MeResponse = { user: { currencyCode: string; locale: string } };

// "2026-09-24" -> "Sep 24, 2026", a log can span years, so unlike the dashboard's
// "next 7 days" list this always shows the year.
function formatLogDate(isoDate: string, locale: string): string {
  const date = new Date(`${isoDate}T00:00:00`);
  if (Number.isNaN(date.getTime())) {
    return isoDate;
  }
  return date.toLocaleDateString(locale || "en", { month: "short", day: "numeric", year: "numeric" });
}

// A lightweight log of money actually invested somewhere real, compared against
// what the Investment category was funded with this month. See
// docs/01-domain-model.md section 1.6, InvestmentLog is deliberately separate from
// the category's own ledger movement.
export function InvestmentsScreen() {
  const accessToken = useAuthStore((state) => state.accessToken);
  const [accountLocale, setAccountLocale] = useState({ currencyCode: "USD", locale: "en" });
  const [logs, setLogs] = useState<InvestmentLog[]>([]);
  const [summary, setSummary] = useState<InvestmentSummary | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function load() {
    const now = new Date();
    Promise.all([
      apiFetch<InvestmentLog[]>("/investments", { token: accessToken }),
      apiFetch<InvestmentSummary>(`/investments/summary?year=${now.getFullYear()}&month=${now.getMonth() + 1}`, { token: accessToken }),
    ])
      .then(([logsResult, summaryResult]) => {
        setLogs(logsResult);
        setSummary(summaryResult);
      })
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not load your investment log."));
  }

  useEffect(() => {
    if (!accessToken) {
      return;
    }
    apiFetch<MeResponse>("/me", { token: accessToken })
      .then((me) => setAccountLocale({ currencyCode: me.user.currencyCode, locale: me.user.locale }))
      .catch(() => {});
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken]);

  async function handleDelete(id: string) {
    try {
      await apiFetch(`/investments/${id}`, { method: "DELETE", token: accessToken });
      load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not remove that entry.");
    }
  }

  return (
    <div>
      <SiteHeader />

      <main className="px-4 pb-16 pt-6 sm:px-6 lg:px-10 xl:px-16 2xl:px-24">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <h1 className="text-2xl font-bold text-text">Investment log</h1>
          <Button type="button" onClick={() => setShowForm((v) => !v)} className="w-full sm:w-auto">
            {showForm ? "Cancel" : "Log an investment"}
          </Button>
        </div>

        {error && <p className="mt-4 rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

        {summary && (
          <div className="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-3">
            <SummaryTile label="Allocated this month" amount={summary.allocated} currencyCode={accountLocale.currencyCode} locale={accountLocale.locale} />
            <SummaryTile label="Actually invested" amount={summary.invested} currencyCode={accountLocale.currencyCode} locale={accountLocale.locale} />
            <SummaryTile label="Shortfall" amount={summary.shortfall} currencyCode={accountLocale.currencyCode} locale={accountLocale.locale} warn={summary.shortfall > 0} />
          </div>
        )}

        {showForm && (
          <NewInvestmentForm
            accessToken={accessToken}
            onDone={() => {
              setShowForm(false);
              load();
            }}
            onCancel={() => setShowForm(false)}
          />
        )}

        <h2 className="mt-8 text-lg font-semibold text-text">History</h2>
        <div className="mt-3 flex flex-col gap-2">
          {logs.length === 0 && (
            <p className="rounded-xl border border-dashed border-border-strong px-4 py-8 text-center text-sm text-muted">
              Nothing logged yet.
            </p>
          )}
          {logs.map((log) => (
            <div
              key={log.id}
              className="flex items-center gap-3 rounded-xl border border-border bg-gradient-to-br from-surface to-primary-tint px-4 py-3 shadow-sm transition-shadow hover:shadow-md"
            >
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-success-tint text-success">
                <TrendingUpIcon size={17} />
              </span>
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-text">{log.platform}</p>
                <p className="text-xs text-muted">{formatLogDate(log.investedOn, accountLocale.locale)}</p>
              </div>
              <div className="flex shrink-0 items-center gap-3">
                <span className="text-sm font-semibold text-text">{formatMoney(log.amount, accountLocale.currencyCode, accountLocale.locale)}</span>
                <button
                  type="button"
                  onClick={() => handleDelete(log.id)}
                  className="rounded-md px-2 py-1 text-xs font-medium text-muted transition-colors hover:bg-danger-tint hover:text-danger"
                >
                  Remove
                </button>
              </div>
            </div>
          ))}
        </div>
      </main>
    </div>
  );
}

function SummaryTile({
  label,
  amount,
  currencyCode,
  locale,
  warn,
}: {
  label: string;
  amount: number;
  currencyCode: string;
  locale: string;
  warn?: boolean;
}) {
  return (
    <div className="rounded-xl border border-border bg-gradient-to-br from-surface to-primary-tint p-4">
      <p className="text-xs font-medium text-muted">{label}</p>
      <p className={`mt-1 font-display text-lg font-bold ${warn ? "text-warning" : "text-text"}`}>{formatMoney(amount, currencyCode, locale)}</p>
    </div>
  );
}

function NewInvestmentForm({ accessToken, onDone, onCancel }: { accessToken: string | null; onDone: () => void; onCancel: () => void }) {
  const [amount, setAmount] = useState("");
  const [platform, setPlatform] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit() {
    if (!amount || Number(amount) <= 0 || !platform.trim()) {
      setError("Enter an amount and where it was invested.");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    const request: CreateInvestmentLogRequest = {
      amount: Number(amount),
      investedOn: new Date().toISOString().slice(0, 10),
      platform: platform.trim(),
      note: null,
    };

    try {
      await apiFetch("/investments", { method: "POST", token: accessToken, body: request });
      onDone();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not log that investment.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="mt-4 rounded-xl border border-border bg-gradient-to-br from-surface to-primary-tint p-4">
      <p className="text-sm font-semibold text-text">Log an investment</p>
      <div className="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-2">
        <TextField label="Amount" type="number" min="0" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
        <TextField label="Platform" placeholder="PiggyVest, Cowrywise…" value={platform} onChange={(e) => setPlatform(e.target.value)} />
      </div>
      {error && <p className="mt-2 text-sm text-danger">{error}</p>}
      <div className="mt-3 flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="button" onClick={handleSubmit} disabled={isSubmitting}>
          {isSubmitting ? "Saving…" : "Log investment"}
        </Button>
      </div>
    </div>
  );
}
