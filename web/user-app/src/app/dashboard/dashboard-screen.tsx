"use client";

import { useEffect, useState } from "react";
import type {
  BalancesResult,
  CategoryBalance,
  DeficitListItem,
  Expense,
  Income,
  IncomeType,
  LogExpenseResult,
  ResolveDeficitRequest,
} from "@natoshare/shared-types";
import { SEGMENT_COLORS } from "@/components/split-ring";
import { SiteHeader } from "@/components/site-header";
import { Button } from "@/components/ui/button";
import { apiFetch, ApiError } from "@/lib/api-client";
import { formatMoney } from "@/lib/format";
import { useAuthStore } from "@/store/auth-store";

type MeResponse = { user: { currencyCode: string; locale: string } };

// One row in the recent activity list, income and expenses merged together and
// sorted by date so it reads like a single timeline.
type ActivityRow = {
  id: string;
  description: string;
  amount: number;
  isIncome: boolean;
  occurredOn: string;
};

const STATUS_STYLES: Record<string, string> = {
  OnTrack: "bg-success-tint text-success",
  OverPace: "bg-warning-tint text-warning",
  InDeficit: "bg-danger-tint text-danger",
  NotApplicable: "bg-surface-2 text-subtle",
};

// The screen a logged-in user lands on: what every category has right now, quick
// forms to log income or an expense, and a way to resolve a category that has gone
// into deficit, all backed by the real /balances, /income and /expenses endpoints.
export function DashboardScreen() {
  const accessToken = useAuthStore((state) => state.accessToken);

  const [accountLocale, setAccountLocale] = useState({ currencyCode: "USD", locale: "en" });
  const [balances, setBalances] = useState<BalancesResult | null>(null);
  const [activity, setActivity] = useState<ActivityRow[]>([]);
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
      const [balancesResult, incomeResult, expenseResult] = await Promise.all([
        apiFetch<BalancesResult>("/balances", { token: accessToken }),
        apiFetch<Income[]>("/income?pageSize=10", { token: accessToken }),
        apiFetch<Expense[]>("/expenses?pageSize=10", { token: accessToken }),
      ]);

      setBalances(balancesResult);

      const rows: ActivityRow[] = [
        ...incomeResult.map((i) => ({ id: i.id, description: i.description, amount: i.amount, isIncome: true, occurredOn: i.occurredOn })),
        ...expenseResult.map((e) => ({ id: e.id, description: e.description, amount: e.amount, isIncome: false, occurredOn: e.occurredOn })),
      ].sort((a, b) => b.occurredOn.localeCompare(a.occurredOn));

      setActivity(rows.slice(0, 10));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not load your balances.");
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <div>
      <SiteHeader />

      <main className="mx-auto max-w-5xl px-4 pb-16">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-2xl font-bold text-text">
            {balances ? `${monthName(balances.month.month)} ${balances.month.year}` : "Your money"}
          </h1>
          <div className="flex gap-2">
            <Button type="button" variant="secondary" onClick={() => setShowExpenseForm((v) => !v)}>
              Log expense
            </Button>
            <Button type="button" onClick={() => setShowIncomeForm((v) => !v)}>
              Log income
            </Button>
          </div>
        </div>

        {error && <p className="mt-4 rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

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

        {showExpenseForm && balances && (
          <LogExpenseForm
            accessToken={accessToken}
            categories={balances.categories}
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
        ) : balances ? (
          <>
            <div className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
              <TotalTile label="Allocated" amount={balances.totals.allocated} currencyCode={accountLocale.currencyCode} locale={accountLocale.locale} />
              <TotalTile label="Spent" amount={balances.totals.spent} currencyCode={accountLocale.currencyCode} locale={accountLocale.locale} />
              <TotalTile label="Available" amount={balances.totals.available} currencyCode={accountLocale.currencyCode} locale={accountLocale.locale} />
              <TotalTile
                label="Flexible Pool"
                amount={balances.flexiblePool.balance}
                currencyCode={accountLocale.currencyCode}
                locale={accountLocale.locale}
              />
            </div>

            <h2 className="mt-8 text-lg font-semibold text-text">Categories</h2>
            <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {balances.categories.map((category, index) => (
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
                  year={balances.month.year}
                  month={balances.month.month}
                />
              ))}
            </div>

            <h2 className="mt-8 text-lg font-semibold text-text">Recent activity</h2>
            <div className="mt-3 flex flex-col gap-1.5">
              {activity.length === 0 && <p className="text-sm text-muted">Nothing logged yet.</p>}
              {activity.map((row) => (
                <div key={row.id} className="flex items-center justify-between rounded-xl border border-border bg-surface px-4 py-2.5">
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

function TotalTile({ label, amount, currencyCode, locale }: { label: string; amount: number; currencyCode: string; locale: string }) {
  return (
    <div className="rounded-xl border border-border bg-surface p-4">
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

  return (
    <div className="rounded-xl border border-border bg-surface p-4">
      <div className="flex items-center justify-between">
        <span className="font-semibold text-text">{category.name}</span>
        <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${STATUS_STYLES[statusLabel] ?? STATUS_STYLES.NotApplicable}`}>
          {statusLabel === "InDeficit" ? "In deficit" : statusLabel === "OverPace" ? "Over pace" : statusLabel === "OnTrack" ? "On track" : ""}
        </span>
      </div>

      <p className="mt-2 font-display text-xl font-bold text-text">
        {category.deficit > 0
          ? `Over by ${formatMoney(category.deficit, currencyCode, locale)}`
          : formatMoney(category.available, currencyCode, locale)}
      </p>
      <p className="text-xs text-muted">{category.deficit > 0 ? "needs cover" : "available"}</p>

      <div className="relative mt-2 h-2 overflow-hidden rounded-full bg-surface-2">
        <div className="h-full rounded-full" style={{ width: `${fillPercent}%`, backgroundColor: fillColor }} />
        <div className="absolute inset-y-0 w-0.5 bg-text/50" style={{ left: `${todayMarkerPercent}%` }} title="Today" />
      </div>
      <p className="mt-1 text-[11px] text-muted">
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
            className="mt-1 h-9 w-full rounded-lg border border-border-strong bg-surface px-2 text-sm outline-none focus:border-primary"
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
            className="mt-1 h-9 w-full rounded-lg border border-border-strong bg-surface px-2 text-sm outline-none focus:border-primary"
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
    <div className="mt-4 rounded-xl border border-border bg-surface p-4">
      <p className="text-sm font-semibold text-text">Log income</p>
      <div className="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-3">
        <select
          value={type}
          onChange={(e) => setType(e.target.value as IncomeType)}
          className="h-10 rounded-lg border border-border-strong bg-surface px-2 text-sm outline-none focus:border-primary"
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
          className="h-10 rounded-lg border border-border-strong bg-surface px-3 text-sm outline-none focus:border-primary"
        />
        <input
          placeholder="Description"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          className="h-10 rounded-lg border border-border-strong bg-surface px-3 text-sm outline-none focus:border-primary"
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
    <div className="mt-4 rounded-xl border border-border bg-surface p-4">
      <p className="text-sm font-semibold text-text">Log expense</p>
      <div className="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-4">
        <select
          value={source}
          onChange={(e) => setSource(e.target.value as "Category" | "FlexiblePool")}
          className="h-10 rounded-lg border border-border-strong bg-surface px-2 text-sm outline-none focus:border-primary"
        >
          <option value="Category">From a category</option>
          <option value="FlexiblePool">From the Flexible Pool</option>
        </select>
        {source === "Category" && (
          <select
            value={categoryId}
            onChange={(e) => setCategoryId(e.target.value)}
            className="h-10 rounded-lg border border-border-strong bg-surface px-2 text-sm outline-none focus:border-primary"
          >
            {categories.map((c) => (
              <option key={c.categoryId} value={c.categoryId}>
                {c.name}
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
          className="h-10 rounded-lg border border-border-strong bg-surface px-3 text-sm outline-none focus:border-primary"
        />
        <input
          placeholder="Description"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          className="h-10 rounded-lg border border-border-strong bg-surface px-3 text-sm outline-none focus:border-primary"
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
