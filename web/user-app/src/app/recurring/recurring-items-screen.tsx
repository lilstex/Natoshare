"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import type {
  CategoryBalance,
  CommittedTotal,
  CreateRecurringItemRequest,
  IncomeType,
  RecurringCadence,
  RecurringItem,
  RecurringItemKind,
  RecurringItemMode,
} from "@natoshare/shared-types";
import { SiteHeader } from "@/components/site-header";
import { Button } from "@/components/ui/button";
import { SelectField } from "@/components/ui/select-field";
import { TextField } from "@/components/ui/text-field";
import { apiFetch, ApiError } from "@/lib/api-client";
import { formatMoney } from "@/lib/format";
import { useAuthStore } from "@/store/auth-store";

type MeResponse = { user: { currencyCode: string; locale: string } };

const WEEKDAY_NAMES = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

// Things that happen on a schedule, like rent or a subscription. This whole feature
// needs an active Pro trial or plan (see IEntitlementService on the backend), a
// lapsed trial gets a friendly explanation here instead of a generic error.
export function RecurringItemsScreen() {
  const accessToken = useAuthStore((state) => state.accessToken);
  const [accountLocale, setAccountLocale] = useState({ currencyCode: "USD", locale: "en" });
  const [categories, setCategories] = useState<CategoryBalance[]>([]);
  const [items, setItems] = useState<RecurringItem[]>([]);
  const [committedTotal, setCommittedTotal] = useState<CommittedTotal | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [needsUpgrade, setNeedsUpgrade] = useState(false);

  function load() {
    if (!accessToken) {
      return;
    }

    Promise.all([
      apiFetch<RecurringItem[]>("/recurring", { token: accessToken }),
      apiFetch<CommittedTotal>("/recurring/committed-total", { token: accessToken }),
    ])
      .then(([itemsResult, totalResult]) => {
        setItems(itemsResult);
        setCommittedTotal(totalResult);
        setError(null);
        setNeedsUpgrade(false);
      })
      .catch((err) => {
        if (err instanceof ApiError && err.status === 403) {
          setNeedsUpgrade(true);
        } else {
          setError(err instanceof ApiError ? err.message : "Could not load your recurring items.");
        }
      })
      .finally(() => setIsLoading(false));
  }

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    apiFetch<MeResponse>("/me", { token: accessToken })
      .then((me) => setAccountLocale({ currencyCode: me.user.currencyCode, locale: me.user.locale }))
      .catch(() => {});

    apiFetch<{ categories: CategoryBalance[] }>("/balances", { token: accessToken })
      .then((result) => setCategories(result.categories))
      .catch(() => {});

    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken]);

  async function handleSkipNext(id: string) {
    try {
      await apiFetch(`/recurring/${id}/skip-next`, { method: "POST", token: accessToken });
      load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not skip that.");
    }
  }

  async function handleTogglePause(item: RecurringItem) {
    try {
      await apiFetch(`/recurring/${item.id}`, { method: "PATCH", token: accessToken, body: { isActive: !item.isActive } });
      load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not update that.");
    }
  }

  async function handleDelete(id: string) {
    try {
      await apiFetch(`/recurring/${id}`, { method: "DELETE", token: accessToken });
      load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not remove that.");
    }
  }

  return (
    <div>
      <SiteHeader />

      <main className="mx-auto max-w-3xl px-4 pb-16">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold text-text">Recurring items</h1>
          {!needsUpgrade && (
            <Button type="button" onClick={() => setShowForm((v) => !v)}>
              {showForm ? "Cancel" : "Add recurring item"}
            </Button>
          )}
        </div>

        {needsUpgrade && (
          <p className="mt-4 rounded-xl bg-warning-tint px-3.5 py-2.5 text-sm text-warning">
            Recurring items need an active Pro plan.{" "}
            <Link href="/plans" className="font-semibold underline">
              See plans
            </Link>
            .
          </p>
        )}

        {error && <p className="mt-4 rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

        {!needsUpgrade && committedTotal && (
          <div className="mt-5 rounded-xl border border-border bg-surface p-4">
            <p className="text-xs font-medium text-muted">Committed subscriptions, per month</p>
            <p className="mt-1 font-display text-lg font-bold text-text">
              {formatMoney(committedTotal.monthlyExpenseTotal, accountLocale.currencyCode, accountLocale.locale)}
            </p>
          </div>
        )}

        {showForm && (
          <NewRecurringItemForm
            accessToken={accessToken}
            categories={categories}
            onDone={() => {
              setShowForm(false);
              load();
            }}
            onCancel={() => setShowForm(false)}
          />
        )}

        {!needsUpgrade && (
          <div className="mt-6 flex flex-col gap-3">
            {isLoading ? (
              <p className="text-sm text-muted">Loading…</p>
            ) : items.length === 0 && !showForm ? (
              <p className="text-sm text-muted">Nothing recurring set up yet.</p>
            ) : (
              items.map((item) => (
                <div key={item.id} className="rounded-xl border border-border bg-surface p-4">
                  <div className="flex items-center justify-between">
                    <span className="font-semibold text-text">{item.description}</span>
                    <span
                      className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                        item.isActive ? "bg-success-tint text-success" : "bg-surface-2 text-subtle"
                      }`}
                    >
                      {item.isActive ? "Active" : "Paused"}
                    </span>
                  </div>
                  <p className="mt-1 text-sm text-muted">
                    {formatMoney(item.amount, accountLocale.currencyCode, accountLocale.locale)} · {item.kind} · {item.cadence} · {item.mode}
                  </p>
                  <p className="text-xs text-muted">
                    Next: {item.nextRunOn}
                    {item.lastPostedOn ? ` · last posted ${item.lastPostedOn}` : ""}
                  </p>
                  <div className="mt-3 flex gap-2">
                    <Button type="button" variant="secondary" className="h-8 text-xs" onClick={() => handleSkipNext(item.id)}>
                      Skip next
                    </Button>
                    <Button type="button" variant="secondary" className="h-8 text-xs" onClick={() => handleTogglePause(item)}>
                      {item.isActive ? "Pause" : "Resume"}
                    </Button>
                    <Button type="button" variant="secondary" className="h-8 text-xs" onClick={() => handleDelete(item.id)}>
                      Remove
                    </Button>
                  </div>
                </div>
              ))
            )}
          </div>
        )}
      </main>
    </div>
  );
}

function NewRecurringItemForm({
  accessToken,
  categories,
  onDone,
  onCancel,
}: {
  accessToken: string | null;
  categories: CategoryBalance[];
  onDone: () => void;
  onCancel: () => void;
}) {
  const [kind, setKind] = useState<RecurringItemKind>("Expense");
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");
  const [categoryId, setCategoryId] = useState<string>("");
  const [incomeType, setIncomeType] = useState<IncomeType>("Allocatable");
  const [cadence, setCadence] = useState<RecurringCadence>("Monthly");
  const [anchorDay, setAnchorDay] = useState(1);
  const [mode, setMode] = useState<RecurringItemMode>("Remind");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit() {
    if (!amount || Number(amount) <= 0 || !description.trim()) {
      setError("Enter an amount and a description.");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    const request: CreateRecurringItemRequest = {
      kind,
      amount: Number(amount),
      description: description.trim(),
      categoryId: kind === "Expense" && categoryId ? categoryId : null,
      incomeType: kind === "Income" ? incomeType : null,
      cadence,
      anchorDay,
      mode,
    };

    try {
      await apiFetch("/recurring", { method: "POST", token: accessToken, body: request });
      onDone();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not save that.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="mt-4 rounded-xl border border-border bg-surface p-4">
      <p className="text-sm font-semibold text-text">Add a recurring item</p>
      <div className="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-2">
        <SelectField label="Kind" value={kind} onChange={(e) => setKind(e.target.value as RecurringItemKind)}>
          <option value="Expense">Expense</option>
          <option value="Income">Income</option>
        </SelectField>

        {kind === "Expense" ? (
          <SelectField label="Category" value={categoryId} onChange={(e) => setCategoryId(e.target.value)}>
            <option value="">Flexible Pool</option>
            {categories.map((c) => (
              <option key={c.categoryId} value={c.categoryId}>
                {c.name}
              </option>
            ))}
          </SelectField>
        ) : (
          <SelectField label="Income type" value={incomeType} onChange={(e) => setIncomeType(e.target.value as IncomeType)}>
            <option value="Allocatable">Allocatable</option>
            <option value="Flexible">Flexible</option>
          </SelectField>
        )}

        <TextField label="Amount" type="number" min="0" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
        <TextField label="Description" value={description} onChange={(e) => setDescription(e.target.value)} />

        <SelectField
          label="Repeats"
          value={cadence}
          onChange={(e) => {
            const next = e.target.value as RecurringCadence;
            setCadence(next);
            setAnchorDay(next === "Monthly" ? 1 : 0);
          }}
        >
          <option value="Monthly">Monthly</option>
          <option value="Weekly">Weekly</option>
          <option value="BiWeekly">Every two weeks</option>
        </SelectField>

        {cadence === "Monthly" ? (
          <SelectField label="Day of month" value={anchorDay} onChange={(e) => setAnchorDay(Number(e.target.value))}>
            <option value={0}>Last day of the month</option>
            {Array.from({ length: 28 }, (_, i) => i + 1).map((day) => (
              <option key={day} value={day}>
                {day}
              </option>
            ))}
          </SelectField>
        ) : (
          <SelectField label="Day of week" value={anchorDay} onChange={(e) => setAnchorDay(Number(e.target.value))}>
            {WEEKDAY_NAMES.map((name, index) => (
              <option key={name} value={index}>
                {name}
              </option>
            ))}
          </SelectField>
        )}

        <SelectField label="Mode" value={mode} onChange={(e) => setMode(e.target.value as RecurringItemMode)}>
          <option value="Remind">Just remind me</option>
          <option value="AutoPost">Post it automatically</option>
        </SelectField>
      </div>
      {error && <p className="mt-2 text-sm text-danger">{error}</p>}
      <div className="mt-3 flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="button" onClick={handleSubmit} disabled={isSubmitting}>
          {isSubmitting ? "Saving…" : "Save"}
        </Button>
      </div>
    </div>
  );
}
