"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import type {
  AllocationPreviewResult,
  BudgetTemplate,
  CategoryKind,
  CurrencyOption,
  OnboardingCategoryInput,
  OnboardingCompleteRequest,
} from "@natoshare/shared-types";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import { SelectField } from "@/components/ui/select-field";
import { SplitRing } from "@/components/split-ring";
import { apiFetch, ApiError } from "@/lib/api-client";
import { currentMonthInTimeZone, formatMoney, getSupportedTimeZones } from "@/lib/format";
import { useAuthStore } from "@/store/auth-store";

// One row in the category builder. It carries a client-only id, just so React and the
// live preview call have something stable to key on, the real category is only
// created once the whole wizard is submitted.
type CategoryRow = {
  clientId: string;
  name: string;
  kind: CategoryKind;
  percentage: number;
};

function newRow(name: string, kind: CategoryKind, percentage: number): CategoryRow {
  return { clientId: crypto.randomUUID(), name, kind, percentage };
}

// Walks a brand new account through picking a currency and timezone, saying how much
// they earn each month, and setting up their categories, all in one flow. This is the
// one thing every account has to do before anything else in Natoshare makes sense.
export function OnboardingWizard() {
  const router = useRouter();
  const accessToken = useAuthStore((state) => state.accessToken);

  const [step, setStep] = useState<1 | 2>(1);
  const [isChecking, setIsChecking] = useState(true);

  const [currencies, setCurrencies] = useState<CurrencyOption[]>([]);
  const [templates, setTemplates] = useState<BudgetTemplate[]>([]);

  const [currencyCode, setCurrencyCode] = useState("NGN");
  const [currencySymbol, setCurrencySymbol] = useState("₦");
  const [timeZoneId, setTimeZoneId] = useState("Africa/Lagos");
  // Picks up the browser's own language as a starting guess for locale. This only
  // runs once, on the client, the "en" fallback is what a server render sees.
  const [locale, setLocale] = useState(() => (typeof navigator !== "undefined" ? navigator.language : "en"));

  const [incomeInput, setIncomeInput] = useState("");
  const [rows, setRows] = useState<CategoryRow[]>([]);
  const [selectedTemplateId, setSelectedTemplateId] = useState<string | null>(null);
  const [preview, setPreview] = useState<AllocationPreviewResult | null>(null);

  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const timeZones = useMemo(() => getSupportedTimeZones(), []);
  const income = Number(incomeInput) || 0;
  const totalPercentage = rows.reduce((sum, row) => sum + row.percentage, 0);

  // A returning visitor should not see this wizard again once they have finished it.
  useEffect(() => {
    if (!accessToken) {
      return;
    }

    apiFetch<{ done: boolean }>("/onboarding/state", { token: accessToken })
      .then((state) => {
        if (state.done) {
          router.replace("/dashboard");
        } else {
          setIsChecking(false);
        }
      })
      .catch(() => setIsChecking(false));
  }, [accessToken, router]);

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    apiFetch<CurrencyOption[]>("/onboarding/currencies", { token: accessToken }).then(setCurrencies).catch(() => {});
    apiFetch<BudgetTemplate[]>("/onboarding/templates", { token: accessToken }).then(setTemplates).catch(() => {});
  }, [accessToken]);

  const isPreviewEligible = income > 0 && rows.length > 0 && totalPercentage === 100;

  // Whenever the rows or the income change, ask the API to work out the real
  // per-category amounts, using the same rounding the server will use when it
  // actually saves the split, so what the user sees here is exactly what they get.
  // The split is not cleared here when it stops being eligible, isPreviewEligible
  // below hides a stale one instead, so this effect only ever writes state from the
  // API response, not synchronously from its own body.
  useEffect(() => {
    if (!accessToken || !isPreviewEligible) {
      return;
    }

    const timer = setTimeout(() => {
      apiFetch<AllocationPreviewResult>("/allocation/preview", {
        method: "POST",
        token: accessToken,
        body: {
          fixedIncomeAmount: income,
          allocations: rows.map((row) => ({ categoryId: row.clientId, percentage: row.percentage })),
        },
      })
        .then(setPreview)
        .catch(() => setPreview(null));
    }, 300);

    return () => clearTimeout(timer);
  }, [accessToken, income, rows, isPreviewEligible]);

  const visiblePreview = isPreviewEligible ? preview : null;

  function pickTemplate(template: BudgetTemplate) {
    setSelectedTemplateId(template.id);
    setRows(template.items.map((item) => newRow(item.name, item.kind, item.percentage)));
  }

  function startFromScratch() {
    setSelectedTemplateId(null);
    setRows([newRow("", "Standard", 0)]);
  }

  function updateRow(clientId: string, patch: Partial<CategoryRow>) {
    setRows((current) => current.map((row) => (row.clientId === clientId ? { ...row, ...patch } : row)));
  }

  function removeRow(clientId: string) {
    setRows((current) => current.filter((row) => row.clientId !== clientId));
  }

  function goToStepTwo() {
    if (!currencyCode.trim() || !timeZoneId.trim() || !locale.trim()) {
      setError("Please fill in your currency, timezone and locale before continuing.");
      return;
    }

    setError(null);
    setStep(2);
  }

  async function handleFinish() {
    setError(null);

    if (income <= 0) {
      setError("Enter how much you earn each month.");
      return;
    }

    const trimmedNames = rows.map((row) => row.name.trim().toLowerCase());
    if (rows.some((row) => !row.name.trim())) {
      setError("Every category needs a name.");
      return;
    }
    if (new Set(trimmedNames).size !== rows.length) {
      setError("Two categories cannot have the same name.");
      return;
    }
    if (rows.some((row) => row.percentage <= 0)) {
      setError("Every category needs a percentage above 0.");
      return;
    }
    if (totalPercentage !== 100) {
      setError(`Your percentages add up to ${totalPercentage}%, they need to add up to exactly 100%.`);
      return;
    }

    setIsSubmitting(true);

    const categories: OnboardingCategoryInput[] = rows.map((row) => ({
      name: row.name.trim(),
      kind: row.kind,
      percentage: row.percentage,
      externalAccountLabel: null,
      subCategories: null,
    }));

    const request: OnboardingCompleteRequest = {
      currency: { code: currencyCode.toUpperCase(), symbol: currencySymbol },
      timeZoneId,
      locale,
      fixedIncomeAmount: income,
      effectiveFromMonth: currentMonthInTimeZone(timeZoneId),
      categories,
    };

    try {
      await apiFetch("/onboarding/complete", { method: "POST", token: accessToken, body: request });
      router.push("/dashboard");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  if (isChecking) {
    return null;
  }

  return (
    <main className="flex min-h-screen flex-col items-center bg-bg px-4 py-12">
      <div className="flex items-center gap-2.5">
        <span className="font-display text-lg font-semibold text-text">Natoshare</span>
      </div>

      <div className="mt-7 flex gap-1.5">
        <span className={`h-1.5 w-7 rounded-full ${step >= 1 ? "bg-primary" : "bg-surface-2"}`} />
        <span className={`h-1.5 w-7 rounded-full ${step >= 2 ? "bg-primary" : "bg-surface-2"}`} />
      </div>

      <div className="mt-6 w-full max-w-2xl rounded-[26px] border border-border bg-surface p-8 shadow-lg">
        {step === 1 ? (
          <>
            <p className="text-xs font-semibold tracking-wide text-subtle uppercase">Step 1 of 2</p>
            <h1 className="mt-1.5 text-2xl font-bold text-text">Where should we set you up?</h1>
            <p className="mt-1 text-sm text-muted">
              Natoshare works the same wherever you are, this just tells us how to show your money.
            </p>

            <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2">
              <SelectField
                id="currency"
                label="Currency"
                value={currencyCode}
                onChange={(e) => {
                  const selected = currencies.find((c) => c.code === e.target.value);
                  setCurrencyCode(e.target.value);
                  setCurrencySymbol(selected?.symbol ?? "");
                }}
              >
                {currencies.map((c) => (
                  <option key={c.code} value={c.code}>
                    {c.code} — {c.name}
                  </option>
                ))}
              </SelectField>

              <div className="flex flex-col gap-1.5 text-left">
                <label htmlFor="timezone" className="text-sm font-medium text-muted">
                  Timezone
                </label>
                <input
                  id="timezone"
                  list="timezone-options"
                  value={timeZoneId}
                  onChange={(e) => setTimeZoneId(e.target.value)}
                  className="h-11 rounded-xl border border-border-strong bg-surface px-3.5 text-[15px] text-text outline-none focus:border-primary focus:ring-4 focus:ring-primary-tint"
                />
                <datalist id="timezone-options">
                  {timeZones.map((tz) => (
                    <option key={tz} value={tz} />
                  ))}
                </datalist>
              </div>
            </div>

            <div className="mt-4">
              <TextField
                id="locale"
                label="Locale"
                value={locale}
                onChange={(e) => setLocale(e.target.value)}
              />
              <p className="mt-1 text-xs text-subtle">
                Controls how dates and numbers look, for example &quot;en-NG&quot; or &quot;de-DE&quot;.
              </p>
            </div>

            {error && <p className="mt-4 rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

            <div className="mt-7 flex justify-end">
              <Button type="button" onClick={goToStepTwo}>
                Continue
              </Button>
            </div>
          </>
        ) : (
          <>
            <p className="text-xs font-semibold tracking-wide text-subtle uppercase">Step 2 of 2</p>
            <h1 className="mt-1.5 text-2xl font-bold text-text">What&apos;s your monthly budget?</h1>
            <p className="mt-1 text-sm text-muted">
              Enter the income you can rely on each month, then pick a template or build your own categories.
            </p>

            <div className="mt-6">
              <label htmlFor="income" className="text-sm font-medium text-muted">
                Fixed allocatable income
              </label>
              <div className="mt-1.5 flex h-14 items-center rounded-2xl border border-primary px-4 shadow-[0_0_0_3px_var(--nato-primary-tint)]">
                <span className="mr-1 text-lg text-subtle">{currencySymbol}</span>
                <input
                  id="income"
                  type="number"
                  min="0"
                  step="0.01"
                  value={incomeInput}
                  onChange={(e) => setIncomeInput(e.target.value)}
                  className="flex-1 bg-transparent text-right font-display text-2xl font-bold text-text outline-none"
                />
              </div>
            </div>

            <div className="mt-6">
              <p className="text-sm font-semibold text-text">Start from a template</p>
              <div className="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-2">
                {templates.map((template) => (
                  <button
                    key={template.id}
                    type="button"
                    onClick={() => pickTemplate(template)}
                    className={`rounded-xl border px-3.5 py-2.5 text-left text-sm transition-colors ${
                      selectedTemplateId === template.id
                        ? "border-primary bg-primary-tint"
                        : "border-border-strong bg-surface hover:bg-surface-2"
                    }`}
                  >
                    <span className="font-semibold text-text">{template.name}</span>
                    <p className="mt-0.5 text-xs text-muted">{template.description}</p>
                  </button>
                ))}
                <button
                  type="button"
                  onClick={startFromScratch}
                  className={`rounded-xl border px-3.5 py-2.5 text-left text-sm transition-colors ${
                    selectedTemplateId === null && rows.length > 0
                      ? "border-primary bg-primary-tint"
                      : "border-border-strong bg-surface hover:bg-surface-2"
                  }`}
                >
                  <span className="font-semibold text-text">Build my own</span>
                  <p className="mt-0.5 text-xs text-muted">Start empty and add your own categories.</p>
                </button>
              </div>
            </div>

            {rows.length > 0 && (
              <div className="mt-6">
                <div className="flex items-center justify-between">
                  <p className="text-sm font-semibold text-text">Your categories</p>
                  <span className={`text-sm font-semibold ${totalPercentage === 100 ? "text-success" : "text-warning"}`}>
                    {totalPercentage}% allocated
                  </span>
                </div>

                <div className="mt-2 flex flex-col gap-2">
                  {rows.map((row) => (
                    <div key={row.clientId} className="flex items-center gap-2">
                      <input
                        value={row.name}
                        onChange={(e) => updateRow(row.clientId, { name: e.target.value })}
                        placeholder="Category name"
                        className="h-10 flex-1 rounded-lg border border-border-strong bg-surface px-3 text-sm text-text outline-none focus:border-primary"
                      />
                      <select
                        value={row.kind}
                        onChange={(e) => updateRow(row.clientId, { kind: e.target.value as CategoryKind })}
                        className="h-10 rounded-lg border border-border-strong bg-surface px-2 text-sm text-text outline-none focus:border-primary"
                      >
                        <option value="Standard">Standard</option>
                        <option value="FixedAccount">Fixed account</option>
                      </select>
                      <input
                        type="number"
                        min="0"
                        max="100"
                        step="0.01"
                        value={row.percentage}
                        onChange={(e) => updateRow(row.clientId, { percentage: Number(e.target.value) })}
                        className="h-10 w-20 rounded-lg border border-border-strong bg-surface px-2 text-right text-sm text-text outline-none focus:border-primary"
                      />
                      <span className="text-sm text-subtle">%</span>
                      <button
                        type="button"
                        onClick={() => removeRow(row.clientId)}
                        className="text-sm text-subtle hover:text-danger"
                        aria-label={`Remove ${row.name || "category"}`}
                      >
                        ✕
                      </button>
                    </div>
                  ))}
                </div>

                <button
                  type="button"
                  onClick={() => setRows((current) => [...current, newRow("", "Standard", 0)])}
                  className="mt-2 text-sm font-semibold text-primary"
                >
                  + Add category
                </button>
              </div>
            )}

            {visiblePreview && (
              <div className="mt-6 flex items-center gap-6 border-t border-border pt-6">
                <SplitRing segments={rows.map((row) => ({ percentage: row.percentage }))} />
                <div className="flex-1">
                  <p className="mb-1.5 text-sm font-semibold text-text">Your income splits like this</p>
                  {visiblePreview.categories.map((c, i) => (
                    <div key={i} className="flex items-center gap-2.5 py-1 text-sm">
                      <span className="flex-1 text-muted">{rows[i]?.name}</span>
                      <span className="text-muted">{c.percentage}%</span>
                      <span className="w-24 text-right font-semibold text-text">
                        {formatMoney(c.allocatedAmount, currencyCode, locale)}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {error && <p className="mt-4 rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

            <div className="mt-7 flex justify-between">
              <Button type="button" variant="secondary" onClick={() => setStep(1)}>
                Back
              </Button>
              <Button type="button" onClick={handleFinish} disabled={isSubmitting}>
                {isSubmitting ? "Finishing setup…" : "Finish setup"}
              </Button>
            </div>
          </>
        )}
      </div>
    </main>
  );
}
