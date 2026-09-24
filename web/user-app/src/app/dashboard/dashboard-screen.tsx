"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import type {
  CategoryBalance,
  Dashboard,
  DeficitListItem,
  IncomeType,
  LogExpenseResult,
  PlanEntitlements,
  ResolveDeficitRequest,
  SpendingSummary,
  SpendingTrend,
} from "@natoshare/shared-types";
import { SEGMENT_COLORS } from "@/components/split-ring";
import { SiteHeader } from "@/components/site-header";
import { Button } from "@/components/ui/button";
import { CalendarClockIcon, LightbulbIcon, MinusIcon, TrendingDownIcon, TrendingUpIcon } from "@/components/ui/icons";
import { apiFetch, ApiError } from "@/lib/api-client";
import { formatMoney } from "@/lib/format";
import { useAuthStore } from "@/store/auth-store";

type MeResponse = { user: { currencyCode: string; locale: string } };

const STATUS_STYLES: Record<string, string> = {
  OnTrack: "bg-success-tint text-success",
  OverPace: "bg-warning-tint text-warning",
  InDeficit: "bg-danger-tint text-danger",
  NotApplicable: "bg-surface-2 text-subtle",
};

const OBLIGATION_LABELS: Record<string, string> = {
  DebtDue: "Debt due",
  LoanReturn: "Loan expected back",
  PromiseReminder: "Promise",
  RecurringItem: "Recurring item",
  FixedAccountConfirm: "Confirm transfer",
  MonthClose: "Month needs closing",
  CarriedDeficit: "Carried deficit",
};

// Matches obligations-screen.tsx's own severity colours, so an item reads the
// same way whether you see it in this dashboard preview or the full calendar.
const OBLIGATION_SEVERITY_STYLES: Record<string, string> = {
  Info: "bg-surface-2 text-subtle",
  Warning: "bg-warning-tint text-warning",
  Critical: "bg-danger-tint text-danger",
};

// "2026-09-30" -> "Sep 30", so the list reads at a glance instead of like raw data.
function formatObligationDate(isoDate: string, locale: string): string {
  const date = new Date(`${isoDate}T00:00:00`);
  if (Number.isNaN(date.getTime())) {
    return isoDate;
  }
  return date.toLocaleDateString(locale || "en", { month: "short", day: "numeric" });
}

// The screen a logged-in user lands on: a hero net-position card, every category's
// current standing, quick forms to log income or an expense, a way to resolve a
// deficit, and what needs attention in the next 7 days, all from the one
// /dashboard aggregate endpoint (matching docs/06-design-system.md's dashboard
// screen pattern) plus the plain-English spending summary from /insights.
export function DashboardScreen() {
  const accessToken = useAuthStore((state) => state.accessToken);

  const [accountLocale, setAccountLocale] = useState({ currencyCode: "USD", locale: "en" });
  const [dashboard, setDashboard] = useState<Dashboard | null>(null);
  const [spendingSummary, setSpendingSummary] = useState<SpendingSummary | null>(null);
  const [entitlements, setEntitlements] = useState<PlanEntitlements | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [showIncomeForm, setShowIncomeForm] = useState(false);
  const [showExpenseForm, setShowExpenseForm] = useState(false);
  const [resolvingCategoryId, setResolvingCategoryId] = useState<string | null>(null);

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    apiFetch<MeResponse>("/me", { token: accessToken })
      .then((me) => setAccountLocale({ currencyCode: me.user.currencyCode, locale: me.user.locale }))
      .catch(() => {});

    loadAll();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken]);

  async function loadAll() {
    setIsLoading(true);
    try {
      const [dashboardResult, spendingSummaryResult, entitlementsResult] = await Promise.all([
        apiFetch<Dashboard>("/dashboard", { token: accessToken }),
        apiFetch<SpendingSummary>("/insights/spending-summary", { token: accessToken }),
        apiFetch<PlanEntitlements>("/me/entitlements", { token: accessToken }),
      ]);

      setDashboard(dashboardResult);
      setSpendingSummary(spendingSummaryResult);
      setEntitlements(entitlementsResult);
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not load your dashboard.");
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <div>
      <SiteHeader />

      <main className="px-4 pb-16 pt-6 sm:px-6 lg:px-10 xl:px-16 2xl:px-24">
        <div className="flex flex-col gap-4 sm:flex-row sm:flex-wrap sm:items-center sm:justify-between">
          <h1 className="text-2xl font-bold text-text">
            {dashboard ? `${monthName(dashboard.month.month)} ${dashboard.month.year}` : "Your money"}
          </h1>
          {/* A real grid below "sm", every button the same width and lined up in
              two clean columns, instead of a flex-wrap row that leaves the right
              edge ragged once labels are different lengths. From "sm" up there is
              room for the original pill row. */}
          <div className="grid grid-cols-2 gap-2 sm:flex sm:flex-wrap">
            <Link
              href="/people-money"
              className="col-span-2 inline-flex h-11 items-center justify-center rounded-md border border-border-strong bg-gradient-to-b from-surface to-surface-2 px-6 text-sm font-semibold text-text shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-md sm:col-span-1"
            >
              Loans, debts & promises
            </Link>
            <Link
              href="/obligations"
              className="inline-flex h-11 items-center justify-center rounded-md border border-border-strong bg-gradient-to-b from-surface to-surface-2 px-3 text-sm font-semibold text-text shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-md sm:px-6"
            >
              Obligations
            </Link>
            <Link
              href="/recurring"
              className="inline-flex h-11 items-center justify-center rounded-md border border-border-strong bg-gradient-to-b from-surface to-surface-2 px-3 text-sm font-semibold text-text shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-md sm:px-6"
            >
              Recurring
            </Link>
            <Link
              href="/reports"
              className="inline-flex h-11 items-center justify-center rounded-md border border-border-strong bg-gradient-to-b from-surface to-surface-2 px-3 text-sm font-semibold text-text shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-md sm:px-6"
            >
              Reports
            </Link>
            <Link
              href="/close-month"
              className="inline-flex h-11 items-center justify-center rounded-md border border-border-strong bg-gradient-to-b from-surface to-surface-2 px-3 text-sm font-semibold text-text shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-md sm:px-6"
            >
              Close month
            </Link>
            <Button type="button" variant="secondary" onClick={() => setShowExpenseForm((v) => !v)}>
              Log expense
            </Button>
            <Button type="button" onClick={() => setShowIncomeForm((v) => !v)}>
              Log income
            </Button>
          </div>
        </div>

        {error && <p className="mt-4 rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

        {entitlements?.isTrial && (
          <Link
            href="/plans"
            className="mt-4 flex items-center justify-between rounded-xl bg-primary-tint px-4 py-2.5 text-sm text-primary shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-md"
          >
            <span>Pro trial active until {new Date(entitlements.trialEndsAt).toLocaleDateString()}.</span>
            <span className="font-semibold">See plans →</span>
          </Link>
        )}

        {entitlements && !entitlements.isTrial && entitlements.plan === "Free" && (
          <Link
            href="/plans"
            className="mt-4 flex items-center justify-between rounded-xl bg-surface-2 px-4 py-2.5 text-sm text-muted shadow-sm transition-all hover:-translate-y-0.5 hover:text-text hover:shadow-md"
          >
            <span>You are on the Free plan, limited to {entitlements.maxCategories} categories and {entitlements.historyWindowDays} days of history.</span>
            <span className="font-semibold">Upgrade to Pro →</span>
          </Link>
        )}

        {dashboard && dashboard.unreadAlerts > 0 && (
          <Link
            href="/notifications"
            className="mt-4 flex items-center justify-between rounded-xl bg-warning-tint px-4 py-2.5 text-sm text-warning shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-md"
          >
            <span>
              {dashboard.unreadAlerts} unread alert{dashboard.unreadAlerts === 1 ? "" : "s"}
            </span>
            <span className="font-semibold">View →</span>
          </Link>
        )}

        {showIncomeForm && (
          <LogIncomeForm
            accessToken={accessToken}
            onDone={() => {
              setShowIncomeForm(false);
              loadAll();
            }}
            onCancel={() => setShowIncomeForm(false)}
          />
        )}

        {showExpenseForm && dashboard && (
          <LogExpenseForm
            accessToken={accessToken}
            categories={dashboard.categoryCards}
            currencyCode={accountLocale.currencyCode}
            locale={accountLocale.locale}
            onDone={() => {
              setShowExpenseForm(false);
              loadAll();
            }}
            onCancel={() => setShowExpenseForm(false)}
          />
        )}

        {isLoading ? (
          <p className="mt-8 text-sm text-muted">Loading…</p>
        ) : dashboard ? (
          <>
            {/* Hero net-position card, see docs/06-design-system.md's dashboard screen pattern.
                The breakdown below is a real grid, not a flex-wrap row: five items do not
                divide evenly, so with only a flex row wrapping wherever it runs out of
                space, the second column never lined up once one label was longer than the
                one above it, and a leftover fifth item was stuck alone at the left edge on
                its own line. A grid keeps every column's x-position fixed regardless. */}
            <div className="mt-6 rounded-2xl p-6 text-white shadow-lg" style={{ background: "var(--gradient-brand)" }}>
              <p className="text-sm font-medium text-white/80">Net position</p>
              <p className="mt-1 font-display text-4xl font-bold">
                {formatMoney(dashboard.netPosition.total, accountLocale.currencyCode, accountLocale.locale)}
              </p>
              <div className="mt-5 grid grid-cols-2 gap-x-4 gap-y-3 border-t border-white/15 pt-4 sm:grid-cols-5">
                <NetPositionStat
                  label="Savings"
                  amount={dashboard.netPosition.breakdown.savings}
                  currencyCode={accountLocale.currencyCode}
                  locale={accountLocale.locale}
                />
                <NetPositionStat
                  label="Deployed"
                  amount={dashboard.netPosition.breakdown.deployed}
                  currencyCode={accountLocale.currencyCode}
                  locale={accountLocale.locale}
                />
                <NetPositionStat
                  label="Pool"
                  amount={dashboard.netPosition.breakdown.pool}
                  currencyCode={accountLocale.currencyCode}
                  locale={accountLocale.locale}
                />
                <NetPositionStat
                  label="Loaned out"
                  amount={dashboard.netPosition.breakdown.loansOut}
                  currencyCode={accountLocale.currencyCode}
                  locale={accountLocale.locale}
                />
                <NetPositionStat
                  label="Owed"
                  amount={dashboard.netPosition.breakdown.debtsIn}
                  currencyCode={accountLocale.currencyCode}
                  locale={accountLocale.locale}
                />
              </div>
            </div>

            {spendingSummary && <SpendingInsightCard summary={spendingSummary} />}

            <div className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
              <TotalTile
                label="Flexible Pool"
                amount={dashboard.flexiblePool.balance}
                currencyCode={accountLocale.currencyCode}
                locale={accountLocale.locale}
              />
              <TotalTile
                label="Safe to spend today"
                amount={dashboard.categoryCards.reduce((sum, c) => sum + c.safeToSpend.daily, 0)}
                currencyCode={accountLocale.currencyCode}
                locale={accountLocale.locale}
              />
              <TotalTile
                label="Loaned out"
                amount={dashboard.outstandingLoansOut}
                currencyCode={accountLocale.currencyCode}
                locale={accountLocale.locale}
              />
              <TotalTile label="Owed" amount={dashboard.debtsOwed} currencyCode={accountLocale.currencyCode} locale={accountLocale.locale} />
            </div>

            {dashboard.obligations.length > 0 && (
              <>
                <h2 className="mt-8 text-lg font-semibold text-text">Next 7 days</h2>
                <div className="mt-3 flex flex-col gap-2">
                  {dashboard.obligations.map((item, index) => (
                    <div
                      key={index}
                      className="flex items-center gap-3 rounded-xl border border-border bg-gradient-to-br from-surface to-primary-tint px-4 py-3 shadow-sm"
                    >
                      <span
                        className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${OBLIGATION_SEVERITY_STYLES[item.severity] ?? OBLIGATION_SEVERITY_STYLES.Info}`}
                      >
                        <CalendarClockIcon size={17} />
                      </span>
                      <div className="min-w-0 flex-1">
                        <p className="text-[11px] font-semibold tracking-wide text-subtle uppercase">
                          {OBLIGATION_LABELS[item.type] ?? item.type}
                        </p>
                        <p className="truncate text-sm text-text">{item.title}</p>
                      </div>
                      <div className="shrink-0 text-right">
                        {item.amount !== null && (
                          <p className="text-sm font-semibold text-text">
                            {formatMoney(item.amount, accountLocale.currencyCode, accountLocale.locale)}
                          </p>
                        )}
                        <p className="text-xs text-muted">{formatObligationDate(item.date, accountLocale.locale)}</p>
                      </div>
                    </div>
                  ))}
                </div>
              </>
            )}

            <h2 className="mt-8 text-lg font-semibold text-text">Categories</h2>
            <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {dashboard.categoryCards.map((category, index) => (
                <CategoryCard
                  key={category.categoryId}
                  category={category}
                  colorIndex={index}
                  currencyCode={accountLocale.currencyCode}
                  locale={accountLocale.locale}
                  isResolving={resolvingCategoryId === category.categoryId}
                  onStartResolve={() => setResolvingCategoryId(category.categoryId)}
                  onCancelResolve={() => setResolvingCategoryId(null)}
                  onResolved={() => {
                    setResolvingCategoryId(null);
                    loadAll();
                  }}
                  accessToken={accessToken}
                  year={dashboard.month.year}
                  month={dashboard.month.month}
                />
              ))}
            </div>

            <h2 className="mt-8 text-lg font-semibold text-text">Recent activity</h2>
            <div className="mt-3 flex flex-col gap-1.5">
              {dashboard.recentTransactions.length === 0 && <p className="text-sm text-muted">Nothing logged yet.</p>}
              {dashboard.recentTransactions.map((row) => (
                <div key={row.id} className="flex items-center justify-between rounded-xl border border-border bg-gradient-to-br from-surface to-primary-tint px-4 py-2.5">
                  <span className="text-sm text-text">{row.description}</span>
                  <span className={`text-sm font-semibold ${row.isIncome ? "text-success" : "text-text"}`}>
                    {row.isIncome ? "+" : "−"}
                    {formatMoney(row.amount, accountLocale.currencyCode, accountLocale.locale)}
                  </span>
                </div>
              ))}
            </div>
          </>
        ) : null}
      </main>
    </div>
  );
}

function monthName(month: number): string {
  return new Date(2000, month - 1, 1).toLocaleString("en-US", { month: "long" });
}

// One label-over-value pair inside the net-position card's breakdown grid.
function NetPositionStat({
  label,
  amount,
  currencyCode,
  locale,
}: {
  label: string;
  amount: number;
  currencyCode: string;
  locale: string;
}) {
  return (
    <div>
      <p className="text-[11px] font-medium tracking-wide text-white/60 uppercase">{label}</p>
      <p className="mt-0.5 text-sm font-semibold text-white">{formatMoney(amount, currencyCode, locale)}</p>
    </div>
  );
}

const TREND_STYLES: Record<SpendingTrend["direction"], { label: string; classes: string; Icon: typeof TrendingUpIcon }> = {
  Up: { label: "Spending up", classes: "bg-warning-tint text-warning", Icon: TrendingUpIcon },
  Down: { label: "Spending down", classes: "bg-success-tint text-success", Icon: TrendingDownIcon },
  Flat: { label: "Steady", classes: "bg-surface-2 text-muted", Icon: MinusIcon },
};

// Turns the plain-English spending summary (which was the whole card before) into
// something that actually reads as a designed insight: an icon marking it as one,
// a trend chip using data the API already sent but nothing displayed
// (SpendingSummary.trend), and up to three of the month's top categories as chips
// using SpendingSummary.topCategories, also already fetched and unused before this.
function SpendingInsightCard({ summary }: { summary: SpendingSummary }) {
  const trend = TREND_STYLES[summary.trend.direction];

  return (
    <div className="mt-4 flex items-start gap-3 rounded-xl border border-border bg-gradient-to-br from-surface to-primary-tint p-4 shadow-sm">
      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary-tint text-primary">
        <LightbulbIcon size={18} />
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <p className="text-xs font-semibold tracking-wide text-subtle uppercase">This month&apos;s insight</p>
          <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold ${trend.classes}`}>
            <trend.Icon size={12} />
            {trend.label}
            {summary.trend.percentChange !== null ? ` ${Math.abs(summary.trend.percentChange)}%` : ""}
          </span>
        </div>
        <p className="mt-1.5 text-sm text-text">{summary.plainEnglish}</p>
        {summary.topCategories.length > 0 && (
          <div className="mt-2.5 flex flex-wrap gap-2">
            {summary.topCategories.slice(0, 3).map((category, index) => (
              <span
                key={category.categoryId}
                className="inline-flex items-center gap-1.5 rounded-full bg-surface px-2.5 py-1 text-xs font-medium text-text"
              >
                <span
                  className="h-2 w-2 rounded-full"
                  style={{ backgroundColor: SEGMENT_COLORS[index % SEGMENT_COLORS.length] }}
                />
                {category.name}
              </span>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function TotalTile({ label, amount, currencyCode, locale }: { label: string; amount: number; currencyCode: string; locale: string }) {
  return (
    <div className="rounded-xl border border-border bg-gradient-to-br from-surface to-primary-tint p-4 shadow-sm">
      <p className="text-xs font-medium text-muted">{label}</p>
      <p className="mt-1 font-display text-lg font-bold text-text">{formatMoney(amount, currencyCode, locale)}</p>
    </div>
  );
}

function CategoryCard({
  category,
  colorIndex,
  currencyCode,
  locale,
  isResolving,
  onStartResolve,
  onCancelResolve,
  onResolved,
  accessToken,
  year,
  month,
}: {
  category: CategoryBalance;
  colorIndex: number;
  currencyCode: string;
  locale: string;
  isResolving: boolean;
  onStartResolve: () => void;
  onCancelResolve: () => void;
  onResolved: () => void;
  accessToken: string | null;
  year: number;
  month: number;
}) {
  const statusLabel = category.deficit > 0 ? "InDeficit" : category.pace.status;

  // Pace bar: track fills spent ÷ funded, clamped at 100% (a small chevron shows
  // when the real number is over that), tinted by status, with a marker showing
  // where "today" sits across the month.
  const rawPercent = category.funded > 0 ? (category.spent / category.funded) * 100 : 0;
  const fillPercent = Math.min(100, Math.round(rawPercent));
  const isOverflowing = rawPercent > 100;
  const fillColor =
    statusLabel === "InDeficit" ? "bg-danger" : statusLabel === "OverPace" ? "bg-warning" : SEGMENT_COLORS[colorIndex % SEGMENT_COLORS.length];

  const daysInMonth = new Date(year, month, 0).getDate();
  const todayDayOfMonth = new Date().getDate();
  const todayMarkerPercent = Math.min(100, Math.round((todayDayOfMonth / daysInMonth) * 100));

  const categoryColor = SEGMENT_COLORS[colorIndex % SEGMENT_COLORS.length];

  return (
    <div className="rounded-xl border border-border bg-gradient-to-br from-surface to-primary-tint p-4 shadow-sm transition-shadow hover:shadow-md">
      <div className="flex items-center justify-between gap-2">
        <div className="flex min-w-0 items-center gap-3">
          {/* The category icon well docs/06-design-system.md's Category card
              section always specified but never actually got built: a tinted
              well in the category's own colour. Categories are free-text a
              person names themselves, not a fixed list, so this is a colour
              dot, not a per-category icon, nothing to reliably pick an icon
              from for a name like "Side hustle" or "Kids' school fees". */}
          <span
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg"
            style={{ backgroundColor: `color-mix(in srgb, ${categoryColor} 18%, white)` }}
          >
            <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: categoryColor }} />
          </span>
          <span className="flex min-w-0 items-center gap-1.5 truncate font-semibold text-text">
            {category.name}
            {category.isLocked && (
              <span className="shrink-0 rounded-full bg-warning-tint px-1.5 py-0.5 text-[10px] font-semibold text-warning">Locked</span>
            )}
          </span>
        </div>
        <span className={`shrink-0 rounded-full px-2 py-0.5 text-[11px] font-semibold ${STATUS_STYLES[statusLabel] ?? STATUS_STYLES.NotApplicable}`}>
          {statusLabel === "InDeficit" ? "In deficit" : statusLabel === "OverPace" ? "Over pace" : statusLabel === "OnTrack" ? "On track" : ""}
        </span>
      </div>

      <p className="mt-3 font-display text-xl font-bold text-text">
        {category.deficit > 0
          ? `Over by ${formatMoney(category.deficit, currencyCode, locale)}`
          : formatMoney(category.available, currencyCode, locale)}
      </p>
      <p className="text-xs text-muted">{category.deficit > 0 ? "needs cover" : "available"}</p>

      <div className="relative mt-3 h-2.5 overflow-hidden rounded-full bg-surface-2">
        <div className="h-full rounded-full transition-all" style={{ width: `${fillPercent}%`, backgroundColor: fillColor }} />
        <div className="absolute inset-y-0 w-0.5 bg-text/50" style={{ left: `${todayMarkerPercent}%` }} title="Today" />
      </div>
      <p className="mt-1.5 text-[11px] text-muted">
        {formatMoney(category.spent, currencyCode, locale)} spent of {formatMoney(category.funded, currencyCode, locale)}
        {isOverflowing ? " ▸" : ""}
      </p>

      {category.deficit > 0 && !isResolving && (
        <Button type="button" variant="secondary" className="mt-3 h-8 w-full text-xs" onClick={onStartResolve}>
          Resolve
        </Button>
      )}

      {isResolving && (
        <ResolveDeficitPanel
          category={category}
          accessToken={accessToken}
          year={year}
          month={month}
          onCancel={onCancelResolve}
          onResolved={onResolved}
        />
      )}
    </div>
  );
}

function ResolveDeficitPanel({
  category,
  accessToken,
  year,
  month,
  onCancel,
  onResolved,
}: {
  category: CategoryBalance;
  accessToken: string | null;
  year: number;
  month: number;
  onCancel: () => void;
  onResolved: () => void;
}) {
  const [sources, setSources] = useState<DeficitListItem["suggestedSources"]>([]);
  const [method, setMethod] = useState<string>("");
  const [sourceCategoryId, setSourceCategoryId] = useState<string | null>(null);
  const [amount, setAmount] = useState(category.deficit);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    apiFetch<DeficitListItem[]>(`/deficits?year=${year}&month=${month}`, { token: accessToken })
      .then((deficits) => {
        const match = deficits.find((d) => d.categoryId === category.categoryId);
        const found = match?.suggestedSources ?? [];
        setSources(found);
        if (found.length > 0) {
          setMethod(found[0].kind);
          setSourceCategoryId(found[0].categoryId);
        }
      })
      .catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function handleConfirm() {
    if (!method) {
      setError("There is nothing with money available to cover this from yet.");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    const request: ResolveDeficitRequest = {
      categoryId: category.categoryId,
      year,
      month,
      amount,
      method: method as ResolveDeficitRequest["method"],
      sourceCategoryId: method === "OtherCategorySavings" ? sourceCategoryId : null,
      note: null,
    };

    try {
      await apiFetch("/deficits/resolve", { method: "POST", token: accessToken, body: request });
      onResolved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not resolve this deficit.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="mt-3 rounded-lg bg-surface-2 p-3">
      {sources.length === 0 ? (
        <p className="text-xs text-muted">Nothing has enough saved up to cover this yet.</p>
      ) : (
        <>
          <label className="text-xs font-medium text-muted">Cover from</label>
          <select
            value={method}
            onChange={(e) => {
              const selected = sources.find((s) => s.kind === e.target.value);
              setMethod(e.target.value);
              setSourceCategoryId(selected?.categoryId ?? null);
            }}
            className="mt-1 h-9 w-full rounded-md border border-border-strong bg-surface px-2 text-sm outline-none focus:border-primary"
          >
            {sources.map((s, i) => (
              <option key={i} value={s.kind}>
                {s.kind === "FlexiblePool" ? "Flexible Pool" : s.kind === "OwnSavings" ? "This category's savings" : "Another category's savings"}
              </option>
            ))}
          </select>

          <label className="mt-2 block text-xs font-medium text-muted">Amount</label>
          <input
            type="number"
            min="0"
            step="0.01"
            value={amount}
            onChange={(e) => setAmount(Number(e.target.value))}
            className="mt-1 h-9 w-full rounded-md border border-border-strong bg-surface px-2 text-sm outline-none focus:border-primary"
          />

          {error && <p className="mt-2 text-xs text-danger">{error}</p>}

          <div className="mt-3 flex gap-2">
            <Button type="button" variant="secondary" className="h-8 flex-1 text-xs" onClick={onCancel}>
              Cancel
            </Button>
            <Button type="button" className="h-8 flex-1 text-xs" onClick={handleConfirm} disabled={isSubmitting}>
              {isSubmitting ? "Covering…" : "Confirm"}
            </Button>
          </div>
        </>
      )}
    </div>
  );
}

function LogIncomeForm({ accessToken, onDone, onCancel }: { accessToken: string | null; onDone: () => void; onCancel: () => void }) {
  const [type, setType] = useState<IncomeType>("Allocatable");
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit() {
    if (!amount || Number(amount) <= 0 || !description.trim()) {
      setError("Enter an amount and a description.");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      await apiFetch("/income", {
        method: "POST",
        token: accessToken,
        body: { type, amount: Number(amount), description: description.trim(), occurredOn: null },
      });
      onDone();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not log that income.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="mt-4 rounded-xl border border-border bg-gradient-to-br from-surface to-primary-tint p-4">
      <p className="text-sm font-semibold text-text">Log income</p>
      <div className="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-3">
        <select
          value={type}
          onChange={(e) => setType(e.target.value as IncomeType)}
          className="h-10 rounded-md border border-border-strong bg-surface px-2 text-sm outline-none focus:border-primary"
        >
          <option value="Allocatable">Allocatable (splits across categories)</option>
          <option value="Flexible">Flexible (goes to the pool)</option>
        </select>
        <input
          type="number"
          min="0"
          step="0.01"
          placeholder="Amount"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          className="h-10 rounded-md border border-border-strong bg-surface px-3 text-sm outline-none focus:border-primary"
        />
        <input
          placeholder="Description"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          className="h-10 rounded-md border border-border-strong bg-surface px-3 text-sm outline-none focus:border-primary"
        />
      </div>
      {error && <p className="mt-2 text-sm text-danger">{error}</p>}
      <div className="mt-3 flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="button" onClick={handleSubmit} disabled={isSubmitting}>
          {isSubmitting ? "Logging…" : "Log income"}
        </Button>
      </div>
    </div>
  );
}

function LogExpenseForm({
  accessToken,
  categories,
  currencyCode,
  locale,
  onDone,
  onCancel,
}: {
  accessToken: string | null;
  categories: CategoryBalance[];
  currencyCode: string;
  locale: string;
  onDone: () => void;
  onCancel: () => void;
}) {
  const [source, setSource] = useState<"Category" | "FlexiblePool">("Category");
  const [categoryId, setCategoryId] = useState(categories[0]?.categoryId ?? "");
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [deficitNotice, setDeficitNotice] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const selectedCategory = categories.find((c) => c.categoryId === categoryId);
  const enteredAmount = Number(amount) || 0;
  // A heads up before they even submit, so someone can back out or adjust the
  // amount instead of only finding out after the fact.
  const willGoOverBudget =
    source === "Category" && selectedCategory !== undefined && enteredAmount > 0 && enteredAmount > selectedCategory.available;

  async function handleSubmit() {
    if (!amount || Number(amount) <= 0 || !description.trim()) {
      setError("Enter an amount and a description.");
      return;
    }

    setIsSubmitting(true);
    setError(null);
    setDeficitNotice(null);

    try {
      const result = await apiFetch<LogExpenseResult>("/expenses", {
        method: "POST",
        token: accessToken,
        body: {
          amount: Number(amount),
          description: description.trim(),
          source,
          categoryId: source === "Category" ? categoryId : null,
          subCategory: null,
          occurredOn: null,
          tags: null,
        },
      });

      if (result.wentIntoDeficit && result.deficit) {
        setDeficitNotice(
          `This pushed the category ${formatMoney(result.deficit.amount, currencyCode, locale)} over budget, you can resolve it from the category card.`,
        );
        setTimeout(onDone, 1500);
      } else {
        onDone();
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not log that expense.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="mt-4 rounded-xl border border-border bg-gradient-to-br from-surface to-primary-tint p-4">
      <p className="text-sm font-semibold text-text">Log expense</p>
      <div className="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-4">
        <select
          value={source}
          onChange={(e) => setSource(e.target.value as "Category" | "FlexiblePool")}
          className="h-10 rounded-md border border-border-strong bg-surface px-2 text-sm outline-none focus:border-primary"
        >
          <option value="Category">From a category</option>
          <option value="FlexiblePool">From the Flexible Pool</option>
        </select>
        {source === "Category" && (
          <select
            value={categoryId}
            onChange={(e) => setCategoryId(e.target.value)}
            className="h-10 rounded-md border border-border-strong bg-surface px-2 text-sm outline-none focus:border-primary"
          >
            {categories.map((c) => (
              <option key={c.categoryId} value={c.categoryId} disabled={c.isLocked}>
                {c.name}
                {c.isLocked ? " (locked, upgrade to use)" : ""}
              </option>
            ))}
          </select>
        )}
        <input
          type="number"
          min="0"
          step="0.01"
          placeholder="Amount"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          className="h-10 rounded-md border border-border-strong bg-surface px-3 text-sm outline-none focus:border-primary"
        />
        <input
          placeholder="Description"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          className="h-10 rounded-md border border-border-strong bg-surface px-3 text-sm outline-none focus:border-primary"
        />
      </div>
      {willGoOverBudget && selectedCategory && (
        <p className="mt-2 rounded-lg bg-warning-tint px-3 py-2 text-sm text-warning">
          This is {formatMoney(enteredAmount - selectedCategory.available, currencyCode, locale)} more than {selectedCategory.name} has
          available, it will go into deficit.
        </p>
      )}
      {error && <p className="mt-2 text-sm text-danger">{error}</p>}
      {deficitNotice && <p className="mt-2 text-sm text-warning">{deficitNotice}</p>}
      <div className="mt-3 flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="button" onClick={handleSubmit} disabled={isSubmitting}>
          {isSubmitting ? "Logging…" : "Log expense"}
        </Button>
      </div>
    </div>
  );
}
