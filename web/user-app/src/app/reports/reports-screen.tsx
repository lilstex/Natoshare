"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import type { AnnualReport, MonthlyReport, PlanEntitlements, RangeReport } from "@natoshare/shared-types";
import { SiteHeader } from "@/components/site-header";
import { Button } from "@/components/ui/button";
import { SelectField } from "@/components/ui/select-field";
import { API_URL, apiFetch, ApiError } from "@/lib/api-client";
import { formatMoney } from "@/lib/format";
import { useAuthStore } from "@/store/auth-store";

type MeResponse = { user: { currencyCode: string; locale: string } };
type Scope = "month" | "range" | "annual";

const MONTH_NAMES = [
  "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December",
];

const STATUS_STYLES: Record<string, string> = {
  Over: "bg-danger-tint text-danger",
  Under: "bg-success-tint text-success",
  Unused: "bg-surface-2 text-subtle",
};

const STATUS_BAR_COLORS: Record<string, string> = {
  Over: "var(--nato-danger, #e0526b)",
  Under: "var(--nato-primary, #6d4ae0)",
  Unused: "var(--nato-border-strong, #c9c5db)",
};

// This month / Range / Year, each backed by its own read-only /reports endpoint,
// plus CSV/PDF export via a direct browser download. See docs/06-design-system.md's
// "Reports" screen pattern.
export function ReportsScreen() {
  const accessToken = useAuthStore((state) => state.accessToken);
  const [accountLocale, setAccountLocale] = useState({ currencyCode: "USD", locale: "en" });
  const [entitlements, setEntitlements] = useState<PlanEntitlements | null>(null);

  const today = new Date();
  const [scope, setScope] = useState<Scope>("month");
  const [year, setYear] = useState(today.getFullYear());
  const [month, setMonth] = useState(today.getMonth() + 1);
  const [fromYear, setFromYear] = useState(today.getFullYear());
  const [fromMonth, setFromMonth] = useState(1);
  const [toYear, setToYear] = useState(today.getFullYear());
  const [toMonth, setToMonth] = useState(today.getMonth() + 1);

  const [months, setMonths] = useState<MonthlyReport[]>([]);
  const [totals, setTotals] = useState<{ totalBudget: number; totalActual: number; totalSaved: number; overallSavingsRate: number } | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!accessToken) {
      return;
    }
    apiFetch<MeResponse>("/me", { token: accessToken })
      .then((me) => setAccountLocale({ currencyCode: me.user.currencyCode, locale: me.user.locale }))
      .catch(() => {});
    apiFetch<PlanEntitlements>("/me/entitlements", { token: accessToken })
      .then(setEntitlements)
      .catch(() => {});
  }, [accessToken]);

  async function loadReport() {
    if (!accessToken) {
      return;
    }

    setIsLoading(true);
    setError(null);

    const url =
      scope === "month"
        ? `/reports/monthly?year=${year}&month=${month}`
        : scope === "range"
          ? `/reports/range?fromYear=${fromYear}&fromMonth=${fromMonth}&toYear=${toYear}&toMonth=${toMonth}`
          : `/reports/annual?year=${year}`;

    try {
      const result = await apiFetch<MonthlyReport | RangeReport | AnnualReport>(url, { token: accessToken });

      if (scope === "month") {
        const single = result as MonthlyReport;
        setMonths([single]);
        setTotals({
          totalBudget: single.totalBudget,
          totalActual: single.totalActual,
          totalSaved: single.totalSaved,
          overallSavingsRate: single.overallSavingsRate,
        });
      } else {
        const multi = result as RangeReport | AnnualReport;
        setMonths(multi.months.filter((m) => m.categories.length > 0));
        setTotals({
          totalBudget: multi.totalBudget,
          totalActual: multi.totalActual,
          totalSaved: multi.totalSaved,
          overallSavingsRate: multi.overallSavingsRate,
        });
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not load that report.");
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    // loadReport sets isLoading/error itself before its first await, the same
    // fetch-on-mount shape dashboard-screen.tsx and close-month-screen.tsx already
    // use without issue, this rule just does not see through the function call.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadReport();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken, scope, year, month, fromYear, fromMonth, toYear, toMonth]);

  function exportUrl(format: "csv" | "pdf"): string {
    const params = new URLSearchParams({ scope, format, accessToken: accessToken ?? "" });
    if (scope === "month") {
      params.set("year", String(year));
      params.set("month", String(month));
    } else if (scope === "range") {
      params.set("fromYear", String(fromYear));
      params.set("fromMonth", String(fromMonth));
      params.set("toYear", String(toYear));
      params.set("toMonth", String(toMonth));
    } else {
      params.set("year", String(year));
    }
    return `${API_URL}/reports/export?${params.toString()}`;
  }

  return (
    <div>
      <SiteHeader />

      <main className="mx-auto max-w-4xl px-4 pb-16">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold text-text">Reports</h1>
          {entitlements && !entitlements.export ? (
            <Link
              href="/plans"
              className="inline-flex h-11 items-center justify-center rounded-full bg-warning-tint px-6 text-sm font-semibold text-warning hover:opacity-90"
            >
              Upgrade to export
            </Link>
          ) : (
            <div className="flex gap-2">
              <Button variant="secondary" onClick={() => window.open(exportUrl("csv"), "_blank")}>
                Export CSV
              </Button>
              <Button onClick={() => window.open(exportUrl("pdf"), "_blank")}>Export PDF</Button>
            </div>
          )}
        </div>

        {/* Segmented control, see docs/06-design-system.md section 5. */}
        <div className="mt-4 inline-flex rounded-full bg-surface-2 p-1">
          {(["month", "range", "annual"] as Scope[]).map((s) => (
            <button
              key={s}
              type="button"
              onClick={() => setScope(s)}
              className={`rounded-full px-4 py-1.5 text-sm font-semibold transition-colors ${
                scope === s ? "bg-surface text-text shadow-sm" : "text-muted"
              }`}
            >
              {s === "month" ? "This month" : s === "range" ? "Range" : "Year"}
            </button>
          ))}
        </div>

        <div className="mt-3 flex flex-wrap gap-2">
          {scope === "month" && (
            <>
              <SelectField label="Year" value={year} onChange={(e) => setYear(Number(e.target.value))} className="w-28">
                {yearOptions(today.getFullYear()).map((y) => (
                  <option key={y} value={y}>
                    {y}
                  </option>
                ))}
              </SelectField>
              <SelectField label="Month" value={month} onChange={(e) => setMonth(Number(e.target.value))} className="w-40">
                {MONTH_NAMES.map((name, index) => (
                  <option key={name} value={index + 1}>
                    {name}
                  </option>
                ))}
              </SelectField>
            </>
          )}

          {scope === "range" && (
            <>
              <SelectField label="From year" value={fromYear} onChange={(e) => setFromYear(Number(e.target.value))} className="w-28">
                {yearOptions(today.getFullYear()).map((y) => (
                  <option key={y} value={y}>
                    {y}
                  </option>
                ))}
              </SelectField>
              <SelectField label="From month" value={fromMonth} onChange={(e) => setFromMonth(Number(e.target.value))} className="w-40">
                {MONTH_NAMES.map((name, index) => (
                  <option key={name} value={index + 1}>
                    {name}
                  </option>
                ))}
              </SelectField>
              <SelectField label="To year" value={toYear} onChange={(e) => setToYear(Number(e.target.value))} className="w-28">
                {yearOptions(today.getFullYear()).map((y) => (
                  <option key={y} value={y}>
                    {y}
                  </option>
                ))}
              </SelectField>
              <SelectField label="To month" value={toMonth} onChange={(e) => setToMonth(Number(e.target.value))} className="w-40">
                {MONTH_NAMES.map((name, index) => (
                  <option key={name} value={index + 1}>
                    {name}
                  </option>
                ))}
              </SelectField>
            </>
          )}

          {scope === "annual" && (
            <SelectField label="Year" value={year} onChange={(e) => setYear(Number(e.target.value))} className="w-28">
              {yearOptions(today.getFullYear()).map((y) => (
                <option key={y} value={y}>
                  {y}
                </option>
              ))}
            </SelectField>
          )}
        </div>

        {error && <p className="mt-4 rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

        {isLoading ? (
          <p className="mt-8 text-sm text-muted">Loading…</p>
        ) : (
          <>
            {totals && (
              <div className="mt-6 flex items-center gap-6 rounded-xl border border-border bg-surface p-4">
                <SavingsRing rate={totals.overallSavingsRate} />
                <div className="flex flex-col gap-1 text-sm">
                  <span className="text-text">
                    Budget {formatMoney(totals.totalBudget, accountLocale.currencyCode, accountLocale.locale)}
                  </span>
                  <span className="text-text">
                    Actual {formatMoney(totals.totalActual, accountLocale.currencyCode, accountLocale.locale)}
                  </span>
                  <span className="text-success">
                    Saved {formatMoney(totals.totalSaved, accountLocale.currencyCode, accountLocale.locale)}
                  </span>
                </div>
              </div>
            )}

            {months.length === 0 && <p className="mt-8 text-sm text-muted">Nothing to report for this period yet.</p>}

            {months.map((monthReport) => (
              <div key={`${monthReport.year}-${monthReport.month}`} className="mt-6">
                <h2 className="text-lg font-semibold text-text">
                  {MONTH_NAMES[monthReport.month - 1]} {monthReport.year}
                  {!monthReport.isClosed && <span className="ml-2 text-xs font-normal text-muted">still open</span>}
                </h2>

                <div className="mt-3 overflow-x-auto rounded-xl border border-border bg-surface">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-border text-left text-xs font-medium text-muted">
                        <th className="px-4 py-2">Category</th>
                        <th className="px-4 py-2">Budget vs actual</th>
                        <th className="px-4 py-2">Saved</th>
                        <th className="px-4 py-2">Variance</th>
                        <th className="px-4 py-2">Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {monthReport.categories.map((line) => {
                        const fillPercent = line.budget > 0 ? Math.min(100, (line.actual / line.budget) * 100) : line.actual > 0 ? 100 : 0;
                        return (
                          <tr key={line.categoryId} className="border-b border-border last:border-0">
                            <td className="px-4 py-2.5 font-medium text-text">{line.categoryName}</td>
                            <td className="px-4 py-2.5">
                              <div className="relative h-2 w-40 overflow-hidden rounded-full bg-surface-2">
                                <div
                                  className="h-full rounded-full"
                                  style={{ width: `${fillPercent}%`, backgroundColor: STATUS_BAR_COLORS[line.status] }}
                                />
                              </div>
                              <p className="mt-1 text-xs text-muted">
                                {formatMoney(line.actual, accountLocale.currencyCode, accountLocale.locale)} of{" "}
                                {formatMoney(line.budget, accountLocale.currencyCode, accountLocale.locale)}
                              </p>
                            </td>
                            <td className="px-4 py-2.5 text-success">{formatMoney(line.saved, accountLocale.currencyCode, accountLocale.locale)}</td>
                            <td className="px-4 py-2.5">{formatMoney(line.variance, accountLocale.currencyCode, accountLocale.locale)}</td>
                            <td className="px-4 py-2.5">
                              <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${STATUS_STYLES[line.status]}`}>
                                {line.status}
                              </span>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>

                {monthReport.deficitsAndCoverage.length > 0 && (
                  <div className="mt-3 rounded-xl border border-border bg-surface p-4">
                    <p className="text-sm font-semibold text-text">Deficits & coverage</p>
                    <ul className="mt-2 flex flex-col gap-1 text-sm text-muted">
                      {monthReport.deficitsAndCoverage.map((line, index) => (
                        <li key={index}>
                          {line.categoryName}: {formatMoney(line.amount, accountLocale.currencyCode, accountLocale.locale)} via {line.method}
                          {line.carriedForward ? " (carried to next month)" : ""}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            ))}
          </>
        )}
      </main>
    </div>
  );
}

function yearOptions(currentYear: number): number[] {
  return Array.from({ length: 6 }, (_, i) => currentYear - 4 + i);
}

// A simple donut built from a conic-gradient, no chart library needed for one ring.
function SavingsRing({ rate }: { rate: number }) {
  const percent = Math.round(Math.max(0, Math.min(1, rate)) * 100);

  return (
    <div
      className="relative flex h-20 w-20 shrink-0 items-center justify-center rounded-full"
      style={{
        background: `conic-gradient(var(--nato-primary, #6d4ae0) ${percent}%, var(--nato-surface-2, #ece9f7) 0)`,
      }}
    >
      <div className="flex h-14 w-14 items-center justify-center rounded-full bg-surface text-sm font-bold text-text">{percent}%</div>
    </div>
  );
}
